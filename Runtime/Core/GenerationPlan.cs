using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Placement;
using UnityEngine;

namespace Genix.Core
{
    public sealed class GenerationPlan
    {
        private readonly List<PlannedObject> _objects;
        private readonly SpatialBoundsIndex _spatialIndex;
        private readonly SpatialPointIndex2D _horizontalSpacingIndex;

        public IReadOnlyList<PlannedObject> Objects => _objects;
        public int Count => _objects.Count;

        public GenerationPlan(int capacity = 0)
        {
            int safeCapacity = Mathf.Max(0, capacity);
            _objects = safeCapacity > 0
                ? new List<PlannedObject>(safeCapacity)
                : new List<PlannedObject>();
            _spatialIndex = new SpatialBoundsIndex(capacity: safeCapacity);
            _horizontalSpacingIndex = new SpatialPointIndex2D(capacity: safeCapacity);
        }

        public void Add(
            AssetDefinition asset,
            PlacementCandidate candidate,
            string objectName)
        {
            PlannedObject plannedObject = new(
                asset,
                candidate,
                objectName,
                CandidateFactory.GetBounds(candidate, asset));
            Bounds axisAlignedBounds = plannedObject.Bounds.ToAxisAlignedBounds();

            _objects.Add(plannedObject);
            int objectIndex = _objects.Count - 1;
            _spatialIndex.Add(axisAlignedBounds, objectIndex);
            _horizontalSpacingIndex.Add(axisAlignedBounds.center, objectIndex);
        }

        public IEnumerable<PlannedObject> Query(Bounds axisAlignedBounds)
        {
            foreach (int index in _spatialIndex.Query(axisAlignedBounds))
                yield return _objects[index];
        }

        public IEnumerable<PlannedObject> QueryHorizontalSpacing(Bounds candidateBounds, float radius)
        {
            foreach (int index in _horizontalSpacingIndex.Query(candidateBounds.center, radius))
                yield return _objects[index];
        }

        public void Clear()
        {
            _objects.Clear();
            _spatialIndex.Clear();
            _horizontalSpacingIndex.Clear();
        }
    }

    internal sealed class SpatialPointIndex2D
    {
        private const float DefaultCellSize = 4f;

        private readonly Dictionary<CellKey, List<int>> _cells;
        private readonly List<int> _queryMarkers;
        private readonly float _cellSize;
        private int _queryId;

        public SpatialPointIndex2D(float cellSize = DefaultCellSize, int capacity = 0)
        {
            _cellSize = Mathf.Max(0.1f, cellSize);
            int safeCapacity = Mathf.Max(0, capacity);
            _cells = safeCapacity > 0
                ? new Dictionary<CellKey, List<int>>(safeCapacity)
                : new Dictionary<CellKey, List<int>>();
            _queryMarkers = safeCapacity > 0
                ? new List<int>(safeCapacity)
                : new List<int>();
        }

        public void Add(Vector3 point, int objectIndex)
        {
            EnsureMarker(objectIndex);
            CellKey key = new(ToCell(point.x), ToCell(point.z));

            if (!_cells.TryGetValue(key, out List<int> objectIndices))
            {
                objectIndices = new List<int>();
                _cells[key] = objectIndices;
            }

            objectIndices.Add(objectIndex);
        }

        public IEnumerable<int> Query(Vector3 center, float radius)
        {
            if (_cells.Count == 0 || radius <= 0f)
                yield break;

            int minX = ToCell(center.x - radius);
            int maxX = ToCell(center.x + radius);
            int minZ = ToCell(center.z - radius);
            int maxZ = ToCell(center.z + radius);
            int queryId = NextQueryId();

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    CellKey key = new(x, z);

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

        public void Clear()
        {
            _cells.Clear();
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

        private int ToCell(float coordinate) =>
            Mathf.FloorToInt(coordinate / _cellSize);

        private readonly struct CellKey : IEquatable<CellKey>
        {
            public int X { get; }
            public int Z { get; }

            public CellKey(int x, int z)
            {
                X = x;
                Z = z;
            }

            public bool Equals(CellKey other) => X == other.X && Z == other.Z;
            public override bool Equals(object obj) => obj is CellKey other && Equals(other);
            public override int GetHashCode() => (X * 397) ^ Z;
        }
    }

    public readonly struct PlannedObject
    {
        public AssetDefinition Asset { get; }
        public PlacementCandidate Candidate { get; }
        public string ObjectName { get; }
        public OrientedBounds Bounds { get; }

        public PlannedObject(
            AssetDefinition asset,
            PlacementCandidate candidate,
            string objectName,
            OrientedBounds bounds)
        {
            Asset = asset;
            Candidate = candidate;
            ObjectName = objectName;
            Bounds = bounds;
        }
    }
}
