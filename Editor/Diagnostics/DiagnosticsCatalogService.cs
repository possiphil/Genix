using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Diagnostics;
using Genix.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Genix.Editor.Diagnostics
{
    public static class DiagnosticsCatalogService
    {
        private const string CatalogPath = ProjectContentPaths.Diagnostics + "/DiagnosticsCatalog.asset";

        public static DiagnosticsCatalog GetOrCreate()
        {
            DiagnosticsCatalog catalog = AssetDatabase.LoadAssetAtPath<DiagnosticsCatalog>(CatalogPath);

            if (catalog)
                return catalog;

            AssetFileService.EnsureFolder(ProjectContentPaths.Diagnostics);
            catalog = ScriptableObject.CreateInstance<DiagnosticsCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        public static void Refresh()
        {
            DiagnosticsCatalog catalog = GetOrCreate();
            List<DiagnosticsReport> reports = AssetFileService.FindAssets<DiagnosticsReport>(ProjectContentPaths.Diagnostics)
                .OrderByDescending(report => report.CreatedAt).ToList();

            catalog.SetReports(reports);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void RegisterReport(DiagnosticsReport report)
        {
            DiagnosticsCatalog catalog = GetOrCreate();

            catalog.AddReport(report);
            catalog.RemoveMissingReports();

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void DeleteReport(DiagnosticsReport report)
        {
            if (!report)
                return;

            string path = AssetDatabase.GetAssetPath(report);

            if (string.IsNullOrWhiteSpace(path))
                return;

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Refresh();
        }

        public static void Clear()
        {
            DiagnosticsCatalog catalog = GetOrCreate();

            string[] guids = AssetDatabase.FindAssets($"t:{nameof(DiagnosticsReport)}", new[] { ProjectContentPaths.Diagnostics });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrWhiteSpace(path))
                    AssetDatabase.DeleteAsset(path);
            }

            catalog.SetReports(Array.Empty<DiagnosticsReport>());

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void ClearReports(DiagnosticsMode mode)
        {
            DiagnosticsCatalog catalog = GetOrCreate();

            foreach (DiagnosticsReport report in catalog.Reports.Where(report => report && report.Mode == mode).ToList())
                DeleteReport(report);

            Refresh();
        }
    }
}
