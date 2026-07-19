using System;
using System.Globalization;
using Genix.Diagnostics;
using Genix.Editor.Genix.Editor.Diagnostics;
using Genix.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Diagnostics
{
    public static class DiagnosticsReportSaver
    {
        public static void SaveSummary(GenerationDiagnostics diagnostics)
        {
            Save(diagnostics, DiagnosticsMode.Summary, ProjectContentPaths.DiagnosticSummaries);
        }

        public static void SaveDetailed(GenerationDiagnostics diagnostics)
        {
            Save(diagnostics, DiagnosticsMode.Detailed, ProjectContentPaths.DiagnosticDetails);
        }

        private static void Save(GenerationDiagnostics diagnostics, DiagnosticsMode diagnosticsMode, string folderPath)
        {
            if (diagnostics == null)
                return;

            AssetFileService.EnsureFolder(folderPath);

            DateTime createdAt = DateTime.Now;

            DiagnosticsReport report = ScriptableObject.CreateInstance<DiagnosticsReport>();
            report.Initialize(diagnostics, diagnosticsMode, createdAt);

            string path = CreateReportPath(folderPath, diagnostics, diagnosticsMode, createdAt);

            AssetDatabase.CreateAsset(report, path);
            EditorUtility.SetDirty(report);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            DiagnosticsCatalogService.RegisterReport(report);

            Selection.activeObject = report;
            EditorGUIUtility.PingObject(report);

            Debug.Log($"Saved Genix diagnostics report: {path}", report);
        }

        private static string CreateReportPath(string folderPath, GenerationDiagnostics diagnostics, DiagnosticsMode mode, DateTime createdAt)
        {
            string timestamp = createdAt.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string prefix = mode == DiagnosticsMode.Summary ? "GenixSummary" : "GenixDetailed";

            string fileName = $"{prefix}_{timestamp}.asset";
            string path = $"{folderPath}/{fileName}";

            return AssetDatabase.GenerateUniqueAssetPath(path);
        }

    }
}
