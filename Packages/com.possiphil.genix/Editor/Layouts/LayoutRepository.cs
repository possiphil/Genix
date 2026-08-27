using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Areas;
using Genix.Editor.Infrastructure;
using Genix.Layouts;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Layouts
{
    /// <summary>Loads, filters, deletes, and bulk-clears saved layout assets.</summary>
    internal static class LayoutRepository
    {
        private static SavedLayout[] _cachedLayouts;

        public static SavedLayout[] LoadAll()
        {
            if (_cachedLayouts != null)
                return _cachedLayouts;

            if (!AssetDatabase.IsValidFolder(ProjectContentPaths.Layouts))
                return _cachedLayouts = Array.Empty<SavedLayout>();

            return _cachedLayouts = AssetDatabase
                .FindAssets($"t:{nameof(SavedLayout)}", new[] { ProjectContentPaths.Layouts })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SavedLayout>)
                .Where(layout => layout)
                .OrderByDescending(layout => layout.Favorite)
                .ThenByDescending(layout => layout.CreatedAt, StringComparer.OrdinalIgnoreCase)
                .ThenBy(layout => layout.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static void InvalidateCache() => _cachedLayouts = null;

        public static SavedLayout[] LoadForArea(IAreaSource areaSource)
        {
            return areaSource == null
                ? Array.Empty<SavedLayout>()
                : LoadAll().Where(layout => MatchesArea(layout, areaSource)).ToArray();
        }

        public static SavedLayout[] LoadForCurrentScene()
        {
            return LoadAll().Where(MatchesCurrentScene).ToArray();
        }

        public static bool MatchesArea(SavedLayout layout, IAreaSource areaSource)
        {
            if (!layout || areaSource == null)
                return false;

            if (!MatchesCurrentScene(layout))
                return false;

            string sourceId = areaSource.SourceInfo.SourceId;
            return !string.IsNullOrWhiteSpace(sourceId)
                ? string.Equals(layout.TargetAreaId, sourceId, StringComparison.Ordinal)
                : string.Equals(
                    layout.TargetAreaName,
                    areaSource.SourceInfo.SourceName,
                    StringComparison.OrdinalIgnoreCase);
        }

        public static bool MatchesCurrentScene(SavedLayout layout)
        {
            if (!layout)
                return false;

            string activeScenePath = SceneManager.GetActiveScene().path ?? string.Empty;

            if (string.IsNullOrWhiteSpace(activeScenePath))
                return string.IsNullOrWhiteSpace(layout.ScenePath);

            return !string.IsNullOrWhiteSpace(layout.ScenePath) &&
                   string.Equals(layout.ScenePath, activeScenePath, StringComparison.OrdinalIgnoreCase);
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

            string prefabPath = GetOwnedPrefabPath(layout);
            HashSet<string> cleanupFolders = new(StringComparer.OrdinalIgnoreCase);
            AddOwnedFolderCandidates(cleanupFolders, layoutPath);
            AddOwnedFolderCandidates(cleanupFolders, prefabPath);

            if (!string.IsNullOrWhiteSpace(prefabPath))
                AssetDatabase.DeleteAsset(prefabPath);

            AssetDatabase.DeleteAsset(layoutPath);
            DeleteEmptyOwnedFolders(cleanupFolders);
            FinishChanges();
            return true;
        }

        public static bool ClearUnlocked(out int deletedCount, out string error)
        {
            deletedCount = 0;
            error = string.Empty;
            SavedLayout[] layouts = LoadAll();
            DeleteUnlocked(layouts, out deletedCount);
            return true;
        }

        public static bool ClearUnlockedForArea(IAreaSource areaSource, out int deletedCount, out string error)
        {
            deletedCount = 0;

            if (areaSource == null)
            {
                error = "No Target Area is selected.";
                return false;
            }

            error = string.Empty;
            SavedLayout[] layouts = LoadForArea(areaSource);
            DeleteUnlocked(layouts, out deletedCount);
            return true;
        }

        public static bool DeleteMany(
            IEnumerable<SavedLayout> layouts,
            bool includeLocked,
            out int deletedCount,
            out string error)
        {
            deletedCount = 0;
            error = string.Empty;

            SavedLayout[] targets = layouts?
                .Where(layout => layout && (includeLocked || !layout.Locked))
                .Distinct()
                .ToArray() ?? Array.Empty<SavedLayout>();
            if (targets.Length == 0)
                return true;

            HashSet<string> prefabPaths = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> cleanupFolders = new(StringComparer.OrdinalIgnoreCase);
            List<string> layoutPaths = new();
            foreach (SavedLayout layout in targets)
            {
                string layoutPath = AssetDatabase.GetAssetPath(layout);
                if (!string.IsNullOrWhiteSpace(layoutPath))
                {
                    layoutPaths.Add(layoutPath);
                    AddOwnedFolderCandidates(cleanupFolders, layoutPath);
                }

                string prefabPath = GetOwnedPrefabPath(layout);
                if (!string.IsNullOrWhiteSpace(prefabPath))
                {
                    prefabPaths.Add(prefabPath);
                    AddOwnedFolderCandidates(cleanupFolders, prefabPath);
                }
            }

            LayoutPreviewService.ClearAll();
            AssetDatabase.StartAssetEditing();
            try
            {
                int completed = 0;
                int total = prefabPaths.Count + layoutPaths.Count;
                foreach (string prefabPath in prefabPaths)
                {
                    ShowDeleteProgress(completed++, total, prefabPath);
                    AssetDatabase.DeleteAsset(prefabPath);
                }

                foreach (string layoutPath in layoutPaths)
                {
                    ShowDeleteProgress(completed++, total, layoutPath);
                    if (AssetDatabase.DeleteAsset(layoutPath))
                        deletedCount++;
                }
            }
            catch (Exception exception)
            {
                error = $"Layout deletion stopped: {exception.Message}";
                return false;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                DeleteEmptyOwnedFolders(cleanupFolders);
                InvalidateCache();
                AssetDatabase.SaveAssets();
            }

            return true;
        }

        private static void DeleteUnlocked(IEnumerable<SavedLayout> layouts, out int deletedCount)
        {
            DeleteMany(layouts, false, out deletedCount, out _);
        }

        private static void ShowDeleteProgress(int completed, int total, string path)
        {
            if (completed % 25 != 0 && completed + 1 < total)
                return;

            float progress = total > 0 ? completed / (float)total : 1f;
            EditorUtility.DisplayProgressBar("Deleting Genix Layouts", path, progress);
        }

        private static void AddOwnedFolderCandidates(ISet<string> folders, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            string folder = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            string ownedPrefix = ProjectContentPaths.Layouts + "/";

            while (!string.IsNullOrWhiteSpace(folder) &&
                   folder.StartsWith(ownedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                folders.Add(folder);
                folder = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            }
        }

        private static void DeleteEmptyOwnedFolders(IEnumerable<string> folders)
        {
            foreach (string folder in folders.OrderByDescending(path => path.Length))
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;

                if (AssetDatabase.FindAssets(string.Empty, new[] { folder }).Length == 0)
                    AssetDatabase.DeleteAsset(folder);
            }
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
            InvalidateCache();
            AssetDatabase.SaveAssets();
        }
    }
}
