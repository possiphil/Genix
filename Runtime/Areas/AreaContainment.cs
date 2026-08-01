using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using Genix.Profiling;
using UnityEngine;

namespace Genix.Areas
{
    internal sealed class AreaContainment
    {
        private const float FootprintBoundsTolerance = 0.001f;

        private readonly Bounds _worldBounds;
        private readonly IReadOnlyList<SurfaceRegion> _floorRegions;
        private readonly IReadOnlyList<SurfaceRegion> _ceilingRegions;
        private readonly VoxelOccupancy _occupancy;
        private readonly SurfaceProjector _projector;
        private readonly AreaBuildSettings _settings;

        public AreaContainment(
            Bounds worldBounds,
            IReadOnlyList<SurfaceRegion> floorRegions,
            IReadOnlyList<SurfaceRegion> ceilingRegions,
            VoxelOccupancy occupancy,
            SurfaceProjector projector,
            AreaBuildSettings settings)
        {
            _worldBounds = worldBounds;
            _floorRegions = floorRegions;
            _ceilingRegions = ceilingRegions;
            _occupancy = occupancy;
            _projector = projector;
            _settings = settings;
        }

        public bool HasVolumeCells => _occupancy.HasVolumeCells;

        public bool ContainsFootprint(Bounds candidateBounds)
        {
            if (_settings.UsesAllMatchingSurfaceSearch)
                return true;

            if (!ContainsAreaFootprint(candidateBounds))
                return false;

            if (!_settings.UsesPhysicsSurfaceProjection)
                return true;

            return _projector.HasFloorSurfaceAt(candidateBounds.center) &&
                   _projector.HasFloorSurfaceAt(new Vector3(candidateBounds.min.x, candidateBounds.center.y, candidateBounds.min.z)) &&
                   _projector.HasFloorSurfaceAt(new Vector3(candidateBounds.min.x, candidateBounds.center.y, candidateBounds.max.z)) &&
                   _projector.HasFloorSurfaceAt(new Vector3(candidateBounds.max.x, candidateBounds.center.y, candidateBounds.min.z)) &&
                   _projector.HasFloorSurfaceAt(new Vector3(candidateBounds.max.x, candidateBounds.center.y, candidateBounds.max.z));
        }

        public bool ContainsPlacementFootprint(
            PlacementCandidate candidate,
            AssetDefinition asset,
            IGenerationProfiler profiler = null)
        {
            if (!asset)
                return true;

            if (asset.SurfaceFitMode == SurfaceFitMode.Adaptive &&
                candidate.PlacementType is PlacementType.Floor or PlacementType.Ceiling)
            {
                return candidate.HasSurfaceFit ||
                       ContainsAdaptivePlacementFootprint(candidate, asset, profiler);
            }

            Vector3 up = NormalizeOrFallback(candidate.Rotation * Vector3.up, candidate.SurfaceNormal, Vector3.up);
            Vector3 right = NormalizeOrFallback(candidate.Rotation * Vector3.right, default, Vector3.right);
            Vector3 forward = NormalizeOrFallback(candidate.Rotation * Vector3.forward, default, Vector3.forward);
            float width = Mathf.Max(0.01f, asset.Width);
            float depth = Mathf.Max(0.01f, asset.Depth);
            Vector3 bottomCenter = candidate.Position - up * (Mathf.Max(0.01f, asset.Height) * 0.5f);

            if (_settings.UsesAllMatchingSurfaceSearch &&
                candidate.PlacementType is PlacementType.Floor or PlacementType.Ceiling &&
                !IsFootprintInsideWorldBoundsXZ(bottomCenter, right, forward, width, depth))
            {
                return false;
            }

            int widthSegments = _occupancy.GetFootprintSegmentCount(width);
            int depthSegments = _occupancy.GetFootprintSegmentCount(depth);

            for (int x = 0; x <= widthSegments; x++)
            {
                float offsetX = Mathf.Lerp(-width * 0.5f, width * 0.5f, x / (float)widthSegments);

                for (int z = 0; z <= depthSegments; z++)
                {
                    float offsetZ = Mathf.Lerp(-depth * 0.5f, depth * 0.5f, z / (float)depthSegments);
                    Vector3 point = bottomCenter + right * offsetX + forward * offsetZ;

                    if (!ContainsFootprintPoint(point, candidate, profiler))
                        return false;
                }
            }

            return true;
        }

        private bool ContainsAdaptivePlacementFootprint(
            PlacementCandidate candidate,
            AssetDefinition asset,
            IGenerationProfiler profiler)
        {
            Vector3 up = NormalizeOrFallback(candidate.Rotation * Vector3.up, candidate.SurfaceNormal, Vector3.up);
            Vector3 surfaceCenter = candidate.Position - up * (Mathf.Max(0.01f, asset.Height) * 0.5f);

            return _projector.TryEvaluateSurfaceFit(
                surfaceCenter,
                candidate.Rotation,
                asset,
                candidate.SurfaceCollider,
                candidate.VoxelLayer,
                candidate.PlacementType,
                out _,
                profiler);
        }

        public bool ContainsVolume(OrientedBounds candidateBounds) =>
            _occupancy.ContainsVolume(candidateBounds);

        public bool ContainsVolumePoint(Vector3 position) =>
            _occupancy.ContainsVolumePoint(position);

        public bool TryGetRandomVolumePoint(GenerationRandom random, Bounds bounds, out Vector3 position) =>
            _occupancy.TryGetRandomVolumePoint(random, bounds, out position);

        private bool ContainsFootprintPoint(
            Vector3 position,
            PlacementCandidate candidate,
            IGenerationProfiler profiler)
        {
            if (!_settings.UsesAllMatchingSurfaceSearch &&
                !ContainsAreaPoint(position, candidate.PlacementType, candidate.VoxelLayer))
            {
                return false;
            }

            return !_settings.UsesPhysicsSurfaceProjection ||
                   _projector.HasSurfaceAt(
                       position,
                       candidate.PlacementType,
                       candidate.VoxelLayer,
                       candidate.SurfaceCollider,
                       profiler);
        }

        private bool ContainsAreaFootprint(Bounds candidateBounds)
        {
            if (_occupancy.HasGrid(PlacementType.Floor))
                return _occupancy.ContainsFloorFootprint(candidateBounds);

            foreach (SurfaceRegion region in _floorRegions)
            {
                if (region.ContainsBoundsXZ(candidateBounds))
                    return true;
            }

            return false;
        }

        private bool ContainsAreaPoint(Vector3 position, PlacementType placementType, int? voxelLayer)
        {
            if (_occupancy.HasGrid(placementType))
                return _occupancy.ContainsPoint(position, placementType, voxelLayer);

            IReadOnlyList<SurfaceRegion> regions = placementType == PlacementType.Ceiling
                ? _ceilingRegions
                : _floorRegions;

            foreach (SurfaceRegion region in regions)
            {
                if (voxelLayer.HasValue && region.VoxelLayer != voxelLayer)
                    continue;

                if (region.ContainsXZ(position))
                    return true;
            }

            return false;
        }

        private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 alternate, Vector3 fallback)
        {
            if (value.sqrMagnitude > 0.001f)
                return value.normalized;

            return alternate.sqrMagnitude > 0.001f ? alternate.normalized : fallback;
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
    }
}
