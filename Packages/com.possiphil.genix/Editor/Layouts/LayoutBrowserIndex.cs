using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Genix.Areas;
using Genix.Editor.Infrastructure;
using Genix.Layouts;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Layouts
{
    /// <summary>A non-blocking snapshot used by the Content window's layout browser.</summary>
    internal readonly struct LayoutBrowserSnapshot
    {
        public LayoutBrowserSnapshot(LayoutBrowserIndexEntry[] entries, bool isLoading, float progress)
        {
            Entries = entries ?? Array.Empty<LayoutBrowserIndexEntry>();
            IsLoading = isLoading;
            Progress = Mathf.Clamp01(progress);
        }

        public LayoutBrowserIndexEntry[] Entries { get; }
        public bool IsLoading { get; }
        public float Progress { get; }
    }

    [Serializable]
    internal sealed class LayoutBrowserIndexEntry
    {
        [SerializeField] private string assetPath;
        [SerializeField] private string scenePath;
        [SerializeField] private string displayName;
        [SerializeField] private string notes;
        [SerializeField] private bool favorite;
        [SerializeField] private bool locked;
        [SerializeField] private string sceneName;
        [SerializeField] private string targetAreaId;
        [SerializeField] private string targetAreaName;
        [SerializeField] private string styleName;
        [SerializeField] private string assetPoolName;
        [SerializeField] private int objectCount;
        [SerializeField] private string createdAt;
        [SerializeField] private List<string> assetNames = new();

        public string AssetPath => assetPath;
        public string DisplayName => displayName ?? string.Empty;
        public string Notes => notes ?? string.Empty;
        public bool Favorite => favorite;
        public bool Locked => locked;
        public string SceneName => sceneName ?? string.Empty;
        public string TargetAreaName => targetAreaName ?? string.Empty;
        public string StyleName => styleName ?? string.Empty;
        public string AssetPoolName => assetPoolName ?? string.Empty;
        public int ObjectCount => objectCount;
        public string CreatedAt => createdAt ?? string.Empty;
        public IReadOnlyList<string> AssetNames => assetNames != null
            ? assetNames
            : Array.Empty<string>();

        public static LayoutBrowserIndexEntry FromLayout(SavedLayout layout, string path)
        {
            return new LayoutBrowserIndexEntry
            {
                assetPath = path ?? string.Empty,
                scenePath = layout ? layout.ScenePath ?? string.Empty : string.Empty,
                displayName = layout ? layout.DisplayName ?? string.Empty : string.Empty,
                notes = layout ? layout.Notes ?? string.Empty : string.Empty,
                favorite = layout && layout.Favorite,
                locked = layout && layout.Locked,
                sceneName = layout ? layout.SceneName ?? string.Empty : string.Empty,
                targetAreaId = layout ? layout.TargetAreaId ?? string.Empty : string.Empty,
                targetAreaName = layout ? layout.TargetAreaName ?? string.Empty : string.Empty,
                styleName = layout ? layout.StyleName ?? string.Empty : string.Empty,
                assetPoolName = layout && layout.AssetPool ? layout.AssetPool.name : string.Empty,
                objectCount = layout ? layout.ObjectCount : 0,
                createdAt = layout ? layout.CreatedAt ?? string.Empty : string.Empty,
                assetNames = layout
                    ? layout.AssetSummaries
                        .Select(summary => summary.AssetName ?? string.Empty)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    : new List<string>()
            };
        }

        public SavedLayout LoadAsset() =>
            AssetDatabase.LoadAssetAtPath<SavedLayout>(assetPath);

        public bool MatchesScene(string path) =>
            string.Equals(scenePath, path ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        public bool MatchesArea(string path, string areaId, string areaName)
        {
            if (!MatchesScene(path))
                return false;

            return !string.IsNullOrWhiteSpace(areaId)
                ? string.Equals(targetAreaId, areaId, StringComparison.Ordinal)
                : string.Equals(targetAreaName, areaName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [FilePath("Library/Genix/LayoutBrowserIndex.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class LayoutBrowserIndexStore : ScriptableSingleton<LayoutBrowserIndexStore>
    {
        [SerializeField] private int schemaVersion;
        [SerializeField] private bool complete;
        [SerializeField] private List<LayoutBrowserIndexEntry> entries = new();

        public bool IsCurrent(int expectedSchemaVersion) =>
            complete && schemaVersion == expectedSchemaVersion;

        public IReadOnlyList<LayoutBrowserIndexEntry> Entries => entries;

        public void Replace(int newSchemaVersion, IEnumerable<LayoutBrowserIndexEntry> newEntries)
        {
            schemaVersion = newSchemaVersion;
            complete = true;
            entries = newEntries?.Where(entry => entry != null).ToList() ?? new List<LayoutBrowserIndexEntry>();
            Save(true);
        }

        public void ApplyChanges(
            IEnumerable<string> removedPaths,
            IEnumerable<LayoutBrowserIndexEntry> changedEntries)
        {
            HashSet<string> removed = new(
                removedPaths?.Where(IsLayoutAssetPath) ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            if (removed.Count > 0)
                entries.RemoveAll(entry => entry == null || removed.Contains(entry.AssetPath));

            foreach (LayoutBrowserIndexEntry changedEntry in changedEntries ?? Array.Empty<LayoutBrowserIndexEntry>())
            {
                if (changedEntry == null || !IsLayoutAssetPath(changedEntry.AssetPath))
                    continue;

                entries.RemoveAll(entry => entry != null &&
                    string.Equals(entry.AssetPath, changedEntry.AssetPath, StringComparison.OrdinalIgnoreCase));
                entries.Add(changedEntry);
            }

            Save(true);
        }

        public void MarkStale()
        {
            complete = false;
            entries.Clear();
            Save(true);
        }

        private static bool IsLayoutAssetPath(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
            (path.Equals(ProjectContentPaths.Layouts, StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith(ProjectContentPaths.Layouts + "/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Maintains lightweight layout metadata without loading SavedLayout assets for list browsing.
    /// </summary>
    internal static class LayoutBrowserIndex
    {
        private const int SchemaVersion = 2;
        private const double WorkBudgetSeconds = 0.004;

        private static string[] _buildGuids;
        private static int _buildCursor;
        private static List<LayoutBrowserIndexEntry> _buildEntries;

        private static string _queryKey;
        private static LayoutBrowserIndexEntry[] _queryEntries;

        public static LayoutBrowserSnapshot BrowseAll() =>
            Browse("all", _ => true);

        public static LayoutBrowserSnapshot BrowseCurrentScene()
        {
            string scenePath = SceneManager.GetActiveScene().path ?? string.Empty;
            return Browse($"scene:{scenePath}", entry => entry.MatchesScene(scenePath));
        }

        public static LayoutBrowserSnapshot BrowseArea(IAreaSource areaSource)
        {
            if (areaSource == null)
                return new LayoutBrowserSnapshot(Array.Empty<LayoutBrowserIndexEntry>(), false, 1f);

            string scenePath = SceneManager.GetActiveScene().path ?? string.Empty;
            string areaId = areaSource.SourceInfo.SourceId ?? string.Empty;
            string areaName = areaSource.SourceInfo.SourceName ?? string.Empty;
            string key = $"area:{scenePath}:{areaId}:{areaName}";
            return Browse(key, entry => entry.MatchesArea(scenePath, areaId, areaName));
        }

        public static void MarkStale()
        {
            LayoutBrowserIndexStore.instance.MarkStale();
            ResetBuild();
            ResetQuery();
        }

        public static void Refresh(SavedLayout layout)
        {
            if (!layout)
                return;

            string path = AssetDatabase.GetAssetPath(layout);
            if (!IsLayoutPath(path))
                return;

            LayoutBrowserIndexStore store = LayoutBrowserIndexStore.instance;
            if (!store.IsCurrent(SchemaVersion))
                return;

            store.ApplyChanges(
                Array.Empty<string>(),
                new[] { LayoutBrowserIndexEntry.FromLayout(layout, path) });
            ResetQuery();
        }

        public static void ApplyAssetChanges(
            IReadOnlyList<string> importedPaths,
            IReadOnlyList<string> deletedPaths,
            IReadOnlyList<string> movedPaths,
            IReadOnlyList<string> movedFromPaths)
        {
            bool affectsLayouts = ContainsLayoutPath(importedPaths) ||
                                  ContainsLayoutPath(deletedPaths) ||
                                  ContainsLayoutPath(movedPaths) ||
                                  ContainsLayoutPath(movedFromPaths);
            if (!affectsLayouts)
                return;

            LayoutBrowserIndexStore store = LayoutBrowserIndexStore.instance;
            if (!store.IsCurrent(SchemaVersion))
            {
                ResetBuild();
                ResetQuery();
                return;
            }

            List<LayoutBrowserIndexEntry> changedEntries = new();
            foreach (string path in EnumerateLayoutPaths(importedPaths, movedPaths))
            {
                SavedLayout layout = AssetDatabase.LoadAssetAtPath<SavedLayout>(path);
                if (layout)
                    changedEntries.Add(LayoutBrowserIndexEntry.FromLayout(layout, path));
            }

            store.ApplyChanges(
                EnumerateLayoutPaths(deletedPaths, movedFromPaths),
                changedEntries);
            ResetQuery();
        }

        private static LayoutBrowserSnapshot Browse(
            string queryKey,
            Func<LayoutBrowserIndexEntry, bool> predicate)
        {
            if (!ContinueIndexBuild())
                return new LayoutBrowserSnapshot(
                    Array.Empty<LayoutBrowserIndexEntry>(),
                    true,
                    GetIndexBuildProgress());

            if (!string.Equals(_queryKey, queryKey, StringComparison.Ordinal))
                StartQuery(queryKey, predicate);

            return new LayoutBrowserSnapshot(_queryEntries, false, 1f);
        }

        private static bool ContinueIndexBuild()
        {
            LayoutBrowserIndexStore store = LayoutBrowserIndexStore.instance;
            if (store.IsCurrent(SchemaVersion))
                return true;

            if (_buildGuids == null)
            {
                _buildGuids = AssetDatabase.IsValidFolder(ProjectContentPaths.Layouts)
                    ? AssetDatabase.FindAssets(
                        $"t:{nameof(SavedLayout)}",
                        new[] { ProjectContentPaths.Layouts })
                    : Array.Empty<string>();
                _buildCursor = 0;
                _buildEntries = new List<LayoutBrowserIndexEntry>(_buildGuids.Length);
            }

            if (_buildGuids.Length == 0)
            {
                store.Replace(SchemaVersion, Array.Empty<LayoutBrowserIndexEntry>());
                ResetBuild();
                return true;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            do
            {
                string path = AssetDatabase.GUIDToAssetPath(_buildGuids[_buildCursor]);
                SavedLayout layout = AssetDatabase.LoadAssetAtPath<SavedLayout>(path);
                if (layout)
                    _buildEntries.Add(LayoutBrowserIndexEntry.FromLayout(layout, path));

                _buildCursor++;
            }
            while (_buildCursor < _buildGuids.Length && stopwatch.Elapsed.TotalSeconds < WorkBudgetSeconds);

            if (_buildCursor < _buildGuids.Length)
                return false;

            store.Replace(SchemaVersion, _buildEntries);
            ResetBuild();
            ResetQuery();
            return true;
        }

        private static void StartQuery(
            string queryKey,
            Func<LayoutBrowserIndexEntry, bool> predicate)
        {
            _queryKey = queryKey;
            _queryEntries = LayoutBrowserIndexStore.instance.Entries
                .Where(entry => entry != null && predicate(entry))
                .ToArray();
        }

        private static float GetIndexBuildProgress()
        {
            if (_buildGuids == null || _buildGuids.Length == 0)
                return 0f;

            return _buildCursor / (float)_buildGuids.Length;
        }

        private static void ResetBuild()
        {
            _buildGuids = null;
            _buildCursor = 0;
            _buildEntries = null;
        }

        private static void ResetQuery()
        {
            _queryKey = null;
            _queryEntries = null;
        }

        private static bool ContainsLayoutPath(IEnumerable<string> paths) =>
            paths != null && paths.Any(IsLayoutPath);

        private static IEnumerable<string> EnumerateLayoutPaths(
            IEnumerable<string> first,
            IEnumerable<string> second)
        {
            return (first ?? Array.Empty<string>())
                .Concat(second ?? Array.Empty<string>())
                .Where(IsLayoutPath)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsLayoutPath(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            (path.Equals(ProjectContentPaths.Layouts, StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith(ProjectContentPaths.Layouts + "/", StringComparison.OrdinalIgnoreCase));
    }

    internal sealed class LayoutBrowserIndexPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            LayoutBrowserIndex.ApplyAssetChanges(
                importedAssets,
                deletedAssets,
                movedAssets,
                movedFromAssetPaths);
        }
    }
}
