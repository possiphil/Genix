using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Editor.Infrastructure;
using Genix.Layouts;
using UnityEditor;

namespace Genix.Editor.Layouts
{
    internal static class LayoutRepository
    {
        public static SavedLayout[] LoadAll()
        {
            if (!AssetDatabase.IsValidFolder(ProjectContentPaths.Layouts))
                return Array.Empty<SavedLayout>();

            return AssetDatabase
                .FindAssets($"t:{nameof(SavedLayout)}", new[] { ProjectContentPaths.Layouts })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SavedLayout>)
                .Where(layout => layout)
                .OrderByDescending(layout => layout.Favorite)
                .ThenByDescending(layout => layout.CreatedAt, StringComparer.OrdinalIgnoreCase)
                .ThenBy(layout => layout.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static SavedLayout[] LoadForArea(IAreaSource areaSource)
        {
            return areaSource == null
                ? Array.Empty<SavedLayout>()
                : LoadAll().Where(layout => MatchesArea(layout, areaSource)).ToArray();
        }

        public static bool MatchesArea(SavedLayout layout, IAreaSource areaSource)
        {
            if (!layout || areaSource == null)
                return false;

            string sourceId = areaSource.SourceInfo.SourceId;
            return !string.IsNullOrWhiteSpace(sourceId)
                ? string.Equals(layout.TargetAreaId, sourceId, StringComparison.Ordinal)
                : string.Equals(
                    layout.TargetAreaName,
                    areaSource.SourceInfo.SourceName,
                    StringComparison.OrdinalIgnoreCase);
        }

        public static bool Delete(SavedLayout layout, out string error)
        {
            error = string.Empty;

            if (!layout)
            {
                error = "No layout is selected.";
                return false;
            }

            if (layout.Locked)
            {
                error = $"Layout '{layout.DisplayName}' is locked. Unlock it before deleting.";
                return false;
            }

            string layoutPath = AssetDatabase.GetAssetPath(layout);

            if (string.IsNullOrWhiteSpace(layoutPath))
            {
                error = $"Could not find the asset path for layout '{layout.DisplayName}'.";
                return false;
            }

            DeleteOwnedPrefab(layout);
            AssetDatabase.DeleteAsset(layoutPath);
            FinishChanges();
            return true;
        }

        public static bool ClearUnlocked(out int deletedCount, out string error)
        {
            deletedCount = 0;
            error = string.Empty;
            SavedLayout[] layouts = LoadAll();
            HashSet<string> prefabPaths = new(StringComparer.OrdinalIgnoreCase);
            List<string> layoutPaths = new();

            foreach (SavedLayout layout in layouts.Where(layout => layout && !layout.Locked))
            {
                string layoutPath = AssetDatabase.GetAssetPath(layout);

                if (!string.IsNullOrWhiteSpace(layoutPath))
                    layoutPaths.Add(layoutPath);

                string prefabPath = GetOwnedPrefabPath(layout);

                if (!string.IsNullOrWhiteSpace(prefabPath))
                    prefabPaths.Add(prefabPath);
            }

            foreach (string prefabPath in prefabPaths)
                AssetDatabase.DeleteAsset(prefabPath);

            foreach (string layoutPath in layoutPaths)
            {
                if (AssetDatabase.DeleteAsset(layoutPath))
                    deletedCount++;
            }

            FinishChanges();
            return true;
        }

        private static void DeleteOwnedPrefab(SavedLayout layout)
        {
            string prefabPath = GetOwnedPrefabPath(layout);

            if (!string.IsNullOrWhiteSpace(prefabPath))
                AssetDatabase.DeleteAsset(prefabPath);
        }

        private static string GetOwnedPrefabPath(SavedLayout layout)
        {
            string path = layout.Prefab ? AssetDatabase.GetAssetPath(layout.Prefab) : string.Empty;
            return path.StartsWith(ProjectContentPaths.Layouts, StringComparison.OrdinalIgnoreCase)
                ? path
                : string.Empty;
        }

        private static void FinishChanges()
        {
            LayoutPreviewService.ClearAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
