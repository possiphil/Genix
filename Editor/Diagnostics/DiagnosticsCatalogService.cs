using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Diagnostics;
using Genix.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Genix.Editor.Diagnostics
{
    /// <summary>Provides project-level diagnostics catalog operations.</summary>
    public static class DiagnosticsCatalogService
    {
        private const string CatalogPath = ProjectContentPaths.Diagnostics + "/DiagnosticsCatalog.asset";

        /// <summary>Returns the diagnostics catalog, creating it when absent.</summary>
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

        /// <summary>Reloads the backing assets and refreshes derived editor state.</summary>
        public static void Refresh()
        {
            DiagnosticsCatalog catalog = GetOrCreate();
            List<DiagnosticsReport> reports = AssetFileService.FindAssets<DiagnosticsReport>(ProjectContentPaths.Diagnostics)
                .OrderByDescending(report => report.CreatedAt).ToList();

            catalog.SetReports(reports);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds a diagnostics report to the project catalog.</summary>
        public static void RegisterReport(DiagnosticsReport report)
        {
            DiagnosticsCatalog catalog = GetOrCreate();

            catalog.AddReport(report);
            catalog.RemoveMissingReports();

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Deletes a diagnostics report and removes it from the catalog.</summary>
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

        /// <summary>Clears the stored state.</summary>
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

        /// <summary>Clears reports.</summary>
        public static void ClearReports(DiagnosticsMode mode)
        {
            DiagnosticsCatalog catalog = GetOrCreate();

            foreach (DiagnosticsReport report in catalog.Reports.Where(report => report && report.Mode == mode).ToList())
                DeleteReport(report);

            Refresh();
        }
    }
}
