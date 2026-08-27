using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Utilities;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.UI;
using Genix.Extensions;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    /// <summary>Provides guided authoring for static asset lists and dynamic catalog filters.</summary>
    [CustomEditor(typeof(AssetPool))]
    public sealed partial class AssetPoolEditor : UnityEditor.Editor
    {
        private SerializedProperty _mode;
        private SerializedProperty _staticAssets;
        private SerializedProperty _filterByPlacementType;
        private SerializedProperty _placementType;
        private SerializedProperty _filterByOrientationMode;
        private SerializedProperty _orientationMode;
        private SerializedProperty _categoryFilters;
        private SerializedProperty _tagPlacementLimits;
        private SerializedProperty _anchorGroupLimits;

        private int _staticAssetAddSlotPickerControlId = -1;

        private string _staticAssetMessage;
        private MessageType _staticAssetMessageType = MessageType.Info;
        private double _staticAssetMessageUntil;

        private static readonly AssetPoolMode[] PoolModes =
        {
            AssetPoolMode.Static,
            AssetPoolMode.Dynamic
        };

        private static readonly GUIContent[] PoolModeLabels =
        {
            new(AssetPoolMode.Static.ToDisplayName(),
                "Use an explicit, manually curated asset list. Best when the pool must remain stable."),
            new(AssetPoolMode.Dynamic.ToDisplayName(),
                "Resolve matching assets from the catalog at generation time. Best for reusable semantic rules.")
        };

        private bool _showPreview = true;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty("mode");
            _staticAssets = serializedObject.FindProperty("staticAssets");
            _filterByPlacementType = serializedObject.FindProperty("filterByPlacementType");
            _placementType = serializedObject.FindProperty("placementType");
            _filterByOrientationMode = serializedObject.FindProperty("filterByOrientationMode");
            _orientationMode = serializedObject.FindProperty("orientationMode");
            _categoryFilters = serializedObject.FindProperty("categoryFilters");
            _tagPlacementLimits = serializedObject.FindProperty("tagPlacementLimits");
            _anchorGroupLimits = serializedObject.FindProperty("anchorGroupLimits");

            _staticAssets.isExpanded = true;
        }

        /// <summary>Draws and applies the custom Inspector interface.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Asset Pool", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawDisplayNameField();

            DrawModeField();

            if (!DesignerUiPreferences.IsAdvanced &&
                (_tagPlacementLimits.arraySize > 0 || _anchorGroupLimits.arraySize > 0))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.FlexibleSpace();
                    DesignerUiPreferences.DrawAdvancedActiveIndicator(
                        true,
                        "This pool contains advanced shared-count or per-anchor rules. They remain active in Basic mode.");
                }
            }

            EditorGUILayout.Space(6f);

            if (IsStaticPool())
                DrawStaticPool();
            else
                DrawDynamicPool();

            if (DesignerUiPreferences.IsAdvanced)
            {
                EditorGUILayout.Space(6f);
                DrawTagPlacementCounts();
                EditorGUILayout.Space(6f);
                DrawAnchorGroupLimits();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAnchorGroupLimits()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(
                    "Per-Anchor Groups",
                    "Constrains the combined count of an asset-tag group independently for every matching relation anchor. Member assets still use their own Asset Relation settings for distance, side, and facing."),
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("+", "Add a grouped per-anchor count rule."), GUILayout.Width(24f)))
                {
                    int index = _anchorGroupLimits.arraySize;
                    _anchorGroupLimits.arraySize++;
                    SerializedProperty group = _anchorGroupLimits.GetArrayElementAtIndex(index);
                    group.FindPropertyRelative("source").enumValueIndex =
                        (int)AssetRelativeAnchorSource.Any;
                    group.FindPropertyRelative("anchorScope").enumValueIndex =
                        (int)AssetRelativeTargetScope.AssetTag;
                    group.FindPropertyRelative("anchorAsset").objectReferenceValue = null;
                    group.FindPropertyRelative("anchorTag").objectReferenceValue = null;
                    group.FindPropertyRelative("memberTag").objectReferenceValue = null;
                    group.FindPropertyRelative("cardinalityMode").enumValueIndex =
                        (int)AssetRelativeCardinalityMode.Exactly;
                    group.FindPropertyRelative("cardinalityCount").intValue = 1;
                    group.FindPropertyRelative("cardinalityMaximumCount").intValue = 1;
                }
            }

            int removeIndex = -1;
            for (int i = 0; i < _anchorGroupLimits.arraySize; i++)
            {
                SerializedProperty group = _anchorGroupLimits.GetArrayElementAtIndex(i);
                SerializedProperty source = group.FindPropertyRelative("source");
                SerializedProperty anchorScope = group.FindPropertyRelative("anchorScope");
                SerializedProperty anchorAsset = group.FindPropertyRelative("anchorAsset");
                SerializedProperty anchorTag = group.FindPropertyRelative("anchorTag");
                SerializedProperty memberTag = group.FindPropertyRelative("memberTag");
                SerializedProperty mode = group.FindPropertyRelative("cardinalityMode");
                SerializedProperty count = group.FindPropertyRelative("cardinalityCount");
                SerializedProperty maximum = group.FindPropertyRelative("cardinalityMaximumCount");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Group {i + 1}", EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(new GUIContent("×", "Remove this group rule."), EditorStyles.miniButton, GUILayout.Width(22f)))
                            removeIndex = i;
                    }

                    EditorGUILayout.PropertyField(source, new GUIContent(
                        "Anchor Source",
                        "Any accepts generated output and explicit scene anchors. The narrower options limit which anchors receive this group count."));
                    EditorGUILayout.PropertyField(anchorScope, new GUIContent(
                        "Anchor Match",
                        "Choose one concrete anchor asset or every anchor carrying an asset-compatible tag."));

                    AssetRelativeTargetScope scope = (AssetRelativeTargetScope)anchorScope.enumValueIndex;
                    if (scope == AssetRelativeTargetScope.Asset)
                    {
                        EditorGUILayout.PropertyField(anchorAsset, new GUIContent(
                            "Anchor Asset",
                            "Each matching instance owns an independent grouped count."));
                    }
                    else
                    {
                        DrawAnchorGroupTagField(i, anchorTag, false, "Anchor Tag",
                            "Each anchor carrying this asset-compatible tag owns an independent grouped count.");
                    }

                    DrawAnchorGroupTagField(i, memberTag, true, "Member Tag",
                        "All dependent assets carrying this tag share the count at each matched anchor.");
                    EditorGUILayout.PropertyField(mode, new GUIContent(
                        "Cardinality",
                        "At Most only limits. At Least actively fills a minimum. Exactly enforces one value. Between enforces an inclusive range."));

                    AssetRelativeCardinalityMode cardinality =
                        (AssetRelativeCardinalityMode)mode.enumValueIndex;
                    if (cardinality != AssetRelativeCardinalityMode.Unlimited)
                    {
                        string label = cardinality == AssetRelativeCardinalityMode.Between
                            ? "Minimum"
                            : "Count";
                        count.intValue = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent(
                            label,
                            "Per-anchor grouped dependent count."), count.intValue));
                    }

                    if (cardinality == AssetRelativeCardinalityMode.Between)
                    {
                        maximum.intValue = Mathf.Max(count.intValue, EditorGUILayout.IntField(
                            new GUIContent("Maximum", "Inclusive upper grouped count per anchor."),
                            maximum.intValue));
                    }

                    bool missingAnchor = scope == AssetRelativeTargetScope.Asset
                        ? !anchorAsset.objectReferenceValue
                        : !anchorTag.objectReferenceValue;
                    if (missingAnchor || !memberTag.objectReferenceValue)
                    {
                        EditorGUILayout.HelpBox(
                            "Select both a valid anchor and member tag before this rule can take effect.",
                            MessageType.Warning);
                    }
                    else if (cardinality == AssetRelativeCardinalityMode.Unlimited)
                    {
                        EditorGUILayout.HelpBox("Unlimited does not constrain this group.", MessageType.Warning);
                    }
                }
            }

            if (removeIndex >= 0)
                _anchorGroupLimits.DeleteArrayElementAtIndex(removeIndex);
        }

        private void DrawAnchorGroupTagField(
            int groupIndex,
            SerializedProperty property,
            bool member,
            string label,
            string tooltip)
        {
            SemanticTag selected = property.objectReferenceValue as SemanticTag;
            Rect row = EditorGUILayout.GetControlRect();
            Rect button = EditorGUI.PrefixLabel(row, new GUIContent(label, tooltip));
            if (GUI.Button(
                    button,
                    new GUIContent(selected ? selected.DisplayName : "Select Asset Tag"),
                    EditorStyles.popup))
            {
                ShowAnchorGroupTagMenu(groupIndex, member, selected);
            }
        }

        private void ShowAnchorGroupTagMenu(int groupIndex, bool member, SemanticTag selected)
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("None"), !selected, () => SetAnchorGroupTag(groupIndex, member, null));
            menu.AddSeparator(string.Empty);
            List<SemanticTag> tags = AssetCatalogService.GetOrCreate().Tags
                .Where(tag => tag && tag.Category && tag.Category.SupportsAssets)
                .OrderBy(tag => tag.Category.DisplayName)
                .ThenBy(tag => tag.DisplayName)
                .ToList();
            foreach (SemanticTag tag in tags)
            {
                SemanticTag captured = tag;
                menu.AddItem(
                    new GUIContent($"{tag.Category.DisplayName}/{tag.DisplayName}"),
                    tag == selected,
                    () => SetAnchorGroupTag(groupIndex, member, captured));
            }

            if (tags.Count == 0)
                menu.AddDisabledItem(new GUIContent("No asset-compatible tags available"));
            menu.ShowAsContext();
        }

        private void SetAnchorGroupTag(int groupIndex, bool member, SemanticTag tag)
        {
            serializedObject.Update();
            if (groupIndex < 0 || groupIndex >= _anchorGroupLimits.arraySize)
                return;

            _anchorGroupLimits.GetArrayElementAtIndex(groupIndex)
                .FindPropertyRelative(member ? "memberTag" : "anchorTag")
                .objectReferenceValue = tag;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void DrawTagPlacementCounts()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(
                    "Shared Tag Counts",
                    "Set a combined minimum and maximum for all existing and newly generated assets carrying a tag. Counts apply across prefab variants sharing the tag and include existing generated output. Use 1 to 1 for exactly one variant; 0 to 0 blocks all variants."),
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("+", "Add a shared tag placement limit."), GUILayout.Width(24f)))
                {
                    int index = _tagPlacementLimits.arraySize;
                    _tagPlacementLimits.arraySize++;
                    SerializedProperty limit = _tagPlacementLimits.GetArrayElementAtIndex(index);
                    limit.FindPropertyRelative("assetTag").objectReferenceValue = null;
                    limit.FindPropertyRelative("minPlacements").intValue = 0;
                    limit.FindPropertyRelative("maxPlacements").intValue = 1;
                }
            }

            int removeIndex = -1;

            for (int i = 0; i < _tagPlacementLimits.arraySize; i++)
            {
                SerializedProperty limit = _tagPlacementLimits.GetArrayElementAtIndex(i);
                SerializedProperty assetTag = limit.FindPropertyRelative("assetTag");
                SerializedProperty minimum = limit.FindPropertyRelative("minPlacements");
                SerializedProperty maximum = limit.FindPropertyRelative("maxPlacements");
                SemanticTag selectedTag = assetTag.objectReferenceValue as SemanticTag;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Count {i + 1}", EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button(new GUIContent("×", "Remove this limit."), EditorStyles.miniButton, GUILayout.Width(22f)))
                            removeIndex = i;
                    }

                    Rect tagRow = EditorGUILayout.GetControlRect();
                    Rect tagButton = EditorGUI.PrefixLabel(tagRow, new GUIContent(
                        "Asset Tag",
                        "Every asset carrying this tag consumes the shared quota."));

                    if (GUI.Button(
                            tagButton,
                            new GUIContent(selectedTag ? selectedTag.DisplayName : "Select Asset Tag"),
                            EditorStyles.popup))
                    {
                        ShowTagLimitMenu(i, selectedTag);
                    }

                    EditorGUI.BeginChangeCheck();
                    int minimumValue = EditorGUILayout.IntField(new GUIContent(
                        "Minimum",
                        "Minimum combined placements. Missing instances are prioritized and final plan slots are reserved for them."),
                        minimum.intValue);
                    int maximumValue = EditorGUILayout.IntField(new GUIContent(
                        "Maximum",
                        "Maximum combined placements across existing generated output and this run."),
                        maximum.intValue);

                    if (EditorGUI.EndChangeCheck())
                    {
                        minimum.intValue = Mathf.Max(0, minimumValue);
                        maximum.intValue = Mathf.Max(minimum.intValue, maximumValue);
                    }

                    if (!selectedTag)
                    {
                        EditorGUILayout.HelpBox("Select an asset tag before this limit can take effect.", MessageType.Warning);
                    }
                    else if (maximum.intValue == 0)
                    {
                        EditorGUILayout.HelpBox("Assets carrying this tag are blocked in this pool.", MessageType.Warning);
                    }
                }
            }

            if (removeIndex >= 0)
                _tagPlacementLimits.DeleteArrayElementAtIndex(removeIndex);
        }

        private void ShowTagLimitMenu(int limitIndex, SemanticTag selectedTag)
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("None"), !selectedTag, () => SetTagLimitTag(limitIndex, null));
            menu.AddSeparator(string.Empty);
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();
            List<SemanticTag> tags = catalog.Tags
                .Where(tag => tag && tag.Category && tag.Category.SupportsAssets)
                .OrderBy(tag => tag.Category.DisplayName)
                .ThenBy(tag => tag.DisplayName)
                .ToList();

            foreach (SemanticTag tag in tags)
            {
                SemanticTag capturedTag = tag;
                menu.AddItem(
                    new GUIContent($"{tag.Category.DisplayName}/{tag.DisplayName}"),
                    tag == selectedTag,
                    () => SetTagLimitTag(limitIndex, capturedTag));
            }

            if (tags.Count == 0)
                menu.AddDisabledItem(new GUIContent("No asset-compatible tags available"));

            menu.ShowAsContext();
        }

        private void SetTagLimitTag(int limitIndex, SemanticTag tag)
        {
            serializedObject.Update();

            if (limitIndex < 0 || limitIndex >= _tagPlacementLimits.arraySize)
                return;

            _tagPlacementLimits.GetArrayElementAtIndex(limitIndex)
                .FindPropertyRelative("assetTag").objectReferenceValue = tag;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void DrawDisplayNameField()
        {
            EditorGUI.BeginChangeCheck();

            string displayName = EditorGUILayout.DelayedTextField(
                new GUIContent("Display Name", "Designer-facing name shown in Genix asset-pool selectors."),
                target.name);

            if (!EditorGUI.EndChangeCheck())
                return;

            AssetCatalogService.Rename(
                target,
                displayName,
                "New Asset Pool");

            serializedObject.Update();
        }

        private void DrawModeField()
        {
            AssetPoolMode currentMode = GetSerializedMode();
            int currentIndex = System.Array.IndexOf(PoolModes, currentMode);

            if (currentIndex < 0)
                currentIndex = 0;

            int selectedIndex = EditorGUILayout.Popup(
                new GUIContent("Mode", "Static stores chosen assets; Dynamic resolves assets from catalog filters."),
                currentIndex,
                PoolModeLabels);

            SetSerializedMode(PoolModes[selectedIndex]);
        }

        private AssetPoolMode GetSerializedMode()
        {
            string enumName = _mode.enumNames[_mode.enumValueIndex];

            return System.Enum.TryParse(enumName, out AssetPoolMode mode)
                ? mode
                : AssetPoolMode.Static;
        }

        private void SetSerializedMode(AssetPoolMode mode)
        {
            string enumName = mode.ToString();

            for (int i = 0; i < _mode.enumNames.Length; i++)
            {
                if (_mode.enumNames[i] != enumName)
                    continue;

                _mode.enumValueIndex = i;
                return;
            }
        }

        private void RemoveMissingStaticAssets()
        {
            for (int i = _staticAssets.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty assetProperty = _staticAssets.GetArrayElementAtIndex(i);

                if (assetProperty.objectReferenceValue)
                    continue;

                _staticAssets.DeleteArrayElementAtIndex(i);
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private bool IsStaticPool()
        {
            return _mode.enumNames[_mode.enumValueIndex] == nameof(AssetPoolMode.Static);
        }

        private static string SanitizeMenuPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            return value.Replace("/", "-");
        }
    }
}
