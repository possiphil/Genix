using System;
using System.Collections.Generic;
using Genix.Areas;
using UnityEngine;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Converts voxel floor or ceiling cells into rectangular surface regions using the selected fidelity mode.</summary>
    internal static class AreaDecomposer
    {
        public static List<SurfaceRegion> CreateHorizontalRegions(
            HashSet<Vector3Int> cells,
            float voxelSize,
            AreaDecompositionMode mode,
            SurfaceKind kind)
        {
            Func<string, float, float, float, float, float, int?, SurfaceRegion> createRegion =
                kind == SurfaceKind.Ceiling
                    ? SurfaceRegion.CreateCeiling
                    : SurfaceRegion.CreateFloor;
            float surfaceOffset = kind == SurfaceKind.Ceiling ? 0.5f : -0.5f;
            string label = $"{mode} {kind}";

            return mode == AreaDecompositionMode.Fast
                ? CreateLayerBounds(cells, voxelSize, createRegion, label, surfaceOffset)
                : CreateRectangles(cells, voxelSize, createRegion, label, surfaceOffset);
        }

        private static List<SurfaceRegion> CreateLayerBounds(
            HashSet<Vector3Int> cells,
            float voxelSize,
            Func<string, float, float, float, float, float, int?, SurfaceRegion> createRegion,
            string label,
            float surfaceOffset)
        {
            Dictionary<int, LayerExtents> layers = new();

            foreach (Vector3Int cell in cells)
            {
                LayerExtents extents = layers.TryGetValue(cell.y, out LayerExtents existing)
                    ? existing
                    : new LayerExtents(cell.x, cell.x, cell.z, cell.z);
                extents.Encapsulate(cell);
                layers[cell.y] = extents;
            }

            List<SurfaceRegion> regions = new();

            foreach (KeyValuePair<int, LayerExtents> layer in layers)
            {
                LayerExtents extents = layer.Value;
                regions.Add(createRegion(
                    $"{label} y={layer.Key}",
                    (extents.MinX - 0.5f) * voxelSize,
                    (extents.MaxX + 0.5f) * voxelSize,
                    (extents.MinZ - 0.5f) * voxelSize,
                    (extents.MaxZ + 0.5f) * voxelSize,
                    (layer.Key + surfaceOffset) * voxelSize,
                    layer.Key));
            }

            return regions;
        }

        private static List<SurfaceRegion> CreateRectangles(
            HashSet<Vector3Int> cells,
            float voxelSize,
            Func<string, float, float, float, float, float, int?, SurfaceRegion> createRegion,
            string label,
            float surfaceOffset)
        {
            Dictionary<int, HashSet<Vector2Int>> layers = GroupByLayer(cells);
            List<SurfaceRegion> regions = new();

            foreach (KeyValuePair<int, HashSet<Vector2Int>> layer in layers)
            {
                HashSet<Vector2Int> unused = new(layer.Value);

                while (unused.Count > 0)
                {
                    Vector2Int start = GetLowest(unused);
                    int width = GetWidth(start, unused);
                    int depth = GetDepth(start, width, unused);

                    RemoveRectangle(unused, start, width, depth);
                    regions.Add(createRegion(
                        $"{label} y={layer.Key}",
                        (start.x - 0.5f) * voxelSize,
                        (start.x + width - 0.5f) * voxelSize,
                        (start.y - 0.5f) * voxelSize,
                        (start.y + depth - 0.5f) * voxelSize,
                        (layer.Key + surfaceOffset) * voxelSize,
                        layer.Key));
                }
            }

            return regions;
        }

        private static Dictionary<int, HashSet<Vector2Int>> GroupByLayer(IEnumerable<Vector3Int> cells)
        {
            Dictionary<int, HashSet<Vector2Int>> layers = new();

            foreach (Vector3Int cell in cells)
            {
                if (!layers.TryGetValue(cell.y, out HashSet<Vector2Int> layer))
                {
                    layer = new HashSet<Vector2Int>();
                    layers[cell.y] = layer;
                }

                layer.Add(new Vector2Int(cell.x, cell.z));
            }

            return layers;
        }

        private static Vector2Int GetLowest(IEnumerable<Vector2Int> cells)
        {
            Vector2Int best = default;
            bool hasBest = false;

            foreach (Vector2Int cell in cells)
            {
                if (!hasBest || cell.y < best.y || cell.y == best.y && cell.x < best.x)
                {
                    best = cell;
                    hasBest = true;
                }
            }

            return best;
        }

        private static int GetWidth(Vector2Int start, HashSet<Vector2Int> cells)
        {
            int width = 0;

            while (cells.Contains(new Vector2Int(start.x + width, start.y)))
                width++;

            return Mathf.Max(1, width);
        }

        private static int GetDepth(Vector2Int start, int width, HashSet<Vector2Int> cells)
        {
            int depth = 1;

            while (true)
            {
                for (int x = start.x; x < start.x + width; x++)
                {
                    if (!cells.Contains(new Vector2Int(x, start.y + depth)))
                        return depth;
                }

                depth++;
            }
        }

        private static void RemoveRectangle(
            HashSet<Vector2Int> cells,
            Vector2Int start,
            int width,
            int depth)
        {
            for (int x = start.x; x < start.x + width; x++)
            {
                for (int z = start.y; z < start.y + depth; z++)
                    cells.Remove(new Vector2Int(x, z));
            }
        }

        private struct LayerExtents
        {
            public int MinX;
            public int MaxX;
            public int MinZ;
            public int MaxZ;

            public LayerExtents(int minX, int maxX, int minZ, int maxZ)
            {
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
            }

            public void Encapsulate(Vector3Int cell)
            {
                MinX = Mathf.Min(MinX, cell.x);
                MaxX = Mathf.Max(MaxX, cell.x);
                MinZ = Mathf.Min(MinZ, cell.z);
                MaxZ = Mathf.Max(MaxZ, cell.z);
            }
        }
    }
}
