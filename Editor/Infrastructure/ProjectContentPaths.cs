namespace Genix.Editor.Infrastructure
{
    internal static class ProjectContentPaths
    {
        public const string Root = "Assets/Genix";

        public const string AssetsRoot = Root + "/Assets";
        public const string AssetDefinitions = AssetsRoot + "/Definitions";
        public const string TagsRoot = AssetsRoot + "/Tags";
        public const string TagCategories = TagsRoot + "/Categories";
        public const string TagValues = TagsRoot + "/Values";
        public const string AssetPools = AssetsRoot + "/Pools";
        public const string AssetCatalog = AssetsRoot + "/AssetCatalog.asset";

        public const string Layouts = Root + "/Layouts";

        public const string Diagnostics = Root + "/Diagnostics";
        public const string DiagnosticSummaries = Diagnostics + "/Summaries";
        public const string DiagnosticDetails = Diagnostics + "/Details";

        public const string StylePresets = Root + "/Presets";
    }
}
