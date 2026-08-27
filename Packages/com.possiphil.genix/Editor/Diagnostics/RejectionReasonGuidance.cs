using Genix.Extensions;
using Genix.Placement;

namespace Genix.Editor.Diagnostics
{
    /// <summary>Provides actionable editor guidance for placement rejection reasons.</summary>
    internal static class RejectionReasonGuidance
    {
        public static string GetAdvice(RejectionReason reason) =>
            reason switch
            {
                RejectionReason.UnsupportedSupportSurface =>
                    "Check the asset's Required and Forbidden Support Tags and the sampled object's Placement Surface Descriptor. Each configured Required category needs one matching tag; Forbidden tags take precedence.",
                RejectionReason.SurfaceRejectsAsset =>
                    "The sampled Placement Surface Descriptor rejects this asset through Allowed or Forbidden Asset Tags. Forbidden tags take precedence; an empty Allowed list accepts every asset.",
                RejectionReason.SupportCapacityReached =>
                    "The sampled Placement Surface Descriptor has reached Max Capacity. Increase it, disable Limit Capacity, or provide more matching surfaces.",
                RejectionReason.SupportAssetCapacityReached =>
                    "The sampled Placement Surface Descriptor has reached an Asset-Specific Limit matching this asset or one of its tags. Increase or remove that rule, or provide more matching surfaces.",
                RejectionReason.MissingSupportDirection =>
                    "Match Support Forward requires a sampled Floor or Ceiling collider with usable local Z or X direction.",
                RejectionReason.TooFarFromWall =>
                    "The asset uses Near Wall and no detected wall is close enough. Increase Max Wall Distance or move the target area closer to a wall.",
                RejectionReason.TooCloseToWall =>
                    "The asset uses Away From Wall and is inside the configured clearance. Reduce Min Wall Distance or provide more open floor space.",
                RejectionReason.MissingWallReference =>
                    "The asset requests a wall relationship, but this area has no SFS wall regions, wall colliders, or wall-classified terrain slopes. Check the surface layers and classification angles, recompute the area, or use Any Distance.",
                RejectionReason.InsideExclusionRegion =>
                    "The candidate intersects a Genix Exclusion Region. Move or resize the region, or remove this placement type from Affected Targets.",
                RejectionReason.AssetSpacingViolation =>
                    "This asset or a nearby generated asset requires a larger center-to-center distance. Adjust the Asset Spacing rules on either definition.",
                RejectionReason.ClearanceOutsideTargetVolume =>
                    "The asset fits, but its reserved Clearance leaves the target volume. Reduce or reposition the clearance bounds, or choose a roomier candidate.",
                RejectionReason.ClearanceBlocked =>
                    "The asset visual or its reserved Clearance conflicts with fixed geometry, another generated object, or another reserved clearance volume.",
                RejectionReason.MissingAssetRelationAnchor =>
                    "This asset requires a semantic relative-placement target, but no matching generated asset or Asset Relation Anchor is available from the selected source.",
                RejectionReason.OutsideAssetRelationRange =>
                    "Matching relation anchors exist, but the candidate is outside the configured minimum/maximum 3D distance from their bounds.",
                RejectionReason.WrongAssetRelationSide =>
                    "A matching relation anchor is in range, but the candidate is outside its required Front, Back, Left, Right, Above, or Below sector. Check the anchor direction and selected sectors.",
                RejectionReason.DifferentAssetRelationSupportSurface =>
                    "Require Same Support Surface is enabled, but candidate and anchor do not reference the same Placement Surface Descriptor. Assign the correct descriptor to fixed Asset Relation Anchors.",
                RejectionReason.AssetRelationAnchorCapacityReached =>
                    "Every matching relation anchor near this candidate already reached this asset's Per Anchor Count maximum. Increase the count, choose At Least or Unlimited, provide more anchors, or request fewer instances.",
                RejectionReason.AssetRelationGroupCapacityReached =>
                    "The matched anchor reached a pool-level Per-Anchor Group maximum shared by this asset tag. Increase the group maximum, change the member tag, or provide another matching anchor.",
                RejectionReason.OutsideAssetRelationBounds =>
                    "Require Inside Anchor Bounds is enabled, but the complete asset does not fit inside the matched semantic region. Enlarge the anchor bounds, reduce the asset footprint, or provide another matching region.",
                RejectionReason.MissingPathReference =>
                    "This asset requires a semantic path, but the target area contains no active Path Placement Source with the selected tag.",
                RejectionReason.OutsidePathDistance =>
                    "The candidate is outside the configured minimum/maximum horizontal distance from its nearest matching path. Adjust Near Path distance or provide more room beside the path.",
                RejectionReason.TooCloseToPathEndpoint =>
                    "The candidate lies inside the Path Placement Endpoint Margin. Reduce the margin or provide a longer matching path.",
                RejectionReason.WrongPathSide =>
                    "The candidate lies on the opposite side of the nearest matching path. Change Path Side or reverse the authored path direction.",
                _ => string.Empty
            };

        public static string GetAdvice(string displayName)
        {
            foreach (RejectionReason reason in System.Enum.GetValues(typeof(RejectionReason)))
            {
                if (reason.ToDisplayName() == displayName)
                    return GetAdvice(reason);
            }

            return string.Empty;
        }
    }
}
