using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genix.Core
{
    internal sealed class SpatialBoundsIndex
    {
        private const float DefaultCellSize = 4f;
        private const int MaxCellsPerObject = 2048;

        private readonly Dictionary<CellKey, List<int>> _cells;
        private readonly List<int> _globalIndices;
        private readonly List<int> _queryMarkers;
        private readonly float _cellSize;
        private int _queryId;

        public SpatialBoundsIndex(float cellSize = DefaultCellSize, int capacity = 0)
        {
            _cellSize = Mathf.Max(0.1f, cellSize);
            int safeCapacity = Mathf.Max(0, capacity);
            _cells = safeCapacity > 0
                ? new Dictionary<CellKey, List<int>>(safeCapacity)
                : new Dictionary<CellKey, List<int>>();
            _globalIndices = safeCapacity > 0
                ? new List<int>(safeCapacity)
                : new List<int>();
            _queryMarkers = safeCapacity > 0
                ? new List<int>(safeCapacity)
                : new List<int>();
        }

        public void Add(Bounds bounds, int objectIndex)
        {
            EnsureMarker(objectIndex);
            CellRange range = CellRange.FromBounds(bounds, _cellSize);

            if (range.CellCount > MaxCellsPerObject)
            {
                _globalIndices.Add(objectIndex);
                return;
            }

            for (int x = range.MinX; x <= range.MaxX; x++)
            {
                for (int y = range.MinY; y <= range.MaxY; y++)
                {
                    for (int z = range.MinZ; z <= range.MaxZ; z++)
                    {
                        CellKey key = new(x, y, z);

                        if (!_cells.TryGetValue(key, out List<int> objectIndices))
                        {
                            objectIndices = new List<int>();
                            _cells[key] = objectIndices;
                        }

                        objectIndices.Add(objectIndex);
                    }
                }
            }
        }

        public IEnumerable<int> Query(Bounds bounds)
        {
            if (_cells.Count == 0 && _globalIndices.Count == 0)
                yield break;

            CellRange range = CellRange.FromBounds(bounds, _cellSize);
            int queryId = NextQueryId();

            for (int x = range.MinX; x <= range.MaxX; x++)
            {
                for (int y = range.MinY; y <= range.MaxY; y++)
                {
                    for (int z = range.MinZ; z <= range.MaxZ; z++)
                    {
                        CellKey key = new(x, y, z);

                        if (!_cells.TryGetValue(key, out List<int> objectIndices))
                            continue;

                        foreach (int objectIndex in objectIndices)
                        {
                            if (_queryMarkers[objectIndex] == queryId)
                                continue;

                            _queryMarkers[objectIndex] = queryId;
                            yield return objectIndex;
                        }
                    }
                }
            }

            foreach (int objectIndex in _globalIndices)
            {
                if (_queryMarkers[objectIndex] == queryId)
                    continue;

                _queryMarkers[objectIndex] = queryId;
                yield return objectIndex;
            }
        }

        public void Clear()
        {
            _cells.Clear();
            _globalIndices.Clear();
            _queryMarkers.Clear();
            _queryId = 0;
        }

        private void EnsureMarker(int objectIndex)
        {
            int requiredCount = objectIndex + 1;

            if (_queryMarkers.Count >= requiredCount)
                return;

            if (_queryMarkers.Capacity < requiredCount)
                _queryMarkers.Capacity = requiredCount;

            while (_queryMarkers.Count < requiredCount)
                _queryMarkers.Add(0);
        }

        private int NextQueryId()
        {
            if (_queryId < int.MaxValue)
                return ++_queryId;

            for (int i = 0; i < _queryMarkers.Count; i++)
                _queryMarkers[i] = 0;

            _queryId = 1;
            return _queryId;
        }

        private static int ToCell(float coordinate, float cellSize) =>
            Mathf.FloorToInt(coordinate / cellSize);

        private readonly struct CellKey : IEquatable<CellKey>
        {
            public int X { get; }
            public int Y { get; }
            public int Z { get; }

            public CellKey(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public bool Equals(CellKey other) => X == other.X && Y == other.Y && Z == other.Z;
            public override bool Equals(object obj) => obj is CellKey other && Equals(other);
            public override int GetHashCode() => ((X * 397) ^ Y) * 397 ^ Z;
        }

        private readonly struct CellRange
        {
            public int MinX { get; }
            public int MaxX { get; }
            public int MinY { get; }
            public int MaxY { get; }
            public int MinZ { get; }
            public int MaxZ { get; }
            public long CellCount => (long)(MaxX - MinX + 1) *
                                     (MaxY - MinY + 1) *
                                     (MaxZ - MinZ + 1);

            private CellRange(int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
            {
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
                MinZ = minZ;
                MaxZ = maxZ;
            }

            public static CellRange FromBounds(Bounds bounds, float cellSize)
            {
                return new CellRange(
                    ToCell(bounds.min.x, cellSize),
                    ToCell(bounds.max.x, cellSize),
                    ToCell(bounds.min.y, cellSize),
                    ToCell(bounds.max.y, cellSize),
                    ToCell(bounds.min.z, cellSize),
                    ToCell(bounds.max.z, cellSize));
            }
        }
    }
}
