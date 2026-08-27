using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Extensions;
using Genix.Profiling;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>
    /// Memoizes adaptive surface-fit probes for equivalent candidate, asset, collider, layer, and target inputs.
    /// </summary>
    /// <remarks>The cache is owned by one generation context and never crosses run boundaries.</remarks>
    internal sealed class SurfaceFitCache
    {
        private const float PositionPrecision = 1000f;
        private const float RotationPrecision = 10000f;

        private readonly Dictionary<Key, Entry> _entries;

        public SurfaceFitCache(int capacity = 0)
        {
            int safeCapacity = Mathf.Max(0, capacity);
            _entries = safeCapacity > 0
                ? new Dictionary<Key, Entry>(safeCapacity)
                : new Dictionary<Key, Entry>();
        }

        /// <summary>Returns a cached fit or evaluates and stores one through the area's surface projector.</summary>
        public bool TryEvaluate(
            PlacementArea area,
            Vector3 surfaceCenter,
            Quaternion footprintRotation,
            AssetDefinition asset,
            Collider expectedSurfaceCollider,
            int? voxelLayer,
            PlacementType placementType,
            out SurfaceFitResult result,
            IGenerationProfiler profiler = null)
        {
            if (area == null || !asset)
            {
                result = default;
                return false;
            }

            Key key = new(
                asset.GetLocalObjectId(),
                expectedSurfaceCollider ? expectedSurfaceCollider.GetLocalObjectId() : string.Empty,
                voxelLayer ?? int.MinValue,
                placementType,
                surfaceCenter,
                footprintRotation);

            if (_entries.TryGetValue(key, out Entry cached))
            {
                result = cached.Result;
                return cached.IsValid;
            }

            bool isValid = area.TryEvaluateSurfaceFit(
                surfaceCenter,
                footprintRotation,
                asset,
                expectedSurfaceCollider,
                voxelLayer,
                placementType,
                out result,
                profiler);
            _entries[key] = new Entry(isValid, result);
            return isValid;
        }

        private readonly struct Entry
        {
            public bool IsValid { get; }
            public SurfaceFitResult Result { get; }

            public Entry(bool isValid, SurfaceFitResult result)
            {
                IsValid = isValid;
                Result = result;
            }
        }

        private readonly struct Key : IEquatable<Key>
        {
            private readonly string _assetId;
            private readonly string _colliderId;
            private readonly int _voxelLayer;
            private readonly PlacementType _placementType;
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;
            private readonly int _qx;
            private readonly int _qy;
            private readonly int _qz;
            private readonly int _qw;

            public Key(
                string assetId,
                string colliderId,
                int voxelLayer,
                PlacementType placementType,
                Vector3 position,
                Quaternion rotation)
            {
                _assetId = assetId ?? string.Empty;
                _colliderId = colliderId ?? string.Empty;
                _voxelLayer = voxelLayer;
                _placementType = placementType;
                _x = Quantize(position.x, PositionPrecision);
                _y = Quantize(position.y, PositionPrecision);
                _z = Quantize(position.z, PositionPrecision);
                _qx = Quantize(rotation.x, RotationPrecision);
                _qy = Quantize(rotation.y, RotationPrecision);
                _qz = Quantize(rotation.z, RotationPrecision);
                _qw = Quantize(rotation.w, RotationPrecision);
            }

            public bool Equals(Key other)
            {
                return _assetId == other._assetId &&
                       _colliderId == other._colliderId &&
                       _voxelLayer == other._voxelLayer &&
                       _placementType == other._placementType &&
                       _x == other._x &&
                       _y == other._y &&
                       _z == other._z &&
                       _qx == other._qx &&
                       _qy == other._qy &&
                       _qz == other._qz &&
                       _qw == other._qw;
            }

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _assetId.GetHashCode();
                    hash = (hash * 397) ^ _colliderId.GetHashCode();
                    hash = (hash * 397) ^ _voxelLayer;
                    hash = (hash * 397) ^ (int)_placementType;
                    hash = (hash * 397) ^ _x;
                    hash = (hash * 397) ^ _y;
                    hash = (hash * 397) ^ _z;
                    hash = (hash * 397) ^ _qx;
                    hash = (hash * 397) ^ _qy;
                    hash = (hash * 397) ^ _qz;
                    hash = (hash * 397) ^ _qw;
                    return hash;
                }
            }

            private static int Quantize(float value, float precision) =>
                Mathf.RoundToInt(value * precision);
        }
    }
}
