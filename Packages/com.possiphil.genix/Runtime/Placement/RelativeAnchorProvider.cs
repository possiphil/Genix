using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Geometry;
using Genix.Layouts;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Placement
{

    /// <summary>Resolves scene and planned-object anchors used by relative-placement constraints.</summary>
    public static partial class RelativeAnchorProvider
    {
        private const float AnchorDistanceTieTolerance = 0.05f;

        internal static string GetPersistentIdentityKey(object identity)
        {
            if (identity is string text)
            {
                return text.StartsWith("planned:", System.StringComparison.Ordinal)
                    ? $"generated:{text.Substring("planned:".Length)}"
                    : text;
            }

            Transform transform = identity switch
            {
                Transform value => value,
                Component component => component.transform,
                GameObject gameObject => gameObject.transform,
                _ => null
            };
            if (!transform)
                return string.Empty;

            if (transform.GetComponent<GeneratedObjectMetadata>())
                return $"generated:{transform.name}";

            Stack<string> path = new();
            for (Transform current = transform; current; current = current.parent)
                path.Push($"{current.name}[{current.GetSiblingIndex()}]");

            string scenePath = transform.gameObject.scene.path;
            return $"scene:{scenePath}|{string.Join("/", path)}";
        }

        internal static bool IsCandidateInRange(
            PlacementCandidate candidate,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (context?.RelativePlacement == null || !context.RelativePlacement.IsEnabled)
                return true;

            float radius = Mathf.Max(0.01f, context.RelativePlacement.Radius);

            foreach (RelativeAnchor anchor in EnumerateAnchors(context))
            {
                if (DistanceToAnchor(candidate.Position, anchor) > radius)
                    continue;

                relatedObjectName = anchor.Name;
                return true;
            }

            return false;
        }

        internal static bool TryValidateCandidate(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            AssetDefinition asset,
            GenerationContext context,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            rejectionReason = RejectionReason.None;
            relatedObjectName = string.Empty;

            if (!IsCandidateInRange(candidate, context, out relatedObjectName))
            {
                rejectionReason = RejectionReason.OutsideRelativeRadius;
                return false;
            }

            AssetRelativePlacementRule rule = asset ? asset.AssetRelativePlacement : null;

            if (rule?.IsConfigured != true)
                return true;

            bool foundMatchingAnchor = false;
            bool foundAnchorOnRequiredSupport = false;
            bool foundAnchorInRange = false;
            bool foundAnchorOnRequiredSide = false;
            bool foundAnchorInsideRequiredBounds = false;
            float nearestValidDistance = float.PositiveInfinity;
            float nearestValidCenterDistance = float.PositiveInfinity;
            RelativeAnchor nearestValidAnchor = default;
            PlacementSurfaceDescriptor candidateSupport = rule.RequireSameSupportSurface
                ? PlacementSupportRules.GetDescriptor(candidate.SurfaceCollider)
                : null;

            if (rule.RequireSameSupportSurface && !candidateSupport)
            {
                rejectionReason = RejectionReason.DifferentAssetRelationSupportSurface;
                relatedObjectName = candidate.SurfaceCollider ? candidate.SurfaceCollider.name : string.Empty;
                return false;
            }

            foreach (RelativeAnchor anchor in EnumerateAssetAnchors(context, asset, rule, candidate.Position))
            {
                if (!MatchesRequiredAnchor(context, anchor, candidate.RelationAnchorIdentity))
                    continue;

                if (!anchor.Matches(rule))
                    continue;

                foundMatchingAnchor = true;

                if (rule.RequireSameSupportSurface && anchor.SupportSurface != candidateSupport)
                {
                    if (string.IsNullOrEmpty(relatedObjectName))
                        relatedObjectName = anchor.Name;
                    continue;
                }

                foundAnchorOnRequiredSupport = true;
                float distance = DistanceBetweenBounds(
                    candidateBounds.ToAxisAlignedBounds(),
                    anchor.Bounds);

                if (distance < rule.MinimumDistance || distance > rule.MaximumDistance)
                    continue;

                foundAnchorInRange = true;

                if (!MatchesSide(candidate.Position, anchor, rule))
                    continue;

                foundAnchorOnRequiredSide = true;
                if (rule.RequireInsideAnchorBounds && !Contains(anchor, candidateBounds))
                    continue;

                foundAnchorInsideRequiredBounds = true;
                float centerDistance = (candidate.Position - anchor.Position).sqrMagnitude;
                if (IsBetterAnchor(
                        distance,
                        centerDistance,
                        nearestValidDistance,
                        nearestValidCenterDistance))
                {
                    nearestValidDistance = distance;
                    nearestValidCenterDistance = centerDistance;
                    nearestValidAnchor = anchor;
                }
            }

            if (foundAnchorInsideRequiredBounds)
            {
                relatedObjectName = nearestValidAnchor.Name;
                if (!HasRemainingPerAnchorCapacity(context, asset, rule, nearestValidAnchor))
                {
                    rejectionReason = RejectionReason.AssetRelationAnchorCapacityReached;
                    return false;
                }

                if (context.AssetPool && context.AssetPool.TryGetReachedAnchorGroupLimit(
                        asset,
                        context,
                        nearestValidAnchor,
                        out AssetPoolAnchorGroupLimit reachedGroup))
                {
                    rejectionReason = RejectionReason.AssetRelationGroupCapacityReached;
                    relatedObjectName = $"{nearestValidAnchor.Name} ({reachedGroup.MemberTag.DisplayName})";
                    return false;
                }

                return true;
            }

            rejectionReason = !foundMatchingAnchor
                ? HasMatchingAssetAnchor(context, asset, rule)
                    ? RejectionReason.OutsideAssetRelationRange
                    : RejectionReason.MissingAssetRelationAnchor
                : !foundAnchorOnRequiredSupport
                    ? RejectionReason.DifferentAssetRelationSupportSurface
                    : !foundAnchorInRange
                        ? RejectionReason.OutsideAssetRelationRange
                        : !foundAnchorOnRequiredSide
                            ? RejectionReason.WrongAssetRelationSide
                            : RejectionReason.OutsideAssetRelationBounds;
            if (string.IsNullOrEmpty(relatedObjectName))
            {
                relatedObjectName = rule.TargetScope == AssetRelativeTargetScope.Asset
                    ? rule.TargetAsset ? rule.TargetAsset.AssetName : "Asset Relation"
                    : rule.TargetTag ? rule.TargetTag.DisplayName : "Asset Relation";
            }
            return false;
        }

    }
}
