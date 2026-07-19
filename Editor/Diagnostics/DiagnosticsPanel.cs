using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Windows;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Extensions;
using Genix.Placement;
using Genix.Sampling;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Diagnostics
{
    public sealed class DiagnosticsPanel
    {
        private bool _showDiagnostics = true;
        private bool _showStyleSettings;
        private bool _showPlacedObjects;
        private bool _showRejectedObjects;
        private bool _showSceneViewOptions;

        public void Draw()
        {
            EditorGUILayout.Space(8f);

            _showDiagnostics = EditorGUILayout.Foldout(_showDiagnostics, "Diagnostics", true);

            if (!_showDiagnostics)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GenerationDiagnostics diagnostics = DiagnosticsStore.LastDiagnostics;

                if (diagnostics == null)
                {
                    EditorGUILayout.LabelField("Last Run", "No diagnostics available.");
                    DrawActions(false);
                    return;
                }

                DrawRunSummary(diagnostics);
                DrawCandidateSummary(diagnostics);
                DrawSceneViewOptions(diagnostics);
                DrawActions(true);
            }
        }

        private void DrawRunSummary(GenerationDiagnostics diagnostics)
        {
            EditorGUILayout.LabelField("Last Run", EditorStyles.boldLabel);

            DrawStat("Run ID", ShortenRunId(diagnostics.RunId));
            DrawStat("Target", diagnostics.TargetName);
            DrawStat("Mode", diagnostics.GenerationMode.ToDisplayName());
            DrawStat("Best Effort", diagnostics.BestEffort ? "Enabled" : "Disabled");

            if (diagnostics.UseRandomSeed)
                DrawStat("Seed", diagnostics.RandomSeed.ToString());

            if (diagnostics.RelativePlacement.IsEnabled)
            {
                DrawStat("Relative To", diagnostics.RelativePlacement.Source.ToDisplayName());
                DrawStat("Relative Radius", diagnostics.RelativePlacement.Radius.ToString("0.##"));
            }

            if (diagnostics.GenerationMode == GenerationMode.TargetPlacement)
            {
                DrawStat("Targets", FormatPlacementTargets(diagnostics.PlacementTargets));
                DrawStat("Distribution", diagnostics.TargetDistributionMode.ToDisplayName());

                if (diagnostics.TargetDistributionMode == TargetDistributionMode.Weighted)
                    DrawStat("Weights", FormatTargetWeights(diagnostics.TargetDistributionWeights));

                DrawTargetBudgetSummary(diagnostics);
            }

            _showStyleSettings = DrawFoldoutStat(_showStyleSettings, "Style", GetStyleDisplayName(diagnostics));

            if (_showStyleSettings)
                DrawStyleSettings(diagnostics.StyleSettings);

            DrawStat("Requested Objects", diagnostics.RequestedObjectCount.ToString());

            _showPlacedObjects = DrawFoldoutStat(_showPlacedObjects, "Placed Objects", diagnostics.PlacedObjectCount.ToString());

            if (_showPlacedObjects)
                DrawPlacedObjectSummary(diagnostics);

            int rejectedAttemptCount = diagnostics.Candidates.Count(candidate => !candidate.Accepted);

            _showRejectedObjects = DrawFoldoutStat(_showRejectedObjects, "Rejected Attempts", rejectedAttemptCount.ToString());

            if (_showRejectedObjects)
            {
                DrawRejectionSummary(diagnostics, "No rejected attempts.");
                DrawRejectedAssetSummary(diagnostics);
            }
        }

        private static string GetStyleDisplayName(GenerationDiagnostics diagnostics)
        {
            return string.IsNullOrWhiteSpace(diagnostics.StyleName) ? diagnostics.SamplingAlgorithm.ToAlgorithmName() : diagnostics.StyleName;
        }

        private static void DrawTargetBudgetSummary(GenerationDiagnostics diagnostics)
        {
            if (diagnostics.TargetBudgets.Count == 0)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (TargetBudgetDiagnostic budget in diagnostics.TargetBudgets)
                    DrawStat(budget.PlacementType.ToDisplayName(), $"{budget.PlacedCount}/{budget.TargetCount}");
            }
        }

        private static string FormatPlacementTargets(PlacementTarget targets)
        {
            targets &= PlacementTarget.All;

            if (targets == PlacementTarget.All)
                return "Any";

            if (targets == PlacementTarget.None)
                return "None";

            List<string> labels = new();

            if ((targets & PlacementTarget.Floor) != 0)
                labels.Add("Floor");

            if ((targets & PlacementTarget.Wall) != 0)
                labels.Add("Wall");

            if ((targets & PlacementTarget.Ceiling) != 0)
                labels.Add("Ceiling");

            if ((targets & PlacementTarget.InsideSpace) != 0)
                labels.Add("Inside Space");

            return string.Join(", ", labels);
        }

        private static string FormatTargetWeights(TargetDistributionWeights weights)
        {
            return $"Floor {weights.Floor}, Wall {weights.Wall}, Ceiling {weights.Ceiling}, Inside Space {weights.InsideSpace}";
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
            return algorithm is SamplingAlgorithm.Random or SamplingAlgorithm.Cluster or SamplingAlgorithm.BridsonPoissonDisk;
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

        private static void DrawPlacedObjectSummary(GenerationDiagnostics diagnostics)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                if (diagnostics.Placements.Count == 0)
                {
                    EditorGUILayout.LabelField("No objects placed.");
                    return;
                }

                foreach (IGrouping<string, PlacementDiagnostic> group in diagnostics.Placements.GroupBy(placement => placement.AssetId)
                             .OrderByDescending(group => group.Count()))
                {
                    DrawStat(group.Key, group.Count().ToString());
                }
            }
        }

        private void DrawCandidateSummary(GenerationDiagnostics diagnostics)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Candidates", EditorStyles.boldLabel);

            int testedPositions = diagnostics.Sampler.TestedCandidateSeeds;
            int assetAttempts = diagnostics.Candidates.Count;
            int acceptedAttempts = diagnostics.Candidates.Count(candidate => candidate.Accepted);
            int rejectedAttempts = diagnostics.Candidates.Count(candidate => !candidate.Accepted);
            int unusedPositions = Mathf.Max(0, diagnostics.Sampler.GeneratedCandidates - testedPositions);
            PositionOutcomeCounts positionOutcomes = DiagnosticPositionCounter.Count(diagnostics.Candidates, candidate => candidate.Position, candidate => candidate.Accepted);

            DrawStat("Generated Positions", diagnostics.Sampler.GeneratedCandidates.ToString());
            DrawStat("Tested Positions", testedPositions.ToString());
            DrawStat("Accepted Positions", positionOutcomes.AcceptedPositions.ToString());
            DrawStat("Rejected Positions", positionOutcomes.RejectedPositions.ToString());
            DrawStat("Asset Attempts", assetAttempts.ToString());
            DrawStat("Accepted Attempts", acceptedAttempts.ToString());
            DrawStat("Rejected Attempts", rejectedAttempts.ToString());

            DrawStat("Unused Positions", unusedPositions.ToString());

            if (!string.IsNullOrWhiteSpace(diagnostics.StopReason))
                EditorGUILayout.HelpBox(diagnostics.StopReason, MessageType.Warning);
        }

        private static void DrawRejectionSummary(GenerationDiagnostics diagnostics, string emptyMessage = "No rejected candidates.")
        {
            IEnumerable<IGrouping<RejectionReason, CandidateDiagnostic>> rejectionGroups = diagnostics.Candidates
                .Where(candidate => !candidate.Accepted).GroupBy(candidate => candidate.RejectionReason).OrderByDescending(group => group.Count());

            using (new EditorGUI.IndentLevelScope())
            {
                bool hasRejections = false;

                foreach (IGrouping<RejectionReason, CandidateDiagnostic> group in rejectionGroups)
                {
                    hasRejections = true;
                    DrawStat(group.Key.ToDisplayName(), group.Count().ToString());
                }

                if (!hasRejections)
                    EditorGUILayout.LabelField(emptyMessage);
            }
        }

        private static void DrawRejectedAssetSummary(GenerationDiagnostics diagnostics)
        {
            List<IGrouping<string, CandidateDiagnostic>> rejectedAssetGroups = diagnostics.Candidates.Where(candidate => !candidate.Accepted && !string.IsNullOrWhiteSpace(candidate.AssetId))
                .GroupBy(candidate => candidate.AssetId).OrderByDescending(group => group.Count()).Take(5).ToList();

            if (rejectedAssetGroups.Count == 0)
                return;

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Top Rejected Assets", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (IGrouping<string, CandidateDiagnostic> group in rejectedAssetGroups)
                {
                    string topReason = group.GroupBy(candidate => candidate.RejectionReason).OrderByDescending(reasonGroup => reasonGroup.Count())
                        .Select(reasonGroup => reasonGroup.Key.ToDisplayName()).FirstOrDefault() ?? "Unknown";

                    DrawStat(group.Key, $"{group.Count()} ({topReason})");
                }
            }
        }

        private void DrawSceneViewOptions(GenerationDiagnostics diagnostics)
        {
            EditorGUILayout.Space(4f);

            _showSceneViewOptions = EditorGUILayout.Foldout(_showSceneViewOptions, "Scene View", true);

            if (!_showSceneViewOptions)
                return;

            bool isClustered = diagnostics.SamplingAlgorithm == SamplingAlgorithm.Cluster;
            bool isGridBased = diagnostics.SamplingAlgorithm is SamplingAlgorithm.Grid or SamplingAlgorithm.JitteredGrid;

            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.IndentLevelScope())
            {
                DiagnosticsStore.ShowTargetBounds = EditorGUILayout.Toggle("Show Bounds", DiagnosticsStore.ShowTargetBounds);

                DiagnosticsStore.ShowGrid = isGridBased && EditorGUILayout.Toggle("Show Grid", DiagnosticsStore.ShowGrid);

                DiagnosticsStore.ShowCandidateSeeds = EditorGUILayout.Toggle("Show Candidates", DiagnosticsStore.ShowCandidateSeeds);
                DiagnosticsStore.ShowAcceptedCandidates = EditorGUILayout.Toggle("Show Accepted", DiagnosticsStore.ShowAcceptedCandidates);
                DiagnosticsStore.ShowRejectedCandidates = EditorGUILayout.Toggle("Show Rejected", DiagnosticsStore.ShowRejectedCandidates);

                DiagnosticsStore.ShowClusters = isClustered && EditorGUILayout.Toggle("Show Clusters", DiagnosticsStore.ShowClusters);
            }

            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();
        }

        private static void DrawActions(bool hasDiagnostics)
        {
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(!hasDiagnostics);

                if (GUILayout.Button("Save Summary"))
                    DiagnosticsReportSaver.SaveSummary(DiagnosticsStore.LastDiagnostics);

                if (GUILayout.Button("Save Detailed"))
                    DiagnosticsReportSaver.SaveDetailed(DiagnosticsStore.LastDiagnostics);

                if (GUILayout.Button("Clear"))
                    DiagnosticsStore.Clear();

                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Open Catalog"))
                    DiagnosticsWindow.Open();
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

        private static string ShortenRunId(string runId)
        {
            if (string.IsNullOrEmpty(runId))
                return "-";

            return runId.Length <= 8 ? runId : runId.Substring(0, 8);
        }
    }
}
