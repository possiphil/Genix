using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Diagnostics;
using UnityEngine;
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
            HashSet<Vector3Int> floorCells = VoxelSurfaceExtractor.GetHorizontalCells(subspace, Vector3Int.down);
            HashSet<Vector3Int> ceilingCells = VoxelSurfaceExtractor.GetHorizontalCells(subspace, Vector3Int.up);

            if (floorCells.Count == 0 && ceilingCells.Count == 0)
            {
                area = null;
                error = $"Location '{space.name}' has no detected horizontal surface cells.";
                return false;
            }

            List<SurfaceRegion> floorRegions = AreaDecomposer.CreateHorizontalRegions(
                floorCells,
                voxelSize,
                settings.decompositionMode,
                SurfaceKind.Floor);
            List<SurfaceRegion> ceilingRegions = AreaDecomposer.CreateHorizontalRegions(
                ceilingCells,
                voxelSize,
                settings.decompositionMode,
                SurfaceKind.Ceiling);
            List<SurfaceRegion> wallRegions = VoxelSurfaceExtractor.CreateWallRegions(subspace, voxelSize);
            Bounds bounds = SurfaceRegionBounds.Calculate(floorRegions, wallRegions, ceilingRegions);

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
                isSourceCollider);
            error = string.Empty;
            return true;
        }
    }
}
