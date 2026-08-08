using System;
using System.Collections.Generic;
using System.IO;
using Genix.Areas;
using Genix.Core;
using Genix.Diagnostics;
using UnityEditor;
using UnityEngine;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsFoundation = SpaceFoundationSystem.SpaceFoundation;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Caches fully built placement areas in memory and as compressed project-local assets.</summary>
    internal static class SfsAreaCache
    {
        private const int CacheVersion = 7;
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
            DeletePersistentStore();
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
            string foundationId = SfsFoundationUtility.CreateCacheIdentity(foundation);

            if (string.IsNullOrWhiteSpace(foundationId))
                foundationId = "missing-foundation";

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
            DeletePersistentStore();
            store = ScriptableObject.CreateInstance<SfsAreaCacheAsset>();
            AssetDatabase.CreateAsset(store, CacheAssetPath);
            AssetDatabase.SaveAssets();
            return store;
        }

        private static void DeletePersistentStore()
        {
            AssetDatabase.DeleteAsset(CacheAssetPath);
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

}
