using System;
using System.Collections.Generic;
using System.IO;
using Genix.Areas;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Extensions;
using UnityEditor;
using UnityEngine;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsFoundation = SpaceFoundationSystem.SpaceFoundation;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    internal static class SfsAreaCache
    {
        private const int CacheVersion = 6;
        private const int MaxEntries = 2;
        private const int MaxPersistentEntries = 8;
        private const int MaxPersistentSurfaceCells = 500_000;
        private const string CacheFolderPath = "Assets/Genix/Cache";
        private const string CacheAssetPath = CacheFolderPath + "/SfsAreaCache.asset";

        private static readonly List<Entry> Entries = new();

        public static bool TryGetMemory(
            SfsSpace space,
            SfsAnchor anchor,
            AreaBuildSettings settings,
            float voxelSize,
            out PlacementArea area)
        {
            area = null;

            if (!TryCreateStableSourceKey(space, anchor, out string sourceKey))
                return false;

            string memoryKey = CreateMemoryKey(sourceKey, settings, voxelSize);

            for (int i = 0; i < Entries.Count; i++)
            {
                Entry entry = Entries[i];

                if (entry.MemoryKey != memoryKey)
                    continue;

                area = entry.Area;
                Entries.RemoveAt(i);
                Entries.Insert(0, entry);
                return area != null;
            }

            return false;
        }

        public static bool TryGet(
            SfsSpace space,
            SfsAnchor anchor,
            SpatialSourceInfo sourceInfo,
            AreaBuildSettings settings,
            SfsSubspaceResolutionInfo subspaceInfo,
            HashSet<Vector3Int> subspace,
            int subspaceCellCount,
            float voxelSize,
            Predicate<Collider> isSourceCollider,
            out PlacementArea area)
        {
            string sourceKey = CreateSourceKey(space, anchor, subspaceInfo, subspaceCellCount, voxelSize);
            string key = CreateKey(sourceKey, settings, subspaceCellCount, voxelSize);

            for (int i = 0; i < Entries.Count; i++)
            {
                Entry entry = Entries[i];

                if (entry.Key != key)
                    continue;

                area = entry.Area;
                Entries.RemoveAt(i);
                Entries.Insert(0, entry);
                return area != null;
            }

            if (TryGetPersistent(
                    key,
                    CreateMemoryKey(sourceKey, settings, voxelSize),
                    sourceInfo,
                    settings,
                    subspace,
                    voxelSize,
                    isSourceCollider,
                    out area))
            {
                return true;
            }

            area = null;
            return false;
        }

        public static void Store(
            SfsSpace space,
            SfsAnchor anchor,
            AreaBuildSettings settings,
            SfsSubspaceResolutionInfo subspaceInfo,
            int subspaceCellCount,
            float voxelSize,
            PlacementArea area)
        {
            if (area == null)
                return;

            string sourceKey = CreateSourceKey(space, anchor, subspaceInfo, subspaceCellCount, voxelSize);
            string memoryKey = CreateMemoryKey(sourceKey, settings, voxelSize);
            string key = CreateKey(sourceKey, settings, subspaceCellCount, voxelSize);
            Entries.RemoveAll(entry => entry.Key == key || entry.MemoryKey == memoryKey || entry.Area == null);
            Entries.Insert(0, new Entry(key, memoryKey, area));
            TrimMemory();
            StorePersistent(key, area);
        }

        public static void Clear()
        {
            Entries.Clear();
            SfsAreaCacheAsset store = LoadPersistentStore(false);

            if (!store)
                return;

            store.Clear();
            EditorUtility.SetDirty(store);
            AssetDatabase.SaveAssets();
        }

        private static string CreateSourceKey(
            SfsSpace space,
            SfsAnchor anchor,
            SfsSubspaceResolutionInfo subspaceInfo,
            int subspaceCellCount,
            float voxelSize)
        {
            return !string.IsNullOrWhiteSpace(subspaceInfo.CacheKey)
                ? subspaceInfo.CacheKey
                : CreateFallbackSourceKey(space, anchor, subspaceCellCount, voxelSize);
        }

        private static bool TryCreateStableSourceKey(
            SfsSpace space,
            SfsAnchor anchor,
            out string sourceKey)
        {
            sourceKey = string.Empty;

            if (!SfsPersistentDataReader.TryRead(space, anchor, out PersistentSubspaceData data))
                return TryCreateLiveSnapshotSourceKey(space, anchor, out sourceKey);

            PersistentSubspaceCacheKey key = PersistentSubspaceCacheKey.Create(data);

            if (!key.IsValid)
                return TryCreateLiveSnapshotSourceKey(space, anchor, out sourceKey);

            sourceKey = key.ToStableString();
            return true;
        }

        private static bool TryCreateLiveSnapshotSourceKey(
            SfsSpace space,
            SfsAnchor anchor,
            out string sourceKey)
        {
            sourceKey = string.Empty;

            if (!anchor)
                return false;

            string anchorId = anchor.GetUniqueId();
            SfsFoundation foundation = SfsFoundationUtility.Find(space, anchor);

            if (string.IsNullOrWhiteSpace(anchorId) || !foundation)
                return false;

            PersistentSubspaceCacheKey key = PersistentSubspaceCacheKey.CreateLiveSnapshot(foundation, anchorId);

            if (!key.IsValid)
                return false;

            sourceKey = key.ToStableString();
            return true;
        }

        private static string CreateKey(
            string sourceKey,
            AreaBuildSettings settings,
            int subspaceCellCount,
            float voxelSize)
        {
            return string.Join("|",
                CreateMemoryKey(sourceKey, settings, voxelSize),
                subspaceCellCount);
        }

        private static string CreateMemoryKey(
            string sourceKey,
            AreaBuildSettings settings,
            float voxelSize)
        {
            PlacementTarget targets = settings.placementTargets & PlacementTarget.All;

            if (targets == PlacementTarget.None)
                targets = PlacementTarget.All;

            return string.Join("|",
                CacheVersion,
                sourceKey,
                Mathf.RoundToInt(voxelSize * 100_000f),
                settings.decompositionMode,
                settings.EffectiveSurfaceDiscoveryMode,
                settings.placementSurfaceLayers.value,
                settings.floorSurfaceLayers.value,
                settings.wallSurfaceLayers.value,
                settings.ceilingSurfaceLayers.value,
                Mathf.RoundToInt(settings.floorNormalYThreshold * 1000f),
                Mathf.RoundToInt(settings.ceilingNormalYThreshold * 1000f),
                targets);
        }

        private static string CreateFallbackSourceKey(
            SfsSpace space,
            SfsAnchor anchor,
            int subspaceCellCount,
            float voxelSize)
        {
            SfsFoundation foundation = SfsFoundationUtility.Find(space, anchor);
            string foundationId = foundation
                ? $"{foundation.GetLocalObjectId()}:{foundation.assetName}"
                : "missing-foundation";
            string anchorId = anchor ? anchor.GetUniqueId() : string.Empty;

            return $"{foundationId}|{anchorId}|live|{subspaceCellCount}|{Mathf.RoundToInt(voxelSize * 100_000f)}";
        }

        private static void TrimMemory()
        {
            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(Entries.Count - 1);
        }

        private static bool TryGetPersistent(
            string key,
            string memoryKey,
            SpatialSourceInfo sourceInfo,
            AreaBuildSettings settings,
            HashSet<Vector3Int> subspace,
            float voxelSize,
            Predicate<Collider> isSourceCollider,
            out PlacementArea area)
        {
            area = null;
            SfsAreaCacheAsset store = LoadPersistentStore(false);
            bool hit = store &&
                       store.TryGet(
                           key,
                           sourceInfo,
                           settings,
                           subspace,
                           voxelSize,
                           isSourceCollider,
                           out area);

            if (hit)
            {
                Entries.Insert(0, new Entry(key, memoryKey, area));
                TrimMemory();
            }

            return hit;
        }

        private static void StorePersistent(string key, PlacementArea area)
        {
            if (!CanStorePersistent(area))
                return;

            SfsAreaCacheAsset store = LoadPersistentStore(true);

            if (!store)
                return;

            store.Store(key, area, MaxPersistentEntries, MaxPersistentSurfaceCells);
            EditorUtility.SetDirty(store);
            AssetDatabase.SaveAssets();
        }

        private static bool CanStorePersistent(PlacementArea area)
        {
            if (area == null)
                return false;

            int surfaceCellCount = (area.FloorCells?.Count ?? 0) + (area.CeilingCells?.Count ?? 0);
            return surfaceCellCount <= MaxPersistentSurfaceCells;
        }

        private static SfsAreaCacheAsset LoadPersistentStore(bool create)
        {
            SfsAreaCacheAsset store = AssetDatabase.LoadAssetAtPath<SfsAreaCacheAsset>(CacheAssetPath);

            if (store || !create)
                return store;

            EnsureFolder(CacheFolderPath);
            store = ScriptableObject.CreateInstance<SfsAreaCacheAsset>();
            AssetDatabase.CreateAsset(store, CacheAssetPath);
            AssetDatabase.SaveAssets();
            return store;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private readonly struct Entry
        {
            public string Key { get; }
            public string MemoryKey { get; }
            public PlacementArea Area { get; }

            public Entry(string key, string memoryKey, PlacementArea area)
            {
                Key = key;
                MemoryKey = memoryKey;
                Area = area;
            }
        }
    }

    internal sealed class SfsAreaCacheAsset : ScriptableObject
    {
        [SerializeField] private List<PersistentEntry> entries = new();

        public bool TryGet(
            string key,
            SpatialSourceInfo sourceInfo,
            AreaBuildSettings settings,
            HashSet<Vector3Int> subspace,
            float voxelSize,
            Predicate<Collider> isSourceCollider,
            out PlacementArea area)
        {
            area = null;
            PersistentEntry entry = entries.Find(item => item.Key == key);

            if (entry == null)
                return false;

            return entry.TryCreateArea(
                sourceInfo,
                settings,
                subspace,
                voxelSize,
                isSourceCollider,
                out area);
        }

        public void Store(
            string key,
            PlacementArea area,
            int maxEntries,
            int maxSurfaceCells)
        {
            entries.RemoveAll(entry => entry.Key == key || !entry.IsValid);
            entries.Insert(0, new PersistentEntry(key, area));
            Trim(maxEntries, maxSurfaceCells);
        }

        public void Clear()
        {
            entries.Clear();
        }

        private void Trim(int maxEntries, int maxSurfaceCells)
        {
            while (entries.Count > Mathf.Max(1, maxEntries))
                entries.RemoveAt(entries.Count - 1);

            while (GetSurfaceCellCount() > maxSurfaceCells && entries.Count > 0)
                entries.RemoveAt(entries.Count - 1);
        }

        private int GetSurfaceCellCount()
        {
            int count = 0;

            foreach (PersistentEntry entry in entries)
                count += entry.SurfaceCellCount;

            return count;
        }

        [Serializable]
        private sealed class PersistentEntry
        {
            [SerializeField] private string key;
            [SerializeField] private Bounds bounds;
            [SerializeField] private List<SurfaceEntry> floors = new();
            [SerializeField] private List<SurfaceEntry> walls = new();
            [SerializeField] private List<SurfaceEntry> ceilings = new();
            [SerializeField] private List<Vector3Int> floorCells = new();
            [SerializeField] private List<Vector3Int> ceilingCells = new();

            public string Key => key;
            public bool IsValid => !string.IsNullOrWhiteSpace(key);
            public int SurfaceCellCount => (floorCells?.Count ?? 0) + (ceilingCells?.Count ?? 0);

            public PersistentEntry()
            {
            }

            public PersistentEntry(string key, PlacementArea area)
            {
                this.key = key;
                bounds = area.WorldBounds;
                floors = CreateSurfaceEntries(area.FloorRegions);
                walls = CreateSurfaceEntries(area.WallRegions);
                ceilings = CreateSurfaceEntries(area.CeilingRegions);
                floorCells = area.FloorCells != null ? new List<Vector3Int>(area.FloorCells) : new List<Vector3Int>();
                ceilingCells = area.CeilingCells != null ? new List<Vector3Int>(area.CeilingCells) : new List<Vector3Int>();
            }

            public bool TryCreateArea(
                SpatialSourceInfo sourceInfo,
                AreaBuildSettings settings,
                HashSet<Vector3Int> subspace,
                float voxelSize,
                Predicate<Collider> isSourceCollider,
                out PlacementArea area)
            {
                area = null;

                if (!IsValid || subspace == null || subspace.Count == 0)
                    return false;

                List<SurfaceRegion> floorRegions = CreateRegions(floors);
                List<SurfaceRegion> wallRegions = CreateRegions(walls);
                List<SurfaceRegion> ceilingRegions = CreateRegions(ceilings);
                VoxelCellMask subspaceMask = new(subspace);

                area = new PlacementArea(
                    sourceInfo,
                    bounds,
                    floorRegions,
                    wallRegions,
                    floorCells,
                    voxelSize,
                    settings,
                    subspace,
                    ceilingRegions,
                    ceilingCells,
                    isSourceCollider,
                    subspaceMask);
                return true;
            }

            private static List<SurfaceEntry> CreateSurfaceEntries(IReadOnlyList<SurfaceRegion> regions)
            {
                List<SurfaceEntry> result = new();

                if (regions == null)
                    return result;

                foreach (SurfaceRegion region in regions)
                    result.Add(new SurfaceEntry(region));

                return result;
            }

            private static List<SurfaceRegion> CreateRegions(IEnumerable<SurfaceEntry> entries)
            {
                List<SurfaceRegion> regions = new();

                if (entries == null)
                    return regions;

                foreach (SurfaceEntry entry in entries)
                {
                    if (entry.TryCreateRegion(out SurfaceRegion region))
                        regions.Add(region);
                }

                return regions;
            }
        }

        [Serializable]
        private sealed class SurfaceEntry
        {
            [SerializeField] private string name;
            [SerializeField] private SurfaceKind kind;
            [SerializeField] private Bounds bounds;
            [SerializeField] private Vector3 normal;
            [SerializeField] private Vector3 wallStart;
            [SerializeField] private Vector3 wallEnd;
            [SerializeField] private float surfaceY;
            [SerializeField] private bool hasVoxelLayer;
            [SerializeField] private int voxelLayer;

            public SurfaceEntry()
            {
            }

            public SurfaceEntry(SurfaceRegion region)
            {
                name = region.Name;
                kind = region.Kind;
                bounds = region.Bounds;
                normal = region.Normal;
                wallStart = region.WallStart;
                wallEnd = region.WallEnd;
                surfaceY = region.SurfaceY;
                hasVoxelLayer = region.VoxelLayer.HasValue;
                voxelLayer = region.VoxelLayer.GetValueOrDefault();
            }

            public bool TryCreateRegion(out SurfaceRegion region)
            {
                int? layer = hasVoxelLayer ? voxelLayer : null;

                region = kind switch
                {
                    SurfaceKind.Floor => SurfaceRegion.CreateFloor(
                        name,
                        bounds.min.x,
                        bounds.max.x,
                        bounds.min.z,
                        bounds.max.z,
                        surfaceY,
                        layer),
                    SurfaceKind.Ceiling => SurfaceRegion.CreateCeiling(
                        name,
                        bounds.min.x,
                        bounds.max.x,
                        bounds.min.z,
                        bounds.max.z,
                        surfaceY,
                        layer),
                    SurfaceKind.Wall => SurfaceRegion.CreateWall(
                        name,
                        wallStart,
                        wallEnd,
                        bounds.max.y,
                        normal,
                        layer),
                    _ => null
                };

                return region != null;
            }
        }
    }
}
