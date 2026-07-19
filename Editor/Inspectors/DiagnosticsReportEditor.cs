using System;
using System.Collections.Generic;
using Genix.Editor.Diagnostics;
using Genix.Diagnostics;
using Genix.Extensions;
using Genix.Sampling;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    [CustomEditor(typeof(DiagnosticsReport))]
    public sealed class DiagnosticsReportEditor : UnityEditor.Editor
    {
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

        public override void OnInspectorGUI()
        {
            DiagnosticsReport report = (DiagnosticsReport)target;
            DiagnosticsPreview.SetReport(report);

            EditorGUILayout.LabelField(GetReportTitle(report), EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawRunSummary(report);
            DrawCandidateSummary(report);
            DrawSceneViewOptions(report);
        }

        private void OnDisable()
        {
            DiagnosticsReport report = target as DiagnosticsReport;

            if (report)
                DiagnosticsPreview.ClearIfCurrent(report);
        }

        private void DrawRunSummary(DiagnosticsReport report)
        {
            DrawStat("Created At", report.CreatedAt);
            DrawStat("Run ID", ShortenRunId(report.RunId));
            DrawStat("Target", report.TargetName);
            DrawStat("Mode", report.GenerationMode);
            DrawStat("Best Effort", report.BestEffort ? "Enabled" : "Disabled");

            if (report.UseRandomSeed)
                DrawStat("Seed", report.RandomSeed.ToString());

            if (!string.IsNullOrWhiteSpace(report.PlacementTargets) &&
                !string.Equals(report.PlacementTargets, "None", StringComparison.OrdinalIgnoreCase))
            {
                DrawStat("Targets", report.PlacementTargets);
                DrawStat("Distribution", report.TargetDistributionMode);

                if (string.Equals(report.TargetDistributionMode, "Weighted", StringComparison.OrdinalIgnoreCase))
                    DrawStat("Weights", report.TargetDistributionWeights);

                if (!string.IsNullOrWhiteSpace(report.RelativeSource))
                {
                    DrawStat("Relative To", report.RelativeSource);
                    DrawStat("Relative Radius", report.RelativeRadius.ToString("0.##"));
                }

                DrawTargetBudgetEntries(report.TargetBudgets);
            }

            _showStyleSettings = DrawFoldoutStat(
                _showStyleSettings,
                "Style",
                report.StyleName);

            if (_showStyleSettings)
                DrawStyleSettings(report.StyleSettings);

            DrawStat("Requested Objects", report.RequestedObjectCount.ToString());

            _showPlacedObjects = DrawFoldoutStat(
                _showPlacedObjects,
                "Placed Objects",
                report.PlacedObjectCount.ToString());

            if (_showPlacedObjects)
                DrawCountEntries(report.PlacedObjects, "No objects placed.");

            _showRejectedObjects = DrawFoldoutStat(
                _showRejectedObjects,
                "Rejected Attempts",
                report.RejectedCandidates.ToString());

            if (_showRejectedObjects)
                DrawCountEntries(report.RejectionReasons, "No rejected attempts.");
        }

        private void DrawCandidateSummary(DiagnosticsReport report)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Candidates", EditorStyles.boldLabel);

            if (report.IsDetailed)
            {
                DrawDetailedCandidateSummary(report);
            }
            else
            {
                DrawSummaryCandidateSummary(report);
            }

            if (!string.IsNullOrWhiteSpace(report.StopReason))
                EditorGUILayout.HelpBox(report.StopReason, MessageType.Warning);
        }

        private static void DrawTargetBudgetEntries(
            IReadOnlyList<DiagnosticsReport.TargetBudgetEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (DiagnosticsReport.TargetBudgetEntry entry in entries)
                    DrawStat(entry.Target, $"{entry.PlacedCount}/{entry.TargetCount}");
            }
        }

        private void DrawSummaryCandidateSummary(DiagnosticsReport report)
        {
            DrawStat("Generated Positions", report.GeneratedCandidates.ToString());
            DrawStat("Tested Positions", report.TestedCandidateSeeds.ToString());
            DrawStat("Accepted Positions", report.AcceptedPositions.ToString());
            DrawStat("Rejected Positions", report.RejectedPositions.ToString());
            DrawStat("Asset Attempts", report.CandidateAttempts.ToString());
            DrawStat("Accepted Attempts", report.AcceptedCandidates.ToString());
            DrawStat("Rejected Attempts", report.RejectedCandidates.ToString());
            DrawStat("Unused Positions", report.UnusedCandidates.ToString());
        }

        private void DrawDetailedCandidateSummary(DiagnosticsReport report)
        {
            DrawStat("Tested Positions", report.TestedCandidateSeeds.ToString());
            DrawStat("Accepted Positions", report.AcceptedPositions.ToString());
            DrawStat("Rejected Positions", report.RejectedPositions.ToString());

            _showGeneratedCandidates = DrawFoldoutStat(
                _showGeneratedCandidates,
                "Generated Positions",
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
                    "No accepted candidates.",
                    CandidateEntryDisplayMode.Accepted);

            _showRejectedCandidates = DrawFoldoutStat(
                _showRejectedCandidates,
                "Rejected Attempts",
                report.RejectedCandidates.ToString());

            if (_showRejectedCandidates)
                DrawCandidateEntries(
                    report.CandidateDetails,
                    candidate => !candidate.Accepted,
                    "No rejected candidates.",
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

                EditorGUILayout.LabelField("Candidate Positions", EditorStyles.boldLabel);
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
                    "No unused candidates.");
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

                DrawStat("Position", FormatVector3(entry.Position));
                DrawStat("Rotation", FormatVector3(entry.Rotation.eulerAngles));
                DrawStat("Bounds Center", FormatVector3(entry.Bounds.center));
                DrawStat("Bounds Size", FormatVector3(entry.Bounds.size));

                if (!string.IsNullOrWhiteSpace(entry.PlacementType))
                    DrawStat("Placement", entry.PlacementType);

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
                        DrawStat("Reason", entry.RejectionReason);
                        DrawRelatedObject(entry);
                    }

                    break;

                case CandidateEntryDisplayMode.Accepted:
                    break;

                case CandidateEntryDisplayMode.Rejected:
                    DrawStat("Reason", entry.RejectionReason);
                    DrawRelatedObject(entry);
                    break;
            }
        }

        private static void DrawRelatedObject(DiagnosticsReport.CandidateEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.RelatedObjectName))
                return;

            DrawStat("Object", entry.RelatedObjectName);
        }

        private void DrawSceneViewOptions(DiagnosticsReport report)
        {
            EditorGUILayout.Space(4f);

            _showSceneViewOptions = EditorGUILayout.Foldout(_showSceneViewOptions, "Scene View", true);

            if (!_showSceneViewOptions)
                return;

            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.IndentLevelScope())
            {
                DiagnosticsPreview.ShowBounds = EditorGUILayout.Toggle(
                    "Show Bounds",
                    DiagnosticsPreview.ShowBounds);

                if (report.SupportsGrid)
                {
                    DiagnosticsPreview.ShowGrid = EditorGUILayout.Toggle(
                        "Show Grid",
                        DiagnosticsPreview.ShowGrid);
                }
                else
                {
                    DiagnosticsPreview.ShowGrid = false;
                }

                DiagnosticsPreview.ShowCandidateSeeds = EditorGUILayout.Toggle(
                    "Show Candidates",
                    DiagnosticsPreview.ShowCandidateSeeds);

                DiagnosticsPreview.ShowAccepted = EditorGUILayout.Toggle(
                    "Show Accepted",
                    DiagnosticsPreview.ShowAccepted);

                DiagnosticsPreview.ShowRejected = EditorGUILayout.Toggle(
                    "Show Rejected",
                    DiagnosticsPreview.ShowRejected);

                if (report.SupportsClusters)
                {
                    DiagnosticsPreview.ShowClusters = EditorGUILayout.Toggle(
                        "Show Clusters",
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
                DrawStat("Algorithm", settings.algorithm.ToAlgorithmName());

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
            EditorGUILayout.LabelField("Candidates", EditorStyles.boldLabel);

            DrawStat("Multiplier", settings.candidates.multiplier.ToString());
            DrawStat("Minimum Count", settings.candidates.minimumCount.ToString());
            DrawStat("Shuffle", settings.candidates.shuffle ? "Enabled" : "Disabled");
        }

        private static void DrawPlacementSettings(StyleSettings settings)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);

            DrawStat("Fixed Clearance", settings.placement.useFixedObjectClearance ? "Enabled" : "Disabled");

            if (settings.placement.useFixedObjectClearance)
                DrawStat("Distance", settings.placement.fixedObjectDistance.ToString("0.###"));
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

            DrawStat("Cell Size", settings.grid.cellSize.ToString("0.###"));
        }

        private static void DrawJitteredGridSettings(StyleSettings settings)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Jittered Grid", EditorStyles.boldLabel);

            DrawStat("Cell Size", settings.grid.cellSize.ToString("0.###"));
            DrawStat("Jitter", settings.grid.jitterAmount.ToString("0.###"));
        }

        private static void DrawClusterSettings(StyleSettings settings)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Cluster", EditorStyles.boldLabel);

            DrawStat("Count", settings.cluster.count.ToString());
            DrawStat("Radius", settings.cluster.radius.ToString("0.###"));
            DrawStat("Center Spacing", settings.cluster.useMinCenterDistance ? "Enabled" : "Disabled");

            if (settings.cluster.useMinCenterDistance)
                DrawStat("Min Distance", settings.cluster.minCenterDistance.ToString("0.###"));
        }

        private static void DrawPoissonSettings(StyleSettings settings)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Poisson Disk", EditorStyles.boldLabel);

            DrawStat("Min Distance", settings.poisson.minDistance.ToString("0.###"));
            DrawStat("Attempts", settings.poisson.attempts.ToString());
        }

        private static void DrawCountEntries(
            IReadOnlyList<DiagnosticsReport.CountEntry> entries,
            string emptyMessage)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                if (entries.Count == 0)
                {
                    EditorGUILayout.LabelField(emptyMessage);
                    return;
                }

                foreach (DiagnosticsReport.CountEntry entry in entries)
                    DrawStat(entry.Label, entry.Count.ToString());
            }
        }

        private static void DrawStat(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
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
                ? "Genix Diagnostics Detailed Report"
                : "Genix Diagnostics Summary";
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
