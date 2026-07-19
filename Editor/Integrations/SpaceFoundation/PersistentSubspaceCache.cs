using System;
using System.Collections.Generic;
using Genix.Extensions;
using UnityEngine;

namespace Genix.SpaceFoundation.Editor
{
    internal static class PersistentSubspaceCache
    {
        private const int MaxEntries = 32;
        private const int MaxCells = 2_000_000;

        private static readonly Dictionary<PersistentSubspaceCacheKey, HashSet<Vector3Int>> Entries = new();
        private static int _cellCount;

        public static bool TryGet(
            PersistentSubspaceCacheKey key,
            int minimumCellCount,
            out HashSet<Vector3Int> subspace)
        {
            subspace = null;

            if (!Entries.TryGetValue(key, out HashSet<Vector3Int> cached))
                return false;

            if (cached.Count < minimumCellCount)
            {
                _cellCount = Mathf.Max(0, _cellCount - cached.Count);
                Entries.Remove(key);
                return false;
            }

            subspace = new HashSet<Vector3Int>(cached);
            return true;
        }

        public static void Store(PersistentSubspaceCacheKey key, HashSet<Vector3Int> subspace)
        {
            if (subspace == null || subspace.Count > MaxCells)
                return;

            if (Entries.TryGetValue(key, out HashSet<Vector3Int> existing))
                _cellCount = Mathf.Max(0, _cellCount - existing.Count);

            if (Entries.Count >= MaxEntries || _cellCount + subspace.Count > MaxCells)
                Clear();

            Entries[key] = new HashSet<Vector3Int>(subspace);
            _cellCount += subspace.Count;
        }

        public static void Clear()
        {
            Entries.Clear();
            _cellCount = 0;
        }
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
                $"{data.Foundation.GetLocalObjectId()}:{data.Foundation.assetName}",
                data.AnchorId,
                Mathf.RoundToInt(SfsFoundationUtility.GetVoxelSize(data.Foundation) * 100_000f),
                data.Bounds.Min,
                data.Bounds.Max,
                count,
                hashXor,
                hashSum);
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
    }
}
