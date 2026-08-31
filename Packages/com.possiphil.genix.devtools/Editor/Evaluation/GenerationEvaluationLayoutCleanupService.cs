using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Genix.Editor.Infrastructure;
using Genix.Editor.Layouts;
using Genix.Layouts;
using UnityEditor;

namespace Genix.Editor.Evaluation
{
    /// <summary>Describes the report-aware deletion of superseded evaluation layouts.</summary>
    internal sealed class GenerationEvaluationLayoutCleanupPlan
    {
        public string Error { get; set; } = string.Empty;
        public GenerationEvaluationReport BaselineReport { get; set; }
        public IReadOnlyList<GenerationEvaluationReport> ProtectedReports { get; set; } =
            Array.Empty<GenerationEvaluationReport>();
        public IReadOnlyList<string> ProtectedLayoutPaths { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> DeletableLayoutPaths { get; set; } = Array.Empty<string>();
        public int MissingProtectedLayouts { get; set; }

        public bool IsValid => string.IsNullOrWhiteSpace(Error) && BaselineReport;
    }

    /// <summary>
    /// Keeps the latest completed full campaign and the latest newer completed rerun per scenario, then removes
    /// older locked layouts that are known to evaluation reports for the same suite.
    /// </summary>
    internal static class GenerationEvaluationLayoutCleanupService
    {
        private const string RunAllScope = "RunAll";
        private const string SelectedScenarioScope = "SelectedScenario";
        private const string EvaluationLayoutMarker = "Locked evaluation observation.";

        public static GenerationEvaluationLayoutCleanupPlan BuildPlan(GenerationEvaluationSuite suite)
        {
            if (!suite)
            {
                return new GenerationEvaluationLayoutCleanupPlan
                {
                    Error = "Select an evaluation suite before cleaning up layouts."
                };
            }

            string suitePath = AssetDatabase.GetAssetPath(suite);
            GenerationEvaluationReport[] reports = LoadReports();
            return BuildPlan(reports, suite.name, suitePath, IsDeletableEvaluationLayout);
        }

        internal static GenerationEvaluationLayoutCleanupPlan BuildPlan(
            IEnumerable<GenerationEvaluationReport> reports,
            string suiteName,
            string suiteAssetPath,
            Func<string, bool> isDeletableEvaluationLayout)
        {
            GenerationEvaluationReport[] matching = reports?
                .Where(report => report && MatchesSuite(report, suiteName, suiteAssetPath))
                .ToArray() ?? Array.Empty<GenerationEvaluationReport>();
            GenerationEvaluationReport baseline = matching
                .Where(report => IsComplete(report) &&
                                 string.Equals(report.RunScope, RunAllScope, StringComparison.Ordinal))
                .OrderByDescending(GetCreatedAt)
                .ThenByDescending(GetStableReportName, StringComparer.Ordinal)
                .FirstOrDefault();
            if (!baseline)
            {
                return new GenerationEvaluationLayoutCleanupPlan
                {
                    Error = "No completed full campaign exists for this suite. Run the full evaluation before cleaning up layouts."
                };
            }

            DateTimeOffset baselineTime = GetCreatedAt(baseline);
            GenerationEvaluationReport[] overrides = matching
                .Where(report => IsComplete(report) &&
                                 string.Equals(report.RunScope, SelectedScenarioScope, StringComparison.Ordinal) &&
                                 report.SelectedScenarioIndex >= 0 &&
                                 GetCreatedAt(report) > baselineTime)
                .GroupBy(report => report.SelectedScenarioIndex)
                .Select(group => group
                    .OrderByDescending(GetCreatedAt)
                    .ThenByDescending(GetStableReportName, StringComparer.Ordinal)
                    .First())
                .OrderBy(report => report.SelectedScenarioIndex)
                .ToArray();
            GenerationEvaluationReport[] protectedReports = new[] { baseline }
                .Concat(overrides)
                .ToArray();

            HashSet<string> protectedPaths = CollectLayoutPaths(protectedReports);
            HashSet<string> knownPaths = CollectLayoutPaths(matching);
            string[] existingProtectedPaths = protectedPaths
                .Where(IsExistingLayout)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] deletablePaths = knownPaths
                .Except(protectedPaths, StringComparer.OrdinalIgnoreCase)
                .Where(path => isDeletableEvaluationLayout?.Invoke(path) == true)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new GenerationEvaluationLayoutCleanupPlan
            {
                BaselineReport = baseline,
                ProtectedReports = protectedReports,
                ProtectedLayoutPaths = existingProtectedPaths,
                DeletableLayoutPaths = deletablePaths,
                MissingProtectedLayouts = protectedPaths.Count - existingProtectedPaths.Length
            };
        }

        public static bool Execute(
            GenerationEvaluationLayoutCleanupPlan plan,
            out int deletedCount,
            out string error)
        {
            deletedCount = 0;
            error = string.Empty;
            if (plan == null || !plan.IsValid)
            {
                error = plan?.Error ?? "No cleanup plan is available.";
                return false;
            }

            SavedLayout[] layouts = plan.DeletableLayoutPaths
                .Select(AssetDatabase.LoadAssetAtPath<SavedLayout>)
                .Where(layout => layout)
                .ToArray();
            return LayoutWorkflow.DeleteLayouts(layouts, true, out deletedCount, out error);
        }

        private static GenerationEvaluationReport[] LoadReports()
        {
            if (!AssetDatabase.IsValidFolder(DevToolsContentPaths.EvaluationReports))
                return Array.Empty<GenerationEvaluationReport>();

            // Reports created by older DevTools package layouts can retain a managed class identifier
            // without a MonoScript reference. Unity loads them correctly, but its type index may omit them.
            return AssetDatabase
                .FindAssets(string.Empty, new[] { DevToolsContentPaths.EvaluationReports })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                .Select(AssetDatabase.LoadAssetAtPath<GenerationEvaluationReport>)
                .Where(report => report)
                .ToArray();
        }

        private static bool MatchesSuite(
            GenerationEvaluationReport report,
            string suiteName,
            string suiteAssetPath)
        {
            if (!report)
                return false;

            if (!string.IsNullOrWhiteSpace(suiteAssetPath) &&
                !string.IsNullOrWhiteSpace(report.SuiteAssetPath))
            {
                return string.Equals(
                    report.SuiteAssetPath,
                    suiteAssetPath,
                    StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(report.SuiteName, suiteName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsComplete(GenerationEvaluationReport report) =>
            report &&
            report.CampaignCompleted &&
            report.ExpectedRunCount > 0 &&
            report.Runs.Count == report.ExpectedRunCount;

        private static DateTimeOffset GetCreatedAt(GenerationEvaluationReport report) =>
            DateTimeOffset.TryParse(
                report?.CreatedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset createdAt)
                ? createdAt
                : DateTimeOffset.MinValue;

        private static string GetStableReportName(GenerationEvaluationReport report) =>
            report ? report.name ?? string.Empty : string.Empty;

        private static HashSet<string> CollectLayoutPaths(IEnumerable<GenerationEvaluationReport> reports)
        {
            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
            foreach (GenerationEvaluationReport report in reports.Where(report => report))
            {
                foreach (GenerationEvaluationRunRecord run in report.Runs.Where(run => run != null))
                {
                    string path = ResolveLayoutPath(run);
                    if (!string.IsNullOrWhiteSpace(path))
                        paths.Add(path);
                }
            }

            return paths;
        }

        private static string ResolveLayoutPath(GenerationEvaluationRunRecord run)
        {
            if (!string.IsNullOrWhiteSpace(run.layoutGuid))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(run.layoutGuid);
                if (!string.IsNullOrWhiteSpace(guidPath))
                    return guidPath;
            }

            return run.layoutAssetPath ?? string.Empty;
        }

        private static bool IsExistingLayout(string path) =>
            !string.IsNullOrWhiteSpace(path) && AssetDatabase.LoadAssetAtPath<SavedLayout>(path);

        private static bool IsDeletableEvaluationLayout(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith(ProjectContentPaths.Layouts + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            SavedLayout layout = AssetDatabase.LoadAssetAtPath<SavedLayout>(path);
            return layout &&
                   layout.Locked &&
                   layout.Notes?.StartsWith(EvaluationLayoutMarker, StringComparison.Ordinal) == true;
        }
    }
}
