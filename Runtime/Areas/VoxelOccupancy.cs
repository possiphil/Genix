using System.Collections.Generic;
using Genix.Assets;
using Genix.Placement;
using UnityEngine;

namespace Genix.Areas
{
    internal sealed class VoxelOccupancy
    {
        private const float CellEpsilon = 0.0001f;
        private const int MinFootprintSegments = 2;
        private const int MaxFootprintSegments = 4;

        private readonly HashSet<Vector3Int> _floorCells;
        private readonly HashSet<Vector3Int> _ceilingCells;
        private readonly HashSet<Vector3Int> _subspaceCells;
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
            float cellSize)
        {
            CellSize = cellSize;
            _floorCells = floorCells != null ? new HashSet<Vector3Int>(floorCells) : new HashSet<Vector3Int>();
            _ceilingCells = ceilingCells != null ? new HashSet<Vector3Int>(ceilingCells) : new HashSet<Vector3Int>();
            _subspaceCells = subspaceCells != null ? new HashSet<Vector3Int>(subspaceCells) : new HashSet<Vector3Int>();

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
                Mathf.FloorToInt(position.x / CellSize),
                Mathf.FloorToInt(position.z / CellSize));

            if (!voxelLayer.HasValue)
                return columns.Contains(key);

            return columnsByLayer.TryGetValue(voxelLayer.Value, out HashSet<Vector2Int> layerColumns) &&
                   layerColumns.Contains(key);
        }

        public bool ContainsFloorFootprint(Bounds bounds)
        {
            if (!HasGrid(PlacementType.Floor))
                return false;

            int minX = Mathf.FloorToInt((bounds.min.x + CellEpsilon) / CellSize);
            int maxX = Mathf.FloorToInt((bounds.max.x - CellEpsilon) / CellSize);
            int minZ = Mathf.FloorToInt((bounds.min.z + CellEpsilon) / CellSize);
            int maxZ = Mathf.FloorToInt((bounds.max.z - CellEpsilon) / CellSize);

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
                Mathf.FloorToInt(position.x / CellSize),
                Mathf.FloorToInt(position.y / CellSize),
                Mathf.FloorToInt(position.z / CellSize));
            return _subspaceCells.Contains(cell);
        }

        public bool ContainsVolume(OrientedBounds candidateBounds)
        {
            if (_subspaceCells.Count == 0 || CellSize <= 0f)
                return true;

            Bounds bounds = candidateBounds.ToAxisAlignedBounds();
            int minX = Mathf.FloorToInt((bounds.min.x + CellEpsilon) / CellSize);
            int maxX = Mathf.FloorToInt((bounds.max.x - CellEpsilon) / CellSize);
            int minY = Mathf.FloorToInt((bounds.min.y + CellEpsilon) / CellSize);
            int maxY = Mathf.FloorToInt((bounds.max.y - CellEpsilon) / CellSize);
            int minZ = Mathf.FloorToInt((bounds.min.z + CellEpsilon) / CellSize);
            int maxZ = Mathf.FloorToInt((bounds.max.z - CellEpsilon) / CellSize);

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
            Vector3 min = new(cell.x * CellSize, cell.y * CellSize, cell.z * CellSize);
            return new Bounds(min + Vector3.one * (CellSize * 0.5f), Vector3.one * CellSize);
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
