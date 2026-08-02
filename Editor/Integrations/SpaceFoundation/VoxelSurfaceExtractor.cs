using System.Collections.Generic;
using Genix.Areas;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Extracts exposed floor, ceiling, and merged wall faces from occupied subspace cells.</summary>
    internal static class VoxelSurfaceExtractor
    {
        private enum WallFace
        {
            NegativeX,
            PositiveX,
            NegativeZ,
            PositiveZ,
        }

        public static List<SurfaceRegion> CreateWallRegions(
            HashSet<Vector3Int> subspace,
            float voxelSize)
        {
            VoxelCellMask subspaceMask = new(subspace);
            VoxelSurfaceExtraction extraction = ExtractSurfaces(
                subspaceMask,
                voxelSize,
                false,
                false,
                true);
            return extraction.WallRegions;
        }

        public static VoxelSurfaceExtraction ExtractSurfaces(
            VoxelCellMask subspace,
            float voxelSize,
            bool includeFloor,
            bool includeCeiling,
            bool includeWalls)
        {
            HashSet<Vector3Int> floorCells = includeFloor ? new HashSet<Vector3Int>() : new HashSet<Vector3Int>(0);
            HashSet<Vector3Int> ceilingCells = includeCeiling ? new HashSet<Vector3Int>() : new HashSet<Vector3Int>(0);
            Dictionary<WallRunKey, List<int>> runsByWall = includeWalls ? new Dictionary<WallRunKey, List<int>>() : null;
            Stopwatch scanStopwatch = Stopwatch.StartNew();

            if (!includeFloor && !includeCeiling && !includeWalls)
            {
                scanStopwatch.Stop();
                return new VoxelSurfaceExtraction(
                    floorCells,
                    ceilingCells,
                    new List<SurfaceRegion>(),
                    (float)scanStopwatch.Elapsed.TotalMilliseconds,
                    0f);
            }

            foreach (Vector3Int cell in subspace.Cells)
            {
                if (includeFloor && !subspace.Contains(cell + Vector3Int.down))
                    floorCells.Add(cell);
                if (includeCeiling && !subspace.Contains(cell + Vector3Int.up))
                    ceilingCells.Add(cell);

                if (!includeWalls)
                    continue;

                if (!subspace.Contains(cell + Vector3Int.left))
                    AddRunCoordinate(runsByWall, new WallRunKey(WallFace.NegativeX, cell.x, cell.y), cell.z);
                if (!subspace.Contains(cell + Vector3Int.right))
                    AddRunCoordinate(runsByWall, new WallRunKey(WallFace.PositiveX, cell.x + 1, cell.y), cell.z);
                if (!subspace.Contains(cell + new Vector3Int(0, 0, -1)))
                    AddRunCoordinate(runsByWall, new WallRunKey(WallFace.NegativeZ, cell.z, cell.y), cell.x);
                if (!subspace.Contains(cell + new Vector3Int(0, 0, 1)))
                    AddRunCoordinate(runsByWall, new WallRunKey(WallFace.PositiveZ, cell.z + 1, cell.y), cell.x);
            }

            scanStopwatch.Stop();

            Stopwatch wallStopwatch = Stopwatch.StartNew();
            List<SurfaceRegion> walls = includeWalls
                ? CreateWallRegionsFromRuns(runsByWall, voxelSize)
                : new List<SurfaceRegion>();
            wallStopwatch.Stop();

            return new VoxelSurfaceExtraction(
                floorCells,
                ceilingCells,
                walls,
                (float)scanStopwatch.Elapsed.TotalMilliseconds,
                (float)wallStopwatch.Elapsed.TotalMilliseconds);
        }

        private static List<SurfaceRegion> CreateWallRegionsFromRuns(
            Dictionary<WallRunKey, List<int>> runsByWall,
            float voxelSize)
        {
            List<SurfaceRegion> walls = new();

            foreach (KeyValuePair<WallRunKey, List<int>> entry in runsByWall)
            {
                List<int> coordinates = entry.Value;
                coordinates.Sort();

                int runStart = coordinates[0];
                int previous = coordinates[0];

                for (int i = 1; i < coordinates.Count; i++)
                {
                    int coordinate = coordinates[i];
                    if (coordinate == previous)
                        continue;
                    if (coordinate == previous + 1)
                    {
                        previous = coordinate;
                        continue;
                    }

                    AddWallRun(walls, entry.Key, runStart, previous, voxelSize);
                    runStart = coordinate;
                    previous = coordinate;
                }

                AddWallRun(walls, entry.Key, runStart, previous, voxelSize);
            }

            return walls;
        }

        private static void AddRunCoordinate(
            IDictionary<WallRunKey, List<int>> runsByWall,
            WallRunKey key,
            int coordinate)
        {
            if (!runsByWall.TryGetValue(key, out List<int> coordinates))
            {
                coordinates = new List<int>();
                runsByWall.Add(key, coordinates);
            }

            coordinates.Add(coordinate);
        }

        private static void AddWallRun(
            ICollection<SurfaceRegion> walls,
            WallRunKey key,
            int runStart,
            int runEnd,
            float voxelSize)
        {
            float y0 = key.Layer * voxelSize;
            float y1 = (key.Layer + 1) * voxelSize;
            float plane = key.Plane * voxelSize;
            float min = runStart * voxelSize;
            float max = (runEnd + 1) * voxelSize;

            switch (key.Face)
            {
                case WallFace.NegativeX:
                    walls.Add(SurfaceRegion.CreateWall(
                        "SFS Wall -X",
                        new Vector3(plane, y0, min),
                        new Vector3(plane, y0, max),
                        y1,
                        Vector3.right,
                        key.Layer));
                    break;
                case WallFace.PositiveX:
                    walls.Add(SurfaceRegion.CreateWall(
                        "SFS Wall +X",
                        new Vector3(plane, y0, max),
                        new Vector3(plane, y0, min),
                        y1,
                        Vector3.left,
                        key.Layer));
                    break;
                case WallFace.NegativeZ:
                    walls.Add(SurfaceRegion.CreateWall(
                        "SFS Wall -Z",
                        new Vector3(max, y0, plane),
                        new Vector3(min, y0, plane),
                        y1,
                        Vector3.forward,
                        key.Layer));
                    break;
                case WallFace.PositiveZ:
                    walls.Add(SurfaceRegion.CreateWall(
                        "SFS Wall +Z",
                        new Vector3(min, y0, plane),
                        new Vector3(max, y0, plane),
                        y1,
                        Vector3.back,
                        key.Layer));
                    break;
            }
        }

        private readonly struct WallRunKey : System.IEquatable<WallRunKey>
        {
            public WallRunKey(WallFace face, int plane, int layer)
            {
                Face = face;
                Plane = plane;
                Layer = layer;
            }

            public WallFace Face { get; }
            public int Plane { get; }
            public int Layer { get; }

            public bool Equals(WallRunKey other)
            {
                return Face == other.Face &&
                       Plane == other.Plane &&
                       Layer == other.Layer;
            }

            public override bool Equals(object obj)
            {
                return obj is WallRunKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (int)Face;
                    hashCode = (hashCode * 397) ^ Plane;
                    hashCode = (hashCode * 397) ^ Layer;
                    return hashCode;
                }
            }
        }

        /// <summary>Surface cells and wall segments extracted from one voxel subspace.</summary>
        public readonly struct VoxelSurfaceExtraction
        {
            public VoxelSurfaceExtraction(
                HashSet<Vector3Int> floorCells,
                HashSet<Vector3Int> ceilingCells,
                List<SurfaceRegion> wallRegions,
                float scanMilliseconds,
                float wallRegionMilliseconds)
            {
                FloorCells = floorCells;
                CeilingCells = ceilingCells;
                WallRegions = wallRegions;
                ScanMilliseconds = scanMilliseconds;
                WallRegionMilliseconds = wallRegionMilliseconds;
            }

            public HashSet<Vector3Int> FloorCells { get; }
            public HashSet<Vector3Int> CeilingCells { get; }
            public List<SurfaceRegion> WallRegions { get; }
            public float ScanMilliseconds { get; }
            public float WallRegionMilliseconds { get; }
        }
    }
}
