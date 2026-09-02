using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Infrastructure;
using Genix.Profiling;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Profiling
{
    /// <summary>Maintains the project-level catalog of persisted generation profile reports.</summary>
    internal static class GenerationProfileCatalogService
    {
        private const string CatalogPath = DevToolsContentPaths.Profiles + "/GenerationProfileCatalog.asset";

        public static GenerationProfileCatalog GetOrCreate()
        {
            GenerationProfileCatalog catalog = AssetDatabase.LoadAssetAtPath<GenerationProfileCatalog>(CatalogPath);

            if (catalog)
                return catalog;

            AssetFileService.EnsureFolder(DevToolsContentPaths.Profiles);
            catalog = ScriptableObject.CreateInstance<GenerationProfileCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        public static void Refresh()
        {
            GenerationProfileCatalog catalog = GetOrCreate();
            List<GenerationProfileReport> reports = AssetFileService.FindAssets<GenerationProfileReport>(DevToolsContentPaths.Profiles)
                .OrderByDescending(report => report.CreatedAt)
                .ToList();

            if (catalog.Reports.SequenceEqual(reports))
                return;

            catalog.SetReports(reports);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void RegisterReport(GenerationProfileReport report)
        {
            GenerationProfileCatalog catalog = GetOrCreate();

            catalog.AddReport(report);
            catalog.RemoveMissingReports();

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void DeleteReport(GenerationProfileReport report)
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
            GenerationProfileCatalog catalog = GetOrCreate();
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(GenerationProfileReport)}", new[] { DevToolsContentPaths.Profiles });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrWhiteSpace(path))
                    AssetDatabase.DeleteAsset(path);
            }

            catalog.SetReports(Array.Empty<GenerationProfileReport>());

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
