using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Core;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixEditorWindow
    {
        private void DrawPlacementSettingsSection()
        {
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

            DrawSupportDistributionSection();
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
            return PlacementTargetSelectionField.Normalize(placementTargets);
        }

        private void DrawTargetDistributionSection()
        {
            int selectedIndex = Array.IndexOf(TargetDistributionModes, _targetDistributionMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            selectedIndex = EditorGUILayout.Popup(
                new GUIContent("Target Distribution", "Controls how the requested object count is shared across the selected placement targets."),
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
            EditorGUILayout.Space(2f);
            _supportDistributionEnabled = EditorGUILayout.Toggle(
                new GUIContent(
                    "Support Distribution",
                    "Optionally allocate accepted placements across explicitly listed semantic support tags. Add only the support kinds you want to control. Exact counts are allocated first; the remaining object count is divided among weighted rules and Default / Other Surfaces. Every unlisted surface is handled by Default / Other Surfaces."),
                _supportDistributionEnabled);

            if (!_supportDistributionEnabled)
                return;

            EditorGUI.indentLevel++;
            int removeIndex = -1;
            for (int i = 0; i < _supportDistributionRules.Count; i++)
            {
                SupportDistributionRule rule = _supportDistributionRules[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    SemanticTag selectedTag = DrawSupportRuleTag(rule?.SupportTag);
                    SupportDistributionRuleMode mode = (SupportDistributionRuleMode)EditorGUILayout.EnumPopup(
                        rule?.Mode ?? SupportDistributionRuleMode.Weight,
                        GUILayout.Width(94f));
                    int value = Mathf.Max(0, EditorGUILayout.IntField(
                        rule?.Value ?? 1,
                        GUILayout.Width(48f)));

                    if (selectedTag != rule?.SupportTag || mode != rule?.Mode || value != rule?.Value)
                    {
                        rule = new SupportDistributionRule(selectedTag, mode, value);
                        _supportDistributionRules[i] = rule;
                    }

                    int weightSum = GetSupportWeightSum();
                    string share = mode == SupportDistributionRuleMode.Weight && weightSum > 0
                        ? $"{value * 100f / weightSum:0.#}%"
                        : mode == SupportDistributionRuleMode.ExactCount
                            ? $"{value} obj."
                            : "0%";
                    GUILayout.Label(share, EditorStyles.miniLabel, GUILayout.Width(48f));

                    if (GUILayout.Button(new GUIContent("×", "Remove this support rule."), EditorStyles.miniButton, GUILayout.Width(22f)))
                        removeIndex = i;

                }
            }

            if (removeIndex >= 0)
                _supportDistributionRules.RemoveAt(removeIndex);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent(
                        "Default / Other Surfaces",
                        "Relative share for every support surface that does not carry a tag listed above."));
                _defaultSupportWeight = Mathf.Max(0, EditorGUILayout.IntField(
                    _defaultSupportWeight,
                    GUILayout.Width(48f)));
                int currentWeightSum = GetSupportWeightSum();
                GUILayout.Label(
                    currentWeightSum > 0 ? $"{_defaultSupportWeight * 100f / currentWeightSum:0.#}%" : "0%",
                    EditorStyles.miniLabel,
                    GUILayout.Width(74f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(
                        new GUIContent("+ Add Support Rule", "Add one explicit surface-tag allocation. Unlisted tags continue to use Default."),
                        GUILayout.Width(132f)))
                {
                    _supportDistributionRules.Add(new SupportDistributionRule(
                        GetFirstUnusedSupportTag(),
                        SupportDistributionRuleMode.Weight,
                        1));
                }
            }

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
                    $"Exact support counts request {exactTotal} objects, but the run requests only {_objectCount}.",
                    MessageType.Warning);
            }

            if (GetSupportWeightSum() <= 0 && exactTotal < _objectCount)
            {
                EditorGUILayout.HelpBox(
                    "Increase Default Weight or one explicit Weight rule so the remaining objects have a destination.",
                    MessageType.Warning);
            }

            EditorGUI.indentLevel--;
        }

        private int GetSupportWeightSum() => _supportDistributionRules
            .Where(rule => rule?.IsConfigured == true && rule.Mode == SupportDistributionRuleMode.Weight)
            .Sum(rule => rule.Value) + _defaultSupportWeight;

        private static SemanticTag DrawSupportRuleTag(SemanticTag selected)
        {
            List<SemanticTag> tags = AssetCatalogService.GetOrCreate().Tags
                .Where(tag => tag && tag.Category && tag.Category.SupportsSurfaces)
                .OrderBy(tag => tag.Category.DisplayName)
                .ThenBy(tag => tag.DisplayName)
                .ToList();
            int index = selected ? tags.IndexOf(selected) + 1 : 0;
            string[] options = new[] { "Select Support Tag" }
                .Concat(tags.Select(tag => $"{tag.Category.DisplayName} / {tag.DisplayName}"))
                .ToArray();
            index = EditorGUILayout.Popup(Mathf.Max(0, index), options);
            return index > 0 && index <= tags.Count ? tags[index - 1] : null;
        }

        private SemanticTag GetFirstUnusedSupportTag()
        {
            HashSet<SemanticTag> used = _supportDistributionRules
                .Where(rule => rule?.SupportTag)
                .Select(rule => rule.SupportTag)
                .ToHashSet();
            return AssetCatalogService.GetOrCreate().Tags
                .Where(tag => tag && tag.Category && tag.Category.SupportsSurfaces && !used.Contains(tag))
                .OrderBy(tag => tag.Category.DisplayName)
                .ThenBy(tag => tag.DisplayName)
                .FirstOrDefault();
        }

        private SupportDistributionSettings CreateSupportDistributionSettings() => new(
            _supportDistributionEnabled,
            _defaultSupportWeight,
            _supportDistributionRules);

        private void ApplySupportDistributionSettings(SupportDistributionSettings settings)
        {
            settings ??= SupportDistributionSettings.Disabled;
            _supportDistributionEnabled = settings.IsEnabled;
            _defaultSupportWeight = settings.DefaultWeight;
            _supportDistributionRules.Clear();
            _supportDistributionRules.AddRange(settings.Rules.Select(rule => rule.Copy()));
        }

        private void DrawRelativePlacementSection()
        {
            int sourceIndex = Array.IndexOf(RelativeSources, _relativeSource);

            if (sourceIndex < 0)
                sourceIndex = 0;

            sourceIndex = EditorGUILayout.Popup(
                new GUIContent("Relative To", "Optionally require each placement to be within a 3D radius of an anchor object's bounds."),
                sourceIndex,
                RelativeSourceOptions);
            _relativeSource = RelativeSources[Mathf.Clamp(sourceIndex, 0, RelativeSources.Length - 1)];

            if (_relativeSource == RelativePlacementSource.None)
                return;

            EditorGUI.indentLevel++;

            _relativeRadius = Mathf.Max(0.1f, EditorGUILayout.FloatField(
                new GUIContent("Radius", "Maximum 3D world-space distance from the nearest point on an eligible anchor's bounds."),
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
                new GUIContent("Placement Targets", "Surface and volume types that Genix may use. Assets must have a matching placement type."));

            if (!EditorGUI.DropdownButton(dropdownRect, new GUIContent(GetPlacementTargetLabel(_placementTargets)), FocusType.Keyboard))
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
