using System;
using Genix.Editor.State;
using Genix.Editor.Utilities;
using Genix.Extensions;
using Genix.Sampling;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Drawers
{
    public sealed class StyleEditor
    {
        private bool _showPlacementSettings = true;
        private bool _showCandidateSettings = true;
        private bool _showGridSettings = true;
        private bool _showClusterSettings = true;
        private bool _showPoissonSettings = true;

        private const string GreaterThanZero = "must be greater than 0";
        private const string BetweenZeroAndOne = "must be between 0 and 1";

        public bool Draw(StyleEditState state, Action<string, string, string> onInvalid)
        {
            bool changed = false;

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
                    changed |= DrawCandidateSettings(state, onInvalid);
                    break;

                case SamplingAlgorithm.Cluster:
                    changed |= DrawCandidateSettings(state, onInvalid);
                    changed |= DrawClusterSettings(state, onInvalid);
                    break;

                case SamplingAlgorithm.BridsonPoissonDisk:
                    changed |= DrawCandidateSettings(state, onInvalid);
                    changed |= DrawPoissonSettings(state, onInvalid);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(state.EditingSettings.algorithm), state.EditingSettings.algorithm, $"Can't draw algorithm settings: {state.EditingSettings.algorithm.ToAlgorithmName()}");
            }

            return changed;
        }

        private bool DrawPlacementSettings(StyleEditState state, Action<string, string, string> onInvalid)
        {
            return DrawSettingsGroup(ref _showPlacementSettings, EditorGui.ChangedLabel("Placement Settings", state.HasPlacementSettingsChanged()),
                () => {
                    state.EditingSettings.placement.useFixedObjectClearance = EditorGUILayout.Toggle(
                        EditorGui.ChangedLabel("Fixed Clearance", state.HasPlacementUseFixedObjectClearanceChanged()),
                        state.EditingSettings.placement.useFixedObjectClearance);

                    if (state.EditingSettings.placement is { useFixedObjectClearance: true, fixedObjectDistance: <= 0f })
                        state.EditingSettings.placement.fixedObjectDistance = 1f;

                    if (state.EditingSettings.placement.useFixedObjectClearance)
                    {
                        state.EditingSettings.placement.fixedObjectDistance = ValidatedField.DrawFloatField(EditorGui.ChangedLabel("Min Distance", state.HasPlacementFixedObjectDistanceChanged()),
                            state.EditingSettings.placement.fixedObjectDistance, state.SavedSettings.placement.fixedObjectDistance, "Fixed Object Distance",
                            value => value > 0f, GreaterThanZero, onInvalid);
                    }
                }
            );
        }

        private static bool DrawBaseSettings(StyleEditState state)
        {
            EditorGUILayout.LabelField(EditorGui.ChangedLabel("Description", state.HasDescriptionChanged()), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            state.EditingSettings.description = EditorGUILayout.TextArea(state.EditingSettings.description, GUILayout.MinHeight(45));

            EditorGUILayout.Space(4);

            state.EditingSettings.algorithm = (SamplingAlgorithm)EditorGUILayout.EnumPopup(EditorGui.ChangedLabel("Algorithm", state.HasAlgorithmChanged()), state.EditingSettings.algorithm);

            return EditorGUI.EndChangeCheck();
        }

        private bool DrawCandidateSettings(StyleEditState state, Action<string, string, string> onInvalid)
        {
            return DrawSettingsGroup(ref _showCandidateSettings, EditorGui.ChangedLabel("Candidate Settings", state.HasCandidateSettingsChanged()),
                () => {
                    state.EditingSettings.candidates.multiplier = ValidatedField.DrawIntField(EditorGui.ChangedLabel("Candidate Multiplier", state.HasCandidateMultiplierChanged()),
                        state.EditingSettings.candidates.multiplier, state.SavedSettings.candidates.multiplier, "Candidate Multiplier",
                        value => value > 0, GreaterThanZero, onInvalid);

                    state.EditingSettings.candidates.minimumCount = ValidatedField.DrawIntField(EditorGui.ChangedLabel("Minimum Candidates", state.HasMinimumCandidatesChanged()),
                        state.EditingSettings.candidates.minimumCount, state.SavedSettings.candidates.minimumCount, "Minimum Candidates",
                        value => value > 0, GreaterThanZero, onInvalid);

                    state.EditingSettings.candidates.shuffle = EditorGUILayout.Toggle(EditorGui.ChangedLabel("Shuffle Candidates", state.HasShuffleCandidatesChanged()),
                        state.EditingSettings.candidates.shuffle);
                }
            );
        }

        private bool DrawGridSettings(StyleEditState state, bool showJitter, Action<string, string, string> onInvalid)
        {
            return DrawSettingsGroup(ref _showGridSettings, EditorGui.ChangedLabel("Grid Settings", state.HasGridSettingsChanged()),
                () => {
                    state.EditingSettings.grid.cellSize = ValidatedField.DrawFloatField(EditorGui.ChangedLabel("Cell Size", state.HasGridCellSizeChanged()),
                        state.EditingSettings.grid.cellSize, state.SavedSettings.grid.cellSize, "Cell Size",
                        value => value > 0f, GreaterThanZero, onInvalid);

                    if (showJitter)
                        state.EditingSettings.grid.jitterAmount = ValidatedField.DrawFloatField(EditorGui.ChangedLabel("Jitter Amount", state.HasGridJitterChanged()),
                            state.EditingSettings.grid.jitterAmount, state.SavedSettings.grid.jitterAmount, "Jitter Amount",
                            value => value >= 0f && value <= 1f, BetweenZeroAndOne, onInvalid);
                    else
                        state.EditingSettings.grid.jitterAmount = 0f;
                }
            );
        }

        private bool DrawClusterSettings(StyleEditState state, Action<string, string, string> onInvalid)
        {
            return DrawSettingsGroup(ref _showClusterSettings, EditorGui.ChangedLabel("Cluster Settings", state.HasClusterSettingsChanged()),
                () => {
                    state.EditingSettings.cluster.count = ValidatedField.DrawIntField(EditorGui.ChangedLabel("Cluster Count", state.HasClusterCountChanged()),
                        state.EditingSettings.cluster.count, state.SavedSettings.cluster.count, "Cluster Count",
                        value => value > 0, GreaterThanZero, onInvalid);

                    state.EditingSettings.cluster.radius = ValidatedField.DrawFloatField(EditorGui.ChangedLabel("Cluster Radius", state.HasClusterRadiusChanged()),
                        state.EditingSettings.cluster.radius, state.SavedSettings.cluster.radius, "Cluster Radius",
                        value => value > 0f, GreaterThanZero, onInvalid);

                    state.EditingSettings.cluster.useMinCenterDistance = EditorGUILayout.Toggle(EditorGui.ChangedLabel("Center Spacing", state.HasClusterUseMinCenterDistanceChanged()),
                        state.EditingSettings.cluster.useMinCenterDistance);

                    if (state.EditingSettings.cluster is { useMinCenterDistance: true, minCenterDistance: <= 0f })
                        state.EditingSettings.cluster.minCenterDistance = 5f;

                    if (state.EditingSettings.cluster.useMinCenterDistance)
                    {
                        state.EditingSettings.cluster.minCenterDistance = ValidatedField.DrawFloatField(EditorGui.ChangedLabel("Min Distance", state.HasClusterMinCenterDistanceChanged()),
                            state.EditingSettings.cluster.minCenterDistance, state.SavedSettings.cluster.minCenterDistance, "Cluster Center Min Distance",
                            value => value > 0f, GreaterThanZero, onInvalid);
                    }
                }
            );
        }

        private bool DrawPoissonSettings(StyleEditState state, Action<string, string, string> onInvalid)
        {
            return DrawSettingsGroup(ref _showPoissonSettings, EditorGui.ChangedLabel("Poisson Settings", state.HasPoissonSettingsChanged()),
                () => {
                    state.EditingSettings.poisson.minDistance = ValidatedField.DrawFloatField(EditorGui.ChangedLabel("Min Distance", state.HasPoissonMinDistanceChanged()),
                        state.EditingSettings.poisson.minDistance, state.SavedSettings.poisson.minDistance, "Min Distance",
                        value => value > 0f, GreaterThanZero, onInvalid);

                    state.EditingSettings.poisson.attempts = ValidatedField.DrawIntField(EditorGui.ChangedLabel("Attempts", state.HasPoissonAttemptsChanged()),
                        state.EditingSettings.poisson.attempts, state.SavedSettings.poisson.attempts, "Attempts",
                        value => value > 0, GreaterThanZero, onInvalid);
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
    }
}