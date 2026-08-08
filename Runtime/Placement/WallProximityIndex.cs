using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>Provides opt-in horizontal distance checks against detected non-terrain walls.</summary>
    internal sealed class WallProximityIndex
    {
        private readonly List<WallReference> _references;

        public bool HasReferences => _references.Count > 0;

        private WallProximityIndex(List<WallReference> references)
        {
            _references = references;
        }

        public static WallProximityIndex Create(PlacementArea area)
        {
            List<WallReference> references = new();

            if (area == null)
                return new WallProximityIndex(references);

            foreach (SurfaceRegion region in area.WallRegions)
            {
                if (region == null || region.Kind != SurfaceKind.Wall)
                    continue;

                references.Add(WallReference.FromSegment(region.Name, region.WallStart, region.WallEnd));
            }

            if (references.Count > 0)
                return new WallProximityIndex(references);

            HashSet<Collider> seen = new();

            foreach (WallSurfaceSource source in area.WallSurfaceSources)
            {
                if (!source.Collider || source.IsTerrain || !seen.Add(source.Collider))
                    continue;

                references.Add(WallReference.FromBounds(source.Collider.name, source.Bounds));
            }

            return new WallProximityIndex(references);
        }

        public bool TryGetNearestGap(
            OrientedBounds bounds,
            out float gap,
            out string wallName)
        {
            gap = float.PositiveInfinity;
            wallName = string.Empty;

            for (int i = 0; i < _references.Count; i++)
            {
                WallReference reference = _references[i];
                Vector2 center = new(bounds.Center.x, bounds.Center.z);
                Vector2 nearest = reference.GetNearestPoint(center);
                Vector2 direction = nearest - center;
                float centerDistance = direction.magnitude;
                float candidateRadius = centerDistance > 0.0001f
                    ? GetHorizontalRadius(bounds, direction / centerDistance)
                    : 0f;
                float candidateGap = Mathf.Max(0f, centerDistance - candidateRadius);

                if (candidateGap >= gap)
                    continue;

                gap = candidateGap;
                wallName = reference.Name;
            }

            return !float.IsPositiveInfinity(gap);
        }

        private static float GetHorizontalRadius(OrientedBounds bounds, Vector2 direction)
        {
            Vector3 worldDirection = new(direction.x, 0f, direction.y);
            Vector3 extents = bounds.Extents;
            return Mathf.Abs(Vector3.Dot(bounds.Right, worldDirection)) * extents.x +
                   Mathf.Abs(Vector3.Dot(bounds.Up, worldDirection)) * extents.y +
                   Mathf.Abs(Vector3.Dot(bounds.Forward, worldDirection)) * extents.z;
        }

        private readonly struct WallReference
        {
            private readonly Vector2 _start;
            private readonly Vector2 _end;
            private readonly Rect _rect;
            private readonly bool _usesBounds;

            public string Name { get; }

            private WallReference(
                string name,
                Vector2 start,
                Vector2 end,
                Rect rect,
                bool usesBounds)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Wall" : name;
                _start = start;
                _end = end;
                _rect = rect;
                _usesBounds = usesBounds;
            }

            public static WallReference FromSegment(string name, Vector3 start, Vector3 end) =>
                new(name, new Vector2(start.x, start.z), new Vector2(end.x, end.z), default, false);

            public static WallReference FromBounds(string name, Bounds bounds) =>
                new(
                    name,
                    default,
                    default,
                    Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z),
                    true);

            public Vector2 GetNearestPoint(Vector2 point)
            {
                if (_usesBounds)
                {
                    return new Vector2(
                        Mathf.Clamp(point.x, _rect.xMin, _rect.xMax),
                        Mathf.Clamp(point.y, _rect.yMin, _rect.yMax));
                }

                Vector2 segment = _end - _start;
                float lengthSquared = segment.sqrMagnitude;

                if (lengthSquared <= 0.0001f)
                    return _start;

                float t = Mathf.Clamp01(Vector2.Dot(point - _start, segment) / lengthSquared);
                return _start + segment * t;
            }
        }
    }

    /// <summary>Evaluates optional asset-to-wall distance constraints.</summary>
    internal static class WallProximityRules
    {
        public static bool TryValidate(
            AssetDefinition asset,
            OrientedBounds bounds,
            GenerationContext context,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            rejectionReason = RejectionReason.None;
            relatedObjectName = string.Empty;

            if (!asset || asset.WallProximityMode == WallProximityMode.AnyDistance)
                return true;

            WallProximityIndex index = context.WallProximity;

            if (!index.HasReferences || !index.TryGetNearestGap(bounds, out float gap, out relatedObjectName))
            {
                rejectionReason = RejectionReason.MissingWallReference;
                return false;
            }

            if (asset.WallProximityMode == WallProximityMode.NearWall && gap > asset.WallDistance)
            {
                rejectionReason = RejectionReason.TooFarFromWall;
                return false;
            }

            if (asset.WallProximityMode == WallProximityMode.AwayFromWall && gap < asset.WallDistance)
            {
                rejectionReason = RejectionReason.TooCloseToWall;
                return false;
            }

            return true;
        }
    }
}
