namespace Genix.Diagnostics
{
    /// <summary>Controls how much placement information a generation run records.</summary>
    public enum DiagnosticsMode
    {
        /// <summary>Disables diagnostic collection.</summary>
        None,
        /// <summary>Records aggregate counts and rejection summaries.</summary>
        Summary,
        /// <summary>Also records per-candidate positions, bounds, and outcomes.</summary>
        Detailed
    }
}
