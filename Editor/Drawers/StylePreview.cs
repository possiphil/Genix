using System;
using Genix.Editor.Utilities;
using Genix.Extensions;
using Genix.Sampling;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Drawers
{
    public sealed class StylePreview
    {
        private bool _showTechnicalDetails;

        private bool _showPlacementSettings = true;
        private bool _showCandidateSettings = true;
        private bool _showGridSettings = true;
        private bool _showClusterSettings = true;
        private bool _showPoissonSettings = true;

        public void Draw(StylePreset preset)
        {
            if (!preset)
                return;

            Draw(preset.Settings);
        }

        public void Draw(StyleSettings settings)
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(settings.description, EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(4);

            _showTechnicalDetails = EditorGUILayout.Foldout(_showTechnicalDetails, "Technical Details");
            if (!_showTechnicalDetails)
                return;

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Algorithm", settings.algorithm.ToAlgorithmName());

            DrawPlacementSettings(settings);
            DrawRelevantAlgorithmSettings(settings);

            EditorGUI.indentLevel--;
        }

        private void DrawPlacementSettings(StyleSettings settings)
        {
            DrawPreviewGroup(ref _showPlacementSettings, "Placement Settings",
                () => {
                    EditorGUILayout.LabelField("Fixed Clearance", settings.placement.useFixedObjectClearance ? "Enabled" : "Disabled");

                    if (settings.placement.useFixedObjectClearance)
                        EditorGUILayout.LabelField("Min Distance", settings.placement.fixedObjectDistance.ToString("0.###"));
                }
            );
        }

        private void DrawRelevantAlgorithmSettings(StyleSettings settings)
        {
            switch (settings.algorithm)
            {
                case SamplingAlgorithm.Random:
                    DrawCandidateSettings(settings);
                    break;

                case SamplingAlgorithm.Grid:
                    DrawGridSettings(settings, showJitter: false);
                    break;

                case SamplingAlgorithm.JitteredGrid:
                    DrawGridSettings(settings, showJitter: true);
                    break;

                case SamplingAlgorithm.Cluster:
                    DrawCandidateSettings(settings);
                    DrawClusterSettings(settings);
                    break;

                case SamplingAlgorithm.BridsonPoissonDisk:
                    DrawCandidateSettings(settings);
                    DrawPoissonSettings(settings);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(settings.algorithm), settings.algorithm, $"Can't draw relevant settings from algorithm: {settings.algorithm.ToAlgorithmName()}.");
            }
        }

        private void DrawCandidateSettings(StyleSettings settings)
        {
            DrawPreviewGroup(ref _showCandidateSettings, "Candidate Settings",
                () => {
                    EditorGUILayout.LabelField("Candidate Multiplier", settings.candidates.multiplier.ToString());
                    EditorGUILayout.LabelField("Minimum Candidates", settings.candidates.minimumCount.ToString());
                    EditorGUILayout.LabelField("Shuffle Candidates", settings.candidates.shuffle ? "Enabled" : "Disabled");
                }
            );
        }

        private void DrawGridSettings(StyleSettings settings, bool showJitter)
        {
            DrawPreviewGroup(ref _showGridSettings, "Grid Settings",
                () => {
                    EditorGUILayout.LabelField("Cell Size", settings.grid.cellSize.ToString("0.###"));

                    if (showJitter)
                        EditorGUILayout.LabelField("Jitter Amount", settings.grid.jitterAmount.ToString("0.###"));
                }
            );
        }

        private void DrawClusterSettings(StyleSettings settings)
        {
            DrawPreviewGroup(ref _showClusterSettings, "Cluster Settings",
                () => {
                    EditorGUILayout.LabelField("Cluster Count", settings.cluster.count.ToString());
                    EditorGUILayout.LabelField("Cluster Radius", settings.cluster.radius.ToString("0.###"));

                    EditorGUILayout.LabelField("Center Spacing", settings.cluster.useMinCenterDistance ? "Enabled" : "Disabled");
                    if (settings.cluster.useMinCenterDistance)
                        EditorGUILayout.LabelField("Min Distance", settings.cluster.minCenterDistance.ToString("0.###"));
                }
            );
        }

        private void DrawPoissonSettings(StyleSettings settings)
        {
            DrawPreviewGroup(ref _showPoissonSettings, "Poisson Settings",
                () => {
                    EditorGUILayout.LabelField("Min Distance", settings.poisson.minDistance.ToString("0.###"));
                    EditorGUILayout.LabelField("Attempts", settings.poisson.attempts.ToString());
                }
            );
        }

        private static void DrawPreviewGroup(ref bool isExpanded, string label, Action drawContent)
        {
            EditorGUILayout.Space(4);

            isExpanded = EditorGui.DrawIndentedFoldout(isExpanded, label);
            if (!isExpanded)
                return;

            EditorGUI.indentLevel++;
            drawContent();
            EditorGUI.indentLevel--;
        }
    }
}
