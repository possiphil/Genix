using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Diagnostics;
using Genix.Placement;
using UnityEngine;

namespace Genix.Areas
{
    public sealed class PlacementArea
    {
        private readonly AreaContainment _containment;
        private readonly SurfaceProjector _projector;
        private readonly AreaBuildSettings _settings;
        private readonly float _cellSize;

        public SpatialSourceInfo SourceInfo { get; }
        public Bounds WorldBounds { get; }
        public IReadOnlyList<SurfaceRegion> FloorRegions { get; }
        public IReadOnlyList<SurfaceRegion> WallRegions { get; }
        public IReadOnlyList<SurfaceRegion> CeilingRegions { get; }

        public string SurfaceSettingsCacheKey =>
            $"{_settings.usePlacementSurfaceCheck}:{_settings.placementSurfaceLayers.value}:" +
            $"{Mathf.RoundToInt(_settings.floorNormalYThreshold * 1000f)}:" +
            $"{Mathf.RoundToInt(_settings.ceilingNormalYThreshold * 1000f)}:" +
            $"{Mathf.RoundToInt(_cellSize * 1000f)}:" +
            $"{FloorRegions.Count}:{WallRegions.Count}:{CeilingRegions.Count}";

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
            Predicate<Collider> isSourceCollider = null)
        {
            SourceInfo = sourceInfo;
            WorldBounds = worldBounds;
            FloorRegions = floorRegions ?? Array.Empty<SurfaceRegion>();
            WallRegions = wallRegions ?? Array.Empty<SurfaceRegion>();
            CeilingRegions = ceilingRegions ?? Array.Empty<SurfaceRegion>();
            _settings = settings;
            _cellSize = cellSize;

            VoxelOccupancy occupancy = new(floorCells, ceilingCells, subspaceCells, cellSize);
            _projector = new SurfaceProjector(
                worldBounds,
                FloorRegions,
                CeilingRegions,
                occupancy,
                settings,
                isSourceCollider);
            _containment = new AreaContainment(
                FloorRegions,
                CeilingRegions,
                occupancy,
                _projector,
                settings);
        }

        public bool ContainsFootprint(Bounds candidateBounds) =>
            _containment.ContainsFootprint(candidateBounds);

        public bool ContainsPlacementFootprint(PlacementCandidate candidate, AssetDefinition asset) =>
            _containment.ContainsPlacementFootprint(candidate, asset);

        public bool ContainsPlacementVolume(OrientedBounds candidateBounds) =>
            _containment.ContainsVolume(candidateBounds);

        public bool ContainsVolumePoint(Vector3 position) =>
            _containment.ContainsVolumePoint(position);

        public bool TryProjectToFloor(Vector3 position, out SurfacePoint point) =>
            _projector.TryProjectToFloor(position, null, out point);

        public bool TryProjectToFloor(Vector3 position, SurfaceRegion targetRegion, out SurfacePoint point) =>
            _projector.TryProjectToFloor(position, targetRegion, out point);

        public bool TryProjectToCeiling(Vector3 position, out SurfacePoint point) =>
            _projector.TryProjectToCeiling(position, null, out point);

        public bool TryProjectToCeiling(Vector3 position, SurfaceRegion targetRegion, out SurfacePoint point) =>
            _projector.TryProjectToCeiling(position, targetRegion, out point);

        public bool TryProjectToWall(
            Vector3 position,
            Vector3 inwardNormal,
            int? targetVoxelLayer,
            out SurfacePoint point) =>
            _projector.TryProjectToWall(position, inwardNormal, targetVoxelLayer, out point);
    }
}
