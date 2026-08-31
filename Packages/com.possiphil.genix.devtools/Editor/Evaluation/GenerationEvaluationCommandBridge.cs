using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Evaluation
{
    /// <summary>Lets local automation start the same evaluation runner used by the Editor window.</summary>
    [InitializeOnLoad]
    internal static class GenerationEvaluationCommandBridge
    {
        private const string RequestPath = "Library/Genix/EvaluationCommandRequest.json";
        private const string ResponsePath = "Library/Genix/EvaluationCommandResponse.json";
        private const string RefreshSessionKey = "Genix.OpenEditorEvaluationBridge.RefreshPending";
        private const double PollIntervalSeconds = 0.25d;

        private static double _nextPoll;
        private static bool _ownsActiveRun;

        static GenerationEvaluationCommandBridge()
        {
            EditorApplication.update += Poll;
            GenerationEvaluationRunner.Changed += HandleRunnerChanged;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll ||
                GenerationEvaluationRunner.IsRunning ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
                return;

            _nextPoll = EditorApplication.timeSinceStartup + PollIntervalSeconds;
            if (!File.Exists(RequestPath))
            {
                SessionState.SetBool(RefreshSessionKey, false);
                return;
            }

            // Local packages can remain stale while Unity is in the background. Keep the request
            // across the possible domain reload and consume it after a forced synchronous import.
            if (!SessionState.GetBool(RefreshSessionKey, false))
            {
                SessionState.SetBool(RefreshSessionKey, true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return;
            }

            SessionState.SetBool(RefreshSessionKey, false);

            EvaluationCommandRequest request;
            try
            {
                request = JsonUtility.FromJson<EvaluationCommandRequest>(File.ReadAllText(RequestPath));
                File.Delete(RequestPath);
            }
            catch (Exception exception)
            {
                WriteResponse(new EvaluationCommandResponse { error = exception.Message });
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(request.suiteAssetPath))
                    throw new InvalidOperationException("A suiteAssetPath is required.");

                GenerationEvaluationSuite suite = AssetDatabase.LoadAssetAtPath<GenerationEvaluationSuite>(
                    request.suiteAssetPath);
                if (!suite)
                    throw new InvalidOperationException($"Evaluation suite was not found at '{request.suiteAssetPath}'.");

                if (request.validateOnly)
                {
                    List<string> errors = GenerationEvaluationRunner.Validate(suite);
                    int enabledScenarios = suite.Scenarios.Count(scenario =>
                        scenario is { Enabled: true });
                    WriteResponse(new EvaluationCommandResponse
                    {
                        status = errors.Count == 0
                            ? $"Validated {enabledScenarios} scenarios and {enabledScenarios * suite.RunsPerScenario} runs."
                            : "Evaluation suite validation failed.",
                        error = string.Join("\n", errors),
                        expectedRuns = enabledScenarios * suite.RunsPerScenario
                    });
                    return;
                }

                if (!GenerationEvaluationRunner.Start(suite, request.scenarioIndex))
                {
                    WriteResponse(new EvaluationCommandResponse
                    {
                        error = GenerationEvaluationRunner.LastError,
                        status = GenerationEvaluationRunner.Status
                    });
                    return;
                }

                _ownsActiveRun = true;
            }
            catch (Exception exception)
            {
                WriteResponse(new EvaluationCommandResponse { error = exception.ToString() });
            }
        }

        private static void HandleRunnerChanged()
        {
            if (!_ownsActiveRun || GenerationEvaluationRunner.IsRunning)
                return;

            _ownsActiveRun = false;
            IReadOnlyCollection<GenerationEvaluationRunRecord> runs = GenerationEvaluationRunner.RunRecords;
            WriteResponse(new EvaluationCommandResponse
            {
                status = GenerationEvaluationRunner.Status,
                error = GenerationEvaluationRunner.LastError,
                completedRuns = runs.Count,
                expectedRuns = GenerationEvaluationRunner.LastExpectedRunCount,
                campaignCompleted = GenerationEvaluationRunner.LastCampaignCompleted,
                campaignCancelled = GenerationEvaluationRunner.LastCampaignCancelled,
                runScope = GenerationEvaluationRunner.LastRunScope,
                failedRuns = runs.Count(run => run.AutomaticVerdict == EvaluationAutomaticVerdict.Failed),
                incompleteRuns = runs.Count(run => run.AutomaticVerdict == EvaluationAutomaticVerdict.Incomplete),
                invalidReviewRuns = runs.Count(run => run.HasInvalidVisualReviewEvidence),
                missingLayoutAssets = runs.Count(run => run.HasMissingLayoutAsset),
                outputDirectory = GenerationEvaluationRunner.LastOutputDirectory,
                reportAssetPath = GenerationEvaluationRunner.LastReport
                    ? AssetDatabase.GetAssetPath(GenerationEvaluationRunner.LastReport)
                    : string.Empty
            });
        }

        private static void WriteResponse(EvaluationCommandResponse response)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResponsePath) ?? "Library/Genix");
            string temporaryPath = ResponsePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(response, true));
            if (File.Exists(ResponsePath))
                File.Delete(ResponsePath);
            File.Move(temporaryPath, ResponsePath);
        }

        [Serializable]
        private sealed class EvaluationCommandRequest
        {
            public string suiteAssetPath = string.Empty;
            public int scenarioIndex = -1;
            public bool validateOnly = false;
        }

        [Serializable]
        private sealed class EvaluationCommandResponse
        {
            public string status = string.Empty;
            public string error = string.Empty;
            public int completedRuns;
            public int expectedRuns;
            public bool campaignCompleted;
            public bool campaignCancelled;
            public string runScope = "Unknown";
            public int failedRuns;
            public int incompleteRuns;
            public int invalidReviewRuns;
            public int missingLayoutAssets;
            public string outputDirectory = string.Empty;
            public string reportAssetPath = string.Empty;
        }
    }
}
