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
        private bool TryFindCeiling(
            Vector3 position,
            Collider expectedCollider,
            SurfaceRegion targetRegion,
            int? targetVoxelLayer,
            out SurfacePoint point,
            IGenerationProfiler profiler = null)
        {
            point = default;
            bool requireRegionMatch = _settings.UsesBoundarySurfaceProjection;

            if (_settings.GetSurfaceLayers(PlacementType.Ceiling).value == 0 ||
                requireRegionMatch &&
                !HasMatchingRegion(position, targetRegion, targetVoxelLayer, _ceilingRegions))
            {
                return false;
            }

            float raycastHeight = Mathf.Max(0.01f, _settings.surfaceRaycastHeight);
            float originY = (targetRegion?.SurfaceY ?? position.y) - raycastHeight;
            Ray ray = new(new Vector3(position.x, originY, position.z), Vector3.up);
            int hitCount = GetHits(
                ray,
                _settings.surfaceRaycastDistance,
                PlacementType.Ceiling,
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
                    SurfaceClassifier.Classify(hit.normal, _settings) != PlacementType.Ceiling ||
                    hit.point.y < _worldBounds.min.y - _settings.surfaceRaycastHeight ||
                    hit.point.y > _worldBounds.max.y + BoundaryTolerance ||
                    !IsSurfaceFacingAreaVolume(hit.point, hit.normal))
                {
                    continue;
                }

                if (requireRegionMatch &&
                    !TryGetMatchingRegion(
                        hit.point,
                        targetRegion,
                        targetVoxelLayer,
                        _ceilingRegions,
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

        private bool TryFindWall(
            Vector3 position,
            Vector3 inwardNormal,
            int? targetVoxelLayer,
            out SurfacePoint point,
            IGenerationProfiler profiler = null)
        {
            float offset = _occupancy.CellSize > 0f
                ? Mathf.Max(0.05f, _occupancy.CellSize * 0.5f)
                : 0.5f;
            float configuredDistance = Mathf.Max(0.01f, _settings.surfaceRaycastDistance);
            float desiredDistance = _occupancy.CellSize > 0f
                ? Mathf.Max(1f, _occupancy.CellSize * 4f)
                : Mathf.Min(configuredDistance, 5f);
            float distance = Mathf.Min(configuredDistance, desiredDistance);

            return TryFindWallAlongRay(
                       position + inwardNormal * offset,
                       -inwardNormal,
                       distance,
                       inwardNormal,
                       targetVoxelLayer,
                       out point,
                       profiler) ||
                   TryFindWallAlongRay(
                       position - inwardNormal * offset,
                       inwardNormal,
                       distance,
                       inwardNormal,
                       targetVoxelLayer,
                       out point,
                       profiler);
        }

        private bool TryFindWallAlongRay(
            Vector3 origin,
            Vector3 direction,
            float distance,
            Vector3 inwardNormal,
            int? targetVoxelLayer,
            out SurfacePoint point,
            IGenerationProfiler profiler = null)
        {
            point = default;

            if (_settings.GetSurfaceLayers(PlacementType.Wall).value == 0)
                return false;

            int hitCount = GetHits(
                new Ray(origin, direction),
                distance,
                PlacementType.Wall,
                out RaycastHit[] hits,
                profiler);
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            SurfacePoint bestPoint = default;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];

                if (!IsUsableHit(hit, null) ||
                    SurfaceClassifier.Classify(hit.normal, _settings) != PlacementType.Wall)
                {
                    continue;
                }

                Vector3 surfaceNormal = hit.normal.normalized;

                if (Vector3.Dot(surfaceNormal, inwardNormal) < 0f)
                    surfaceNormal = -surfaceNormal;

                if (Vector3.Dot(surfaceNormal, inwardNormal) < 0.25f ||
                    hit.point.y < _worldBounds.min.y - BoundaryTolerance ||
                    hit.point.y > _worldBounds.max.y + BoundaryTolerance ||
                    !IsSurfaceFacingAreaVolume(hit.point, surfaceNormal))
                {
                    continue;
                }

                if (hit.distance >= bestDistance)
                    continue;

                bestDistance = hit.distance;
                bestPoint = new SurfacePoint(hit.point, surfaceNormal, hit.collider, targetVoxelLayer);
                found = true;
            }

            point = bestPoint;
            return found;
        }
    }
}
