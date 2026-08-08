using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Placement;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Core
{
    /// <summary>Mutable collection of accepted placements and the spatial indices used while constructing it.</summary>
    public sealed class GenerationPlan
    {
        private readonly List<PlannedObject> _objects;
        private readonly SpatialBoundsIndex _spatialIndex;
        private readonly SpatialPointIndex2D _horizontalSpacingIndex;
        private readonly Dictionary<PlacementSurfaceDescriptor, int> _supportCounts = new();
        private readonly Dictionary<AssetDefinition, int> _assetCounts = new();

        /// <summary>Gets objects.</summary>
        public IReadOnlyList<PlannedObject> Objects => _objects;
        /// <summary>Gets the number of stored items.</summary>
        public int Count => _objects.Count;

        /// <summary>Initializes a new instance of generation plan.</summary>
        public GenerationPlan(int capacity = 0)
        {
            int safeCapacity = Mathf.Max(0, capacity);
            _objects = safeCapacity > 0
                ? new List<PlannedObject>(safeCapacity)
                : new List<PlannedObject>();
            _spatialIndex = new SpatialBoundsIndex(capacity: safeCapacity);
            _horizontalSpacingIndex = new SpatialPointIndex2D(capacity: safeCapacity);
        }

        /// <summary>Adds an accepted placement and updates overlap and horizontal-spacing indices.</summary>
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
            _assetCounts[asset] = GetAssetCount(asset) + 1;

            PlacementSurfaceDescriptor support = PlacementSupportRules.GetDescriptor(candidate.SurfaceCollider);

            if (support)
                _supportCounts[support] = GetSupportCount(support) + 1;
        }

        /// <summary>Returns how many objects in this plan use the supplied semantic support surface.</summary>
        public int GetSupportCount(PlacementSurfaceDescriptor supportSurface)
        {
            return supportSurface && _supportCounts.TryGetValue(supportSurface, out int count) ? count : 0;
        }

        /// <summary>Returns how many instances of the supplied asset have been accepted in this plan.</summary>
        public int GetAssetCount(AssetDefinition asset)
        {
            return asset && _assetCounts.TryGetValue(asset, out int count) ? count : 0;
        }

        /// <summary>Enumerates planned objects whose indexed bounds may intersect the supplied bounds.</summary>
        public IEnumerable<PlannedObject> Query(Bounds axisAlignedBounds)
        {
            foreach (int index in _spatialIndex.Query(axisAlignedBounds))
                yield return _objects[index];
        }

        /// <summary>Enumerates planned objects within index cells that overlap a horizontal search radius.</summary>
        public IEnumerable<PlannedObject> QueryHorizontalSpacing(Bounds candidateBounds, float radius)
        {
            foreach (int index in _horizontalSpacingIndex.Query(candidateBounds.center, radius))
                yield return _objects[index];
        }

        /// <summary>Enumerates planned objects whose bounds may lie within a three-dimensional search radius.</summary>
        public IEnumerable<PlannedObject> QuerySpatialSpacing(Bounds candidateBounds, float radius)
        {
            Bounds queryBounds = candidateBounds;
            queryBounds.Expand(Mathf.Max(0f, radius) * 2f);

            foreach (int index in _spatialIndex.Query(queryBounds))
                yield return _objects[index];
        }

        /// <summary>Clears the stored state.</summary>
        public void Clear()
        {
            _objects.Clear();
            _spatialIndex.Clear();
            _horizontalSpacingIndex.Clear();
            _supportCounts.Clear();
            _assetCounts.Clear();
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

    /// <summary>Immutable accepted asset placement retained by a generation plan.</summary>
    public readonly struct PlannedObject
    {
        /// <summary>Gets asset.</summary>
        public AssetDefinition Asset { get; }
        /// <summary>Gets candidate.</summary>
        public PlacementCandidate Candidate { get; }
        /// <summary>Gets object name.</summary>
        public string ObjectName { get; }
        /// <summary>Gets bounds.</summary>
        public OrientedBounds Bounds { get; }

        /// <summary>Initializes a new instance of planned object.</summary>
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
