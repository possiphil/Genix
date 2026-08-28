using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Layouts;
using Genix.Profiling;
using Genix.Semantics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Areas
{
    internal sealed partial class SurfaceProjector
    {
        private static bool TryProjectToRegion(
            Vector3 position,
            SurfaceRegion targetRegion,
            IReadOnlyList<SurfaceRegion> regions,
            Vector3 normal,
            out SurfacePoint point)
        {
            if (targetRegion != null)
            {
                point = new SurfacePoint(
                    new Vector3(position.x, targetRegion.SurfaceY, position.z),
                    normal,
                    null,
                    targetRegion.VoxelLayer);
                return true;
            }

            foreach (SurfaceRegion region in regions)
            {
                if (!region.ContainsXZ(position))
                    continue;

                point = new SurfacePoint(
                    new Vector3(position.x, region.SurfaceY, position.z),
                    normal,
                    null,
                    region.VoxelLayer);
                return true;
            }

            point = default;
            return false;
        }

        private int CollectHorizontalSurfaces(
            Vector3 position,
            PlacementType placementType,
            Vector3 direction,
            float originY,
            float distance,
            List<SurfacePoint> points,
            IGenerationProfiler profiler)
        {
            if (points == null ||
                !_settings.UsesAllMatchingSurfaceSearch ||
                _settings.GetSurfaceLayers(placementType).value == 0)
            {
                return 0;
            }

            int initialCount = points.Count;
            Ray ray = new(new Vector3(position.x, originY, position.z), direction);
            int hitCount = GetHits(ray, distance, placementType, out RaycastHit[] hits, profiler);
            SortHitsByDistance(hits, hitCount);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];

                if (!IsUsableHit(hit, null) ||
                    SurfaceClassifier.Classify(hit.normal, _settings) != placementType ||
                    hit.point.y < _worldBounds.min.y - BoundaryTolerance ||
                    hit.point.y > _worldBounds.max.y + BoundaryTolerance ||
                    !IsSurfaceFacingAreaVolume(hit.point, hit.normal))
                {
                    continue;
                }

                points.Add(new SurfacePoint(hit.point, hit.normal, hit.collider, null));
            }

            return points.Count - initialCount;
        }

        private bool TryFindFloor(
            Vector3 position,
            Collider expectedCollider,
            SurfaceRegion targetRegion,
            int? targetVoxelLayer,
            out SurfacePoint point,
            IGenerationProfiler profiler = null)
        {
            point = default;
            bool requireRegionMatch = _settings.UsesBoundarySurfaceProjection;

            if (_settings.GetSurfaceLayers(PlacementType.Floor).value == 0 ||
                requireRegionMatch &&
                !HasMatchingRegion(position, targetRegion, targetVoxelLayer, _floorRegions))
            {
                return false;
            }

            if (!requireRegionMatch)
            {
                if (TryFindTerrainFloor(position, expectedCollider, out point))
                {
                    return true;
                }
            }

            float raycastHeight = Mathf.Max(0.01f, _settings.surfaceRaycastHeight);
            float originY = (targetRegion?.SurfaceY ?? position.y) + raycastHeight;
            Ray ray = new(new Vector3(position.x, originY, position.z), Vector3.down);
            int hitCount = GetHits(
                ray,
                _settings.surfaceRaycastDistance,
                PlacementType.Floor,
                out RaycastHit[] hits,
                profiler);
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            SurfacePoint bestPoint = default;
            SurfaceRegion matchedRegion = null;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];

                if (!IsUsableHit(hit, expectedCollider) ||
                    SurfaceClassifier.Classify(hit.normal, _settings) != PlacementType.Floor ||
                    hit.point.y < _worldBounds.min.y - BoundaryTolerance ||
                    hit.point.y > _worldBounds.max.y + _settings.surfaceRaycastHeight ||
                    !IsSurfaceFacingAreaVolume(hit.point, hit.normal))
                {
                    continue;
                }

                if (requireRegionMatch &&
                    !TryGetMatchingRegion(
                        hit.point,
                        targetRegion,
                        targetVoxelLayer,
                        _floorRegions,
                        out matchedRegion))
                {
                    continue;
                }

                if (hit.distance >= bestDistance)
                    continue;

                bestDistance = hit.distance;
                bestPoint = new SurfacePoint(hit.point, hit.normal, hit.collider, matchedRegion?.VoxelLayer);
                found = true;
            }

            point = bestPoint;
            return found;
        }

        private bool TryFindSupportPoint(
            Vector3 position,
            Collider expectedCollider,
            int? targetVoxelLayer,
            PlacementType placementType,
            out SurfacePoint point,
            IGenerationProfiler profiler = null)
        {
            if (placementType == PlacementType.Ceiling)
            {
                return TryFindCeiling(
                    position,
                    expectedCollider,
                    null,
                    targetVoxelLayer,
                    out point,
                    profiler);
            }

            return TryFindFloor(
                position,
                expectedCollider,
                null,
                targetVoxelLayer,
                out point,
                profiler);
        }

        private bool TryFindWallSupportPoint(
            Vector3 position,
            Vector3 expectedNormal,
            float maxDepthDifference,
            Collider expectedCollider,
            int? targetVoxelLayer,
            out SurfacePoint point,
            IGenerationProfiler profiler)
        {
            point = default;
            float probeOffset = Mathf.Max(0.05f, maxDepthDifference + 0.05f);
            float distance = Mathf.Min(
                Mathf.Max(0.01f, _settings.surfaceRaycastDistance),
                probeOffset * 2f + BoundaryTolerance);
            int hitCount = GetHits(
                new Ray(position + expectedNormal * probeOffset, -expectedNormal),
                distance,
                PlacementType.Wall,
                out RaycastHit[] hits,
                profiler);
            bool found = false;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];

                if (!IsUsableHit(hit, expectedCollider) ||
                    SurfaceClassifier.Classify(hit.normal, _settings) != PlacementType.Wall ||
                    !IsInsideWorldBounds(hit.point))
                {
                    continue;
                }

                Vector3 surfaceNormal = hit.normal.normalized;

                if (Vector3.Dot(surfaceNormal, expectedNormal) < 0f)
                    surfaceNormal = -surfaceNormal;

                if (Vector3.Dot(surfaceNormal, expectedNormal) < 0.25f ||
                    !IsSurfaceFacingAreaVolume(hit.point, surfaceNormal) ||
                    hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                point = new SurfacePoint(hit.point, surfaceNormal, hit.collider, targetVoxelLayer);
                found = true;
            }

            return found;
        }

        private bool TryFindTerrainFloor(
            Vector3 position,
            Collider expectedCollider,
            out SurfacePoint point)
        {
            point = default;

            if (expectedCollider is not TerrainCollider terrainCollider ||
                !IsLayerIncluded(terrainCollider.gameObject.layer, _settings.GetSurfaceLayers(PlacementType.Floor)) ||
                ShouldIgnoreCollider(terrainCollider) ||
                !TryGetTerrainSurfacePoint(position, terrainCollider, out Vector3 surfacePosition, out Vector3 normal))
            {
                return false;
            }

            float raycastHeight = Mathf.Max(0.01f, _settings.surfaceRaycastHeight);
            float originY = position.y + raycastHeight;
            float distance = Mathf.Max(0.01f, _settings.surfaceRaycastDistance);

            if (surfacePosition.y > originY + BoundaryTolerance ||
                surfacePosition.y < originY - distance - BoundaryTolerance ||
                surfacePosition.y < _worldBounds.min.y - BoundaryTolerance ||
                surfacePosition.y > _worldBounds.max.y + BoundaryTolerance ||
                SurfaceClassifier.Classify(normal, _settings) != PlacementType.Floor ||
                !IsSurfaceFacingAreaVolume(surfacePosition, normal))
            {
                return false;
            }

            point = new SurfacePoint(surfacePosition, normal, terrainCollider, null);
            return true;
        }

        private static bool TryGetTerrainSurfacePoint(
            Vector3 position,
            TerrainCollider terrainCollider,
            out Vector3 surfacePosition,
            out Vector3 normal)
        {
            surfacePosition = default;
            normal = default;

            if (!terrainCollider)
                return false;

            TerrainData terrainData = terrainCollider.terrainData;

            if (!terrainData)
                return false;

            Vector3 terrainSize = terrainData.size;

            if (terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            Transform terrainTransform = terrainCollider.transform;
            Vector3 local = terrainTransform.InverseTransformPoint(position);
            float normalizedX = local.x / terrainSize.x;
            float normalizedZ = local.z / terrainSize.z;

            if (normalizedX < -TerrainCoordinateTolerance ||
                normalizedX > 1f + TerrainCoordinateTolerance ||
                normalizedZ < -TerrainCoordinateTolerance ||
                normalizedZ > 1f + TerrainCoordinateTolerance)
            {
                return false;
            }

            normalizedX = Mathf.Clamp01(normalizedX);
            normalizedZ = Mathf.Clamp01(normalizedZ);
            float localX = Mathf.Clamp(local.x, 0f, terrainSize.x);
            float localZ = Mathf.Clamp(local.z, 0f, terrainSize.z);
            float localY = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
            surfacePosition = terrainTransform.TransformPoint(new Vector3(localX, localY, localZ));
            Vector3 localNormal = terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
            normal = terrainTransform.TransformDirection(localNormal).normalized;

            return normal.sqrMagnitude > 0.001f;
        }

        private bool IsFootprintInsideWorldBoundsXZ(
            Vector3 center,
            Vector3 right,
            Vector3 forward,
            float width,
            float depth)
        {
            Vector3 halfRight = right * (width * 0.5f);
            Vector3 halfForward = forward * (depth * 0.5f);

            return IsPointInsideWorldBoundsXZ(center - halfRight - halfForward) &&
                   IsPointInsideWorldBoundsXZ(center - halfRight + halfForward) &&
                   IsPointInsideWorldBoundsXZ(center + halfRight - halfForward) &&
                   IsPointInsideWorldBoundsXZ(center + halfRight + halfForward);
        }

        private bool IsPointInsideWorldBoundsXZ(Vector3 point)
        {
            return point.x >= _worldBounds.min.x - FootprintBoundsTolerance &&
                   point.x <= _worldBounds.max.x + FootprintBoundsTolerance &&
                   point.z >= _worldBounds.min.z - FootprintBoundsTolerance &&
                   point.z <= _worldBounds.max.z + FootprintBoundsTolerance;
        }

        private bool IsInsideWorldBounds(Vector3 point)
        {
            return point.x >= _worldBounds.min.x - BoundaryTolerance &&
                   point.x <= _worldBounds.max.x + BoundaryTolerance &&
                   point.y >= _worldBounds.min.y - BoundaryTolerance &&
                   point.y <= _worldBounds.max.y + BoundaryTolerance &&
                   point.z >= _worldBounds.min.z - BoundaryTolerance &&
                   point.z <= _worldBounds.max.z + BoundaryTolerance;
        }

        private static bool IsLayerIncluded(int layer, LayerMask mask) =>
            (mask.value & (1 << layer)) != 0;
    }
}
