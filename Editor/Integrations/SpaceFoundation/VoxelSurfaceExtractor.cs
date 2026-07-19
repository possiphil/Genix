using System.Collections.Generic;
using Genix.Areas;
using UnityEngine;

namespace Genix.SpaceFoundation.Editor
{
    internal static class VoxelSurfaceExtractor
    {
        public static HashSet<Vector3Int> GetHorizontalCells(
            HashSet<Vector3Int> subspace,
            Vector3Int outsideDirection)
        {
            HashSet<Vector3Int> result = new();

            foreach (Vector3Int voxel in subspace)
            {
                if (!subspace.Contains(voxel + outsideDirection))
                    result.Add(voxel);
            }

            return result;
        }

        public static List<SurfaceRegion> CreateWallRegions(
            HashSet<Vector3Int> subspace,
            float voxelSize)
        {
            List<SurfaceRegion> walls = new();

            foreach (Vector3Int cell in subspace)
            {
                float x0 = cell.x * voxelSize;
                float x1 = (cell.x + 1) * voxelSize;
                float y0 = cell.y * voxelSize;
                float y1 = (cell.y + 1) * voxelSize;
                float z0 = cell.z * voxelSize;
                float z1 = (cell.z + 1) * voxelSize;

                AddWallIfExposed(
                    subspace,
                    cell,
                    Vector3Int.left,
                    new Vector3(x0, y0, z0),
                    new Vector3(x0, y0, z1),
                    y1,
                    Vector3.right,
                    "SFS Wall -X",
                    walls);
                AddWallIfExposed(
                    subspace,
                    cell,
                    Vector3Int.right,
                    new Vector3(x1, y0, z1),
                    new Vector3(x1, y0, z0),
                    y1,
                    Vector3.left,
                    "SFS Wall +X",
                    walls);
                AddWallIfExposed(
                    subspace,
                    cell,
                    new Vector3Int(0, 0, -1),
                    new Vector3(x1, y0, z0),
                    new Vector3(x0, y0, z0),
                    y1,
                    Vector3.forward,
                    "SFS Wall -Z",
                    walls);
                AddWallIfExposed(
                    subspace,
                    cell,
                    new Vector3Int(0, 0, 1),
                    new Vector3(x0, y0, z1),
                    new Vector3(x1, y0, z1),
                    y1,
                    Vector3.back,
                    "SFS Wall +Z",
                    walls);
            }

            return walls;
        }

        private static void AddWallIfExposed(
            HashSet<Vector3Int> subspace,
            Vector3Int cell,
            Vector3Int direction,
            Vector3 start,
            Vector3 end,
            float top,
            Vector3 normal,
            string name,
            ICollection<SurfaceRegion> walls)
        {
            if (!subspace.Contains(cell + direction))
                walls.Add(SurfaceRegion.CreateWall(name, start, end, top, normal, cell.y));
        }
    }
}
