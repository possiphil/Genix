using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Core;
using Genix.Editor.Assets;
using Genix.Editor.Generation;
using Genix.Editor.UI;
using Genix.Editor.Utilities;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixEditorWindow
    {
        private static GUIStyle _supportPercentageStyle;

        private static GUIStyle SupportPercentageStyle => _supportPercentageStyle ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleRight
        };

        private void DrawPlacementSettingsSection()
        {
            DrawPlacementTargetDropdown();

            if (_placementTargets == PlacementTarget.None)
                EditorGUILayout.HelpBox("Select at least one placement target: Floor, Wall, Ceiling, or Inside Space.", MessageType.Warning);

            if (GetSelectedTargetCount(_placementTargets) <= 1)
                _targetDistributionMode = TargetDistributionMode.Random;
        }

        private void DrawAdvancedDistributionSettings()
        {
            if (GetSelectedTargetCount(_placementTargets) > 1)
                DrawTargetDistributionSection();

            DrawSupportDistributionSection();
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
            return PlacementTargetSelectionField.Normalize(placementTargets);
        }

        private void DrawTargetDistributionSection()
        {
            int selectedIndex = Array.IndexOf(TargetDistributionModes, _targetDistributionMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            selectedIndex = EditorGui.Popup(
                new GUIContent("Target Distribution", "Choose how the requested count is divided across Floor, Wall, Ceiling, and Inside Space."),
                selectedIndex,
                TargetDistributionOptions);
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
                floorWeight = DrawTargetWeight("Floor Weight", floorWeight);

            if ((_placementTargets & PlacementTarget.Wall) != 0)
                wallWeight = DrawTargetWeight("Wall Weight", wallWeight);

            if ((_placementTargets & PlacementTarget.Ceiling) != 0)
                ceilingWeight = DrawTargetWeight("Ceiling Weight", ceilingWeight);

            if ((_placementTargets & PlacementTarget.InsideSpace) != 0)
                insideSpaceWeight = DrawTargetWeight("Inside Space Weight", insideSpaceWeight);

            EditorGUI.indentLevel--;

            _targetDistributionWeights = new TargetDistributionWeights(
                floorWeight,
                wallWeight,
                ceilingWeight,
                insideSpaceWeight);
        }

        private static int DrawTargetWeight(string label, int value)
        {
            return Mathf.Max(0, EditorGUILayout.IntField(
                new GUIContent(label, "Relative share for this target. A weight of zero disables the target while Weighted distribution is active."),
                value));
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

        private void DrawSupportDistributionSection()
        {
            bool canAddSupportRule = GetFirstUnusedSupportTag();
            Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            const float addRuleWidth = 70f;
            Rect addRuleRect = new(
                rowRect.xMax - addRuleWidth,
                rowRect.y,
                addRuleWidth,
                rowRect.height);
            Rect toggleRect = new(
                rowRect.x,
                rowRect.y,
                Mathf.Max(0f, addRuleRect.x - RunButtonGap - rowRect.x),
                rowRect.height);

            _supportDistributionEnabled = EditorGUI.Toggle(
                toggleRect,
                new GUIContent(
                    "Support Distribution",
                    "Divide accepted placements across tagged support surfaces. Unlisted surfaces use the fallback weight below."),
                _supportDistributionEnabled);

            using (new EditorGUI.DisabledScope(!_supportDistributionEnabled || !canAddSupportRule))
            {
                if (GUI.Button(
                        addRuleRect,
                        new GUIContent("Add Rule", "Control one tagged kind of support surface."),
                        EditorStyles.miniButton))
                {
                    AddSupportDistributionRule();
                }
            }

            if (!_supportDistributionEnabled)
                return;

            EditorGUI.indentLevel++;
            int removeIndex = -1;
            int[] allocatedTargets = GetSupportDistributionTargets();
            for (int i = 0; i < _supportDistributionRules.Count; i++)
            {
                SupportDistributionRule rule = _supportDistributionRules[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    SemanticTag selectedTag;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        selectedTag = DrawSupportRuleTag(rule?.SupportTag);
                        if (GUILayout.Button(new GUIContent("×", "Remove this support rule."), EditorStyles.miniButton, GUILayout.Width(22f)))
                            removeIndex = i;
                    }

                    if (!selectedTag)
                    {
                        EditorGUILayout.HelpBox(
                            "Missing Support Tag. Remove this rule and add a replacement after creating an eligible support tag.",
                            MessageType.Warning);
                    }

                    int count = rule?.Mode == SupportDistributionRuleMode.ExactCount
                        ? rule.Value
                        : allocatedTargets[i];

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        count = Mathf.Max(0, EditorGUILayout.IntField(
                            new GUIContent("Count", "Number of requested placements assigned to this support tag."),
                            count));
                        bool countChanged = EditorGUI.EndChangeCheck();

                        float share = GetSupportShare(count);
                        EditorGUI.BeginChangeCheck();
                        share = EditorGUILayout.FloatField(
                            new GUIContent(
                                "Share (%)",
                                "Percentage view of Count. Editing it rounds Count to the nearest whole placement; halfway values round up."),
                            share);
                        bool shareChanged = EditorGUI.EndChangeCheck();

                        if (!countChanged && shareChanged)
                            count = GetSupportCount(share);
                    }

                    if (selectedTag != rule?.SupportTag ||
                        rule?.Mode != SupportDistributionRuleMode.ExactCount ||
                        count != rule?.Value)
                    {
                        rule = new SupportDistributionRule(
                            selectedTag,
                            SupportDistributionRuleMode.ExactCount,
                            count);
                        _supportDistributionRules[i] = rule;
                    }
                }
            }

            if (removeIndex >= 0)
                _supportDistributionRules.RemoveAt(removeIndex);

            Rect fallbackRow = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.PrefixLabel(
                fallbackRow,
                new GUIContent(
                    "All Unlisted Surfaces",
                    "Automatically receives every placement not assigned to a listed support tag."));

            const float percentageWidth = 74f;
            Rect percentageRect = new(
                fallbackRow.xMax - percentageWidth,
                fallbackRow.y,
                percentageWidth,
                fallbackRow.height);
            int[] currentTargets = GetSupportDistributionTargets();
            int unlistedCount = currentTargets[currentTargets.Length - 1];
            EditorGUI.LabelField(
                percentageRect,
                $"{GetSupportShare(unlistedCount):0.#}%",
                SupportPercentageStyle);

            List<SemanticTag> duplicates = _supportDistributionRules
                .Where(rule => rule?.SupportTag)
                .GroupBy(rule => rule.SupportTag)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicates.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Each support tag may appear once. Duplicates: {string.Join(", ", duplicates.Select(tag => tag.DisplayName))}.",
                    MessageType.Warning);
            }

            int exactTotal = _supportDistributionRules
                .Where(rule => rule?.IsConfigured == true && rule.Mode == SupportDistributionRuleMode.ExactCount)
                .Sum(rule => rule.Value);
            if (exactTotal > _objectCount)
            {
                EditorGUILayout.HelpBox(
                    $"Support counts request {exactTotal} objects, but the run requests only {_objectCount}.",
                    MessageType.Warning);
            }

            EditorGUI.indentLevel--;
        }

        private void AddSupportDistributionRule()
        {
            SemanticTag supportTag = GetFirstUnusedSupportTag();

            if (!supportTag)
                return;

            _supportDistributionRules.Add(new SupportDistributionRule(
                supportTag,
                SupportDistributionRuleMode.ExactCount,
                0));
        }

        private int[] GetSupportDistributionTargets() => SupportDistributionAllocator.Allocate(
            _objectCount,
            _supportDistributionRules,
            Mathf.Max(1, _defaultSupportWeight));

        private float GetSupportShare(int count) => _objectCount > 0
            ? Mathf.Max(0, count) * 100f / _objectCount
            : 0f;

        private int GetSupportCount(float share) =>
            EditorGui.RoundPercentageToCount(_objectCount, share);

        private static SemanticTag DrawSupportRuleTag(SemanticTag selected)
        {
            List<SemanticTag> tags = GetSupportTags();
            int index = selected ? tags.IndexOf(selected) : -1;
            GUIContent label = new(
                "Support Tag",
                "Choose the tagged support surfaces controlled by this rule.");

            if (index < 0)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGui.Popup(label, 0, new[] { "Missing Support Tag" });

                return null;
            }

            string[] options = tags
                .Select(tag => $"{tag.Category.DisplayName} / {tag.DisplayName}")
                .ToArray();
            index = EditorGui.Popup(label, index, options);
            return tags[Mathf.Clamp(index, 0, tags.Count - 1)];
        }

        private static List<SemanticTag> GetSupportTags()
        {
            return AssetCatalogService.GetOrCreate().Tags
                .Where(tag => tag && tag.SupportsSurfaces)
                .OrderBy(tag => tag.Category.DisplayName)
                .ThenBy(tag => tag.DisplayName)
                .ToList();
        }

        private SemanticTag GetFirstUnusedSupportTag()
        {
            HashSet<SemanticTag> used = _supportDistributionRules
                .Where(rule => rule?.SupportTag)
                .Select(rule => rule.SupportTag)
                .ToHashSet();
            return GetSupportTags()
                .Where(tag => !used.Contains(tag))
                .FirstOrDefault();
        }

        private SupportDistributionSettings CreateSupportDistributionSettings() => new(
            _supportDistributionEnabled,
            Mathf.Max(1, _defaultSupportWeight),
            _supportDistributionRules);

        private void ApplySupportDistributionSettings(SupportDistributionSettings settings)
        {
            settings ??= SupportDistributionSettings.Disabled;
            _supportDistributionEnabled = settings.IsEnabled;
            _defaultSupportWeight = Mathf.Max(1, settings.DefaultWeight);
            _supportDistributionRules.Clear();
            _supportDistributionRules.AddRange(settings.Rules.Select(rule => rule.Copy()));
        }

        private void DrawRelativePlacementSection()
        {
            int sourceIndex = Array.IndexOf(RelativeSources, _relativeSource);

            if (sourceIndex < 0)
                sourceIndex = 0;

            sourceIndex = EditorGui.Popup(
                new GUIContent("Place Near", "Require every placement to stay near an eligible object's bounds."),
                sourceIndex,
                RelativeSourceOptions);
            _relativeSource = RelativeSources[Mathf.Clamp(sourceIndex, 0, RelativeSources.Length - 1)];

            if (_relativeSource == RelativePlacementSource.None)
                return;

            EditorGUI.indentLevel++;

            _relativeRadius = Mathf.Max(0.1f, EditorGUILayout.FloatField(
                new GUIContent("Maximum Distance", "Maximum 3D distance from the nearest point on an eligible object's bounds."),
                _relativeRadius));

            if (_relativeSource is RelativePlacementSource.SceneObjects or RelativePlacementSource.Any)
            {
                LayerMask sceneLayers = DrawLayerMaskField(
                    new GUIContent("Scene Layers", "Only scene objects on these layers may act as relative-placement anchors."),
                    _relativeSceneLayers);

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
            Rect dropdownRect = EditorGUI.PrefixLabel(
                controlRect,
                new GUIContent("Placement Targets", "Choose the surface and volume types Genix may use. Assets must support a selected target."));

            string selectionLabel = GetPlacementTargetLabel(_placementTargets);
            if (!EditorGUI.DropdownButton(
                    dropdownRect,
                    new GUIContent(selectionLabel, selectionLabel),
                    FocusType.Keyboard,
                    EditorGui.EllipsizedPopupStyle))
                return;

            PlacementTargetSelectionField.Show(
                dropdownRect,
                _placementTargets,
                SetPlacementTargets,
                "Disable every placement target. Generation remains unavailable until at least one target is selected.",
                "Allow Floor, Wall, Ceiling, and Inside Space assets in the same run.");
        }

        private void SetPlacementTargets(PlacementTarget targets)
        {
            _placementTargets = NormalizePlacementTargets(targets);
            Repaint();
        }

        private static string GetPlacementTargetLabel(PlacementTarget targets)
        {
            return PlacementTargetSelectionField.GetLabel(targets, "Select Target");
        }
    }
}
