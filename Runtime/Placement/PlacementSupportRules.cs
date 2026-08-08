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
            rejectionReason = RejectionReason.None;
            relatedObjectName = string.Empty;

            if (!asset || seed.PlacementType == PlacementType.InsideSpace)
            {
                if (asset && asset.OrientationMode == OrientationMode.MatchSupportForward)
                    rejectionReason = RejectionReason.MissingSupportDirection;

                return rejectionReason == RejectionReason.None;
            }

            PlacementSurfaceDescriptor descriptor = GetDescriptor(seed.SurfaceCollider);

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

            bool hasRequiredSupportTag = false;
            bool matchesRequiredTag = false;

            foreach (SemanticTag requiredTag in asset.RequiredSupportTags)
            {
                if (!IsSurfaceTag(requiredTag))
                    continue;

                hasRequiredSupportTag = true;

                if (descriptor && descriptor.HasTag(requiredTag))
                {
                    matchesRequiredTag = true;
                    break;
                }
            }

            if (hasRequiredSupportTag && !matchesRequiredTag)
            {
                rejectionReason = RejectionReason.UnsupportedSupportSurface;
                relatedObjectName = descriptor ? descriptor.name : seed.SurfaceCollider ? seed.SurfaceCollider.name : string.Empty;
                return false;
            }

            if (asset.OrientationMode == OrientationMode.MatchSupportForward &&
                !TryGetPreferredForward(seed, descriptor, out _))
            {
                rejectionReason = RejectionReason.MissingSupportDirection;
                relatedObjectName = descriptor ? descriptor.name : seed.SurfaceCollider ? seed.SurfaceCollider.name : string.Empty;
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

        /// <summary>Returns the usable preferred direction projected onto the sampled surface plane.</summary>
        public static bool TryGetPreferredForward(
            CandidateSeed seed,
            PlacementSurfaceDescriptor descriptor,
            out Vector3 preferredForward)
        {
            preferredForward = default;

            if (!descriptor ||
                !descriptor.UsePreferredForward ||
                seed.PlacementType is PlacementType.Wall or PlacementType.InsideSpace)
            {
                return false;
            }

            Vector3 normal = seed.SurfaceNormal.sqrMagnitude > 0.001f
                ? seed.SurfaceNormal.normalized
                : seed.PlacementType == PlacementType.Ceiling ? Vector3.down : Vector3.up;
            preferredForward = Vector3.ProjectOnPlane(descriptor.PreferredForward, normal);

            if (preferredForward.sqrMagnitude <= 0.001f)
                return false;

            preferredForward.Normalize();
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

        private static bool IsSurfaceTag(SemanticTag tag) =>
            tag && tag.Category && tag.Category.SupportsSurfaces;
    }
}
