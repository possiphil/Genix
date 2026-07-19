using System.Collections.Generic;
using UnityEngine;

namespace Genix.SpaceFoundation.Editor
{
    internal static class VoxelFloodFill
    {
        private static readonly Vector3Int[] Directions =
        {
            Vector3Int.right,
            Vector3Int.left,
            Vector3Int.up,
            Vector3Int.down,
            new(0, 0, 1),
            new(0, 0, -1)
        };

        public static HashSet<Vector3Int> Fill(
            Vector3Int seed,
            string anchorId,
            Dictionary<Vector3Int, string> borderOwners,
            VoxelBounds bounds)
        {
            HashSet<Vector3Int> result = new() { seed };
            Queue<Vector3Int> queue = new();

            if (!borderOwners.ContainsKey(seed))
                queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();

                foreach (Vector3Int direction in Directions)
                {
                    Vector3Int next = current + direction;

                    if (!bounds.Contains(next) || result.Contains(next))
                        continue;

                    if (borderOwners.TryGetValue(next, out string owner))
                    {
                        if (owner == anchorId)
                            result.Add(next);

                        continue;
                    }

                    result.Add(next);
                    queue.Enqueue(next);
                }
            }

            return result;
        }

        public static bool TryFindInteriorSeed(
            IReadOnlyList<Vector3Int> anchorBorders,
            string anchorId,
            Dictionary<Vector3Int, string> borderOwners,
            VoxelBounds bounds,
            out Vector3Int seed)
        {
            foreach (Vector3Int border in anchorBorders)
            {
                foreach (Vector3Int direction in Directions)
                {
                    Vector3Int candidate = border + direction;

                    if (IsValidSeed(candidate, anchorId, borderOwners, bounds) &&
                        !borderOwners.ContainsKey(candidate))
                    {
                        seed = candidate;
                        return true;
                    }
                }
            }

            seed = default;
            return false;
        }

        public static bool IsValidSeed(
            Vector3Int seed,
            string anchorId,
            Dictionary<Vector3Int, string> borderOwners,
            VoxelBounds bounds)
        {
            return bounds.Contains(seed) &&
                   (!borderOwners.TryGetValue(seed, out string owner) || owner == anchorId);
        }
    }

    internal readonly struct VoxelBounds
    {
        public Vector3Int Min { get; }
        public Vector3Int Max { get; }
        public long CellCount =>
            (Max.x - Min.x + 1L) *
            (Max.y - Min.y + 1L) *
            (Max.z - Min.z + 1L);

        public VoxelBounds(Vector3Int min, Vector3Int max)
        {
            Min = min;
            Max = max;
        }

        public static VoxelBounds From(IReadOnlyList<Vector3Int> cells)
        {
            Vector3Int min = cells[0];
            Vector3Int max = cells[0];

            foreach (Vector3Int cell in cells)
            {
                min = Vector3Int.Min(min, cell);
                max = Vector3Int.Max(max, cell);
            }

            return new VoxelBounds(min, max);
        }

        public VoxelBounds Encapsulate(Vector3Int cell) =>
            new(Vector3Int.Min(Min, cell), Vector3Int.Max(Max, cell));

        public VoxelBounds Expand(int cells) =>
            new(Min - Vector3Int.one * cells, Max + Vector3Int.one * cells);

        public bool Contains(Vector3Int cell)
        {
            return cell.x >= Min.x && cell.x <= Max.x &&
                   cell.y >= Min.y && cell.y <= Max.y &&
                   cell.z >= Min.z && cell.z <= Max.z;
        }
    }
}
