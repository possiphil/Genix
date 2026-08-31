using System;
using Genix.Editor.State;
using Genix.Editor.UI;
using Genix.Editor.Utilities;
using Genix.Extensions;
using Genix.Sampling;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Drawers
{
    /// <summary>Draws and validates the editable settings of a generation style preset.</summary>
    public sealed class StyleEditor
    {
        private bool _showPlacementSettings = true;
        private bool _showCandidateSettings = true;
        private bool _showGridSettings = true;
        private bool _showClusterSettings = true;
        private bool _showPoissonSettings = true;

        private const string GreaterThanZero = "must be greater than 0";
        private const string BetweenZeroAndOne = "must be between 0 and 1";

        /// <summary>Draws the control in the current editor layout.</summary>
        public bool Draw(StyleEditState state, Action<string, string, string> onInvalid)
        {
            bool changed = false;
            bool advanced = DesignerUiPreferences.IsAdvanced;

            changed |= DrawBaseSettings(state);
            changed |= DrawPlacementSettings(state, onInvalid);

            switch (state.EditingSettings.algorithm)
            {
                case SamplingAlgorithm.Grid:
                    changed |= DrawGridSettings(state, showJitter: false, onInvalid);
                    break;

                case SamplingAlgorithm.JitteredGrid:
                    changed |= DrawGridSettings(state, showJitter: true, onInvalid);
                    break;

                case SamplingAlgorithm.Random:
                    if (advanced)
                        changed |= DrawCandidateSettings(state, onInvalid);
                    break;

                case SamplingAlgorithm.Cluster:
                    if (advanced)
                        changed |= DrawCandidateSettings(state, onInvalid);
                    changed |= DrawClusterSettings(state, onInvalid, advanced);
                    break;

                case SamplingAlgorithm.BridsonPoissonDisk:
                    if (advanced)
                        changed |= DrawCandidateSettings(state, onInvalid);
                    changed |= DrawPoissonSettings(state, onInvalid, advanced);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(state.EditingSettings.algorithm), state.EditingSettings.algorithm, $"Can't draw algorithm settings: {state.EditingSettings.algorithm.ToAlgorithmName()}");
            }

            return changed;
        }

        private bool DrawPlacementSettings(StyleEditState state, Action<string, string, string> onInvalid)
        {
            return DrawSettingsGroup(ref _showPlacementSettings, EditorGui.ChangedLabel("Scene Clearance", state.HasPlacementSettingsChanged()),
                () => {
                    state.EditingSettings.placement.useFixedObjectClearance = EditorGUILayout.Toggle(
                        Explain(EditorGui.ChangedLabel("Avoid Existing Scene Objects", state.HasPlacementUseFixedObjectClearanceChanged()),
                            "Keep generated objects away from existing non-Genix colliders."),
                        state.EditingSettings.placement.useFixedObjectClearance);

                    if (state.EditingSettings.placement is { useFixedObjectClearance: true, fixedObjectDistance: <= 0f })
                        state.EditingSettings.placement.fixedObjectDistance = 1f;

                    if (state.EditingSettings.placement.useFixedObjectClearance)
                    {
                        state.EditingSettings.placement.fixedObjectDistance = ValidatedField.DrawFloatField(Explain(EditorGui.ChangedLabel("Minimum Distance (units)", state.HasPlacementFixedObjectDistanceChanged()),
                                "Minimum horizontal clearance from existing scene objects."),
                            state.EditingSettings.placement.fixedObjectDistance, state.SavedSettings.placement.fixedObjectDistance, "Fixed Object Distance",
                            value => value > 0f, GreaterThanZero, onInvalid);
                    }
                }
            );
        }

        private static bool DrawBaseSettings(StyleEditState state)
        {
            EditorGUILayout.LabelField(
                Explain(EditorGui.ChangedLabel("Description", state.HasDescriptionChanged()),
                    "Summarize the visual result and when designers should use this style."),
                EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            state.EditingSettings.description = EditorGUILayout.TextArea(state.EditingSettings.description, GUILayout.MinHeight(45));

            EditorGUILayout.Space(4);

            state.EditingSettings.algorithm = (SamplingAlgorithm)EditorGUILayout.EnumPopup(
                Explain(EditorGui.ChangedLabel("Distribution Method", state.HasAlgorithmChanged()),
                    "Choose random, regular, varied-grid, clustered, or evenly spaced placement."),
                state.EditingSettings.algorithm);

            return EditorGUI.EndChangeCheck();
        }

        private bool DrawCandidateSettings(StyleEditState state, Action<string, string, string> onInvalid)
        {
            return DrawSettingsGroup(ref _showCandidateSettings, EditorGui.ChangedLabel("Search Limits", state.HasCandidateSettingsChanged()),
                () => {
                    state.EditingSettings.candidates.multiplier = ValidatedField.DrawIntField(Explain(EditorGui.ChangedLabel("Candidates per Object", state.HasCandidateMultiplierChanged()),
                            "Increase the search effort per requested object. Higher values may fill constrained areas but increase worst-case work."),
                        state.EditingSettings.candidates.multiplier, state.SavedSettings.candidates.multiplier, "Candidate Multiplier",
                        value => value > 0, GreaterThanZero, onInvalid);

                    state.EditingSettings.candidates.minimumCount = ValidatedField.DrawIntField(Explain(EditorGui.ChangedLabel("Minimum Candidate Count", state.HasMinimumCandidatesChanged()),
                            "Keep a useful search budget when generating only a few objects."),
                        state.EditingSettings.candidates.minimumCount, state.SavedSettings.candidates.minimumCount, "Minimum Candidates",
                        value => value > 0, GreaterThanZero, onInvalid);

                    state.EditingSettings.candidates.shuffle = EditorGUILayout.Toggle(Explain(EditorGui.ChangedLabel("Shuffle Candidates", state.HasShuffleCandidatesChanged()),
                            "Randomize candidate evaluation order. Disable when the sampler's natural traversal order is desired."),
                        state.EditingSettings.candidates.shuffle);
                }
            );
        }

        private bool DrawGridSettings(StyleEditState state, bool showJitter, Action<string, string, string> onInvalid)
        {
            return DrawSettingsGroup(ref _showGridSettings, EditorGui.ChangedLabel(showJitter ? "Varied Grid" : "Grid", state.HasGridSettingsChanged()),
                () => {
                    state.EditingSettings.grid.cellSize = ValidatedField.DrawFloatField(Explain(EditorGui.ChangedLabel("Spacing (units)", state.HasGridCellSizeChanged()),
                            "Distance between neighboring grid sample positions."),
                        state.EditingSettings.grid.cellSize, state.SavedSettings.grid.cellSize, "Cell Size",
                        value => value > 0f, GreaterThanZero, onInvalid);

                    if (showJitter)
                        state.EditingSettings.grid.jitterAmount = ValidatedField.DrawFloatField(Explain(EditorGui.ChangedLabel("Jitter Amount", state.HasGridJitterChanged()),
                            "Random offset as a fraction of grid spacing. Zero keeps a regular grid; one uses the full cell range."),
                            state.EditingSettings.grid.jitterAmount, state.SavedSettings.grid.jitterAmount, "Jitter Amount",
                            value => value >= 0f && value <= 1f, BetweenZeroAndOne, onInvalid);
                    else
                        state.EditingSettings.grid.jitterAmount = 0f;
                }
            );
        }

        private bool DrawClusterSettings(
            StyleEditState state,
            Action<string, string, string> onInvalid,
            bool advanced)
        {
            return DrawSettingsGroup(ref _showClusterSettings, EditorGui.ChangedLabel("Clusters", state.HasClusterSettingsChanged()),
                () => {
                    state.EditingSettings.cluster.count = ValidatedField.DrawIntField(Explain(EditorGui.ChangedLabel("Cluster Count", state.HasClusterCountChanged()),
                            "Number of cluster centers used to group candidate positions."),
                        state.EditingSettings.cluster.count, state.SavedSettings.cluster.count, "Cluster Count",
                        value => value > 0, GreaterThanZero, onInvalid);

                    state.EditingSettings.cluster.radius = ValidatedField.DrawFloatField(Explain(EditorGui.ChangedLabel("Cluster Radius (units)", state.HasClusterRadiusChanged()),
                            "Maximum horizontal distance of candidates from their cluster center."),
                        state.EditingSettings.cluster.radius, state.SavedSettings.cluster.radius, "Cluster Radius",
                        value => value > 0f, GreaterThanZero, onInvalid);

                    if (advanced)
                    {
                        state.EditingSettings.cluster.useMinCenterDistance = EditorGUILayout.Toggle(Explain(EditorGui.ChangedLabel("Center Spacing", state.HasClusterUseMinCenterDistanceChanged()),
                                "Require separation between cluster centers to avoid merged clusters."),
                            state.EditingSettings.cluster.useMinCenterDistance);

                        if (state.EditingSettings.cluster is { useMinCenterDistance: true, minCenterDistance: <= 0f })
                            state.EditingSettings.cluster.minCenterDistance = 5f;

                        if (state.EditingSettings.cluster.useMinCenterDistance)
                        {
                            state.EditingSettings.cluster.minCenterDistance = ValidatedField.DrawFloatField(Explain(EditorGui.ChangedLabel("Minimum Center Distance (units)", state.HasClusterMinCenterDistanceChanged()),
                                    "Minimum horizontal distance between cluster centers."),
                                state.EditingSettings.cluster.minCenterDistance, state.SavedSettings.cluster.minCenterDistance, "Cluster Center Min Distance",
                                value => value > 0f, GreaterThanZero, onInvalid);
                        }
                    }
                }
            );
        }

        private bool DrawPoissonSettings(
            StyleEditState state,
            Action<string, string, string> onInvalid,
            bool advanced)
        {
            return DrawSettingsGroup(ref _showPoissonSettings, EditorGui.ChangedLabel("Even Spacing", state.HasPoissonSettingsChanged()),
                () => {
                    state.EditingSettings.poisson.minDistance = ValidatedField.DrawFloatField(Explain(EditorGui.ChangedLabel("Minimum Distance (units)", state.HasPoissonMinDistanceChanged()),
                            "Minimum object spacing. Floor and ceiling use horizontal distance; wall and inside-space use full 3D distance."),
                        state.EditingSettings.poisson.minDistance, state.SavedSettings.poisson.minDistance, "Min Distance",
                        value => value > 0f, GreaterThanZero, onInvalid);

                    if (advanced)
                    {
                        state.EditingSettings.poisson.attempts = ValidatedField.DrawIntField(Explain(EditorGui.ChangedLabel("Attempts", state.HasPoissonAttemptsChanged()),
                                "Attempts around each active sample before it is retired. Higher values fill difficult regions more thoroughly."),
                            state.EditingSettings.poisson.attempts, state.SavedSettings.poisson.attempts, "Attempts",
                            value => value > 0, GreaterThanZero, onInvalid);
                    }
                }
            );
        }

        private static bool DrawSettingsGroup(ref bool isExpanded, GUIContent label, Action drawContent)
        {
            EditorGUILayout.Space(4);

            isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(isExpanded, label);
            if (!isExpanded)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return false;
            }

            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            drawContent();
            bool changed = EditorGUI.EndChangeCheck();

            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();

            return changed;
        }

        private static GUIContent Explain(GUIContent content, string tooltip)
        {
            content.tooltip = tooltip;
            return content;
        }
    }
}
