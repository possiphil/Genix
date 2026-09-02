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
                RejectionReason.OutsideTargetArea =>
                    "The asset footprint leaves its sampled floor, wall, or ceiling surface. Use a larger matching surface, reduce the asset footprint, or use Adaptive Surface Fit where appropriate.",
                RejectionReason.InsufficientSurfaceSupport =>
                    "Adaptive Surface Fit found too little compatible surface below the asset footprint. Reduce Min Support, increase Max Height or Depth Difference, or provide a flatter surface.",
                RejectionReason.OutsideTargetVolume =>
                    "The complete asset bounds leave the target area's valid 3D volume. Use a smaller asset, enlarge the target area, or choose a roomier position.",
                RejectionReason.ExceedsTargetHeight =>
                    "The asset is taller than the target area's available height. Correct its placement bounds, reduce its scale, or use a taller target area.",
                RejectionReason.OverlapsGenerated =>
                    "The asset intersects an object already placed by Genix. Request fewer objects, use smaller assets, or provide more placement space.",
                RejectionReason.OverlapsFixed =>
                    "The asset intersects an existing scene object. Move the scene object, correct its collider or layer, or provide more placement space.",
                RejectionReason.TooCloseToGenerated =>
                    "The asset is closer to a generated object than the active style or asset spacing allows. Reduce the relevant spacing or provide more room.",
                RejectionReason.TooCloseToFixed =>
                    "The asset is closer to a scene object than Scene Clearance allows. Reduce Minimum Distance or provide more room.",
                RejectionReason.OutsideRelativeRadius =>
                    "The position is outside the configured Global Proximity distance. Increase Maximum Distance or provide more matching source objects.",
                RejectionReason.UnsupportedSupportSurface =>
                    "The surface's support tags do not match this asset. Check Required and Blocked Support Tags; every required category needs one matching tag, and blocked tags take precedence.",
                RejectionReason.SurfaceRejectsAsset =>
                    "The sampled surface rejects this asset through Required or Blocked Asset Tags. Blocked tags take precedence; an empty Required list accepts every asset.",
                RejectionReason.SupportCapacityReached =>
                    "The sampled surface reached Max Capacity. Increase it, disable Limit Capacity, or provide more matching surfaces.",
                RejectionReason.SupportAssetCapacityReached =>
                    "The sampled surface reached an asset-specific limit. Increase or remove the matching rule, or provide more matching surfaces.",
                RejectionReason.MissingSupportDirection =>
                    "Match Support Forward requires a sampled Floor or Ceiling collider with usable local Z or X direction.",
                RejectionReason.TooFarFromWall =>
                    "The asset uses Near Wall and no detected wall is close enough. Increase Max Wall Distance or move the target area closer to a wall.",
                RejectionReason.TooCloseToWall =>
                    "The asset uses Away From Wall and is inside the configured clearance. Reduce Min Wall Distance or provide more open floor space.",
                RejectionReason.MissingWallReference =>
                    "The asset requests a wall relationship, but this area has no SFS wall regions, wall colliders, or wall-classified terrain slopes. Check the surface layers and classification angles, recompute the area, or use Any Distance.",
                RejectionReason.InsideExclusionRegion =>
                    "The position intersects a Genix Exclusion Region. Move or resize the region, or remove this placement target from Blocks Placement On.",
                RejectionReason.AssetSpacingViolation =>
                    "This asset or a nearby generated asset requires a larger center-to-center distance. Adjust the Asset Spacing rules on either definition.",
                RejectionReason.ClearanceOutsideTargetVolume =>
                    "The asset fits, but its reserved Clearance leaves the target volume. Reduce or reposition the clearance bounds, or choose a roomier position.",
                RejectionReason.ClearanceBlocked =>
                    "The asset visual or its reserved Clearance conflicts with scene geometry, another generated object, or another reserved clearance volume. Move the conflicting object or reduce the reserved Clearance.",
                RejectionReason.MissingAssetRelationAnchor =>
                    "Object Relationship requires a matching generated object or Asset Relation Anchor, but none is available from the selected Anchor Objects source.",
                RejectionReason.OutsideAssetRelationRange =>
                    "Matching relation anchors exist, but the position is outside their configured Minimum and Maximum Distance.",
                RejectionReason.WrongAssetRelationSide =>
                    "A matching relation anchor is in range, but the position is outside the selected Place On Sides sectors. Check the anchor direction and selected sides.",
                RejectionReason.DifferentAssetRelationSupportSurface =>
                    "Same Support Surface is enabled, but the position and anchor use different configured surfaces. Assign the correct support surface to the fixed relation anchor.",
                RejectionReason.AssetRelationAnchorCapacityReached =>
                    "Every nearby matching anchor reached this asset's Instances per Anchor maximum. Increase the count, provide more anchors, or request fewer instances.",
                RejectionReason.AssetRelationGroupCapacityReached =>
                    "The matched anchor reached a pool-level Per-Anchor Group maximum shared by this asset tag. Increase the group maximum, change the member tag, or provide another matching anchor.",
                RejectionReason.OutsideAssetRelationBounds =>
                    "Stay Inside Anchor Area is enabled, but the complete asset does not fit. Enlarge the anchor bounds, reduce the asset footprint, or provide another matching area.",
                RejectionReason.MissingPathReference =>
                    "This asset requires a semantic path, but the target area contains no active Genix path with the selected tag. Add or enable a matching Path Placement Source, or change the asset's Path Tag.",
                RejectionReason.OutsidePathDistance =>
                    "The position is outside the configured Minimum and Maximum Distance from its nearest matching path. Adjust those distances or provide more room beside the path.",
                RejectionReason.TooCloseToPathEndpoint =>
                    "The position lies inside the path's End Margin. Reduce End Margin or provide a longer matching path.",
                RejectionReason.WrongPathSide =>
                    "The position lies on the opposite side of the nearest matching path. Change Side or reverse the authored path direction.",
                _ => string.Empty
            };

        public static string GetAdvice(string displayName)
        {
            if (TryResolveReason(displayName, out RejectionReason reason))
                return GetAdvice(reason);

            return string.Empty;
        }

        public static string GetDisplayName(string storedDisplayName)
        {
            return TryResolveReason(storedDisplayName, out RejectionReason reason)
                ? reason.ToDisplayName()
                : storedDisplayName;
        }

        private static bool TryResolveReason(string displayName, out RejectionReason result)
        {
            foreach (RejectionReason reason in System.Enum.GetValues(typeof(RejectionReason)))
            {
                string currentDisplayName = reason.ToDisplayName();
                string legacyDisplayName = EnumDisplayNameExtensions.ToDisplayName((System.Enum)reason);

                if (currentDisplayName == displayName || legacyDisplayName == displayName)
                {
                    result = reason;
                    return true;
                }
            }

            result = RejectionReason.None;
            return false;
        }
    }
}
