using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SfsFoundation = SpaceFoundationSystem.SpaceFoundation;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>
    /// Stores compressed SFS subspace cells in memory and in a project-local ScriptableObject cache.
    /// </summary>
    /// <remarks>Keys include the foundation, anchor, voxel size, bounds, and source revision to prevent stale reuse.</remarks>
    internal static class PersistentSubspaceCache
    {
        private const int MaxEntries = 32;
        private const int MaxCells = 2_000_000;
        private const int MaxPersistentCells = MaxCells;
        private const string CacheFolderPath = "Assets/Genix/Cache";
        private const string CacheAssetPath = CacheFolderPath + "/SfsSubspaceCache.asset";

        private static readonly Dictionary<PersistentSubspaceCacheKey, HashSet<Vector3Int>> Entries = new();
        private static int _cellCount;

        public static bool TryGet(
            PersistentSubspaceCacheKey key,
            int minimumCellCount,
            out HashSet<Vector3Int> subspace)
        {
            return TryGet(key, minimumCellCount, out subspace, out _);
        }

        public static bool TryGet(
            PersistentSubspaceCacheKey key,
            int minimumCellCount,
            out HashSet<Vector3Int> subspace,
            out PersistentSubspaceCacheSource source)
        {
            subspace = null;
            source = PersistentSubspaceCacheSource.None;

            if (!key.IsValid)
                return false;

            if (!Entries.TryGetValue(key, out HashSet<Vector3Int> cached))
            {
                if (!TryGetPersistent(key, minimumCellCount, out subspace))
                    return false;

                StoreMemory(key, subspace);
                source = PersistentSubspaceCacheSource.Persistent;
                return true;
            }

            if (cached.Count < minimumCellCount)
            {
                _cellCount = Mathf.Max(0, _cellCount - cached.Count);
                Entries.Remove(key);
                return false;
            }

            subspace = new HashSet<Vector3Int>(cached);
            source = PersistentSubspaceCacheSource.Memory;
            return true;
        }

        public static PersistentSubspaceCacheStoreResult Store(PersistentSubspaceCacheKey key, HashSet<Vector3Int> subspace)
        {
            if (!key.IsValid || subspace == null || subspace.Count > MaxCells)
                return PersistentSubspaceCacheStoreResult.NotStored;

            if (Contains(key, subspace.Count))
                return PersistentSubspaceCacheStoreResult.AlreadyCached;

            StoreMemory(key, subspace);
            return StorePersistent(key, subspace)
                ? PersistentSubspaceCacheStoreResult.MemoryAndPersistent
                : PersistentSubspaceCacheStoreResult.MemoryOnly;
        }

        public static bool Contains(PersistentSubspaceCacheKey key, int minimumCellCount)
        {
            if (!key.IsValid)
                return false;

            if (Entries.TryGetValue(key, out HashSet<Vector3Int> cached) &&
                cached.Count >= minimumCellCount)
            {
                return true;
            }

            SfsSubspaceCacheAsset store = LoadPersistentStore(false);
            return store && store.Contains(key.ToStableString(), minimumCellCount);
        }

        private static void StoreMemory(PersistentSubspaceCacheKey key, HashSet<Vector3Int> subspace)
        {
            if (Entries.TryGetValue(key, out HashSet<Vector3Int> existing))
                _cellCount = Mathf.Max(0, _cellCount - existing.Count);

            if (Entries.Count >= MaxEntries || _cellCount + subspace.Count > MaxCells)
                ClearMemory();

            Entries[key] = new HashSet<Vector3Int>(subspace);
            _cellCount += subspace.Count;
        }

        public static void Clear()
        {
            ClearMemory();
            DeletePersistentStore();
        }

        private static void ClearMemory()
        {
            Entries.Clear();
            _cellCount = 0;
        }

        private static bool TryGetPersistent(
            PersistentSubspaceCacheKey key,
            int minimumCellCount,
            out HashSet<Vector3Int> subspace)
        {
            subspace = null;
            SfsSubspaceCacheAsset store = LoadPersistentStore(false);

            return store && store.TryGet(key.ToStableString(), minimumCellCount, out subspace);
        }

        private static bool StorePersistent(PersistentSubspaceCacheKey key, HashSet<Vector3Int> subspace)
        {
            if (subspace.Count > MaxPersistentCells)
                return false;

            SfsSubspaceCacheAsset store = LoadPersistentStore(true);

            if (!store)
                return false;

            store.Store(key.ToStableString(), subspace, MaxEntries, MaxPersistentCells);
            EditorUtility.SetDirty(store);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static SfsSubspaceCacheAsset LoadPersistentStore(bool create)
        {
            SfsSubspaceCacheAsset store = AssetDatabase.LoadAssetAtPath<SfsSubspaceCacheAsset>(CacheAssetPath);

            if (store || !create)
                return store;

            EnsureFolder(CacheFolderPath);
            DeletePersistentStore();
            store = ScriptableObject.CreateInstance<SfsSubspaceCacheAsset>();
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
    }

    internal enum PersistentSubspaceCacheSource
    {
        None,
        Memory,
        Persistent
    }

    internal enum PersistentSubspaceCacheStoreResult
    {
        NotStored,
        AlreadyCached,
        MemoryOnly,
        MemoryAndPersistent
    }

    internal readonly struct PersistentSubspaceCacheKey : IEquatable<PersistentSubspaceCacheKey>
    {
        private readonly string _foundation;
        private readonly string _anchor;
        private readonly int _voxelSize;
        private readonly Vector3Int _min;
        private readonly Vector3Int _max;
        private readonly int _borderCount;
        private readonly int _borderHashXor;
        private readonly int _borderHashSum;

        public bool IsValid => !string.IsNullOrWhiteSpace(_foundation) &&
                               !string.IsNullOrWhiteSpace(_anchor);

        private PersistentSubspaceCacheKey(
            string foundation,
            string anchor,
            int voxelSize,
            Vector3Int min,
            Vector3Int max,
            int borderCount,
            int borderHashXor,
            int borderHashSum)
        {
            _foundation = foundation ?? string.Empty;
            _anchor = anchor ?? string.Empty;
            _voxelSize = voxelSize;
            _min = min;
            _max = max;
            _borderCount = borderCount;
            _borderHashXor = borderHashXor;
            _borderHashSum = borderHashSum;
        }

        public static PersistentSubspaceCacheKey Create(PersistentSubspaceData data)
        {
            int count = 0;
            int hashXor = 0;
            int hashSum = 0;

            foreach (KeyValuePair<Vector3Int, string> border in data.BorderOwners)
            {
                if (!data.Bounds.Contains(border.Key))
                    continue;

                int hash = Hash(border.Key, border.Value);
                count++;
                hashXor ^= hash;

                unchecked
                {
                    hashSum += hash;
                }
            }

            return new PersistentSubspaceCacheKey(
                CreateFoundationId(data.Foundation),
                data.AnchorId,
                Mathf.RoundToInt(SfsFoundationUtility.GetVoxelSize(data.Foundation) * 100_000f),
                data.Bounds.Min,
                data.Bounds.Max,
                count,
                hashXor,
                hashSum);
        }

        public static PersistentSubspaceCacheKey CreateLiveSnapshot(
            SfsFoundation foundation,
            string anchorId)
        {
            return new PersistentSubspaceCacheKey(
                CreateFoundationId(foundation),
                anchorId,
                Mathf.RoundToInt(SfsFoundationUtility.GetVoxelSize(foundation) * 100_000f),
                Vector3Int.zero,
                Vector3Int.zero,
                -1,
                -1,
                -1);
        }

        public bool Equals(PersistentSubspaceCacheKey other)
        {
            return _foundation == other._foundation &&
                   _anchor == other._anchor &&
                   _voxelSize == other._voxelSize &&
                   _min == other._min &&
                   _max == other._max &&
                   _borderCount == other._borderCount &&
                   _borderHashXor == other._borderHashXor &&
                   _borderHashSum == other._borderHashSum;
        }

        public override bool Equals(object obj) =>
            obj is PersistentSubspaceCacheKey other && Equals(other);

        public string ToStableString()
        {
            return string.Join("|",
                _foundation,
                _anchor,
                _voxelSize,
                _min.x,
                _min.y,
                _min.z,
                _max.x,
                _max.y,
                _max.z,
                _borderCount,
                _borderHashXor,
                _borderHashSum);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_foundation);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_anchor);
                hash = hash * 31 + _voxelSize;
                hash = hash * 31 + Hash(_min, null);
                hash = hash * 31 + Hash(_max, null);
                hash = hash * 31 + _borderCount;
                hash = hash * 31 + _borderHashXor;
                hash = hash * 31 + _borderHashSum;
                return hash;
            }
        }

        private static int Hash(Vector3Int position, string owner)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + position.x;
                hash = hash * 31 + position.y;
                hash = hash * 31 + position.z;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(owner ?? string.Empty);
                return hash;
            }
        }

        private static string CreateFoundationId(SfsFoundation foundation) =>
            SfsFoundationUtility.CreateCacheIdentity(foundation);
    }

}
