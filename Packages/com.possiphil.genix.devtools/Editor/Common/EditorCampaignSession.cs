using System;
using Genix.Editor.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Common
{
    /// <summary>
    /// Owns editor-global state temporarily changed by an unattended benchmark or evaluation campaign.
    /// </summary>
    /// <remarks>
    /// Disposing the session restores the original scene setup, profiling state, reload lock, and
    /// interruption marker even when one cleanup operation fails.
    /// </remarks>
    internal sealed class EditorCampaignSession : IDisposable
    {
        private readonly string _interruptedSessionKey;
        private readonly string _originalScenePath;
        private readonly SceneSetup[] _originalSceneSetup;
        private readonly bool _profilingWasEnabled;
        private bool _assembliesLocked;
        private bool _disposed;

        private EditorCampaignSession(string interruptedSessionKey)
        {
            _interruptedSessionKey = interruptedSessionKey;
            _originalScenePath = SceneManager.GetActiveScene().path;
            _originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            _profilingWasEnabled = GenerationProfilerService.ProfilingEnabled;
            StartedAt = EditorApplication.timeSinceStartup;
        }

        /// <summary>Gets the editor time at which the session started.</summary>
        public double StartedAt { get; }

        /// <summary>Gets elapsed wall-clock time while the session is active.</summary>
        public double ElapsedSeconds => _disposed
            ? 0d
            : Math.Max(0d, EditorApplication.timeSinceStartup - StartedAt);

        /// <summary>Consumes and clears an interruption marker left by a domain reload or editor restart.</summary>
        public static bool ConsumeInterruptedMarker(string sessionKey)
        {
            bool interrupted = SessionState.GetBool(sessionKey, false);
            if (interrupted)
                SessionState.SetBool(sessionKey, false);

            return interrupted;
        }

        /// <summary>Captures editor state and disables instrumentation for an unattended campaign.</summary>
        public static EditorCampaignSession Begin(string interruptedSessionKey)
        {
            if (string.IsNullOrWhiteSpace(interruptedSessionKey))
                throw new ArgumentException("An interrupted-session key is required.", nameof(interruptedSessionKey));

            EditorCampaignSession session = new(interruptedSessionKey);

            try
            {
                GenerationProfilerService.SetProfilingEnabled(false);
                EditorApplication.LockReloadAssemblies();
                session._assembliesLocked = true;
                SessionState.SetBool(interruptedSessionKey, true);
                return session;
            }
            catch
            {
                try
                {
                    session.Dispose();
                }
                catch
                {
                    // Preserve the original startup exception.
                }

                throw;
            }
        }

        /// <summary>Restores all editor-global state owned by this campaign.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Exception failure = null;

            TryCleanup(RestoreOriginalScenes, ref failure);
            TryCleanup(() => GenerationProfilerService.SetProfilingEnabled(_profilingWasEnabled), ref failure);
            TryCleanup(() => SessionState.SetBool(_interruptedSessionKey, false), ref failure);

            if (_assembliesLocked)
            {
                _assembliesLocked = false;
                TryCleanup(EditorApplication.UnlockReloadAssemblies, ref failure);
            }

            if (failure != null)
                throw new InvalidOperationException("Campaign editor state could not be restored completely.", failure);
        }

        private void RestoreOriginalScenes()
        {
            if (_originalSceneSetup.Length > 0 &&
                Array.Exists(_originalSceneSetup, setup => !string.IsNullOrWhiteSpace(setup.path)))
            {
                EditorSceneManager.RestoreSceneManagerSetup(_originalSceneSetup);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_originalScenePath) &&
                !string.Equals(
                    SceneManager.GetActiveScene().path,
                    _originalScenePath,
                    StringComparison.Ordinal))
            {
                EditorSceneManager.OpenScene(_originalScenePath, OpenSceneMode.Single);
            }
        }

        private static void TryCleanup(Action cleanup, ref Exception failure)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }
    }
}
