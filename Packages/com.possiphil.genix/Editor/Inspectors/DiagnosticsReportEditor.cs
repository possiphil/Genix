using System;
using System.Collections.Generic;
using Genix.Editor.Diagnostics;
using Genix.Editor.UI;
using Genix.Diagnostics;
using Genix.Extensions;
using Genix.Sampling;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    /// <summary>Provides the custom Inspector for diagnostics report.</summary>
    [CustomEditor(typeof(DiagnosticsReport))]
    public sealed class DiagnosticsReportEditor : UnityEditor.Editor
    {
        private const float MinimumStatLabelWidth = 190f;
        private const float MaximumStatLabelWidth = 240f;
        private const float StatLabelWidthRatio = 0.38f;

        private enum CandidateEntryDisplayMode
        {
            Tested,
            Accepted,
            Rejected
        }

        private bool _showStyleSettings;
        private bool _showPlacedObjects;
        private bool _showRejectedCandidates;
        private bool _showSceneViewOptions;

        private bool _showRejectedObjects;
        private bool _showGeneratedCandidates;
        private bool _showTestedCandidates;
        private bool _showAcceptedCandidates;
        private bool _showUnusedCandidates;
        private bool _showSupportCandidates;

        internal bool? TechnicalDetailsOverride { private get; set; }

        /// <summary>Draws and applies the custom Inspector interface.</summary>
        public override void OnInspectorGUI()
        {
            DiagnosticsReport report = (DiagnosticsReport)target;
            DiagnosticsPreview.SetReport(report);

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(
                EditorGUIUtility.currentViewWidth * StatLabelWidthRatio,
                MinimumStatLabelWidth,
                MaximumStatLabelWidth);

            try
            {
                DrawReport(report);
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        private void DrawReport(DiagnosticsReport report)
        {
            EditorGUILayout.LabelField(GetReportTitle(report), EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            if (TechnicalDetailsOverride ?? DesignerUiPreferences.IsAdvanced)
            {
                DrawRunSummary(report);
                DrawCandidateSummary(report);

                DrawSceneViewOptions(report);
            }
            else
            {
                DrawOutcomeSummary(report);
            }
        }

        private void DrawOutcomeSummary(DiagnosticsReport report)
        {
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            bool hasRecordedIssue = !string.IsNullOrWhiteSpace(report.StopReason);
            string outcome = report.PlacedObjectCount >= report.RequestedObjectCount
                ? hasRecordedIssue ? "Completed with Issues" : "All Objects Placed"
                : report.PlacedObjectCount > 0 ? "Some Objects Placed" : "No Objects Placed";
            DrawStat("Status", outcome);
            DrawStat("Target Area", report.TargetName);
            DrawStat("Run", report.DryRun ? "Preview" : "Generation");
            DrawStat("Style", report.StyleName);
            DrawStat("Requested Objects", report.RequestedObjectCount.ToString());
            DrawStat(report.DryRun ? "Planned Objects" : "Placed Objects", report.PlacedObjectCount.ToString());

            if (report.RejectionReasons.Count > 0)
            {
                DiagnosticsReport.CountEntry topRejection = report.RejectionReasons[0];
                string advice = RejectionReasonGuidance.GetAdvice(topRejection.Label);
                string label = RejectionReasonGuidance.GetDisplayName(topRejection.Label);
                DrawStat("Main Placement Issue", $"{label} ({topRejection.Count})", advice);
            }

            _showPlacedObjects = DrawFoldoutStat(
                _showPlacedObjects,
                report.DryRun ? "Planned Objects" : "Placed Objects",
                report.PlacedObjectCount.ToString());
            if (_showPlacedObjects)
                DrawCountEntries(
                    report.PlacedObjects,
                    report.DryRun ? "No objects were planned." : "No objects were placed.");

            if (hasRecordedIssue)
                DrawDesignerIssueSummary(report, TechnicalDetailsOverride.HasValue);
        }

        private static void DrawDesignerIssueSummary(
            DiagnosticsReport report,
            bool usesLocalTechnicalDetails)
        {
            string detailsAction = usesLocalTechnicalDetails
                ? "enable Technical Details"
                : "enable Advanced";
            string message = report.PlacedObjectCount >= report.RequestedObjectCount
                ? $"The requested number of objects was placed, but one or more placement requirements could not be completed. Check Main Placement Issue or {detailsAction} for technical details."
                : $"Genix could not place every requested object. Check Main Placement Issue or {detailsAction} for technical details.";
            EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        private void OnDisable()
        {
            DiagnosticsReport report = target as DiagnosticsReport;

            if (report)
                DiagnosticsPreview.ClearIfCurrent(report);
        }

        private void DrawRunSummary(DiagnosticsReport report)
        {
            DrawStat("Created", report.CreatedAt);
            DrawStat("Run ID", ShortenRunId(report.RunId));
            DrawStat("Target Area", report.TargetName);
            DrawStat("Allow Partial Results", report.BestEffort ? "Enabled" : "Disabled");
            DrawStat("Run", report.DryRun ? "Preview" : "Generation");
            DrawStat("Random Seed", report.RandomSeed.ToString());

            if (!string.IsNullOrWhiteSpace(report.PlacementTargets) &&
                !string.Equals(report.PlacementTargets, "None", StringComparison.OrdinalIgnoreCase))
            {
                DrawStat("Placement Targets", report.PlacementTargets);
                DrawStat("Target Distribution", report.TargetDistributionMode);

                if (string.Equals(report.TargetDistributionMode, "Weighted", StringComparison.OrdinalIgnoreCase))
                    DrawStat("Target Weights", report.TargetDistributionWeights);

                if (!string.IsNullOrWhiteSpace(report.RelativeSource))
                {
                    EditorGUILayout.LabelField("Global Proximity", EditorStyles.miniBoldLabel);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawStat("Place Near", report.RelativeSource);
                        DrawStat("Maximum Distance", report.RelativeRadius.ToString("0.##"));
                    }
                }

                DrawTargetBudgetEntries(report.TargetBudgets);
                DrawSupportBudgetEntries(report.SupportBudgets);
            }

            _showStyleSettings = DrawFoldoutStat(
                _showStyleSettings,
                "Generation Style",
                report.StyleName);

            if (_showStyleSettings)
                DrawStyleSettings(report.StyleSettings);

            DrawStat("Requested Objects", report.RequestedObjectCount.ToString());

            _showPlacedObjects = DrawFoldoutStat(
                _showPlacedObjects,
                report.DryRun ? "Planned Objects" : "Placed Objects",
                report.PlacedObjectCount.ToString());

            if (_showPlacedObjects)
                DrawCountEntries(report.PlacedObjects, "No objects placed.");

            _showRejectedObjects = DrawFoldoutStat(
                _showRejectedObjects,
                "Rejected Asset Attempts",
                report.RejectedCandidates.ToString());

            if (_showRejectedObjects)
                DrawCountEntries(
                    report.RejectionReasons,
                    "No rejected attempts.",
                    RejectionReasonGuidance.GetDisplayName);
        }

        private void DrawCandidateSummary(DiagnosticsReport report)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Placement Search", EditorStyles.boldLabel);

            if (report.IsDetailed)
            {
                DrawDetailedCandidateSummary(report);
            }
            else
            {
                DrawSummaryCandidateSummary(report);
            }

            if (!string.IsNullOrWhiteSpace(report.StopReason))
            {
                EditorGUILayout.LabelField("Technical Stop Reason", EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox(report.StopReason, MessageType.Warning);
            }
        }

        private void DrawSupportCandidateSummary(DiagnosticsReport report)
        {
            _showSupportCandidates = DrawFoldoutStat(
                _showSupportCandidates,
                "Support Surface Coverage",
                report.SupportCandidates.Count.ToString());

            if (!_showSupportCandidates)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (DiagnosticsReport.SupportCandidateEntry entry in report.SupportCandidates)
                {
                    DrawStat(
                        entry.Label,
                        $"{entry.CandidateCount} positions / {entry.SurfaceCount} surfaces");
                }
            }
        }

        private static void DrawTargetBudgetEntries(
            IReadOnlyList<DiagnosticsReport.TargetBudgetEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            EditorGUILayout.LabelField("Objects by Placement Target", EditorStyles.miniBoldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (DiagnosticsReport.TargetBudgetEntry entry in entries)
                    DrawStat(
                        entry.Target,
                        $"{entry.PlacedCount}/{entry.TargetCount}",
                        "Placed or planned objects / target objects");
            }
        }

        private static void DrawSupportBudgetEntries(
            IReadOnlyList<DiagnosticsReport.SupportBudgetEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            EditorGUILayout.LabelField("Objects by Support Surface", EditorStyles.miniBoldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (DiagnosticsReport.SupportBudgetEntry entry in entries)
                    DrawStat(
                        entry.Label,
                        $"{entry.PlacedCount}/{entry.TargetCount}",
                        "Placed or planned objects / target objects");
            }
        }

        private void DrawSummaryCandidateSummary(DiagnosticsReport report)
        {
            DrawStat("Candidate Positions", report.GeneratedCandidates.ToString());
            DrawSupportCandidateSummary(report);
            DrawStat("Evaluated Positions", report.TestedCandidateSeeds.ToString());
            DrawStat("Accepted Positions", report.AcceptedPositions > 0
                ? report.AcceptedPositions.ToString()
                : report.AcceptedCandidates.ToString());
            DrawStat("Asset Attempts", report.CandidateAttempts.ToString());
            DrawStat("Accepted Attempts", report.AcceptedCandidates.ToString());
            DrawStat("Rejected Attempts", report.RejectedCandidates.ToString());
            if (report.SupportPrefilterSkips > 0)
                DrawStat("Attempts Skipped by Support Rules", report.SupportPrefilterSkips.ToString());
            DrawStat("Unused Positions", report.UnusedCandidates.ToString());

            if (report.RejectionReasons.Count > 0)
            {
                DiagnosticsReport.CountEntry topRejection = report.RejectionReasons[0];
                string advice = RejectionReasonGuidance.GetAdvice(topRejection.Label);
                string label = RejectionReasonGuidance.GetDisplayName(topRejection.Label);
                DrawStat("Primary Rejection Reason", $"{label} ({topRejection.Count})", advice);
            }
        }

        private void DrawDetailedCandidateSummary(DiagnosticsReport report)
        {
            DrawStat("Evaluated Positions", report.TestedCandidateSeeds.ToString());
            DrawStat("Accepted Positions", report.AcceptedPositions.ToString());
            DrawStat("Rejected Positions", report.RejectedPositions.ToString());
            DrawSupportCandidateSummary(report);
            if (report.SupportPrefilterSkips > 0)
                DrawStat("Attempts Skipped by Support Rules", report.SupportPrefilterSkips.ToString());

            _showGeneratedCandidates = DrawFoldoutStat(
                _showGeneratedCandidates,
                "Candidate Positions",
                report.GeneratedCandidates.ToString());

            if (_showGeneratedCandidates)
                DrawGeneratedCandidates(report);

            _showTestedCandidates = DrawFoldoutStat(
                _showTestedCandidates,
                "Asset Attempts",
                report.CandidateAttempts.ToString());

            if (_showTestedCandidates)
                DrawCandidateEntries(
                    report.CandidateDetails,
                    _ => true,
                    "No asset attempts.",
                    CandidateEntryDisplayMode.Tested);

            _showAcceptedCandidates = DrawFoldoutStat(
                _showAcceptedCandidates,
                "Accepted Attempts",
                report.AcceptedCandidates.ToString());

            if (_showAcceptedCandidates)
                DrawCandidateEntries(
                    report.CandidateDetails,
                    candidate => candidate.Accepted,
                    "No accepted asset attempts.",
                    CandidateEntryDisplayMode.Accepted);

            _showRejectedCandidates = DrawFoldoutStat(
                _showRejectedCandidates,
                "Rejected Attempts",
                report.RejectedCandidates.ToString());

            if (_showRejectedCandidates)
                DrawCandidateEntries(
                    report.CandidateDetails,
                    candidate => !candidate.Accepted,
                    "No rejected asset attempts.",
                    CandidateEntryDisplayMode.Rejected);

            _showUnusedCandidates = DrawFoldoutStat(
                _showUnusedCandidates,
                "Unused Positions",
                report.UnusedCandidates.ToString());

            if (_showUnusedCandidates)
                DrawUnusedCandidates(report);
        }

        private static void DrawGeneratedCandidates(DiagnosticsReport report)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                if (report.SupportsGrid && report.RawSamplePositions.Count > 0)
                {
                    EditorGUILayout.LabelField("Base Grid Positions", EditorStyles.boldLabel);
                    DrawVector3Entries(report.RawSamplePositions, "No base grid positions.");
                    EditorGUILayout.Space(2f);
                }

                if (report.SupportsClusters && report.ClusterCenters.Count > 0)
                {
                    EditorGUILayout.LabelField("Cluster Centers", EditorStyles.boldLabel);
                    DrawVector3Entries(report.ClusterCenters, "No cluster centers.");
                    EditorGUILayout.Space(2f);
                }

                EditorGUILayout.LabelField("All Candidate Positions", EditorStyles.boldLabel);
                DrawVector3Entries(report.CandidateSeeds, "No candidate positions.");
            }
        }

        private static void DrawUnusedCandidates(DiagnosticsReport report)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                int firstUnusedIndex = Mathf.Clamp(
                    report.TestedCandidateSeeds,
                    0,
                    report.CandidateSeeds.Count);

                DrawVector3Entries(
                    report.CandidateSeeds,
                    firstUnusedIndex,
                    report.CandidateSeeds.Count,
                    "No unused positions.");
            }
        }

        private static void DrawVector3Entries(
            IReadOnlyList<Vector3> entries,
            string emptyMessage)
        {
            DrawVector3Entries(entries, 0, entries.Count, emptyMessage);
        }

        private static void DrawVector3Entries(
            IReadOnlyList<Vector3> entries,
            int startIndex,
            int endIndex,
            string emptyMessage)
        {
            int clampedStartIndex = Mathf.Clamp(startIndex, 0, entries.Count);
            int clampedEndIndex = Mathf.Clamp(endIndex, clampedStartIndex, entries.Count);

            if (clampedStartIndex >= clampedEndIndex)
            {
                EditorGUILayout.LabelField(emptyMessage);
                return;
            }

            for (int i = clampedStartIndex; i < clampedEndIndex; i++)
                DrawStat(i.ToString(), FormatVector3(entries[i]));
        }

        private static void DrawCandidateEntries(
            IReadOnlyList<DiagnosticsReport.CandidateEntry> entries,
            Func<DiagnosticsReport.CandidateEntry, bool> include,
            string emptyMessage,
            CandidateEntryDisplayMode displayMode)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                bool hasEntries = false;

                for (int i = 0; i < entries.Count; i++)
                {
                    DiagnosticsReport.CandidateEntry entry = entries[i];

                    if (!include(entry))
                        continue;

                    hasEntries = true;
                    DrawCandidateEntry(entry, i, displayMode);
                }

                if (!hasEntries)
                    EditorGUILayout.LabelField(emptyMessage);
            }
        }

        private static void DrawCandidateEntry(
            DiagnosticsReport.CandidateEntry entry,
            int index,
            CandidateEntryDisplayMode displayMode)
        {
            string title = string.IsNullOrWhiteSpace(entry.ObjectName)
                ? entry.AssetId
                : entry.ObjectName;

            EditorGUILayout.LabelField($"{index}: {title}", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                if (!string.IsNullOrWhiteSpace(entry.ObjectName))
                    DrawStat("Asset", entry.AssetId);

                DrawStat("World Position", FormatVector3(entry.Position));
                DrawStat("World Rotation", FormatVector3(entry.Rotation.eulerAngles));
                DrawStat("World Bounds Center", FormatVector3(entry.Bounds.center));
                DrawStat("World Bounds Size", FormatVector3(entry.Bounds.size));

                if (!string.IsNullOrWhiteSpace(entry.PlacementType))
                    DrawStat("Placement Target", entry.PlacementType);

                DrawCandidateResult(entry, displayMode);
            }

            EditorGUILayout.Space(2f);
        }

        private static void DrawCandidateResult(
            DiagnosticsReport.CandidateEntry entry,
            CandidateEntryDisplayMode displayMode)
        {
            switch (displayMode)
            {
                case CandidateEntryDisplayMode.Tested:
                    DrawStat("Result", entry.Accepted ? "Accepted" : "Rejected");

                    if (!entry.Accepted)
                    {
                        DrawStat("Rejection Reason", RejectionReasonGuidance.GetDisplayName(entry.RejectionReason));
                        DrawRelatedObject(entry);
                    }

                    break;

                case CandidateEntryDisplayMode.Accepted:
                    break;

                case CandidateEntryDisplayMode.Rejected:
                    DrawStat("Rejection Reason", RejectionReasonGuidance.GetDisplayName(entry.RejectionReason));
                    DrawRelatedObject(entry);
                    break;
            }
        }

        private static void DrawRelatedObject(DiagnosticsReport.CandidateEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.RelatedObjectName))
                return;

            DrawStat("Related Object", entry.RelatedObjectName);
        }

        private void DrawSceneViewOptions(DiagnosticsReport report)
        {
            EditorGUILayout.Space(4f);

            _showSceneViewOptions = EditorGUILayout.Foldout(_showSceneViewOptions, "Scene View Overlay", true);

            if (!_showSceneViewOptions)
                return;

            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.IndentLevelScope())
            {
                DiagnosticsPreview.ShowBounds = EditorGUILayout.Toggle(
                    new GUIContent("Show Target Bounds", "Draw the recorded target area's world bounds."),
                    DiagnosticsPreview.ShowBounds);

                if (report.SupportsGrid)
                {
                    DiagnosticsPreview.ShowGrid = EditorGUILayout.Toggle(
                        new GUIContent("Show Sampling Grid", "Draw recorded grid cells for Grid and Jittered Grid sampling."),
                        DiagnosticsPreview.ShowGrid);
                }
                else
                {
                    DiagnosticsPreview.ShowGrid = false;
                }

                DiagnosticsPreview.ShowCandidateSeeds = EditorGUILayout.Toggle(
                    new GUIContent("Show Candidate Positions", "Draw the sampled candidate positions captured in this report."),
                    DiagnosticsPreview.ShowCandidateSeeds);

                DiagnosticsPreview.ShowAccepted = EditorGUILayout.Toggle(
                    new GUIContent("Show Accepted Attempts", "Draw accepted asset placement attempts from this report."),
                    DiagnosticsPreview.ShowAccepted);

                DiagnosticsPreview.ShowRejected = EditorGUILayout.Toggle(
                    new GUIContent("Show Rejected Attempts", "Draw rejected asset placement attempts and their recorded bounds."),
                    DiagnosticsPreview.ShowRejected);

                if (report.SupportsClusters)
                {
                    DiagnosticsPreview.ShowClusters = EditorGUILayout.Toggle(
                        new GUIContent("Show Cluster Regions", "Draw recorded cluster centers and radii for Cluster sampling."),
                        DiagnosticsPreview.ShowClusters);
                }
                else
                {
                    DiagnosticsPreview.ShowClusters = false;
                }
            }

            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();
        }

        private static void DrawStyleSettings(StyleSettings settings)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                DrawStat("Sampling Algorithm", settings.algorithm.ToAlgorithmName());

                if (ShouldDrawCandidateSettings(settings.algorithm))
                    DrawCandidateSettings(settings);

                DrawPlacementSettings(settings);
                DrawRelevantAlgorithmSettings(settings);
            }
        }

        private static bool ShouldDrawCandidateSettings(SamplingAlgorithm algorithm)
        {
            return algorithm is
                SamplingAlgorithm.Random or
                SamplingAlgorithm.Cluster or
                SamplingAlgorithm.BridsonPoissonDisk;
        }

        private static void DrawCandidateSettings(StyleSettings settings)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Search Limits", EditorStyles.boldLabel);

            DrawStat("Candidates per Object", settings.candidates.multiplier.ToString());
            DrawStat("Minimum Candidate Count", settings.candidates.minimumCount.ToString());
            DrawStat("Shuffle Candidates", settings.candidates.shuffle ? "Enabled" : "Disabled");
        }

        private static void DrawPlacementSettings(StyleSettings settings)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Scene Clearance", EditorStyles.boldLabel);

            DrawStat("Avoid Existing Scene Objects", settings.placement.useFixedObjectClearance ? "Enabled" : "Disabled");

            if (settings.placement.useFixedObjectClearance)
                DrawStat("Minimum Distance", settings.placement.fixedObjectDistance.ToString("0.###"));
        }

        private static void DrawRelevantAlgorithmSettings(StyleSettings settings)
        {
            switch (settings.algorithm)
            {
                case SamplingAlgorithm.Grid:
                    DrawGridSettings(settings);
                    break;

                case SamplingAlgorithm.JitteredGrid:
                    DrawJitteredGridSettings(settings);
                    break;

                case SamplingAlgorithm.Cluster:
                    DrawClusterSettings(settings);
                    break;

                case SamplingAlgorithm.BridsonPoissonDisk:
                    DrawPoissonSettings(settings);
                    break;
            }
        }

        private static void DrawGridSettings(StyleSettings settings)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);

            DrawStat("Spacing", settings.grid.cellSize.ToString("0.###"));
        }

        private static void DrawJitteredGridSettings(StyleSettings settings)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Varied Grid", EditorStyles.boldLabel);

            DrawStat("Spacing", settings.grid.cellSize.ToString("0.###"));
            DrawStat("Jitter Amount", settings.grid.jitterAmount.ToString("0.###"));
        }

        private static void DrawClusterSettings(StyleSettings settings)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Clusters", EditorStyles.boldLabel);

            DrawStat("Cluster Count", settings.cluster.count.ToString());
            DrawStat("Cluster Radius", settings.cluster.radius.ToString("0.###"));
            DrawStat("Center Spacing", settings.cluster.useMinCenterDistance ? "Enabled" : "Disabled");

            if (settings.cluster.useMinCenterDistance)
                DrawStat("Minimum Center Distance", settings.cluster.minCenterDistance.ToString("0.###"));
        }

        private static void DrawPoissonSettings(StyleSettings settings)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Even Spacing", EditorStyles.boldLabel);

            DrawStat("Minimum Distance", settings.poisson.minDistance.ToString("0.###"));
            DrawStat("Attempts", settings.poisson.attempts.ToString());
        }

        private static void DrawCountEntries(
            IReadOnlyList<DiagnosticsReport.CountEntry> entries,
            string emptyMessage,
            Func<string, string> formatLabel = null)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                if (entries.Count == 0)
                {
                    EditorGUILayout.LabelField(emptyMessage);
                    return;
                }

                foreach (DiagnosticsReport.CountEntry entry in entries)
                    DrawStat(formatLabel?.Invoke(entry.Label) ?? entry.Label, entry.Count.ToString());
            }
        }

        private static void DrawStat(string label, string value, string tooltip = null)
        {
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), new GUIContent(value, tooltip));
        }

        private static bool DrawFoldoutStat(bool expanded, string label, string value)
        {
            Rect rowRect = EditorGUILayout.GetControlRect();
            Rect indentedRect = EditorGUI.IndentedRect(rowRect);

            float labelWidth = EditorGUIUtility.labelWidth - (indentedRect.x - rowRect.x) + 2f;
            Rect labelRect = new(indentedRect.x, indentedRect.y, labelWidth, indentedRect.height);
            Rect valueRect = new(indentedRect.x + labelWidth, indentedRect.y, indentedRect.width - labelWidth, indentedRect.height);

            expanded = EditorGUI.Foldout(labelRect, expanded, label, true);
            EditorGUI.LabelField(valueRect, value);

            return expanded;
        }

        private static string GetReportTitle(DiagnosticsReport report)
        {
            return report.IsDetailed
                ? "Detailed Diagnostics"
                : "Diagnostics Summary";
        }

        private static string ShortenRunId(string runId)
        {
            if (string.IsNullOrEmpty(runId))
                return "-";

            return runId.Length <= 8 ? runId : runId.Substring(0, 8);
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

    }
}
