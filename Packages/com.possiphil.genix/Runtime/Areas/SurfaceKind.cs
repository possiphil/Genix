namespace Genix.Areas
{
    /// <summary>Classification assigned to a discovered surface from its normal.</summary>
    public enum SurfaceKind
    {
        /// <summary>An upward-facing surface within the configured floor angle.</summary>
        Floor,
        /// <summary>A surface between the floor and ceiling angle bands.</summary>
        Wall,
        /// <summary>A downward-facing surface within the configured ceiling angle.</summary>
        Ceiling
    }
}
