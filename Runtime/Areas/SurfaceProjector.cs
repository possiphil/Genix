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
    internal enum WallSurfaceSampleAxis
    {
        Terrain,
        X,
        Z
    }

    internal readonly struct WallSurfaceSource
    {
        public Collider Collider { get; }
        public Bounds Bounds { get; }
        public float Weight { get; }
        public bool IsTerrain => Collider is TerrainCollider;

        public WallSurfaceSource(Collider collider, Bounds bounds, float weight)
        {
            Collider = collider;
            Bounds = bounds;
            Weight = Mathf.Max(0f, weight);
        }
    }

    /// <summary>
    /// Projects candidate coordinates onto matching physics surfaces and evaluates adaptive footprint support.
    /// </summary>
    /// <remarks>
    /// Terrain probes use the terrain height path when possible; other colliders use non-allocating physics queries.
    /// Layer masks, surface normal classification, source-collider exclusion, and voxel-layer hints are enforced here.
    /// </remarks>
    internal sealed class SurfaceProjector
    {
        private const int InitialRaycastBufferSize = 64;
        private const float BoundaryTolerance = 0.1f;
        private const float FootprintBoundsTolerance = 0.001f;
        private const float FullSurfaceSupportThreshold = 0.9999f;
        private const float TerrainCoordinateTolerance = 0.0001f;

        private readonly Bounds _worldBounds;
        private readonly IReadOnlyList<SurfaceRegion> _floorRegions;
        private readonly IReadOnlyList<SurfaceRegion> _ceilingRegions;
        private readonly VoxelOccupancy _occupancy;
        private readonly AreaBuildSettings _settings;
        private readonly Predicate<Collider> _isSourceCollider;
        private readonly WallSurfaceSource[] _wallSurfaceSources;
        private RaycastHit[] _raycastBuffer;

        public IReadOnlyList<WallSurfaceSource> WallSurfaceSources => _wallSurfaceSources;
        public bool HasTerrainSurfaces { get; }

        public SurfaceProjector(
            Bounds worldBounds,
            IReadOnlyList<SurfaceRegion> floorRegions,
            IReadOnlyList<SurfaceRegion> ceilingRegions,
            VoxelOccupancy occupancy,
            AreaBuildSettings settings,
            Predicate<Collider> isSourceCollider)
        {
            _worldBounds = worldBounds;
            _floorRegions = floorRegions;
            _ceilingRegions = ceilingRegions;
            _occupancy = occupancy;
            _settings = settings;
            _isSourceCollider = isSourceCollider;
            _wallSurfaceSources = FindWallSurfaceSources(out bool hasTerrainSurfaces);
            HasTerrainSurfaces = hasTerrainSurfaces;
        }

        /// <summary>Projects a candidate downward onto the nearest valid floor surface.</summary>
        public bool TryProjectToFloor(
            Vector3 position,
            SurfaceRegion targetRegion,
            out SurfacePoint point,
            IGenerationProfiler profiler = null)
        {
            if (targetRegion != null && !targetRegion.ContainsXZ(position))
            {
                point = default;
                return false;
            }

            if (_settings.UsesPhysicsSurfaceProjection)
            {
                return TryFindFloor(
                    position,
                    null,
                    targetRegion,
                    targetRegion?.VoxelLayer,
                    out point,
                    profiler);
            }

            return TryProjectToRegion(position, targetRegion, _floorRegions, Vector3.up, out point);
        }

        /// <summary>Projects a candidate upward onto the nearest valid ceiling surface.</summary>
        public bool TryProjectToCeiling(
            Vector3 position,
            SurfaceRegion targetRegion,
            out SurfacePoint point,
            IGenerationProfiler profiler = null)
        {
            if (targetRegion != null && !targetRegion.ContainsXZ(position))
            {
                point = default;
                return false;
            }

            if (_settings.UsesPhysicsSurfaceProjection)
            {
                return TryFindCeiling(
                    position,
                    null,
                    targetRegion,
                    targetRegion?.VoxelLayer,
                    out point,
                    profiler);
            }

            return TryProjectToRegion(position, targetRegion, _ceilingRegions, Vector3.down, out point);
        }

        /// <summary>Projects a candidate along both horizontal directions onto the nearest valid wall surface.</summary>
        public bool TryProjectToWall(
            Vector3 position,
            Vector3 inwardNormal,
            int? targetVoxelLayer,
            out SurfacePoint point,
            IGenerationProfiler profiler = null)
        {
            inwardNormal = inwardNormal.sqrMagnitude > 0.001f
                ? inwardNormal.normalized
                : Vector3.forward;

            if (!_settings.UsesPhysicsSurfaceProjection)
            {
                point = new SurfacePoint(position, inwardNormal, null, targetVoxelLayer);
                return true;
            }

            return TryFindWall(position, inwardNormal, targetVoxelLayer, out point, profiler);
        }

        public int CollectFloorSurfaces(
            Vector3 position,
            List<SurfacePoint> points,
            IGenerationProfiler profiler = null) =>
            CollectHorizontalSurfaces(
                position,
                PlacementType.Floor,
                Vector3.down,
                _worldBounds.max.y + Mathf.Max(0.01f, _settings.surfaceRaycastHeight),
                _worldBounds.size.y + Mathf.Max(0.01f, _settings.surfaceRaycastHeight) * 2f,
                points,
                profiler);

        public int CollectCeilingSurfaces(
            Vector3 position,
            List<SurfacePoint> points,
            IGenerationProfiler profiler = null) =>
            CollectHorizontalSurfaces(
                position,
                PlacementType.Ceiling,
                Vector3.up,
                _worldBounds.min.y - Mathf.Max(0.01f, _settings.surfaceRaycastHeight),
                _worldBounds.size.y + Mathf.Max(0.01f, _settings.surfaceRaycastHeight) * 2f,
                points,
                profiler);

        /// <summary>Appends wall surfaces sampled from one cached collider source.</summary>
        public int CollectWallSurfaces(
            WallSurfaceSource source,
            WallSurfaceSampleAxis axis,
            Vector3 position,
            List<SurfacePoint> points,
            IGenerationProfiler profiler = null)
        {
            if (points == null ||
                !_settings.UsesAllMatchingSurfaceSearch ||
                !source.Collider ||
                _settings.GetSurfaceLayers(PlacementType.Wall).value == 0)
            {
                return 0;
            }

            int initialCount = points.Count;

            if (source.Collider is TerrainCollider terrainCollider)
            {
                if (!TryGetTerrainSurfacePoint(position, terrainCollider, out Vector3 surfacePosition, out Vector3 normal) ||
                    SurfaceClassifier.Classify(normal, _settings) != PlacementType.Wall ||
                    !IsInsideWorldBounds(surfacePosition) ||
                    !IsSurfaceFacingAreaVolume(surfacePosition, normal))
                {
                    return 0;
                }

                points.Add(new SurfacePoint(surfacePosition, normal, terrainCollider, null));
                return 1;
            }

            if (axis == WallSurfaceSampleAxis.X)
            {
                Vector3 minOrigin = new(source.Bounds.min.x - BoundaryTolerance, position.z, position.x);
                Vector3 maxOrigin = new(source.Bounds.max.x + BoundaryTolerance, position.z, position.x);
                AddBidirectionalColliderSamples(
                    source,
                    minOrigin,
                    Vector3.right,
                    maxOrigin,
                    Vector3.left,
                    points,
                    profiler);
            }
            else if (axis == WallSurfaceSampleAxis.Z)
            {
                Vector3 minOrigin = new(position.x, position.z, source.Bounds.min.z - BoundaryTolerance);
                Vector3 maxOrigin = new(position.x, position.z, source.Bounds.max.z + BoundaryTolerance);
                AddBidirectionalColliderSamples(
                    source,
                    minOrigin,
                    Vector3.forward,
                    maxOrigin,
                    Vector3.back,
                    points,
                    profiler);
            }

            return points.Count - initialCount;
        }

        private void AddBidirectionalColliderSamples(
            WallSurfaceSource source,
            Vector3 firstOrigin,
            Vector3 firstDirection,
            Vector3 secondOrigin,
            Vector3 secondDirection,
            ICollection<SurfacePoint> points,
            IGenerationProfiler profiler)
        {
            bool hasFirst = TrySampleColliderWall(
                source,
                firstOrigin,
                firstDirection,
                out SurfacePoint first,
                profiler);

            if (hasFirst)
                points.Add(first);

            if (!TrySampleColliderWall(
                    source,
                    secondOrigin,
                    secondDirection,
                    out SurfacePoint second,
                    profiler))
            {
                return;
            }

            if (!hasFirst ||
                (second.Position - first.Position).sqrMagnitude > 0.000001f ||
                Vector3.Dot(second.Normal, first.Normal) < 0.999f)
            {
                points.Add(second);
            }
        }

        public bool HasFloorSurfaceAt(Vector3 position) =>
            TryFindFloor(position, null, null, null, out _);

        public bool HasSurfaceAt(
            Vector3 position,
            PlacementType placementType,
            int? voxelLayer,
            Collider expectedSurfaceCollider,
            IGenerationProfiler profiler = null)
        {
            return placementType == PlacementType.Ceiling
                ? TryFindCeiling(position, expectedSurfaceCollider, null, voxelLayer, out _, profiler)
                : TryFindFloor(position, expectedSurfaceCollider, null, voxelLayer, out _, profiler);
        }

        /// <summary>
        /// Probes an asset footprint and derives support ratio, placement height, and an optional fitted normal.
        /// </summary>
        /// <remarks>Returns false when support or height variation violates the asset's adaptive-fit constraints.</remarks>
        public bool TryEvaluateSurfaceFit(
            Vector3 surfaceCenter,
            Quaternion footprintRotation,
            AssetDefinition asset,
            Collider expectedSurfaceCollider,
            int? voxelLayer,
            PlacementType placementType,
            out SurfaceFitResult result,
            IGenerationProfiler profiler = null)
        {
            result = default;

            if (!asset || placementType == PlacementType.InsideSpace)
                return false;

            if (placementType == PlacementType.Wall)
            {
                return TryEvaluateWallSurfaceFit(
                    surfaceCenter,
                    footprintRotation,
                    asset,
                    expectedSurfaceCollider,
                    voxelLayer,
                    out result,
                    profiler);
            }

            Vector3 right = NormalizeOrFallback(footprintRotation * Vector3.right, Vector3.right);
            Vector3 forward = NormalizeOrFallback(footprintRotation * Vector3.forward, Vector3.forward);
            float width = Mathf.Max(0.01f, asset.Width);
            float depth = Mathf.Max(0.01f, asset.Depth);

            if (asset.MinSurfaceSupport >= FullSurfaceSupportThreshold &&
                !IsFootprintInsideWorldBoundsXZ(surfaceCenter, right, forward, width, depth))
            {
                return false;
            }

            int widthSegments = _occupancy.GetFootprintSegmentCount(width);
            int depthSegments = _occupancy.GetFootprintSegmentCount(depth);
            int totalSamples = (widthSegments + 1) * (depthSegments + 1);
            int processedSamples = 0;
            int supportedSamples = 0;
            int requiredSupportedSamples = Mathf.CeilToInt(
                Mathf.Max(0f, asset.MinSurfaceSupport - 0.0001f) * totalSamples);
            float maxHeightDifference = asset.MaxSurfaceHeightDifference;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            float sumY = 0f;
            Vector3 normalSum = Vector3.zero;

            for (int x = 0; x <= widthSegments; x++)
            {
                float offsetX = Mathf.Lerp(-width * 0.5f, width * 0.5f, x / (float)widthSegments);

                for (int z = 0; z <= depthSegments; z++)
                {
                    processedSamples++;
                    float offsetZ = Mathf.Lerp(-depth * 0.5f, depth * 0.5f, z / (float)depthSegments);
                    Vector3 samplePosition = surfaceCenter + right * offsetX + forward * offsetZ;

                    if (!TryFindSupportPoint(
                            samplePosition,
                            expectedSurfaceCollider,
                            voxelLayer,
                            placementType,
                            out SurfacePoint support,
                            profiler))
                    {
                        if (!CanStillReachRequiredSupport(
                                supportedSamples,
                                processedSamples,
                                totalSamples,
                                requiredSupportedSamples))
                        {
                            return false;
                        }

                        continue;
                    }

                    supportedSamples++;
                    minY = Mathf.Min(minY, support.Position.y);
                    maxY = Mathf.Max(maxY, support.Position.y);

                    if (maxY - minY > maxHeightDifference)
                    {
                        return false;
                    }

                    sumY += support.Position.y;
                    normalSum += support.Normal.normalized;
                }
            }

            if (supportedSamples == 0)
                return false;

            float supportRatio = supportedSamples / (float)Mathf.Max(1, totalSamples);

            if (supportRatio + 0.0001f < asset.MinSurfaceSupport)
                return false;

            float heightDifference = maxY - minY;

            if (heightDifference > asset.MaxSurfaceHeightDifference)
                return false;

            float surfaceY = asset.SurfaceHeightMode switch
            {
                SurfaceHeightMode.Lowest => minY,
                SurfaceHeightMode.Highest => maxY,
                _ => sumY / supportedSamples
            };
            Vector3 normal = normalSum.sqrMagnitude > 0.001f
                ? normalSum.normalized
                : placementType == PlacementType.Ceiling ? Vector3.down : Vector3.up;
            result = new SurfaceFitResult(
                new Vector3(surfaceCenter.x, surfaceY, surfaceCenter.z),
                normal,
                heightDifference,
                supportRatio);
            return true;
        }

        private bool TryEvaluateWallSurfaceFit(
            Vector3 surfaceCenter,
            Quaternion footprintRotation,
            AssetDefinition asset,
            Collider expectedSurfaceCollider,
            int? voxelLayer,
            out SurfaceFitResult result,
            IGenerationProfiler profiler)
        {
            result = default;
            Vector3 normal = NormalizeOrFallback(footprintRotation * Vector3.forward, Vector3.forward);
            Vector3 right = NormalizeOrFallback(footprintRotation * Vector3.right, Vector3.right);
            Vector3 up = NormalizeOrFallback(footprintRotation * Vector3.up, Vector3.up);
            float width = Mathf.Max(0.01f, asset.Width);
            float height = Mathf.Max(0.01f, asset.Height);
            int widthSegments = _occupancy.GetFootprintSegmentCount(width);
            int heightSegments = _occupancy.GetFootprintSegmentCount(height);
            int totalSamples = (widthSegments + 1) * (heightSegments + 1);
            int processedSamples = 0;
            int supportedSamples = 0;
            int requiredSupportedSamples = Mathf.CeilToInt(
                Mathf.Max(0f, asset.MinSurfaceSupport - 0.0001f) * totalSamples);
            float minDepth = float.PositiveInfinity;
            float maxDepth = float.NegativeInfinity;
            Vector3 normalSum = Vector3.zero;
            Span<Vector3> supportPositions = stackalloc Vector3[totalSamples];

            for (int x = 0; x <= widthSegments; x++)
            {
                float offsetX = Mathf.Lerp(-width * 0.5f, width * 0.5f, x / (float)widthSegments);

                for (int y = 0; y <= heightSegments; y++)
                {
                    processedSamples++;
                    float offsetY = Mathf.Lerp(-height * 0.5f, height * 0.5f, y / (float)heightSegments);
                    Vector3 samplePosition = surfaceCenter + right * offsetX + up * offsetY;

                    if (!TryFindWallSupportPoint(
                            samplePosition,
                            normal,
                            asset.MaxSurfaceHeightDifference,
                            expectedSurfaceCollider,
                            voxelLayer,
                            out SurfacePoint support,
                            profiler))
                    {
                        if (!CanStillReachRequiredSupport(
                                supportedSamples,
                                processedSamples,
                                totalSamples,
                                requiredSupportedSamples))
                        {
                            return false;
                        }

                        continue;
                    }

                    supportPositions[supportedSamples] = support.Position;
                    supportedSamples++;
                    float depth = Vector3.Dot(support.Position - samplePosition, normal);
                    minDepth = Mathf.Min(minDepth, depth);
                    maxDepth = Mathf.Max(maxDepth, depth);

                    if (maxDepth - minDepth > asset.MaxSurfaceHeightDifference)
                        return false;

                    Vector3 supportNormal = support.Normal.normalized;
                    normalSum += Vector3.Dot(supportNormal, normal) < 0f ? -supportNormal : supportNormal;
                }
            }

            if (supportedSamples == 0)
                return false;

            float supportRatio = supportedSamples / (float)Mathf.Max(1, totalSamples);

            if (supportRatio + 0.0001f < asset.MinSurfaceSupport)
                return false;

            Vector3 fittedNormal = normalSum.sqrMagnitude > 0.001f ? normalSum.normalized : normal;
            float fittedMinDepth = float.PositiveInfinity;
            float fittedMaxDepth = float.NegativeInfinity;
            float fittedDepthSum = 0f;

            for (int i = 0; i < supportedSamples; i++)
            {
                float fittedDepth = Vector3.Dot(supportPositions[i] - surfaceCenter, fittedNormal);
                fittedMinDepth = Mathf.Min(fittedMinDepth, fittedDepth);
                fittedMaxDepth = Mathf.Max(fittedMaxDepth, fittedDepth);
                fittedDepthSum += fittedDepth;
            }

            float depthDifference = fittedMaxDepth - fittedMinDepth;
            if (depthDifference > asset.MaxSurfaceHeightDifference)
                return false;

            float surfaceDepth = asset.SurfaceHeightMode switch
            {
                SurfaceHeightMode.Lowest => fittedMinDepth,
                SurfaceHeightMode.Highest => fittedMaxDepth,
                _ => fittedDepthSum / supportedSamples
            };
            result = new SurfaceFitResult(
                surfaceCenter + fittedNormal * surfaceDepth,
                fittedNormal,
                depthDifference,
                supportRatio);
            return true;
        }

        private static bool CanStillReachRequiredSupport(
            int supportedSamples,
            int processedSamples,
            int totalSamples,
            int requiredSupportedSamples)
        {
            int remainingSamples = Mathf.Max(0, totalSamples - processedSamples);
            return supportedSamples + remainingSamples >= requiredSupportedSamples;
        }

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
