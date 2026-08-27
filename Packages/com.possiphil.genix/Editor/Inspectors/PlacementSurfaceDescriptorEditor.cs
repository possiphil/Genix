using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.Genix.Editor.Common;
using Genix.Editor.SceneConfiguration;
using Genix.Editor.UI;
using Genix.Editor.Windows;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    /// <summary>Provides guided semantic surface, facing, and capacity authoring.</summary>
    [CustomEditor(typeof(PlacementSurfaceDescriptor))]
    public sealed class PlacementSurfaceDescriptorEditor : UnityEditor.Editor
    {
        private SerializedProperty _surfaceTags;
        private SerializedProperty _noneTagCategories;
        private SerializedProperty _allowedAssetTags;
        private SerializedProperty _forbiddenAssetTags;
        private SerializedProperty _limitCapacity;
        private SerializedProperty _maxCapacity;
        private SerializedProperty _assetCapacityRules;

        private void OnEnable()
        {
            _surfaceTags = serializedObject.FindProperty("surfaceTags");
            _noneTagCategories = serializedObject.FindProperty("noneTagCategories");
            _allowedAssetTags = serializedObject.FindProperty("allowedAssetTags");
            _forbiddenAssetTags = serializedObject.FindProperty("forbiddenAssetTags");
            _limitCapacity = serializedObject.FindProperty("limitCapacity");
            _maxCapacity = serializedObject.FindProperty("maxCapacity");
            _assetCapacityRules = serializedObject.FindProperty("assetCapacityRules");
        }

        /// <summary>Draws the custom descriptor inspector.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            PlacementSurfaceDescriptor descriptor = (PlacementSurfaceDescriptor)target;

            if (!descriptor.GetComponentInChildren<Collider>())
            {
                EditorGUILayout.HelpBox(
                    "No collider exists on this object or its children, so Genix cannot sample this surface.",
                    MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(
                       !SupportSurfaceRegionAuthoring.CanCreate(descriptor.gameObject)))
            {
                if (GUILayout.Button(new GUIContent(
                        "Add Support Surface Region",
                        "Create a thin child BoxCollider for an explicit horizontal support level, such as an internal shelf board. Move and resize the selected child to match the usable surface, then duplicate it for additional levels.")))
                {
                    SupportSurfaceRegionAuthoring.Create(
                        descriptor.gameObject,
                        GenixEditorWindow.GetConfiguredSurfaceLayerMask());
                }
            }

            if (!DesignerUiPreferences.IsAdvanced &&
                (_allowedAssetTags.arraySize > 0 ||
                 _forbiddenAssetTags.arraySize > 0 ||
                 _assetCapacityRules.arraySize > 0))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.FlexibleSpace();
                    DesignerUiPreferences.DrawAdvancedActiveIndicator(
                        true,
                        "This surface contains advanced asset filters or asset-specific capacity limits. They remain active in Basic mode.");
                }
            }

            DrawSurfaceTags();

            if (DesignerUiPreferences.IsAdvanced)
                DrawAcceptedAssets();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Capacity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_limitCapacity, new GUIContent(
                "Limit Capacity",
                "Restricts how many generated objects may use this descriptor across generation runs."));

            if (_limitCapacity.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    int capacity = EditorGUILayout.IntField(new GUIContent(
                        "Max Capacity",
                        "Maximum number of generated objects supported by this descriptor. Zero blocks every placement."),
                        _maxCapacity.intValue);

                    if (EditorGUI.EndChangeCheck())
                        _maxCapacity.intValue = Mathf.Max(0, capacity);
                }

                if (_maxCapacity.intValue == 0)
                {
                    EditorGUILayout.HelpBox(
                        "This surface currently accepts no placements because Max Capacity is zero.",
                        MessageType.Warning);
                }
            }

            if (DesignerUiPreferences.IsAdvanced)
                DrawAssetCapacityRules();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAcceptedAssets()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(new GUIContent(
                    "Accepted Assets",
                    "Allowed defaults to Any. Forbidden defaults to None and takes precedence. These rules are configured on the surface, while an asset's Support Tags describe the surfaces that asset can use."),
                EditorStyles.boldLabel);

            DrawAssetTagRule(
                _allowedAssetTags,
                "Allowed Tags",
                "At least one selected tag must belong to the asset. Leave empty to accept any asset.");
            DrawAssetTagRule(
                _forbiddenAssetTags,
                "Forbidden Tags",
                "Any selected tag rejects the asset, even when it is also allowed.");

            List<SemanticTag> conflicts = GetSerializedTags(_allowedAssetTags)
                .Intersect(GetSerializedTags(_forbiddenAssetTags))
                .ToList();

            if (conflicts.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Allowed and Forbidden contain: {string.Join(", ", conflicts.Select(tag => tag.DisplayName))}. Forbidden takes precedence.",
                    MessageType.Warning);
            }
        }

        private void DrawAssetTagRule(SerializedProperty property, string label, string tooltip)
        {
            IReadOnlyList<SemanticTag> selectedTags = GetSerializedTags(property);
            string summary = selectedTags.Count switch
            {
                0 => property == _allowedAssetTags ? "Any" : "None",
                <= 2 => string.Join(", ", selectedTags.Select(tag => tag.DisplayName)),
                _ => $"{selectedTags[0].DisplayName}, {selectedTags[1].DisplayName} +{selectedTags.Count - 2}"
            };

            Rect row = EditorGUILayout.GetControlRect();
            Rect button = EditorGUI.PrefixLabel(row, new GUIContent(label, tooltip));

            if (GUI.Button(button, new GUIContent(summary, tooltip), EditorStyles.popup))
                ShowSurfaceAssetTagMenu(property.propertyPath, selectedTags);
        }

        private void ShowSurfaceAssetTagMenu(string propertyPath, IReadOnlyList<SemanticTag> selectedTags)
        {
            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("Clear"),
                selectedTags.Count == 0,
                () => SetSurfaceAssetTags(propertyPath, Array.Empty<SemanticTag>()));
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
                bool selected = selectedTags.Contains(tag);
                menu.AddItem(
                    new GUIContent($"{tag.Category.DisplayName}/{tag.DisplayName}"),
                    selected,
                    () => ToggleSurfaceAssetTag(propertyPath, capturedTag));
            }

            if (tags.Count == 0)
                menu.AddDisabledItem(new GUIContent("No asset-compatible tags available"));

            menu.ShowAsContext();
        }

        private void ToggleSurfaceAssetTag(string propertyPath, SemanticTag tag)
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            List<SemanticTag> tags = GetSerializedTags(property).ToList();

            if (!tags.Remove(tag))
                tags.Add(tag);

            SetSurfaceAssetTags(propertyPath, tags);
        }

        private void SetSurfaceAssetTags(string propertyPath, IEnumerable<SemanticTag> tags)
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            property.ClearArray();
            int index = 0;

            foreach (SemanticTag tag in tags.Where(tag => tag && tag.Category && tag.Category.SupportsAssets).Distinct())
            {
                property.InsertArrayElementAtIndex(index);
                property.GetArrayElementAtIndex(index).objectReferenceValue = tag;
                index++;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static IReadOnlyList<SemanticTag> GetSerializedTags(SerializedProperty property)
        {
            List<SemanticTag> tags = new();

            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue is SemanticTag tag && tag)
                    tags.Add(tag);
            }

            return tags;
        }

        private void DrawAssetCapacityRules()
        {
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(
                    "Asset-Specific Limits",
                    "Optional per-surface limits for one asset or every asset carrying a selected tag. These limits also apply when total capacity is unlimited."),
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("+", "Add an asset-specific capacity rule."), GUILayout.Width(24f)))
                {
                    int index = _assetCapacityRules.arraySize;
                    _assetCapacityRules.arraySize++;
                    SerializedProperty rule = _assetCapacityRules.GetArrayElementAtIndex(index);
                    rule.FindPropertyRelative("scope").enumValueIndex =
                        (int)PlacementSurfaceCapacityRuleScope.AssetTag;
                    rule.FindPropertyRelative("asset").objectReferenceValue = null;
                    rule.FindPropertyRelative("assetTag").objectReferenceValue = null;
                    rule.FindPropertyRelative("maxCapacity").intValue = 1;
                }
            }

            if (_assetCapacityRules.arraySize == 0)
                return;

            int removeIndex = -1;

            for (int i = 0; i < _assetCapacityRules.arraySize; i++)
            {
                SerializedProperty rule = _assetCapacityRules.GetArrayElementAtIndex(i);
                SerializedProperty scope = rule.FindPropertyRelative("scope");
                SerializedProperty asset = rule.FindPropertyRelative("asset");
                SerializedProperty assetTag = rule.FindPropertyRelative("assetTag");
                SerializedProperty maxCapacity = rule.FindPropertyRelative("maxCapacity");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Rule {i + 1}", EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button(new GUIContent("×", "Remove this rule."), EditorStyles.miniButton, GUILayout.Width(22f)))
                            removeIndex = i;
                    }

                    EditorGUILayout.PropertyField(scope, new GUIContent(
                        "Match By",
                        "Choose one concrete asset or every asset carrying a semantic tag."));

                    PlacementSurfaceCapacityRuleScope selectedScope =
                        (PlacementSurfaceCapacityRuleScope)scope.enumValueIndex;

                    if (selectedScope == PlacementSurfaceCapacityRuleScope.Asset)
                    {
                        EditorGUILayout.PropertyField(asset, new GUIContent(
                            "Asset",
                            "Only instances of this exact Asset Definition consume the rule."));
                    }
                    else
                    {
                        SemanticTag selectedTag = assetTag.objectReferenceValue as SemanticTag;

                        if (GUILayout.Button(
                                new GUIContent(
                                    selectedTag ? selectedTag.DisplayName : "Select Asset Tag",
                                    "Every asset carrying this tag consumes the rule."),
                                EditorStyles.popup))
                        {
                            ShowAssetTagMenu(i, selectedTag);
                        }
                    }

                    EditorGUI.BeginChangeCheck();
                    int capacity = EditorGUILayout.IntField(new GUIContent(
                        "Maximum",
                        "Maximum matching objects supported by this surface. Zero blocks matching assets."),
                        maxCapacity.intValue);

                    if (EditorGUI.EndChangeCheck())
                        maxCapacity.intValue = Mathf.Max(0, capacity);

                    bool missingTarget = selectedScope == PlacementSurfaceCapacityRuleScope.Asset
                        ? !asset.objectReferenceValue
                        : !assetTag.objectReferenceValue;

                    if (missingTarget)
                    {
                        EditorGUILayout.HelpBox(
                            "Select a target before this rule can affect placement.",
                            MessageType.Warning);
                    }
                    else if (maxCapacity.intValue == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "Matching assets are blocked on this surface because Maximum is zero.",
                            MessageType.Warning);
                    }
                }
            }

            if (removeIndex >= 0)
                _assetCapacityRules.DeleteArrayElementAtIndex(removeIndex);
        }

        private void ShowAssetTagMenu(int ruleIndex, SemanticTag selectedTag)
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("None"), !selectedTag, () => SetCapacityRuleTag(ruleIndex, null));
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
                    () => SetCapacityRuleTag(ruleIndex, capturedTag));
            }

            if (tags.Count == 0)
                menu.AddDisabledItem(new GUIContent("No asset-compatible tags available"));

            menu.ShowAsContext();
        }

        private void SetCapacityRuleTag(int ruleIndex, SemanticTag tag)
        {
            serializedObject.Update();

            if (ruleIndex < 0 || ruleIndex >= _assetCapacityRules.arraySize)
                return;

            SerializedProperty rule = _assetCapacityRules.GetArrayElementAtIndex(ruleIndex);
            rule.FindPropertyRelative("assetTag").objectReferenceValue = tag;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void DrawSurfaceTags()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(
                        "Surface Tags",
                        "Assign this descriptor to a surface collider or one of its parents. Descendant colliders share these semantic and capacity rules."),
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(
                           _surfaceTags.arraySize == 0 && _noneTagCategories.arraySize == 0))
                {
                    if (GUILayout.Button(new GUIContent(
                            "Reset",
                            "Reset every surface category to its default Any selection."), GUILayout.Width(52f)))
                    {
                        _surfaceTags.ClearArray();
                        _noneTagCategories.ClearArray();
                        GUI.FocusControl(null);
                    }
                }
            }

            AssetCatalog catalog = AssetCatalogService.GetOrCreate();
            List<TagCategory> categories = catalog.Categories
                .Where(category => category && category.SupportsSurfaces)
                .OrderBy(category => category.DisplayName)
                .ToList();

            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Create a tag category with Usage set to Surface or Asset and Surface before assigning surface tags.",
                    MessageType.Warning);
                return;
            }

            foreach (TagCategory category in categories)
            {
                List<SemanticTag> availableTags = catalog.Tags
                    .Where(tag => tag && tag.Category == category)
                    .OrderBy(tag => tag.DisplayName)
                    .ToList();
                List<SemanticTag> selectedTags = GetTagsInCategory(category);

                TagSelectionField.Draw(
                    category.DisplayName,
                    category,
                    availableTags,
                    selectedTags,
                    null,
                    forceMultiSelect: true,
                    anySelected: ((PlacementSurfaceDescriptor)target).AcceptsAnyTag(category),
                    onChangedWithSpecialSelection: (tags, specialSelection) =>
                        SetCategorySelection(
                            category,
                            tags,
                            specialSelection == TagSelectionField.SpecialSelection.None),
                    showNoneOption: true,
                    showAnyOption: true);
            }
        }

        private List<SemanticTag> GetTagsInCategory(TagCategory category)
        {
            List<SemanticTag> tags = new();

            for (int i = 0; i < _surfaceTags.arraySize; i++)
            {
                SemanticTag tag = _surfaceTags.GetArrayElementAtIndex(i).objectReferenceValue as SemanticTag;

                if (tag && tag.Category == category)
                    tags.Add(tag);
            }

            return tags;
        }

        private void SetCategorySelection(
            TagCategory category,
            IReadOnlyList<SemanticTag> selectedTags,
            bool selectNone)
        {
            PlacementSurfaceDescriptor descriptor = (PlacementSurfaceDescriptor)target;
            Undo.RecordObject(descriptor, "Change Surface Tags");
            descriptor.SetCategorySelection(category, selectedTags, selectNone);
            EditorUtility.SetDirty(descriptor);
            serializedObject.Update();
        }

        [MenuItem("GameObject/Genix/Add Placement Surface Descriptor", false, 29)]
        private static void AddPlacementSurfaceDescriptor(MenuCommand command)
        {
            GameObject gameObject = command.context as GameObject ?? Selection.activeGameObject;

            if (!gameObject || gameObject.GetComponent<PlacementSurfaceDescriptor>())
                return;

            Undo.AddComponent<PlacementSurfaceDescriptor>(gameObject);
            Selection.activeGameObject = gameObject;
        }

        [MenuItem("GameObject/Genix/Add Placement Surface Descriptor", true)]
        private static bool CanAddPlacementSurfaceDescriptor()
        {
            GameObject gameObject = Selection.activeGameObject;
            return gameObject && !gameObject.GetComponent<PlacementSurfaceDescriptor>();
        }

        [MenuItem("GameObject/Genix/Add Support Surface Region", false, 30)]
        private static void AddSupportSurfaceRegion(MenuCommand command)
        {
            GameObject gameObject = command.context as GameObject ?? Selection.activeGameObject;
            SupportSurfaceRegionAuthoring.Create(
                gameObject,
                GenixEditorWindow.GetConfiguredSurfaceLayerMask());
        }

        [MenuItem("GameObject/Genix/Add Support Surface Region", true)]
        private static bool CanAddSupportSurfaceRegion() =>
            SupportSurfaceRegionAuthoring.CanCreate(Selection.activeGameObject);
    }
}
