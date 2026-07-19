using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Diagnostics;
using Genix.Geometry;
using UnityEngine;

namespace Genix.SpaceFoundation.Editor
{
    internal static class BoundsAreaFallback
    {
        public static bool TryBuild(
            GameObject source,
            SpatialSourceInfo sourceInfo,
            AreaBuildSettings settings,
            Predicate<Collider> isSourceCollider,
            out PlacementArea area,
            out string error)
        {
            if (!BoundsUtility.TryGetCombinedBounds(source.transform, out Bounds bounds, true, false))
            {
                area = null;
                error = $"Location '{sourceInfo.SourceName}' has neither persistent voxel data nor readable collider or renderer bounds.";
                return false;
            }

            string name = AreaName.ToDesignerName(source.name);
            List<SurfaceRegion> floors = new()
            {
                SurfaceRegion.CreateFloor(name, bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, bounds.min.y)
            };
            List<SurfaceRegion> ceilings = new()
            {
                SurfaceRegion.CreateCeiling($"{name} Ceiling", bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, bounds.max.y)
            };
            List<SurfaceRegion> walls = CreateWalls(bounds, name);

            area = new PlacementArea(
                sourceInfo,
                bounds,
                floors,
                walls,
                null,
                0f,
                settings,
                null,
                ceilings,
                null,
                isSourceCollider);
            error = string.Empty;
            return true;
        }

        private static List<SurfaceRegion> CreateWalls(Bounds bounds, string name)
        {
            float y = bounds.min.y;

            return new List<SurfaceRegion>
            {
                SurfaceRegion.CreateWall($"{name} Wall -Z", new Vector3(bounds.min.x, y, bounds.min.z), new Vector3(bounds.max.x, y, bounds.min.z), bounds.max.y, Vector3.forward),
                SurfaceRegion.CreateWall($"{name} Wall +X", new Vector3(bounds.max.x, y, bounds.min.z), new Vector3(bounds.max.x, y, bounds.max.z), bounds.max.y, Vector3.left),
                SurfaceRegion.CreateWall($"{name} Wall +Z", new Vector3(bounds.max.x, y, bounds.max.z), new Vector3(bounds.min.x, y, bounds.max.z), bounds.max.y, Vector3.back),
                SurfaceRegion.CreateWall($"{name} Wall -X", new Vector3(bounds.min.x, y, bounds.max.z), new Vector3(bounds.min.x, y, bounds.min.z), bounds.max.y, Vector3.right)
            };
        }
    }

    internal static class SurfaceRegionBounds
    {
        public static Bounds Calculate(
            IReadOnlyList<SurfaceRegion> floors,
            IReadOnlyList<SurfaceRegion> walls,
            IReadOnlyList<SurfaceRegion> ceilings)
        {
            SurfaceRegion first = floors.Count > 0
                ? floors[0]
                : walls.Count > 0
                    ? walls[0]
                    : ceilings[0];
            Bounds bounds = first.Bounds;

            EncapsulateAll(floors, ref bounds);
            EncapsulateAll(walls, ref bounds);
            EncapsulateAll(ceilings, ref bounds);
            return bounds;
        }

        private static void EncapsulateAll(IEnumerable<SurfaceRegion> regions, ref Bounds bounds)
        {
            foreach (SurfaceRegion region in regions)
                bounds.Encapsulate(region.Bounds);
        }
    }
}
