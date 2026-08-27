namespace Genix.Editor.Infrastructure
{
    /// <summary>Canonical project paths owned by the optional Genix DevTools package.</summary>
    internal static class DevToolsContentPaths
    {
        private const string Root = "Assets/Genix";

        public const string Profiles = Root + "/Profiles";

        private const string Evaluations = Root + "/Evaluations";
        public const string EvaluationSuites = Evaluations + "/Suites";
        public const string EvaluationReports = Evaluations + "/Reports";
    }
}
