using Genix.Assets;
using Genix.Core;
using Genix.Orientation;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>Evaluates asset-specific semantic, direction, and capacity rules for a sampled support surface.</summary>
    public static class PlacementSupportRules
    {
        /// <summary>Finds the descriptor that owns a sampled surface collider.</summary>
        public static PlacementSurfaceDescriptor GetDescriptor(Collider surfaceCollider) =>
            surfaceCollider ? surfaceCollider.GetComponentInParent<PlacementSurfaceDescriptor>() : null;

        /// <summary>
        /// Evaluates all support-surface rules that are independent of an asset's rotation and bounds.
        /// </summary>
        public static bool TryValidate(
            CandidateSeed seed,
            AssetDefinition asset,
            GenerationContext context,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            if (!TryValidateCompatibility(seed, asset, out rejectionReason, out relatedObjectName))
                return false;

            PlacementSurfaceDescriptor descriptor = GetDescriptor(seed.SurfaceCollider);

            if (descriptor &&
                TryGetReachedAssetCapacityRule(descriptor, asset, context, out PlacementSurfaceCapacityRule reachedRule))
            {
                rejectionReason = RejectionReason.SupportAssetCapacityReached;
                relatedObjectName = $"{descriptor.name} ({reachedRule.DisplayName}: {reachedRule.MaxCapacity})";
                return false;
            }

            if (descriptor &&
                descriptor.LimitCapacity &&
                GetUsedCapacity(descriptor, context) >= descriptor.MaxCapacity)
            {
                rejectionReason = RejectionReason.SupportCapacityReached;
                relatedObjectName = descriptor.name;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Evaluates immutable support tags, allow/deny rules, and authored direction without consulting run capacity.
        /// </summary>
        internal static bool TryValidateCompatibility(
            CandidateSeed seed,
            AssetDefinition asset,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            rejectionReason = RejectionReason.None;
            relatedObjectName = string.Empty;

            if (!asset || seed.PlacementType == PlacementType.InsideSpace)
            {
                if (asset && asset.OrientationMode == OrientationMode.MatchSupportForward)
                    rejectionReason = RejectionReason.MissingSupportDirection;

                return rejectionReason == RejectionReason.None;
            }

            PlacementSurfaceDescriptor descriptor = GetDescriptor(seed.SurfaceCollider);

            if (descriptor && !descriptor.AcceptsAsset(asset))
            {
                rejectionReason = RejectionReason.SurfaceRejectsAsset;
                relatedObjectName = descriptor.name;
                return false;
            }

            foreach (TagCategory category in asset.ForbiddenSupportAnyCategories)
            {
                if (!category || !category.SupportsSurfaces)
                    continue;

                rejectionReason = RejectionReason.UnsupportedSupportSurface;
                relatedObjectName = descriptor ? descriptor.name : seed.SurfaceCollider ? seed.SurfaceCollider.name : string.Empty;
                return false;
            }

            foreach (TagCategory category in asset.RequiredSupportNoneCategories)
            {
                if (!category || !category.SupportsSurfaces)
                    continue;

                rejectionReason = RejectionReason.UnsupportedSupportSurface;
                relatedObjectName = descriptor ? descriptor.name : seed.SurfaceCollider ? seed.SurfaceCollider.name : string.Empty;
                return false;
            }

            foreach (SemanticTag forbiddenTag in asset.ForbiddenSupportTags)
            {
                if (!IsSurfaceTag(forbiddenTag) || !descriptor || !descriptor.HasTag(forbiddenTag))
                    continue;

                rejectionReason = RejectionReason.UnsupportedSupportSurface;
                relatedObjectName = descriptor.name;
                return false;
            }

            if (!MatchesRequiredSupportTags(asset, descriptor))
            {
                rejectionReason = RejectionReason.UnsupportedSupportSurface;
                relatedObjectName = descriptor ? descriptor.name : seed.SurfaceCollider ? seed.SurfaceCollider.name : string.Empty;
                return false;
            }

            if (asset.OrientationMode == OrientationMode.MatchSupportForward &&
                !TryGetSupportForward(seed, descriptor, out _))
            {
                rejectionReason = RejectionReason.MissingSupportDirection;
                relatedObjectName = descriptor ? descriptor.name : seed.SurfaceCollider ? seed.SurfaceCollider.name : string.Empty;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Requires one matching support tag from every represented category while treating tags within one
        /// category as alternatives. An asset without valid required surface tags accepts any descriptor.
        /// </summary>
        public static bool MatchesRequiredSupportTags(
            AssetDefinition asset,
            PlacementSurfaceDescriptor descriptor)
        {
            if (!asset)
                return true;

            var requiredTags = asset.RequiredSupportTags;
            for (int i = 0; i < requiredTags.Count; i++)
            {
                SemanticTag requiredTag = requiredTags[i];
                if (!IsSurfaceTag(requiredTag))
                    continue;

                TagCategory category = requiredTag.Category;
                bool categoryHandled = false;
                for (int previous = 0; previous < i; previous++)
                {
                    SemanticTag previousTag = requiredTags[previous];
                    if (IsSurfaceTag(previousTag) && previousTag.Category == category)
                    {
                        categoryHandled = true;
                        break;
                    }
                }

                if (categoryHandled)
                    continue;

                bool categoryMatches = false;
                for (int candidate = i; candidate < requiredTags.Count; candidate++)
                {
                    SemanticTag candidateTag = requiredTags[candidate];
                    if (!IsSurfaceTag(candidateTag) || candidateTag.Category != category)
                        continue;

                    if (descriptor && descriptor.HasTag(candidateTag))
                    {
                        categoryMatches = true;
                        break;
                    }
                }

                if (!categoryMatches)
                    return false;
            }

            return true;
        }

        /// <summary>Returns a stable local support direction projected onto the sampled surface plane.</summary>
        public static bool TryGetSupportForward(
            CandidateSeed seed,
            PlacementSurfaceDescriptor descriptor,
            out Vector3 supportForward)
        {
            supportForward = default;

            if (seed.PlacementType is PlacementType.Wall or PlacementType.InsideSpace)
                return false;

            Transform supportTransform = descriptor
                ? descriptor.transform
                : seed.SurfaceCollider
                    ? seed.SurfaceCollider.transform
                    : null;

            if (!supportTransform)
                return false;

            Vector3 normal = seed.SurfaceNormal.sqrMagnitude > 0.001f
                ? seed.SurfaceNormal.normalized
                : seed.PlacementType == PlacementType.Ceiling ? Vector3.down : Vector3.up;
            AssetRelationAnchor relationAnchor = descriptor
                ? descriptor.GetComponentInParent<AssetRelationAnchor>()
                : null;
            Vector3 authoredForward = relationAnchor ? relationAnchor.Forward : supportTransform.forward;
            Vector3 authoredRight = relationAnchor ? relationAnchor.Right : supportTransform.right;
            supportForward = Vector3.ProjectOnPlane(authoredForward, normal);

            if (supportForward.sqrMagnitude <= 0.001f)
                supportForward = Vector3.ProjectOnPlane(authoredRight, normal);

            if (supportForward.sqrMagnitude <= 0.001f)
                return false;

            supportForward.Normalize();
            return true;
        }

        private static int GetUsedCapacity(
            PlacementSurfaceDescriptor descriptor,
            GenerationContext context)
        {
            if (!descriptor || context == null)
                return 0;

            int planned = context.Plan?.GetSupportCount(descriptor) ?? 0;
            int existing = context.GeneratedSceneObjects?.GetSupportCount(descriptor) ?? 0;
            return planned + existing;
        }

        private static bool TryGetReachedAssetCapacityRule(
            PlacementSurfaceDescriptor descriptor,
            AssetDefinition asset,
            GenerationContext context,
            out PlacementSurfaceCapacityRule reachedRule)
        {
            reachedRule = null;

            if (!descriptor || !asset || context == null)
                return false;

            foreach (PlacementSurfaceCapacityRule rule in descriptor.AssetCapacityRules)
            {
                if (rule == null || !rule.Matches(asset))
                    continue;

                int planned = rule.Scope == PlacementSurfaceCapacityRuleScope.Asset
                    ? context.Plan?.GetSupportAssetCount(descriptor, rule.Asset) ?? 0
                    : context.Plan?.GetSupportTagCount(descriptor, rule.AssetTag) ?? 0;
                int existing = rule.Scope == PlacementSurfaceCapacityRuleScope.Asset
                    ? context.GeneratedSceneObjects?.GetSupportAssetCount(descriptor, rule.Asset) ?? 0
                    : context.GeneratedSceneObjects?.GetSupportTagCount(descriptor, rule.AssetTag) ?? 0;

                if (planned + existing < rule.MaxCapacity)
                    continue;

                reachedRule = rule;
                return true;
            }

            return false;
        }

        private static bool IsSurfaceTag(SemanticTag tag) =>
            tag && tag.Category && tag.Category.SupportsSurfaces;
    }
}
