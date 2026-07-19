using System.Collections.Generic;
using UnityEngine;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    internal static class SfsSubspaceProvider
    {
        private const int MaxFloodFillCells = 2_000_000;

        public static HashSet<Vector3Int> Resolve(SfsSpace space, SfsAnchor anchor)
        {
            HashSet<Vector3Int> liveSubspace = anchor.GetSubspace();

            if (liveSubspace != null && liveSubspace.Count > 0)
                return liveSubspace;

            if (!SfsPersistentDataReader.TryRead(space, anchor, out PersistentSubspaceData data) ||
                data.Bounds.CellCount > MaxFloodFillCells)
            {
                return null;
            }

            PersistentSubspaceCacheKey key = PersistentSubspaceCacheKey.Create(data);

            if (PersistentSubspaceCache.TryGet(key, data.AnchorBorders.Count, out HashSet<Vector3Int> cached))
                return cached;

            HashSet<Vector3Int> subspace = VoxelFloodFill.Fill(
                data.Seed,
                data.AnchorId,
                data.BorderOwners,
                data.Bounds);

            if (subspace.Count < data.AnchorBorders.Count)
                return null;

            PersistentSubspaceCache.Store(key, subspace);
            return subspace;
        }
    }
}
