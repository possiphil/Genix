namespace Genix.Placement
{
    public enum RejectionReason
    {
        None,
        OutsideTargetArea,
        OutsideTargetVolume,
        ExceedsTargetHeight,
        OverlapsGenerated,
        OverlapsFixed,
        TooCloseToGenerated,
        TooCloseToFixed,
        OutsideRelativeRadius
    }
}
