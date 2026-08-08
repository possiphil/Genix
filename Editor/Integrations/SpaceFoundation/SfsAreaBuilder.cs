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
    /// <summary>Builds a runtime placement area from resolved SFS cells, extracted surfaces, occupancy, and scene geometry.</summary>
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
            bool collectTiming = settings.profile != null;
            Stopwatch maskStopwatch = collectTiming ? Stopwatch.StartNew() : null;
            VoxelCellMask subspaceMask = new(subspace);
            maskStopwatch?.Stop();
            settings.profile?.AddStepTime(
                AreaBuildProfileStep.VoxelMaskBuild,
                collectTiming ? (float)maskStopwatch.Elapsed.TotalMilliseconds : 0f);
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
                buildWall,
                collectTiming);
            HashSet<Vector3Int> floorCells = extraction.FloorCells;
            HashSet<Vector3Int> ceilingCells = extraction.CeilingCells;
            List<SurfaceRegion> wallRegions = extraction.WallRegions;

            settings.profile?.AddStepTime(
                AreaBuildProfileStep.VoxelScan,
                extraction.ScanMilliseconds);

            Stopwatch surfaceStopwatch = collectTiming ? Stopwatch.StartNew() : null;
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
            surfaceStopwatch?.Stop();
            if (buildFloor || buildCeiling)
            {
                settings.profile?.AddStepTime(
                    AreaBuildProfileStep.SurfaceRegionBuild,
                    collectTiming ? (float)surfaceStopwatch.Elapsed.TotalMilliseconds : 0f);
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

            Stopwatch occupancyStopwatch = collectTiming ? Stopwatch.StartNew() : null;
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
            occupancyStopwatch?.Stop();
            settings.profile?.AddStepTime(
                AreaBuildProfileStep.OccupancyBuild,
                collectTiming ? (float)occupancyStopwatch.Elapsed.TotalMilliseconds : 0f);
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
            Vector3 minWorld = ((Vector3)min - Vector3.one * 0.5f) * voxelSize;
            Vector3 maxWorld = ((Vector3)max + Vector3.one * 0.5f) * voxelSize;
            Bounds bounds = new((minWorld + maxWorld) * 0.5f, maxWorld - minWorld);
            return bounds;
        }
    }
}
