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
        private int GetHits(
            Ray ray,
            float distance,
            PlacementType placementType,
            out RaycastHit[] hits,
            IGenerationProfiler profiler = null)
        {
            Stopwatch stopwatch = profiler is { IsEnabled: true } ? Stopwatch.StartNew() : null;
            hits = GetRaycastBuffer();
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                hits,
                Mathf.Max(0.01f, distance),
                _settings.GetSurfaceLayers(placementType),
                QueryTriggerInteraction.Ignore);

            if (hitCount >= hits.Length)
            {
                hits = Physics.RaycastAll(
                    ray,
                    Mathf.Max(0.01f, distance),
                    _settings.GetSurfaceLayers(placementType),
                    QueryTriggerInteraction.Ignore);
                hitCount = hits.Length;
            }

            stopwatch?.Stop();
            profiler?.RecordRaycast(
                placementType,
                hitCount,
                stopwatch != null ? (float)stopwatch.Elapsed.TotalMilliseconds : 0f);
            return hitCount;
        }

        private RaycastHit[] GetRaycastBuffer()
        {
            if (_raycastBuffer == null || _raycastBuffer.Length == 0)
                _raycastBuffer = new RaycastHit[InitialRaycastBufferSize];

            return _raycastBuffer;
        }

        private static void SortHitsByDistance(RaycastHit[] hits, int hitCount)
        {
            if (hits == null || hitCount <= 1)
                return;

            Array.Sort(hits, 0, hitCount, RaycastHitDistanceComparer.Instance);
        }

        private bool IsUsableHit(RaycastHit hit, Collider expectedCollider)
        {
            return hit.collider &&
                   (!expectedCollider || hit.collider == expectedCollider) &&
                   !ShouldIgnoreCollider(hit.collider);
        }

        private static bool HasMatchingRegion(
            Vector3 position,
            SurfaceRegion targetRegion,
            int? targetVoxelLayer,
            IReadOnlyList<SurfaceRegion> regions)
        {
            if (targetRegion != null)
                return targetRegion.ContainsXZ(position);

            foreach (SurfaceRegion region in regions)
            {
                if (targetVoxelLayer.HasValue && region.VoxelLayer != targetVoxelLayer)
                    continue;

                if (region.ContainsXZ(position))
                    return true;
            }

            return false;
        }

        private bool TryGetMatchingRegion(
            Vector3 hitPoint,
            SurfaceRegion targetRegion,
            int? targetVoxelLayer,
            IReadOnlyList<SurfaceRegion> regions,
            out SurfaceRegion matchedRegion)
        {
            matchedRegion = null;

            if (targetRegion != null)
            {
                if (!targetRegion.ContainsXZ(hitPoint))
                    return false;

                matchedRegion = targetRegion;
                return true;
            }

            foreach (SurfaceRegion region in regions)
            {
                if (targetVoxelLayer.HasValue && region.VoxelLayer != targetVoxelLayer)
                    continue;

                if (!region.ContainsXZ(hitPoint))
                    continue;

                matchedRegion = region;
                return true;
            }

            return false;
        }

        private bool IsSurfaceFacingAreaVolume(Vector3 surfacePoint, Vector3 surfaceNormal)
        {
            if (!_occupancy.HasVolumeCells)
                return true;

            Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f
                ? surfaceNormal.normalized
                : Vector3.up;
            float offset = Mathf.Max(0.02f, _occupancy.CellSize * 0.1f);
            return _occupancy.ContainsVolumePoint(surfacePoint + normal * offset);
        }
    }
}
