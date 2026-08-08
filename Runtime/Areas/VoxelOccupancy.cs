using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using UnityEngine;

namespace Genix.Areas
{
    /// <summary>
    /// Provides cell-based point, footprint, and oriented-volume containment for a placement area.
    /// </summary>
    /// <remarks>Dense and sparse masks are selected by <see cref="VoxelCellMask"/> according to grid density.</remarks>
    internal sealed class VoxelOccupancy
    {
        private const float CellEpsilon = 0.0001f;
        private const int MinFootprintSegments = 2;
        private const int MaxFootprintSegments = 4;
        private const int RandomVolumePointAttempts = 64;

        private readonly HashSet<Vector3Int> _floorCells;
        private readonly HashSet<Vector3Int> _ceilingCells;
        private readonly VoxelCellMask _subspaceCells;
        private readonly HashSet<Vector2Int> _floorColumns = new();
        private readonly HashSet<Vector2Int> _ceilingColumns = new();
        private readonly Dictionary<int, HashSet<Vector2Int>> _floorColumnsByLayer = new();
        private readonly Dictionary<int, HashSet<Vector2Int>> _ceilingColumnsByLayer = new();

        public float CellSize { get; }
        public bool HasSurfaceCells => _floorCells.Count > 0 || _ceilingCells.Count > 0;
        public bool HasVolumeCells => _subspaceCells.Count > 0 && CellSize > 0f;

        public VoxelOccupancy(
            IReadOnlyCollection<Vector3Int> floorCells,
            IReadOnlyCollection<Vector3Int> ceilingCells,
            IReadOnlyCollection<Vector3Int> subspaceCells,
            float cellSize,
            VoxelCellMask subspaceMask = null)
        {
            CellSize = cellSize;
            _floorCells = floorCells != null ? new HashSet<Vector3Int>(floorCells) : new HashSet<Vector3Int>();
            _ceilingCells = ceilingCells != null ? new HashSet<Vector3Int>(ceilingCells) : new HashSet<Vector3Int>();
            _subspaceCells = subspaceMask ?? new VoxelCellMask(subspaceCells);

            PopulateColumns(_floorCells, _floorColumns, _floorColumnsByLayer);
            PopulateColumns(_ceilingCells, _ceilingColumns, _ceilingColumnsByLayer);
        }

        public bool HasGrid(PlacementType placementType)
        {
            HashSet<Vector2Int> columns = placementType == PlacementType.Ceiling
                ? _ceilingColumns
                : _floorColumns;
            return CellSize > 0f && columns.Count > 0;
        }

        public bool ContainsPoint(Vector3 position, PlacementType placementType, int? voxelLayer)
        {
            HashSet<Vector2Int> columns = placementType == PlacementType.Ceiling
                ? _ceilingColumns
                : _floorColumns;
            Dictionary<int, HashSet<Vector2Int>> columnsByLayer = placementType == PlacementType.Ceiling
                ? _ceilingColumnsByLayer
                : _floorColumnsByLayer;

            if (columns.Count == 0 || CellSize <= 0f)
                return false;

            Vector2Int key = new(
                WorldToCell(position.x),
                WorldToCell(position.z));

            if (!voxelLayer.HasValue)
                return columns.Contains(key);

            return columnsByLayer.TryGetValue(voxelLayer.Value, out HashSet<Vector2Int> layerColumns) &&
                   layerColumns.Contains(key);
        }

        public bool ContainsFloorFootprint(Bounds bounds)
        {
            if (!HasGrid(PlacementType.Floor))
                return false;

            int minX = WorldMinToCell(bounds.min.x);
            int maxX = WorldMaxToCell(bounds.max.x);
            int minZ = WorldMinToCell(bounds.min.z);
            int maxZ = WorldMaxToCell(bounds.max.z);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (!_floorColumns.Contains(new Vector2Int(x, z)))
                        return false;
                }
            }

            return true;
        }

        public bool ContainsVolumePoint(Vector3 position)
        {
            if (!HasVolumeCells)
                return true;

            Vector3Int cell = new(
                WorldToCell(position.x),
                WorldToCell(position.y),
                WorldToCell(position.z));
            return _subspaceCells.Contains(cell);
        }

        public bool TryGetRandomVolumePoint(GenerationRandom random, Bounds bounds, out Vector3 position)
        {
            position = default;

            if (!HasVolumeCells || random == null || _subspaceCells.Count == 0)
                return false;

            for (int attempt = 0; attempt < RandomVolumePointAttempts; attempt++)
            {
                Vector3Int cell = _subspaceCells.Cells[random.Range(0, _subspaceCells.Count)];
                position = CreateRandomPointInCell(cell, random);

                if (bounds.Contains(position) && _subspaceCells.Contains(cell))
                    return true;
            }

            return false;
        }

        /// <summary>Samples an oriented box at occupancy-dependent intervals and requires every sample to be valid.</summary>
        public bool ContainsVolume(OrientedBounds candidateBounds)
        {
            if (_subspaceCells.Count == 0 || CellSize <= 0f)
                return true;

            Bounds bounds = candidateBounds.ToAxisAlignedBounds();
            int minX = WorldMinToCell(bounds.min.x);
            int maxX = WorldMaxToCell(bounds.max.x);
            int minY = WorldMinToCell(bounds.min.y);
            int maxY = WorldMaxToCell(bounds.max.y);
            int minZ = WorldMinToCell(bounds.min.z);
            int maxZ = WorldMaxToCell(bounds.max.z);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        Vector3Int cell = new(x, y, z);

                        if (candidateBounds.Intersects(CreateCellBounds(cell)) && !_subspaceCells.Contains(cell))
                            return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Returns enough footprint segments to avoid skipping a voxel cell along the supplied length.</summary>
        public int GetFootprintSegmentCount(float length)
        {
            float spacing = CellSize > 0f
                ? Mathf.Max(0.01f, CellSize)
                : Mathf.Max(0.25f, Mathf.Max(0.01f, length) / MaxFootprintSegments);

            return Mathf.Clamp(
                Mathf.CeilToInt(Mathf.Max(0.01f, length) / spacing),
                MinFootprintSegments,
                MaxFootprintSegments);
        }

        private Bounds CreateCellBounds(Vector3Int cell)
        {
            return new Bounds((Vector3)cell * CellSize, Vector3.one * CellSize);
        }

        private Vector3 CreateRandomPointInCell(Vector3Int cell, GenerationRandom random)
        {
            Vector3 center = (Vector3)cell * CellSize;
            Vector3 halfSize = Vector3.one * (CellSize * 0.5f);
            Vector3 min = center - halfSize;
            Vector3 max = center + halfSize;

            return new Vector3(
                random.Range(min.x, max.x),
                random.Range(min.y, max.y),
                random.Range(min.z, max.z));
        }

        private int WorldToCell(float value)
        {
            return Mathf.RoundToInt(value / CellSize);
        }

        private int WorldMinToCell(float value)
        {
            return Mathf.FloorToInt((value + CellSize * 0.5f + CellEpsilon) / CellSize);
        }

        private int WorldMaxToCell(float value)
        {
            return Mathf.FloorToInt((value + CellSize * 0.5f - CellEpsilon) / CellSize);
        }

        private static void PopulateColumns(
            IEnumerable<Vector3Int> cells,
            HashSet<Vector2Int> columns,
            Dictionary<int, HashSet<Vector2Int>> columnsByLayer)
        {
            foreach (Vector3Int cell in cells)
            {
                Vector2Int column = new(cell.x, cell.z);
                columns.Add(column);

                if (!columnsByLayer.TryGetValue(cell.y, out HashSet<Vector2Int> layerColumns))
                {
                    layerColumns = new HashSet<Vector2Int>();
                    columnsByLayer[cell.y] = layerColumns;
                }

                layerColumns.Add(column);
            }
        }
    }
}
