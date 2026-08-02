using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Profiling;
using UnityEngine;

namespace Genix.Areas
{
    /// <summary>Provides the containment, surface, occupancy, and projection data used to place objects in one area.</summary>
    public sealed class PlacementArea
    {
        private readonly AreaContainment _containment;
        private readonly SurfaceProjector _projector;
        private readonly AreaBuildSettings _settings;
        private readonly float _cellSize;

        /// <summary>Gets source info.</summary>
        public SpatialSourceInfo SourceInfo { get; }
        /// <summary>Gets world bounds.</summary>
        public Bounds WorldBounds { get; }
        /// <summary>Gets floor regions.</summary>
        public IReadOnlyList<SurfaceRegion> FloorRegions { get; }
        /// <summary>Gets wall regions.</summary>
        public IReadOnlyList<SurfaceRegion> WallRegions { get; }
        /// <summary>Gets ceiling regions.</summary>
        public IReadOnlyList<SurfaceRegion> CeilingRegions { get; }
        /// <summary>Gets floor cells.</summary>
        public IReadOnlyCollection<Vector3Int> FloorCells { get; }
        /// <summary>Gets ceiling cells.</summary>
        public IReadOnlyCollection<Vector3Int> CeilingCells { get; }
        /// <summary>Indicates whether volume cells.</summary>
        public bool HasVolumeCells => _containment.HasVolumeCells;
        /// <summary>Gets surface discovery mode.</summary>
        public SurfaceDiscoveryMode SurfaceDiscoveryMode => _settings.EffectiveSurfaceDiscoveryMode;
        /// <summary>Indicates whether candidate projection may search all matching surfaces inside the volume.</summary>
        public bool UsesAllMatchingSurfaceSearch => _settings.UsesAllMatchingSurfaceSearch;

        /// <summary>Determines whether the area contains data for the requested placement type.</summary>
        public bool SupportsPlacementType(PlacementType placementType) =>
            placementType switch
            {
                PlacementType.Floor => HasHorizontalSurfaceSupport(PlacementType.Floor, FloorRegions.Count),
                PlacementType.Wall => HasSurfaceSupport(PlacementType.Wall, WallRegions.Count),
                PlacementType.Ceiling => HasHorizontalSurfaceSupport(PlacementType.Ceiling, CeilingRegions.Count),
                PlacementType.InsideSpace => HasVolumeCells || HasUsableVolumeBounds(WorldBounds),
                _ => false
            };

        private static bool HasUsableVolumeBounds(Bounds bounds) =>
            bounds.size.x > 0f &&
            bounds.size.y > 0f &&
            bounds.size.z > 0f;

        private bool HasHorizontalSurfaceSupport(PlacementType placementType, int regionCount)
        {
            if (_settings.UsesAllMatchingSurfaceSearch)
                return HasUsableVolumeBounds(WorldBounds) &&
                       _settings.GetSurfaceLayers(placementType).value != 0;

            return HasSurfaceSupport(placementType, regionCount);
        }

        private bool HasSurfaceSupport(PlacementType placementType, int regionCount)
        {
            if (regionCount == 0)
                return false;

            if (!_settings.UsesPhysicsSurfaceProjection)
                return true;

            return _settings.GetSurfaceLayers(placementType).value != 0;
        }

        /// <summary>Gets surface settings cache key.</summary>
        public string SurfaceSettingsCacheKey =>
            $"{_settings.EffectiveSurfaceDiscoveryMode}:{_settings.placementSurfaceLayers.value}:" +
            $"{_settings.floorSurfaceLayers.value}:{_settings.wallSurfaceLayers.value}:{_settings.ceilingSurfaceLayers.value}:" +
            $"{Mathf.RoundToInt(_settings.floorNormalYThreshold * 1000f)}:" +
            $"{Mathf.RoundToInt(_settings.ceilingNormalYThreshold * 1000f)}:" +
            $"{Mathf.RoundToInt(_cellSize * 1000f)}:" +
            $"{FloorRegions.Count}:{WallRegions.Count}:{CeilingRegions.Count}";

        /// <summary>Initializes a new instance of placement area.</summary>
        public PlacementArea(
            SpatialSourceInfo sourceInfo,
            Bounds worldBounds,
            IReadOnlyList<SurfaceRegion> floorRegions,
            IReadOnlyList<SurfaceRegion> wallRegions,
            IReadOnlyCollection<Vector3Int> floorCells = null,
            float cellSize = 0f,
            AreaBuildSettings settings = default,
            IReadOnlyCollection<Vector3Int> subspaceCells = null,
            IReadOnlyList<SurfaceRegion> ceilingRegions = null,
            IReadOnlyCollection<Vector3Int> ceilingCells = null,
            Predicate<Collider> isSourceCollider = null,
            VoxelCellMask subspaceMask = null)
        {
            SourceInfo = sourceInfo;
            WorldBounds = worldBounds;
            FloorRegions = floorRegions ?? Array.Empty<SurfaceRegion>();
            WallRegions = wallRegions ?? Array.Empty<SurfaceRegion>();
            CeilingRegions = ceilingRegions ?? Array.Empty<SurfaceRegion>();
            FloorCells = floorCells != null ? new List<Vector3Int>(floorCells) : Array.Empty<Vector3Int>();
            CeilingCells = ceilingCells != null ? new List<Vector3Int>(ceilingCells) : Array.Empty<Vector3Int>();
            _settings = settings;
            _cellSize = cellSize;

            VoxelOccupancy occupancy = new(FloorCells, CeilingCells, subspaceCells, cellSize, subspaceMask);
            _projector = new SurfaceProjector(
                worldBounds,
                FloorRegions,
                CeilingRegions,
                occupancy,
                settings,
                isSourceCollider);
            _containment = new AreaContainment(
                worldBounds,
                FloorRegions,
                CeilingRegions,
                occupancy,
                _projector,
                settings);
        }

        /// <summary>Determines whether the supplied footprint fits inside the area.</summary>
        public bool ContainsFootprint(Bounds candidateBounds) =>
            _containment.ContainsFootprint(candidateBounds);

        /// <summary>Determines whether the complete oriented footprint fits inside the area.</summary>
        public bool ContainsPlacementFootprint(
            PlacementCandidate candidate,
            AssetDefinition asset,
            IGenerationProfiler profiler = null) =>
            _containment.ContainsPlacementFootprint(candidate, asset, profiler);

        /// <summary>Determines whether the complete oriented placement bounds fit inside the volume.</summary>
        public bool ContainsPlacementVolume(OrientedBounds candidateBounds) =>
            _containment.ContainsVolume(candidateBounds);

        /// <summary>Determines whether a world position lies inside the placement volume.</summary>
        public bool ContainsVolumePoint(Vector3 position) =>
            _containment.ContainsVolumePoint(position);

        /// <summary>Attempts to get random volume point.</summary>
        public bool TryGetRandomVolumePoint(GenerationRandom random, out Vector3 position) =>
            _containment.TryGetRandomVolumePoint(random, WorldBounds, out position);

        /// <summary>Attempts to project to floor.</summary>
        public bool TryProjectToFloor(
            Vector3 position,
            out SurfacePoint point,
            IGenerationProfiler profiler = null) =>
            _projector.TryProjectToFloor(position, null, out point, profiler);

        /// <summary>Attempts to project to floor.</summary>
        public bool TryProjectToFloor(
            Vector3 position,
            SurfaceRegion targetRegion,
            out SurfacePoint point,
            IGenerationProfiler profiler = null) =>
            _projector.TryProjectToFloor(position, targetRegion, out point, profiler);

        /// <summary>Attempts to project to ceiling.</summary>
        public bool TryProjectToCeiling(
            Vector3 position,
            out SurfacePoint point,
            IGenerationProfiler profiler = null) =>
            _projector.TryProjectToCeiling(position, null, out point, profiler);

        /// <summary>Attempts to project to ceiling.</summary>
        public bool TryProjectToCeiling(
            Vector3 position,
            SurfaceRegion targetRegion,
            out SurfacePoint point,
            IGenerationProfiler profiler = null) =>
            _projector.TryProjectToCeiling(position, targetRegion, out point, profiler);

        /// <summary>Appends all floor regions to the supplied collection.</summary>
        public int CollectFloorSurfaces(
            Vector3 position,
            List<SurfacePoint> points,
            IGenerationProfiler profiler = null) =>
            _projector.CollectFloorSurfaces(position, points, profiler);

        /// <summary>Appends all ceiling regions to the supplied collection.</summary>
        public int CollectCeilingSurfaces(
            Vector3 position,
            List<SurfacePoint> points,
            IGenerationProfiler profiler = null) =>
            _projector.CollectCeilingSurfaces(position, points, profiler);

        /// <summary>Attempts to project to wall.</summary>
        public bool TryProjectToWall(
            Vector3 position,
            Vector3 inwardNormal,
            int? targetVoxelLayer,
            out SurfacePoint point,
            IGenerationProfiler profiler = null) =>
            _projector.TryProjectToWall(position, inwardNormal, targetVoxelLayer, out point, profiler);

        /// <summary>Attempts to evaluate surface fit.</summary>
        public bool TryEvaluateSurfaceFit(
            Vector3 surfaceCenter,
            Quaternion footprintRotation,
            AssetDefinition asset,
            Collider expectedSurfaceCollider,
            int? voxelLayer,
            PlacementType placementType,
            out SurfaceFitResult result,
            IGenerationProfiler profiler = null) =>
            _projector.TryEvaluateSurfaceFit(
                surfaceCenter,
                footprintRotation,
                asset,
                expectedSurfaceCollider,
                voxelLayer,
                placementType,
                out result,
                profiler);
    }
}
