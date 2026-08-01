using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Genix.Areas
{
    public sealed class VoxelCellMask
    {
        private const long MaxDenseVolume = 16_000_000L;
        private const long MaxDenseToSparseRatio = 16L;

        private readonly List<Vector3Int> _cells;
        private readonly HashSet<Vector3Int> _sparseCells;
        private readonly BitArray _denseCells;
        private readonly Vector3Int _min;
        private readonly Vector3Int _size;

        public IReadOnlyList<Vector3Int> Cells => _cells;
        public int Count => _cells.Count;
        public bool HasBounds { get; }
        public Vector3Int Min => _min;
        public Vector3Int Max { get; }

        public VoxelCellMask(IReadOnlyCollection<Vector3Int> cells)
        {
            _cells = cells != null ? new List<Vector3Int>(cells) : new List<Vector3Int>();

            if (_cells.Count == 0)
            {
                _sparseCells = new HashSet<Vector3Int>();
                return;
            }

            HasBounds = true;
            _min = _cells[0];
            Vector3Int max = _cells[0];

            for (int i = 1; i < _cells.Count; i++)
            {
                Vector3Int cell = _cells[i];
                _min = Vector3Int.Min(_min, cell);
                max = Vector3Int.Max(max, cell);
            }

            Max = max;
            _size = max - _min + Vector3Int.one;
            long volume = (long)_size.x * _size.y * _size.z;
            bool useDenseMask =
                volume > 0L &&
                volume <= MaxDenseVolume &&
                volume <= (long)_cells.Count * MaxDenseToSparseRatio;

            if (!useDenseMask)
            {
                _sparseCells = new HashSet<Vector3Int>(_cells);
                return;
            }

            _denseCells = new BitArray((int)volume);

            foreach (Vector3Int cell in _cells)
                _denseCells[GetDenseIndex(cell)] = true;
        }

        public bool Contains(Vector3Int cell)
        {
            if (_denseCells == null)
                return _sparseCells.Contains(cell);

            if (cell.x < _min.x || cell.x > Max.x ||
                cell.y < _min.y || cell.y > Max.y ||
                cell.z < _min.z || cell.z > Max.z)
            {
                return false;
            }

            return _denseCells[GetDenseIndex(cell)];
        }

        private int GetDenseIndex(Vector3Int cell)
        {
            int x = cell.x - _min.x;
            int y = cell.y - _min.y;
            int z = cell.z - _min.z;
            return (y * _size.z + z) * _size.x + x;
        }
    }
}
