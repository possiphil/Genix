namespace Genix.Tests.Framework
{
    /// <summary>Stable NUnit categories used by the Genix test presets and dashboard.</summary>
    internal static class GenixTestCategories
    {
        public const string Quick = "Genix.Preset.Quick";
        public const string Full = "Genix.Preset.Full";
        public const string Stress = "Genix.Preset.Stress";
        public const string Performance = "Genix.Preset.Performance";

        public const string Property = "Genix.Kind.Property";
        public const string Snapshot = "Genix.Kind.Snapshot";
        public const string Integration = "Genix.Kind.Integration";

        public const string RandomnessArea = "Genix.Area.Randomness";
        public const string GeometryArea = "Genix.Area.Geometry";
        public const string SamplingArea = "Genix.Area.Sampling";
        public const string PlacementArea = "Genix.Area.Placement";
        public const string SemanticsArea = "Genix.Area.Semantics";
        public const string SpatialArea = "Genix.Area.Spatial";
        public const string LayoutsArea = "Genix.Area.Layouts";
        public const string WorkflowArea = "Genix.Area.Workflow";
        public const string RobustnessArea = "Genix.Area.Robustness";
        public const string PerformanceArea = "Genix.Area.Performance";
    }
}
