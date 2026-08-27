using System;
using System.Globalization;
using Genix.Editor.Infrastructure;
using Genix.Profiling;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Profiling
{
    /// <summary>Persists an in-memory profile as a timestamped report asset.</summary>
    internal static class GenerationProfileReportSaver
    {
        public static GenerationProfileReport Save(GenerationProfile profile)
        {
            if (profile == null)
                return null;

            AssetFileService.EnsureFolder(DevToolsContentPaths.Profiles);

            DateTime createdAt = DateTime.Now;
            GenerationProfileReport report = ScriptableObject.CreateInstance<GenerationProfileReport>();
            report.Initialize(profile, createdAt);

            string path = CreateReportPath(profile, createdAt);

            AssetDatabase.CreateAsset(report, path);
            EditorUtility.SetDirty(report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GenerationProfileCatalogService.RegisterReport(report);

            Selection.activeObject = report;
            EditorGUIUtility.PingObject(report);

            Debug.Log($"Saved Genix profile report: {path}", report);
            return report;
        }

        private static string CreateReportPath(GenerationProfile profile, DateTime createdAt)
        {
            string timestamp = createdAt.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string target = string.IsNullOrWhiteSpace(profile.TargetName)
                ? "Unknown"
                : SanitizeFileName(profile.TargetName);
            string path = $"{DevToolsContentPaths.Profiles}/GenixProfile_{timestamp}_{target}.asset";

            return AssetDatabase.GenerateUniqueAssetPath(path);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');

            return value.Replace(' ', '_');
        }
    }
}
