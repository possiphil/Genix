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
        private static void OrderPositions(
            GenerationContext context,
            List<Vector3> positions,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            if (rule.Alignment == AssetRelativeAlignment.Random)
            {
                context.Random.Shuffle(positions);
                return;
            }

            positions.Sort((left, right) => ComparePositions(left, right, asset, anchor, rule));
        }

        private static void OrderSeeds(
            GenerationContext context,
            List<CandidateSeed> seeds,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            if (rule.Alignment == AssetRelativeAlignment.Random)
            {
                context.Random.Shuffle(seeds);
                return;
            }

            seeds.Sort((left, right) =>
                ComparePositions(left.Position, right.Position, asset, anchor, rule));
        }

        private static void AddSidePositions(
            List<Vector3> positions,
            ISet<PositionIdentity> identities,
            Bounds bounds,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            Vector3 forward = HorizontalDirection(anchor.Forward, Vector3.forward);
            Vector3 right = HorizontalDirection(anchor.Right, Vector3.right);
            AssetRelativeSide[] sides =
            {
                AssetRelativeSide.Front,
                AssetRelativeSide.Back,
                AssetRelativeSide.Left,
                AssetRelativeSide.Right
            };
            float candidateHalfExtent = Mathf.Max(asset.Width, asset.Depth) * 0.5f;

            foreach (AssetRelativeSide side in sides)
            {
                if (rule.Side != AssetRelativeSide.Any && !rule.AllowsSide(side))
                    continue;

                Vector3 outward = side switch
                {
                    AssetRelativeSide.Front => forward,
                    AssetRelativeSide.Back => -forward,
                    AssetRelativeSide.Left => -right,
                    _ => right
                };
                Vector3 tangent = side is AssetRelativeSide.Front or AssetRelativeSide.Back
                    ? right
                    : forward;
                float anchorOutwardExtent = ProjectedHorizontalExtent(anchor.Bounds.extents, outward);
                float anchorTangentExtent = ProjectedHorizontalExtent(anchor.Bounds.extents, tangent);

                float[] radialOffsets =
                {
                    anchorOutwardExtent * 0.35f,
                    anchorOutwardExtent * 0.65f,
                    anchorOutwardExtent * 0.85f,
                    anchorOutwardExtent + Mathf.Clamp(
                        Mathf.Max(candidateHalfExtent + 0.005f, rule.MinimumDistance),
                        rule.MinimumDistance,
                        rule.MaximumDistance),
                    anchorOutwardExtent + rule.MaximumDistance
                };
                float[] tangentFactors = { 0f, -0.4f, 0.4f, -0.75f, 0.75f };

                foreach (float radialOffset in radialOffsets)
                {
                    foreach (float tangentFactor in tangentFactors)
                    {
                        Vector3 position = anchor.Position +
                                           outward * radialOffset +
                                           tangent * (anchorTangentExtent * tangentFactor);
                        position.y = anchor.Position.y;
                        AddPosition(positions, identities, bounds, position);
                    }
                }
            }
        }

        private static int ComparePositions(
            Vector3 left,
            Vector3 right,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            int sideComparison = GetSidePenalty(left, anchor, rule)
                .CompareTo(GetSidePenalty(right, anchor, rule));
            if (sideComparison != 0)
                return sideComparison;

            float preferredDistance = rule.RequireSameSupportSurface
                ? Mathf.Clamp(
                    Mathf.Max(asset.Width, asset.Depth) * 0.5f + 0.005f,
                    rule.MinimumDistance,
                    rule.MaximumDistance)
                : (rule.MinimumDistance + rule.MaximumDistance) * 0.5f;
            int distanceComparison = Mathf.Abs(DistanceToBounds(left, anchor.Bounds) - preferredDistance)
                .CompareTo(Mathf.Abs(DistanceToBounds(right, anchor.Bounds) - preferredDistance));
            if (distanceComparison != 0)
                return distanceComparison;

            int alignmentComparison = GetAlignmentValue(left, anchor, rule)
                .CompareTo(GetAlignmentValue(right, anchor, rule));
            if (alignmentComparison != 0)
                return alignmentComparison;

            int zComparison = left.z.CompareTo(right.z);
            return zComparison != 0 ? zComparison : left.x.CompareTo(right.x);
        }

        private static float GetAlignmentValue(
            Vector3 position,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            Vector3 offset = position - anchor.Position;
            Vector3 forward = HorizontalDirection(anchor.Forward, Vector3.forward);
            Vector3 right = HorizontalDirection(anchor.Right, Vector3.right);

            if (rule.Alignment == AssetRelativeAlignment.Center)
            {
                AssetRelativeSide matchedSide = GetDominantSide(offset, forward, right, rule);
                if (matchedSide is AssetRelativeSide.Above or AssetRelativeSide.Below)
                    return Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude;

                return matchedSide is AssetRelativeSide.Left or AssetRelativeSide.Right
                    ? Mathf.Abs(Vector3.Dot(offset, forward))
                    : Mathf.Abs(Vector3.Dot(offset, right));
            }

            Vector3 tangentAxis = rule.Side is AssetRelativeSide.Left or AssetRelativeSide.Right
                ? forward
                : right;
            float tangent = Vector3.Dot(offset, tangentAxis);
            float end = ProjectedHorizontalExtent(anchor.Bounds.extents, tangentAxis);
            float preferred = rule.Alignment == AssetRelativeAlignment.End ? end : -end;
            return Mathf.Abs(tangent - preferred);
        }

        private static AssetRelativeSide GetDominantSide(
            Vector3 offset,
            Vector3 forward,
            Vector3 right,
            AssetRelativePlacementRule rule)
        {
            float forwardDistance = Vector3.Dot(offset, forward);
            float rightDistance = Vector3.Dot(offset, right);
            float verticalDistance = offset.y;
            float horizontalMagnitude = Mathf.Max(
                Mathf.Abs(forwardDistance),
                Mathf.Abs(rightDistance));

            if (rule.UsesVerticalSides && Mathf.Abs(verticalDistance) >= horizontalMagnitude)
            {
                return verticalDistance >= 0f
                    ? AssetRelativeSide.Above
                    : AssetRelativeSide.Below;
            }

            return Mathf.Abs(forwardDistance) >= Mathf.Abs(rightDistance)
                ? forwardDistance >= 0f
                    ? AssetRelativeSide.Front
                    : AssetRelativeSide.Back
                : rightDistance >= 0f
                    ? AssetRelativeSide.Right
                    : AssetRelativeSide.Left;
        }

        private static int GetSidePenalty(
            Vector3 position,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule) =>
            RelativeAnchorProvider.MatchesSide(position, anchor, rule) ? 0 : 1;
    }
}

