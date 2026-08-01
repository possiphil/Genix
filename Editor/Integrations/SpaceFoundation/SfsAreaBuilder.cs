using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Core;
using Genix.Diagnostics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    internal static class SfsAreaBuilder
    {
        public static bool TryBuild(
            SfsSpace space,
            SfsAnchor anchor,
            SpatialSourceInfo sourceInfo,
            HashSet<Vector3Int> subspace,
            AreaBuildSettings settings,
            Predicate<Collider> isSourceCollider,
            out PlacementArea area,
            out string error)
        {
            float voxelSize = SfsFoundationUtility.GetVoxelSize(SfsFoundationUtility.Find(space, anchor));
            Stopwatch maskStopwatch = Stopwatch.StartNew();
            VoxelCellMask subspaceMask = new(subspace);
            maskStopwatch.Stop();
            settings.profile?.AddStepTime(
                AreaBuildProfileStep.VoxelMaskBuild,
                (float)maskStopwatch.Elapsed.TotalMilliseconds);
            PlacementTarget targets = GetBuildTargets(settings);
            bool usesAllSurfaceSearch = settings.UsesAllMatchingSurfaceSearch;
            bool wantsFloor = (targets & PlacementTarget.Floor) != 0;
            bool wantsCeiling = (targets & PlacementTarget.Ceiling) != 0;
            bool buildFloor = wantsFloor && !usesAllSurfaceSearch;
            bool buildWall = (targets & PlacementTarget.Wall) != 0;
            bool buildCeiling = wantsCeiling && !usesAllSurfaceSearch;
            bool buildInsideSpace = (targets & PlacementTarget.InsideSpace) != 0;
            bool usesVolumeSurfaceSearch = usesAllSurfaceSearch && (wantsFloor || wantsCeiling);

            VoxelSurfaceExtractor.VoxelSurfaceExtraction extraction = VoxelSurfaceExtractor.ExtractSurfaces(
                subspaceMask,
                voxelSize,
                buildFloor,
                buildCeiling,
                buildWall);
            HashSet<Vector3Int> floorCells = extraction.FloorCells;
            HashSet<Vector3Int> ceilingCells = extraction.CeilingCells;
            List<SurfaceRegion> wallRegions = extraction.WallRegions;

            settings.profile?.AddStepTime(
                AreaBuildProfileStep.VoxelScan,
                extraction.ScanMilliseconds);

            Stopwatch surfaceStopwatch = Stopwatch.StartNew();
            List<SurfaceRegion> floorRegions = buildFloor
                ? AreaDecomposer.CreateHorizontalRegions(
                    floorCells,
                    voxelSize,
                    settings.decompositionMode,
                    SurfaceKind.Floor)
                : new List<SurfaceRegion>();
            List<SurfaceRegion> ceilingRegions = buildCeiling
                ? AreaDecomposer.CreateHorizontalRegions(
                    ceilingCells,
                    voxelSize,
                    settings.decompositionMode,
                    SurfaceKind.Ceiling)
                : new List<SurfaceRegion>();
            surfaceStopwatch.Stop();
            if (buildFloor || buildCeiling)
            {
                settings.profile?.AddStepTime(
                    AreaBuildProfileStep.SurfaceRegionBuild,
                    (float)surfaceStopwatch.Elapsed.TotalMilliseconds);
            }

            if (buildWall)
            {
                settings.profile?.AddStepTime(
                    AreaBuildProfileStep.WallRegionBuild,
                    extraction.WallRegionMilliseconds);
            }

            if (!buildInsideSpace &&
                !usesVolumeSurfaceSearch &&
                floorCells.Count == 0 &&
                wallRegions.Count == 0 &&
                ceilingCells.Count == 0)
            {
                area = null;
                error = $"Location '{space.name}' has no detected target surface cells.";
                return false;
            }

            Bounds bounds = CalculateAreaBounds(
                subspaceMask,
                voxelSize,
                floorRegions,
                wallRegions,
                ceilingRegions);

            Stopwatch occupancyStopwatch = Stopwatch.StartNew();
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
            occupancyStopwatch.Stop();
            settings.profile?.AddStepTime(
                AreaBuildProfileStep.OccupancyBuild,
                (float)occupancyStopwatch.Elapsed.TotalMilliseconds);
            error = string.Empty;
            return true;
        }

        private static PlacementTarget GetBuildTargets(AreaBuildSettings settings)
        {
            PlacementTarget targets = settings.placementTargets & PlacementTarget.All;
            return targets == PlacementTarget.None ? PlacementTarget.All : targets;
        }

        private static Bounds CalculateAreaBounds(
            VoxelCellMask subspace,
            float voxelSize,
            IReadOnlyList<SurfaceRegion> floorRegions,
            IReadOnlyList<SurfaceRegion> wallRegions,
            IReadOnlyList<SurfaceRegion> ceilingRegions)
        {
            Bounds subspaceBounds = CalculateSubspaceBounds(subspace, voxelSize);
            bool hasSurfaceBounds = floorRegions.Count > 0 ||
                                    wallRegions.Count > 0 ||
                                    ceilingRegions.Count > 0;

            if (!hasSurfaceBounds)
                return subspaceBounds;

            Bounds bounds = SurfaceRegionBounds.Calculate(floorRegions, wallRegions, ceilingRegions);

            if (subspace.Count > 0)
                Encapsulate(bounds, subspaceBounds, out bounds);

            return bounds;
        }

        private static void Encapsulate(Bounds bounds, Bounds other, out Bounds result)
        {
            result = bounds;
            result.Encapsulate(other.min);
            result.Encapsulate(other.max);
        }

        private static Bounds CalculateSubspaceBounds(VoxelCellMask subspace, float voxelSize)
        {
            if (!subspace.HasBounds)
                return new Bounds(Vector3.zero, Vector3.zero);

            Vector3Int min = subspace.Min;
            Vector3Int max = subspace.Max;
            Vector3 minWorld = new(min.x * voxelSize, min.y * voxelSize, min.z * voxelSize);
            Vector3 maxWorld = new((max.x + 1) * voxelSize, (max.y + 1) * voxelSize, (max.z + 1) * voxelSize);
            Bounds bounds = new((minWorld + maxWorld) * 0.5f, maxWorld - minWorld);
            return bounds;
        }
    }
}
