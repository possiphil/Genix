namespace Genix.Placement
{
    public enum RejectionReason
    {
        None,
        OutsideTargetArea,
        InsufficientSurfaceSupport,
        OutsideTargetVolume,
        ExceedsTargetHeight,
        OverlapsGenerated,
        OverlapsFixed,
        TooCloseToGenerated,
        TooCloseToFixed,
        OutsideRelativeRadius
    }
}
