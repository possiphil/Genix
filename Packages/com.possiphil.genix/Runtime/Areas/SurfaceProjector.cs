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
    internal sealed partial class SurfaceProjector
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

    }
}
