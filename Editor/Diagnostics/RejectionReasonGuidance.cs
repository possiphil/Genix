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
                    "Check the asset's Required and Forbidden Support Tags and the sampled object's Placement Surface Descriptor. Forbidden tags take precedence.",
                RejectionReason.SupportCapacityReached =>
                    "The sampled Placement Surface Descriptor has reached Max Capacity. Increase it, disable Limit Capacity, or provide more matching surfaces.",
                RejectionReason.MissingSupportDirection =>
                    "Match Support Forward requires a Placement Surface Descriptor with Use Preferred Forward enabled and a usable local Z direction.",
                RejectionReason.TooFarFromWall =>
                    "The asset uses Near Wall and no detected wall is close enough. Increase Max Wall Distance or move the target area closer to a wall.",
                RejectionReason.TooCloseToWall =>
                    "The asset uses Away From Wall and is inside the configured clearance. Reduce Min Wall Distance or provide more open floor space.",
                RejectionReason.MissingWallReference =>
                    "The asset requests a wall relationship, but this area has no SFS wall regions or sampled non-terrain wall colliders. Recompute the area or use Any Distance.",
                RejectionReason.InsideExclusionRegion =>
                    "The candidate intersects a Genix Exclusion Region. Move or resize the region, or remove this placement type from Affected Targets.",
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
