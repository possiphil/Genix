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
        private bool TrySampleColliderWall(
            WallSurfaceSource source,
            Vector3 origin,
            Vector3 direction,
            out SurfacePoint point,
            IGenerationProfiler profiler)
        {
            point = default;
            float span = Mathf.Abs(direction.x) > 0.5f
                ? source.Bounds.size.x
                : source.Bounds.size.z;
            float distance = Mathf.Max(0.01f, span + BoundaryTolerance * 2f);
            Stopwatch stopwatch = profiler is { IsEnabled: true } ? Stopwatch.StartNew() : null;
            bool hitSurface = source.Collider.Raycast(new Ray(origin, direction), out RaycastHit hit, distance);
            stopwatch?.Stop();
            profiler?.RecordRaycast(
                PlacementType.Wall,
                hitSurface ? 1 : 0,
                stopwatch != null ? (float)stopwatch.Elapsed.TotalMilliseconds : 0f);

            if (!hitSurface ||
                !IsUsableHit(hit, source.Collider) ||
                SurfaceClassifier.Classify(hit.normal, _settings) != PlacementType.Wall ||
                !IsInsideWorldBounds(hit.point) ||
                !IsSurfaceFacingAreaVolume(hit.point, hit.normal))
            {
                return false;
            }

            point = new SurfacePoint(hit.point, hit.normal.normalized, source.Collider, null);
            return true;
        }

        private WallSurfaceSource[] FindWallSurfaceSources(out bool hasTerrainSurfaces)
        {
            hasTerrainSurfaces = false;

            if (!_settings.UsesAllMatchingSurfaceSearch ||
                _settings.GetSurfaceLayers(PlacementType.Wall).value == 0 ||
                _worldBounds.size.sqrMagnitude <= 0f)
            {
                return Array.Empty<WallSurfaceSource>();
            }

            Collider[] overlaps = Physics.OverlapBox(
                _worldBounds.center,
                _worldBounds.extents + Vector3.one * BoundaryTolerance,
                Quaternion.identity,
                _settings.GetSurfaceLayers(PlacementType.Wall),
                QueryTriggerInteraction.Ignore);

            if (overlaps == null || overlaps.Length == 0)
                return Array.Empty<WallSurfaceSource>();

            List<WallSurfaceSource> sources = new(overlaps.Length);
            HashSet<Collider> seen = new();

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];

                if (!collider ||
                    !seen.Add(collider) ||
                    ShouldIgnoreCollider(collider) ||
                    !collider.bounds.Intersects(_worldBounds))
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                float weight;

                if (collider is TerrainCollider terrainCollider)
                {
                    weight = EstimateTerrainWallArea(terrainCollider, bounds);

                    if (weight <= 0f)
                        continue;

                    hasTerrainSurfaces = true;
                }
                else
                {
                    weight = bounds.size.y * (bounds.size.x + bounds.size.z);

                    if (weight <= 0.0001f)
                        continue;
                }

                sources.Add(new WallSurfaceSource(collider, bounds, weight));
            }

            return sources.ToArray();
        }

        private float EstimateTerrainWallArea(TerrainCollider terrainCollider, Bounds terrainBounds)
        {
            const int samplesPerAxis = 9;
            float minX = Mathf.Max(_worldBounds.min.x, terrainBounds.min.x);
            float maxX = Mathf.Min(_worldBounds.max.x, terrainBounds.max.x);
            float minZ = Mathf.Max(_worldBounds.min.z, terrainBounds.min.z);
            float maxZ = Mathf.Min(_worldBounds.max.z, terrainBounds.max.z);

            if (minX >= maxX || minZ >= maxZ)
                return 0f;

            float weightedCoverage = 0f;

            for (int x = 0; x < samplesPerAxis; x++)
            {
                float worldX = Mathf.Lerp(minX, maxX, (x + 0.5f) / samplesPerAxis);

                for (int z = 0; z < samplesPerAxis; z++)
                {
                    float worldZ = Mathf.Lerp(minZ, maxZ, (z + 0.5f) / samplesPerAxis);

                    if (!TryGetTerrainSurfacePoint(
                            new Vector3(worldX, _worldBounds.center.y, worldZ),
                            terrainCollider,
                            out Vector3 surfacePosition,
                            out Vector3 normal) ||
                        SurfaceClassifier.Classify(normal, _settings) != PlacementType.Wall ||
                        !IsInsideWorldBounds(surfacePosition) ||
                        !IsSurfaceFacingAreaVolume(surfacePosition, normal))
                    {
                        continue;
                    }

                    weightedCoverage += Mathf.Min(10f, 1f / Mathf.Max(0.1f, Mathf.Abs(normal.y)));
                }
            }

            float projectedArea = (maxX - minX) * (maxZ - minZ);
            return projectedArea * weightedCoverage / (samplesPerAxis * samplesPerAxis);
        }

        private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 0.001f ? value.normalized : fallback;
        }

        private bool ShouldIgnoreCollider(Collider collider)
        {
            if (!collider || HasDontSaveHideFlags(collider.transform))
                return true;

            if (collider.GetComponentInParent<GeneratedObjectMetadata>() &&
                !collider.GetComponentInParent<PlacementSurfaceDescriptor>())
            {
                return true;
            }

            return _isSourceCollider?.Invoke(collider) == true;
        }

        private static bool HasDontSaveHideFlags(Transform transform)
        {
            while (transform)
            {
                if ((transform.gameObject.hideFlags & HideFlags.DontSave) != 0)
                    return true;

                transform = transform.parent;
            }

            return false;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new();

            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }
    }
}
