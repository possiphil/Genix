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
        OutsideRelativeRadius
    }
}
