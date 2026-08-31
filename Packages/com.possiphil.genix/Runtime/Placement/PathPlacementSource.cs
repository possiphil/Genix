using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Authoring;
using Genix.Core;
using Genix.Profiling;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>
    /// Describes one reusable semantic path as an ordered polyline for distance, side, facing,
    /// and regular station constraints without requiring one scene anchor per placement.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PathPlacementSource : MonoBehaviour
    {
        private const float MinimumSegmentLength = 0.01f;

        [SerializeField] private List<SemanticTag> pathTags = new();
        [SerializeField] private List<Vector3> localPoints = new();

        /// <summary>Gets asset-compatible semantic tags exposed by this path.</summary>
        public IReadOnlyList<SemanticTag> PathTags => pathTags;
        /// <summary>Gets the number of authored polyline points.</summary>
        public int PointCount => localPoints?.Count ?? 0;
        /// <summary>Indicates whether this source contains at least one usable segment.</summary>
        public bool IsConfigured => isActiveAndEnabled && PointCount >= 2 && pathTags.Any(IsAssetTag);

        /// <summary>Replaces semantic path tags with valid, duplicate-free asset-compatible tags.</summary>
        public void SetPathTags(IEnumerable<SemanticTag> tags) =>
            pathTags = NormalizeTags(tags);

        /// <summary>Stores an ordered world-space polyline relative to this component's transform.</summary>
        public void SetWorldPoints(IEnumerable<Vector3> points)
        {
            localPoints = new List<Vector3>();
            if (points == null)
                return;

            foreach (Vector3 worldPoint in points)
            {
                Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                if (localPoints.Count == 0 ||
                    Vector3.Distance(localPoints[localPoints.Count - 1], localPoint) >= MinimumSegmentLength)
                {
                    localPoints.Add(localPoint);
                }
            }
        }

        /// <summary>Returns the nearest horizontal path frame for a world-space position.</summary>
        internal bool TryGetNearestFrame(Vector3 worldPosition, out PathPlacementFrame frame)
        {
            frame = default;
            if (localPoints == null || localPoints.Count < 2)
                return false;

            float bestSqrDistance = float.PositiveInfinity;
            float cumulativeLength = 0f;
            float bestDistanceFromStart = 0f;
            Vector3 bestCenter = default;
            Vector3 bestForward = default;
            int bestSegmentIndex = -1;
            float bestSegmentFactor = 0f;
            for (int i = 0; i < localPoints.Count - 1; i++)
            {
                Vector3 start = transform.TransformPoint(localPoints[i]);
                Vector3 end = transform.TransformPoint(localPoints[i + 1]);
                Vector3 segment = Vector3.ProjectOnPlane(end - start, Vector3.up);
                float sqrLength = segment.sqrMagnitude;
                if (sqrLength < MinimumSegmentLength * MinimumSegmentLength)
                    continue;

                float segmentLength = Mathf.Sqrt(sqrLength);
                Vector3 offset = Vector3.ProjectOnPlane(worldPosition - start, Vector3.up);
                float factor = Mathf.Clamp01(Vector3.Dot(offset, segment) / sqrLength);
                Vector3 center = Vector3.Lerp(start, end, factor);
                float sqrDistance = Vector3.ProjectOnPlane(worldPosition - center, Vector3.up).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestCenter = center;
                    bestForward = segment / segmentLength;
                    bestDistanceFromStart = cumulativeLength + factor * segmentLength;
                    bestSegmentIndex = i;
                    bestSegmentFactor = factor;
                }

                cumulativeLength += segmentLength;
            }

            if (bestSqrDistance >= float.PositiveInfinity)
                return false;

            bestForward = GetSmoothedHorizontalForward(
                bestSegmentIndex,
                bestSegmentFactor,
                bestForward);
            frame = new PathPlacementFrame(
                bestCenter,
                bestForward,
                Vector3.Cross(Vector3.up, bestForward).normalized,
                bestDistanceFromStart,
                Mathf.Max(0f, cumulativeLength - bestDistanceFromStart));
            return true;
        }

        private Vector3 GetSmoothedHorizontalForward(
            int segmentIndex,
            float segmentFactor,
            Vector3 segmentForward)
        {
            Vector3 startForward = segmentForward;
            if (TryGetHorizontalSegmentForward(segmentIndex - 1, out Vector3 previousForward))
                startForward = BlendDirections(previousForward, segmentForward);

            Vector3 endForward = segmentForward;
            if (TryGetHorizontalSegmentForward(segmentIndex + 1, out Vector3 nextForward))
                endForward = BlendDirections(segmentForward, nextForward);

            Vector3 smoothed = Vector3.Lerp(startForward, endForward, segmentFactor);
            return smoothed.sqrMagnitude > 0.001f ? smoothed.normalized : segmentForward;
        }

        private bool TryGetHorizontalSegmentForward(int segmentIndex, out Vector3 forward)
        {
            forward = default;
            if (segmentIndex < 0 || segmentIndex >= localPoints.Count - 1)
                return false;

            Vector3 start = transform.TransformPoint(localPoints[segmentIndex]);
            Vector3 end = transform.TransformPoint(localPoints[segmentIndex + 1]);
            Vector3 segment = Vector3.ProjectOnPlane(end - start, Vector3.up);
            if (segment.sqrMagnitude < MinimumSegmentLength * MinimumSegmentLength)
                return false;

            forward = segment.normalized;
            return true;
        }

        private static Vector3 BlendDirections(Vector3 first, Vector3 second)
        {
            Vector3 blended = first + second;
            return blended.sqrMagnitude > 0.001f ? blended.normalized : second;
        }

        internal bool Matches(SemanticTag tag) => tag && pathTags.Contains(tag);

        internal IEnumerable<PathPlacementFrame> EnumerateStations(float spacing, float endpointMargin)
        {
            if (localPoints == null || localPoints.Count < 2)
                yield break;

            List<Vector3> points = localPoints.Select(transform.TransformPoint).ToList();
            float totalLength = 0f;
            float[] cumulative = new float[points.Count];
            for (int i = 1; i < points.Count; i++)
            {
                totalLength += Vector3.Distance(
                    Vector3.ProjectOnPlane(points[i - 1], Vector3.up),
                    Vector3.ProjectOnPlane(points[i], Vector3.up));
                cumulative[i] = totalLength;
            }

            float margin = Mathf.Min(Mathf.Max(0f, endpointMargin), totalLength * 0.49f);
            float step = Mathf.Max(0.1f, spacing);
            for (float distance = margin; distance <= totalLength - margin + 0.001f; distance += step)
            {
                int segmentIndex = 0;
                while (segmentIndex < cumulative.Length - 2 && cumulative[segmentIndex + 1] < distance)
                    segmentIndex++;

                float segmentStart = cumulative[segmentIndex];
                float segmentLength = cumulative[segmentIndex + 1] - segmentStart;
                if (segmentLength <= MinimumSegmentLength)
                    continue;

                float factor = Mathf.Clamp01((distance - segmentStart) / segmentLength);
                Vector3 start = points[segmentIndex];
                Vector3 end = points[segmentIndex + 1];
                Vector3 forward = Vector3.ProjectOnPlane(end - start, Vector3.up).normalized;
                if (forward.sqrMagnitude <= 0.001f)
                    continue;

                yield return new PathPlacementFrame(
                    Vector3.Lerp(start, end, factor),
                    forward,
                    Vector3.Cross(Vector3.up, forward).normalized);
            }
        }

        internal static IReadOnlyList<PathPlacementSource> Collect(Bounds targetBounds)
        {
            List<PathPlacementSource> sources = new();
            foreach (PathPlacementSource source in FindObjectsByType<PathPlacementSource>())
            {
                if (!source || !source.IsConfigured || !source.TryGetBounds(out Bounds bounds) ||
                    !bounds.Intersects(targetBounds))
                    continue;

                sources.Add(source);
            }

            return sources;
        }

        internal static bool TryFindNearest(
            GenerationContext context,
            PathPlacementRule rule,
            Vector3 position,
            out PathPlacementFrame frame)
        {
            frame = default;
            if (context == null || rule?.IsConfigured != true)
                return false;

            float best = float.PositiveInfinity;
            foreach (PathPlacementSource source in context.PathPlacementSources)
            {
                if (!source || !source.Matches(rule.PathTag) ||
                    !source.TryGetNearestFrame(position, out PathPlacementFrame candidate))
                    continue;

                float distance = candidate.HorizontalDistanceTo(position);
                if (distance >= best)
                    continue;

                best = distance;
                frame = candidate;
            }

            return best < float.PositiveInfinity;
        }

        internal static bool TryValidate(
            GenerationContext context,
            AssetDefinition asset,
            Vector3 position,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            rejectionReason = RejectionReason.None;
            relatedObjectName = string.Empty;
            PathPlacementRule rule = asset ? asset.PathPlacement : null;
            if (rule?.IsConfigured != true)
                return true;

            if (!TryFindNearest(context, rule, position, out PathPlacementFrame frame))
            {
                relatedObjectName = rule.PathTag ? rule.PathTag.DisplayName : "Path";
                rejectionReason = RejectionReason.MissingPathReference;
                return false;
            }

            float distance = frame.HorizontalDistanceTo(position);
            if (distance < rule.MinimumDistance || distance > rule.MaximumDistance)
            {
                relatedObjectName = rule.PathTag ? rule.PathTag.DisplayName : "Path";
                rejectionReason = RejectionReason.OutsidePathDistance;
                return false;
            }

            if (frame.IsInsideEndpointMargin(rule.EndpointMargin))
            {
                relatedObjectName = rule.PathTag ? rule.PathTag.DisplayName : "Path";
                rejectionReason = RejectionReason.TooCloseToPathEndpoint;
                return false;
            }

            float signedSide = frame.SignedSide(position);
            const float sideTolerance = 0.01f;
            if (rule.Side == PathPlacementSide.Left && signedSide > -sideTolerance ||
                rule.Side == PathPlacementSide.Right && signedSide < sideTolerance)
            {
                relatedObjectName = rule.PathTag ? rule.PathTag.DisplayName : "Path";
                rejectionReason = RejectionReason.WrongPathSide;
                return false;
            }

            return true;
        }

        internal static IReadOnlyList<RelativeAnchor> CollectStationAnchors(
            GenerationContext context,
            AssetDefinition asset,
            AssetRelativePlacementRule rule)
        {
            if (context == null || !asset || rule?.UsesPathStations != true)
                return Array.Empty<RelativeAnchor>();

            List<List<List<RelativeAnchor>>> groupsBySource = new();
            foreach (PathPlacementSource source in context.PathPlacementSources)
            {
                if (!source || !source.Matches(rule.TargetTag))
                    continue;

                List<List<RelativeAnchor>> sourceGroups = new();
                int sourceStation = 0;
                foreach (PathPlacementFrame station in source.EnumerateStations(
                             rule.PathStationSpacing,
                             rule.PathStationEndpointMargin))
                {
                    sourceStation++;
                    List<(float Side, SurfacePoint Point)> sides = new();
                    foreach (float side in EnumerateSides(rule.PathStationSides))
                    {
                        Vector3 query = station.Center + station.Right * rule.PathStationLateralOffset * side;
                        if (!context.Area.TryProjectToFloor(query, out SurfacePoint point, NullGenerationProfiler.Instance))
                            continue;

                        CandidateSeed seed = new(
                            point.Position,
                            Quaternion.identity,
                            point.SurfaceCollider,
                            point.Normal,
                            point.VoxelLayer,
                            asset.PlacementType);
                        if (!PlacementSupportRules.TryValidateCompatibility(seed, asset, out _, out _) ||
                            IsExcluded(context, asset, point.Position, station.Forward))
                            continue;

                        sides.Add((side, point));
                    }

                    int requiredSideCount = rule.PathStationSides == PathPlacementSide.BothSides ? 2 : 1;
                    if (sides.Count != requiredSideCount)
                        continue;

                    List<RelativeAnchor> stationGroup = new(requiredSideCount);
                    string sourceKey = RelativeAnchorProvider.GetPersistentIdentityKey(source);
                    foreach ((float side, SurfacePoint point) in sides)
                    {
                        string sideName = side < 0f ? "Left" : "Right";
                        string identity = $"path:{sourceKey}:{sourceStation}:{sideName}";
                        stationGroup.Add(new RelativeAnchor(
                            point.Position,
                            new Bounds(point.Position, new Vector3(0.2f, 0.2f, 0.2f)),
                            $"{source.name} Station {sourceStation} {sideName}",
                            station.Forward,
                            station.Right,
                            assetTags: source.PathTags,
                            supportSurface: PlacementSupportRules.GetDescriptor(point.SurfaceCollider),
                            identity: identity,
                            source: AssetRelativeAnchorSource.SceneAnchors));
                    }
                    sourceGroups.Add(stationGroup);
                }

                if (sourceGroups.Count > 0)
                    groupsBySource.Add(sourceGroups);
            }

            List<RelativeAnchor> anchors = new();
            int acceptedStations = 0;
            for (int stationIndex = 0;
                 acceptedStations < rule.PathStationMaximumCount;
                 stationIndex++)
            {
                bool foundStation = false;
                foreach (List<List<RelativeAnchor>> sourceGroups in groupsBySource)
                {
                    if (stationIndex >= sourceGroups.Count)
                        continue;

                    anchors.AddRange(sourceGroups[stationIndex]);
                    acceptedStations++;
                    foundStation = true;
                    if (acceptedStations >= rule.PathStationMaximumCount)
                        break;
                }

                if (!foundStation)
                    break;
            }

            return anchors;
        }

        private static bool IsExcluded(
            GenerationContext context,
            AssetDefinition asset,
            Vector3 surfacePosition,
            Vector3 forward)
        {
            Quaternion rotation = forward.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(forward, Vector3.up)
                : Quaternion.identity;
            OrientedBounds bounds = new(
                surfacePosition + Vector3.up * asset.Height * 0.5f,
                asset.BoundsSize,
                rotation);
            return context.ExclusionRegions.Any(region =>
                region && region.Intersects(bounds, asset.PlacementType, asset));
        }

        private static IEnumerable<float> EnumerateSides(PathPlacementSide sides)
        {
            if (sides is PathPlacementSide.Left or PathPlacementSide.BothSides)
                yield return -1f;
            if (sides is PathPlacementSide.Right or PathPlacementSide.BothSides)
                yield return 1f;
        }

        private bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            if (localPoints == null || localPoints.Count == 0)
                return false;

            bounds = new Bounds(transform.TransformPoint(localPoints[0]), Vector3.zero);
            for (int i = 1; i < localPoints.Count; i++)
                bounds.Encapsulate(transform.TransformPoint(localPoints[i]));
            bounds.Expand(0.1f);
            return true;
        }

        private void OnValidate()
        {
            pathTags = NormalizeTags(pathTags);
            localPoints ??= new List<Vector3>();
        }

        private void OnDrawGizmos()
        {
            if (AuthoringVisualization.ShowSceneGuides)
                DrawPath(0.5f);
        }

        private void OnDrawGizmosSelected() => DrawPath(0.95f);

        private void DrawPath(float alpha)
        {
            if (localPoints == null || localPoints.Count < 2)
                return;

            Color previous = Gizmos.color;
            Gizmos.color = new Color(0.15f, 0.85f, 0.95f, alpha);
            for (int i = 1; i < localPoints.Count; i++)
                Gizmos.DrawLine(
                    transform.TransformPoint(localPoints[i - 1]),
                    transform.TransformPoint(localPoints[i]));
            Gizmos.color = previous;
        }

        private static List<SemanticTag> NormalizeTags(IEnumerable<SemanticTag> tags) =>
            tags?.Where(IsAssetTag).Distinct().ToList() ?? new List<SemanticTag>();

        private static bool IsAssetTag(SemanticTag tag) =>
            tag && tag.SupportsAssets;
    }

    /// <summary>Nearest center, tangent, and right vector resolved from a semantic path.</summary>
    internal readonly struct PathPlacementFrame
    {
        public Vector3 Center { get; }
        public Vector3 Forward { get; }
        public Vector3 Right { get; }
        public float DistanceFromStart { get; }
        public float DistanceFromEnd { get; }

        public PathPlacementFrame(
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            float distanceFromStart = float.PositiveInfinity,
            float distanceFromEnd = float.PositiveInfinity)
        {
            Center = center;
            Forward = forward;
            Right = right;
            DistanceFromStart = distanceFromStart;
            DistanceFromEnd = distanceFromEnd;
        }

        public float HorizontalDistanceTo(Vector3 position) =>
            Vector3.ProjectOnPlane(position - Center, Vector3.up).magnitude;

        public float SignedSide(Vector3 position) =>
            Vector3.Dot(Vector3.ProjectOnPlane(position - Center, Vector3.up), Right);

        public bool IsInsideEndpointMargin(float margin) =>
            margin > 0f && (DistanceFromStart < margin || DistanceFromEnd < margin);
    }
}
