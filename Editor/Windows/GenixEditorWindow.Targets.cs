using System;
using System.Collections.Generic;
using Genix.Core;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixEditorWindow
    {
        private void DrawGenerationModeSection()
        {
            _generationMode = GenerationMode.TargetPlacement;
            DrawPlacementTargetDropdown();

            if (_placementTargets == PlacementTarget.None)
                EditorGUILayout.HelpBox("Select at least one placement target: Floor, Wall, Ceiling, or Inside Space.", MessageType.Warning);

            if (GetSelectedTargetCount(_placementTargets) > 1)
            {
                DrawTargetDistributionSection();
            }
            else
            {
                _targetDistributionMode = TargetDistributionMode.Random;
            }

            DrawRelativePlacementSection();
        }

        private PlacementTarget GetEffectivePlacementTargets()
        {
            return NormalizePlacementTargets(_placementTargets);
        }

        private TargetDistributionMode GetEffectiveTargetDistributionMode()
        {
            return _targetDistributionMode;
        }

        private TargetDistributionWeights GetEffectiveTargetDistributionWeights()
        {
            return _targetDistributionWeights;
        }

        private RelativePlacementSettings CreateRelativePlacementSettings()
        {
            if (_relativeSource == RelativePlacementSource.None)
                return RelativePlacementSettings.Disabled;

            IReadOnlyList<Transform> selectedTransforms = _relativeSource == RelativePlacementSource.SelectedObjects
                ? Selection.transforms
                : Array.Empty<Transform>();

            return new RelativePlacementSettings(
                _relativeSource,
                _relativeRadius,
                _relativeSceneLayers,
                selectedTransforms);
        }

        private static PlacementTarget NormalizePlacementTargets(PlacementTarget placementTargets)
        {
            return placementTargets & PlacementTarget.All;
        }

        private void DrawTargetDistributionSection()
        {
            int selectedIndex = Array.IndexOf(TargetDistributionModes, _targetDistributionMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            selectedIndex = EditorGUILayout.Popup("Target Distribution", selectedIndex, TargetDistributionOptions);
            _targetDistributionMode = TargetDistributionModes[Mathf.Clamp(selectedIndex, 0, TargetDistributionModes.Length - 1)];

            if (_targetDistributionMode != TargetDistributionMode.Weighted)
                return;

            DrawTargetWeightFields();

            if (GetActiveTargetWeightSum() <= 0)
                EditorGUILayout.HelpBox("Increase at least one selected target weight.", MessageType.Warning);
        }

        private void DrawTargetWeightFields()
        {
            int floorWeight = _targetDistributionWeights.Floor;
            int wallWeight = _targetDistributionWeights.Wall;
            int ceilingWeight = _targetDistributionWeights.Ceiling;
            int insideSpaceWeight = _targetDistributionWeights.InsideSpace;

            EditorGUI.indentLevel++;

            if ((_placementTargets & PlacementTarget.Floor) != 0)
                floorWeight = Mathf.Max(0, EditorGUILayout.IntField("Floor Weight", floorWeight));

            if ((_placementTargets & PlacementTarget.Wall) != 0)
                wallWeight = Mathf.Max(0, EditorGUILayout.IntField("Wall Weight", wallWeight));

            if ((_placementTargets & PlacementTarget.Ceiling) != 0)
                ceilingWeight = Mathf.Max(0, EditorGUILayout.IntField("Ceiling Weight", ceilingWeight));

            if ((_placementTargets & PlacementTarget.InsideSpace) != 0)
                insideSpaceWeight = Mathf.Max(0, EditorGUILayout.IntField("Inside Space Weight", insideSpaceWeight));

            EditorGUI.indentLevel--;

            _targetDistributionWeights = new TargetDistributionWeights(
                floorWeight,
                wallWeight,
                ceilingWeight,
                insideSpaceWeight);
        }

        private int GetActiveTargetWeightSum()
        {
            int sum = 0;

            if ((_placementTargets & PlacementTarget.Floor) != 0)
                sum += _targetDistributionWeights.Floor;

            if ((_placementTargets & PlacementTarget.Wall) != 0)
                sum += _targetDistributionWeights.Wall;

            if ((_placementTargets & PlacementTarget.Ceiling) != 0)
                sum += _targetDistributionWeights.Ceiling;

            if ((_placementTargets & PlacementTarget.InsideSpace) != 0)
                sum += _targetDistributionWeights.InsideSpace;

            return sum;
        }

        private void DrawRelativePlacementSection()
        {
            int sourceIndex = Array.IndexOf(RelativeSources, _relativeSource);

            if (sourceIndex < 0)
                sourceIndex = 0;

            sourceIndex = EditorGUILayout.Popup("Relative To", sourceIndex, RelativeSourceOptions);
            _relativeSource = RelativeSources[Mathf.Clamp(sourceIndex, 0, RelativeSources.Length - 1)];

            if (_relativeSource == RelativePlacementSource.None)
                return;

            EditorGUI.indentLevel++;

            _relativeRadius = Mathf.Max(0.1f, EditorGUILayout.FloatField("Radius", _relativeRadius));

            if (_relativeSource is RelativePlacementSource.SceneObjects or RelativePlacementSource.Any)
            {
                LayerMask sceneLayers = DrawLayerMaskField("Scene Layers", _relativeSceneLayers);

                if (sceneLayers.value != _relativeSceneLayers.value)
                {
                    _relativeSceneLayers = sceneLayers;
                    EditorPrefs.SetInt(RelativeSceneLayersKey, _relativeSceneLayers.value);
                }
            }

            if (_relativeSource == RelativePlacementSource.SelectedObjects)
            {
                int selectedCount = Selection.transforms.Length;
                EditorGUILayout.LabelField("Selected", $"{selectedCount} object(s)");

                if (selectedCount == 0)
                    EditorGUILayout.HelpBox("Select at least one scene object before generating.", MessageType.Warning);
            }

            EditorGUI.indentLevel--;
        }

        private static int GetSelectedTargetCount(PlacementTarget targets)
        {
            targets = NormalizePlacementTargets(targets);
            int count = 0;

            if ((targets & PlacementTarget.Floor) != 0)
                count++;

            if ((targets & PlacementTarget.Wall) != 0)
                count++;

            if ((targets & PlacementTarget.Ceiling) != 0)
                count++;

            if ((targets & PlacementTarget.InsideSpace) != 0)
                count++;

            return count;
        }

        private void DrawPlacementTargetDropdown()
        {
            _placementTargets = NormalizePlacementTargets(_placementTargets);

            Rect controlRect = EditorGUILayout.GetControlRect();
            Rect dropdownRect = EditorGUI.PrefixLabel(controlRect, new GUIContent("Placement Targets"));

            if (!EditorGUI.DropdownButton(dropdownRect, new GUIContent(GetPlacementTargetLabel(_placementTargets)), FocusType.Keyboard))
                return;

            PopupWindow.Show(
                dropdownRect,
                new PlacementTargetPopup(
                    _placementTargets,
                    dropdownRect.width,
                    SetPlacementTargets));
        }

        private void SetPlacementTargets(PlacementTarget targets)
        {
            _placementTargets = NormalizePlacementTargets(targets);
            Repaint();
        }

        private static string GetPlacementTargetLabel(PlacementTarget targets)
        {
            targets = NormalizePlacementTargets(targets);

            if (targets == PlacementTarget.All)
                return "Any";

            if (targets == PlacementTarget.None)
                return "Select Target";

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

        private sealed class PlacementTargetPopup : PopupWindowContent
        {
            private const float RowHeight = 20f;
            private const float VerticalPadding = 4f;

            private readonly float _width;
            private readonly Action<PlacementTarget> _onChanged;

            private PlacementTarget _targets;

            public PlacementTargetPopup(
                PlacementTarget targets,
                float width,
                Action<PlacementTarget> onChanged)
            {
                _targets = NormalizePlacementTargets(targets);
                _width = width;
                _onChanged = onChanged;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(_width, VerticalPadding * 2f + RowHeight * 6f);
            }

            public override void OnGUI(Rect rect)
            {
                GUILayout.Space(VerticalPadding);

                DrawRow("None", _targets == PlacementTarget.None, SelectNone);
                DrawRow("Any", _targets == PlacementTarget.All, SelectAny);
                DrawTargetRow("Floor", PlacementTarget.Floor);
                DrawTargetRow("Wall", PlacementTarget.Wall);
                DrawTargetRow("Ceiling", PlacementTarget.Ceiling);
                DrawTargetRow("Inside Space", PlacementTarget.InsideSpace);
            }

            private void DrawTargetRow(string label, PlacementTarget target)
            {
                DrawRow(label, (_targets & target) != 0, () => ToggleTarget(target));
            }

            private void SelectAny()
            {
                SetTargets(PlacementTarget.All);
            }

            private void SelectNone()
            {
                SetTargets(PlacementTarget.None);
            }

            private void ToggleTarget(PlacementTarget target)
            {
                PlacementTarget updatedTargets = (_targets & target) != 0
                    ? _targets & ~target
                    : _targets | target;

                SetTargets(updatedTargets);
            }

            private void SetTargets(PlacementTarget targets)
            {
                _targets = NormalizePlacementTargets(targets);
                _onChanged?.Invoke(_targets);
                editorWindow.Repaint();
            }

            private static void DrawRow(string label, bool selected, Action onClick)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight);

                if (rowRect.Contains(Event.current.mousePosition))
                    EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.08f));

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                    onClick?.Invoke();

                Rect checkRect = new(rowRect.x + 6f, rowRect.y, 18f, rowRect.height);
                Rect labelRect = new(rowRect.x + 26f, rowRect.y, rowRect.width - 32f, rowRect.height);

                if (selected)
                    GUI.Label(checkRect, "✓");

                GUI.Label(labelRect, label);
            }
        }

    }
}
