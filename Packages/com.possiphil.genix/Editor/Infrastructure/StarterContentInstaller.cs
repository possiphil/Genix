using System;
using System.Reflection;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Generation;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Infrastructure
{
    /// <summary>Creates a self-contained first-run setup without overwriting user-authored content.</summary>
    internal static class StarterContentInstaller
    {
        private const string SceneBridgeTypeName =
            "Genix.SpaceFoundation.Editor.StarterRoomSceneBuilder, Genix.SpaceFoundation.Editor";

        private static readonly GUIContent SetupButtonContent = new(
            "Set Up Starter Content",
            "Create a small editable room, example assets, semantic tags, pools, styles, and a reusable generation preset.");

        public static bool ShouldOfferSetup()
        {
            if (StarterContentBuilder.IsInstalled)
                return false;

            // Keep the recovery action visible when a previous setup stopped partway through.
            if (AssetDatabase.IsValidFolder(StarterContentBuilder.Root) ||
                AssetDatabase.IsValidFolder(StarterContentBuilder.DefinitionsRoot))
            {
                return true;
            }

            bool hasAssets = AssetDatabase.FindAssets(
                $"t:{nameof(AssetDefinition)}",
                new[] { ProjectContentPaths.AssetsRoot }).Length > 0;
            bool hasPools = AssetDatabase.FindAssets(
                $"t:{nameof(AssetPool)}",
                new[] { ProjectContentPaths.AssetsRoot }).Length > 0;
            bool hasStyles = AssetDatabase.FindAssets(
                $"t:{nameof(StylePreset)}",
                new[] { ProjectContentPaths.StylePresets }).Length > 0;

            return !hasAssets && !hasPools && !hasStyles;
        }

        public static void DrawSetupButton()
        {
            if (!ShouldOfferSetup())
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(SetupButtonContent, GUILayout.Width(180f)))
                    Import();
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(6f);
        }

        public static void Import()
        {
            StarterContentBuildResult result = StarterContentBuilder.Build();
            if (!result.Success)
            {
                EditorUtility.DisplayDialog(
                    "Genix Starter Content",
                    $"Starter Content could not be created.\n\n{result.Error}",
                    "OK");
                return;
            }

            bool sceneReady = TryCreateStarterRoom(out string sceneError);
            AssetCatalogService.Refresh();

            if (result.GenerationPreset)
                GenerationPresetPreferences.SetDefault(result.GenerationPreset);

            string summary = result.CreatedCount == 0
                ? "Starter Content is already set up."
                : $"Starter Content is ready. Created {result.CreatedCount} asset(s) and reused {result.ReusedCount}.";

            if (!sceneReady)
            {
                EditorUtility.DisplayDialog(
                    "Genix Starter Content",
                    $"{summary}\n\nThe Starter Room scene could not be created automatically:\n{sceneError}",
                    "OK");
                return;
            }

            bool openScene = EditorUtility.DisplayDialog(
                "Genix Starter Content",
                $"{summary}\n\nOpen the Starter Room now and compute its SFS graph?",
                "Open Starter Room",
                "Later");

            if (openScene && !TryOpenAndComputeStarterRoom(out string openError))
                EditorUtility.DisplayDialog("Genix Starter Content", openError, "OK");
        }

        private static bool TryCreateStarterRoom(out string error)
        {
            return InvokeSceneBridge("TryCreate", out error);
        }

        private static bool TryOpenAndComputeStarterRoom(out string error)
        {
            return InvokeSceneBridge("TryOpenAndCompute", out error);
        }

        private static bool InvokeSceneBridge(string methodName, out string error)
        {
            Type bridgeType = Type.GetType(SceneBridgeTypeName);
            MethodInfo method = bridgeType?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

            if (method == null)
            {
                error = "The Space Foundation integration is unavailable. Install or enable SFS, then use SFS Authoring to create a room.";
                return false;
            }

            object[] arguments = { null };
            try
            {
                bool success = method.Invoke(null, arguments) is true;
                error = arguments[0] as string ?? string.Empty;
                return success;
            }
            catch (TargetInvocationException exception)
            {
                Exception cause = exception.InnerException ?? exception;
                error = cause.Message;
                Debug.LogException(cause);
                return false;
            }
        }
    }
}
