namespace Genix.Placement
{
    /// <summary>Reason an asset attempt could not become a planned object.</summary>
    public enum RejectionReason
    {
        /// <summary>The attempt has not been rejected.</summary>
        None,
        /// <summary>The projected footprint is not contained by the selected target surface.</summary>
        OutsideTargetArea,
        /// <summary>Too little of the asset footprint is supported by the physical surface.</summary>
        InsufficientSurfaceSupport,
        /// <summary>The oriented asset bounds leave the valid target volume.</summary>
        OutsideTargetVolume,
        /// <summary>The asset exceeds the target area's available height.</summary>
        ExceedsTargetHeight,
        /// <summary>The asset intersects another object planned in the current run.</summary>
        OverlapsGenerated,
        /// <summary>The asset intersects an existing fixed scene object.</summary>
        OverlapsFixed,
        /// <summary>The asset violates spacing from an object planned in the current run.</summary>
        TooCloseToGenerated,
        /// <summary>The asset violates spacing from an existing fixed scene object.</summary>
        TooCloseToFixed,
        /// <summary>The asset lies outside the configured relative-placement radius.</summary>
        OutsideRelativeRadius,
        /// <summary>The sampled surface does not satisfy the asset's required or forbidden support tags.</summary>
        UnsupportedSupportSurface,
        /// <summary>The sampled surface's asset allow or deny tags reject this asset.</summary>
        SurfaceRejectsAsset,
        /// <summary>The sampled surface has already reached its configured maximum placement capacity.</summary>
        SupportCapacityReached,
        /// <summary>The sampled surface has reached a capacity rule for this asset or one of its tags.</summary>
        SupportAssetCapacityReached,
        /// <summary>The asset requests support-facing orientation but the sampled surface provides no usable direction.</summary>
        MissingSupportDirection,
        /// <summary>The candidate lies farther from the nearest detected wall than the asset allows.</summary>
        TooFarFromWall,
        /// <summary>The candidate lies closer to a detected wall than the asset allows.</summary>
        TooCloseToWall,
        /// <summary>The area provides no suitable wall reference for the requested relationship.</summary>
        MissingWallReference,
        /// <summary>The candidate intersects an active collider-free placement exclusion region.</summary>
        InsideExclusionRegion,
        /// <summary>The candidate violates an asset-specific minimum-distance rule.</summary>
        AssetSpacingViolation,
        /// <summary>The asset's reserved clearance volume leaves the selected target volume.</summary>
        ClearanceOutsideTargetVolume,
        /// <summary>The asset's visual or reserved clearance volume conflicts with existing geometry or clearance.</summary>
        ClearanceBlocked,
        /// <summary>No generated object or scene relation anchor matches the asset's semantic target.</summary>
        MissingAssetRelationAnchor,
        /// <summary>Matching anchors exist, but none lies within the asset's configured distance interval.</summary>
        OutsideAssetRelationRange,
        /// <summary>A matching anchor is in range, but the candidate lies on the wrong local side.</summary>
        WrongAssetRelationSide,
        /// <summary>A matching anchor and candidate belong to different semantic support surfaces.</summary>
        DifferentAssetRelationSupportSurface,
        /// <summary>The matched relation anchor already owns the configured maximum number of this asset.</summary>
        AssetRelationAnchorCapacityReached,
        /// <summary>The matched anchor reached a pooled maximum shared by assets carrying one tag.</summary>
        AssetRelationGroupCapacityReached,
        /// <summary>The candidate does not fit completely inside the matched relation anchor bounds.</summary>
        OutsideAssetRelationBounds,
        /// <summary>No semantic path source matches the asset's configured path tag.</summary>
        MissingPathReference,
        /// <summary>The candidate lies outside the configured horizontal distance interval from its nearest path.</summary>
        OutsidePathDistance,
        /// <summary>The candidate lies on the opposite side of its nearest path segment.</summary>
        WrongPathSide,
        /// <summary>The candidate lies inside the configured exclusion margin at either path endpoint.</summary>
        TooCloseToPathEndpoint
    }
}
