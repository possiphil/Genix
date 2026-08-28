using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Placement.Providers;
using Genix.Profiling;
using Genix.Semantics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Editor.Generation
{
    internal static partial class RequiredRelationCandidateFactory
    {
        private static void CollectSurfaces(
            GenerationContext context,
            PlacementType placementType,
            Vector3 position,
            List<SurfacePoint> points,
            IGenerationProfiler profiler)
        {
            if (context.Area.UsesAllMatchingSurfaceSearch)
            {
                if (placementType == PlacementType.Ceiling)
                    context.Area.CollectCeilingSurfaces(position, points, profiler);
                else
                    context.Area.CollectFloorSurfaces(position, points, profiler);
                return;
            }

            bool projected = placementType == PlacementType.Ceiling
                ? context.Area.TryProjectToCeiling(position, out SurfacePoint point, profiler)
                : context.Area.TryProjectToFloor(position, out point, profiler);
            if (projected)
                points.Add(point);
        }

        private static int GetSampleCount(float length, float assetSize) =>
            Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0f, length) / assetSize) + 1, 3, MaximumAxisSamples);

        private static float Interpolate(float minimum, float maximum, int index, int count) =>
            count <= 1 ? (minimum + maximum) * 0.5f : Mathf.Lerp(minimum, maximum, index / (float)(count - 1));

        private static float DistanceToBounds(Vector3 position, Bounds bounds) =>
            Vector3.Distance(position, bounds.ClosestPoint(position));

        private static Vector3 HorizontalDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : fallback;
        }

        private static float ProjectedHorizontalExtent(Vector3 extents, Vector3 direction) =>
            Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.z) * extents.z;

        private static void AddPosition(
            ICollection<Vector3> positions,
            ISet<PositionIdentity> identities,
            Bounds bounds,
            Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            if (identities.Add(new PositionIdentity(position)))
                positions.Add(position);
        }

        private static void IntersectHorizontal(ref Bounds bounds, Bounds other)
        {
            float minX = Mathf.Max(bounds.min.x, other.min.x);
            float maxX = Mathf.Min(bounds.max.x, other.max.x);
            float minZ = Mathf.Max(bounds.min.z, other.min.z);
            float maxZ = Mathf.Min(bounds.max.z, other.max.z);

            if (minX > maxX || minZ > maxZ)
                return;

            bounds.SetMinMax(
                new Vector3(minX, bounds.min.y, minZ),
                new Vector3(maxX, bounds.max.y, maxZ));
        }

        private static void Intersect(ref Bounds bounds, Bounds other)
        {
            Vector3 minimum = Vector3.Max(bounds.min, other.min);
            Vector3 maximum = Vector3.Min(bounds.max, other.max);
            if (minimum.x > maximum.x || minimum.y > maximum.y || minimum.z > maximum.z)
                return;

            bounds.SetMinMax(minimum, maximum);
        }

        private static void InsetHorizontal(ref Bounds bounds, AssetDefinition asset)
        {
            float inset = Mathf.Max(asset.Width, asset.Depth) * 0.5f + 0.002f;
            if (bounds.size.x <= inset * 2f || bounds.size.z <= inset * 2f)
                return;

            bounds.SetMinMax(
                new Vector3(bounds.min.x + inset, bounds.min.y, bounds.min.z + inset),
                new Vector3(bounds.max.x - inset, bounds.max.y, bounds.max.z - inset));
        }

        private readonly struct SeedIdentity : System.IEquatable<SeedIdentity>
        {
            private readonly Collider _collider;
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;

            public SeedIdentity(SurfacePoint point)
            {
                _collider = point.SurfaceCollider;
                _x = Mathf.RoundToInt(point.Position.x * 10_000f);
                _y = Mathf.RoundToInt(point.Position.y * 10_000f);
                _z = Mathf.RoundToInt(point.Position.z * 10_000f);
            }

            public bool Equals(SeedIdentity other) =>
                _collider == other._collider && _x == other._x && _y == other._y && _z == other._z;

            public override bool Equals(object obj) => obj is SeedIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _collider ? _collider.GetHashCode() : 0;
                    hash = (hash * 397) ^ _x;
                    hash = (hash * 397) ^ _y;
                    return (hash * 397) ^ _z;
                }
            }
        }

        private readonly struct PositionIdentity : System.IEquatable<PositionIdentity>
        {
            private readonly int _x;
            private readonly int _z;

            public PositionIdentity(Vector3 position)
            {
                _x = Mathf.RoundToInt(position.x * PositionQuantization);
                _z = Mathf.RoundToInt(position.z * PositionQuantization);
            }

            public bool Equals(PositionIdentity other) => _x == other._x && _z == other._z;

            public override bool Equals(object obj) => obj is PositionIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_x * 397) ^ _z;
                }
            }
        }

        private readonly struct WallSeedIdentity : System.IEquatable<WallSeedIdentity>
        {
            private readonly Collider _collider;
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;

            public WallSeedIdentity(CandidateSeed seed)
            {
                _collider = seed.SurfaceCollider;
                _x = Mathf.RoundToInt(seed.Position.x * PositionQuantization);
                _y = Mathf.RoundToInt(seed.Position.y * PositionQuantization);
                _z = Mathf.RoundToInt(seed.Position.z * PositionQuantization);
            }

            public bool Equals(WallSeedIdentity other) =>
                _collider == other._collider && _x == other._x && _y == other._y && _z == other._z;

            public override bool Equals(object obj) => obj is WallSeedIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _collider ? _collider.GetHashCode() : 0;
                    hash = (hash * 397) ^ _x;
                    hash = (hash * 397) ^ _y;
                    return (hash * 397) ^ _z;
                }
            }
        }
    }
}

