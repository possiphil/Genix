using System;
using System.IO;
using Genix.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Evaluation
{
    /// <summary>Materializes read-only package scenes as disposable project assets.</summary>
    internal static class EvaluationSceneWorkspace
    {
        private const string PackagePrefix = "Packages/";

        public static bool TryPrepare(
            string sourceScenePath,
            out string writableScenePath,
            out string error)
        {
            writableScenePath = GetWritableScenePath(sourceScenePath);
            error = string.Empty;

            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(sourceScenePath))
            {
                error = $"Evaluation scene '{sourceScenePath}' is missing.";
                return false;
            }

            if (string.Equals(writableScenePath, sourceScenePath, StringComparison.OrdinalIgnoreCase))
                return true;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(writableScenePath))
                return true;

            string folderPath = Path.GetDirectoryName(writableScenePath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                error = $"Could not determine a workspace folder for '{sourceScenePath}'.";
                return false;
            }

            AssetFileService.EnsureFolder(folderPath);
            if (!AssetDatabase.CopyAsset(sourceScenePath, writableScenePath))
            {
                error = $"Could not create an editable copy of '{sourceScenePath}'.";
                return false;
            }

            AssetDatabase.ImportAsset(writableScenePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(writableScenePath))
                return true;

            error = $"Unity did not import the editable evaluation scene '{writableScenePath}'.";
            return false;
        }

        public static string GetWritableScenePath(string sourceScenePath)
        {
            if (string.IsNullOrWhiteSpace(sourceScenePath) ||
                !sourceScenePath.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return sourceScenePath ?? string.Empty;
            }

            string relativePath = sourceScenePath.Substring(PackagePrefix.Length);
            string directory = Path.GetDirectoryName(relativePath)?.Replace("\\", "/");
            string fileName = Path.GetFileNameWithoutExtension(relativePath);
            string extension = Path.GetExtension(relativePath);
            string dependencyHash = AssetDatabase.GetAssetDependencyHash(sourceScenePath).ToString();
            string versionedFileName = $"{fileName}_{dependencyHash}{extension}";
            return string.IsNullOrWhiteSpace(directory)
                ? $"{DevToolsContentPaths.EvaluationWorkspace}/{versionedFileName}"
                : $"{DevToolsContentPaths.EvaluationWorkspace}/{directory}/{versionedFileName}";
        }

        public static bool MatchesSource(string scenePath, string sourceScenePath) =>
            !string.IsNullOrWhiteSpace(scenePath) &&
            (string.Equals(scenePath, sourceScenePath, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 scenePath,
                 GetWritableScenePath(sourceScenePath),
                 StringComparison.OrdinalIgnoreCase));
    }
}
