using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Utilities;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.Genix.Editor.Common;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    /// <summary>Provides guided authoring for asset placement, bounds, orientation, and semantic metadata.</summary>
    [CustomEditor(typeof(AssetDefinition))]
    public sealed class AssetDefinitionEditor : UnityEditor.Editor
    {
        private static readonly GUIContent AssetNameLabel = new(
            "Asset Name",
            "Designer-facing name used in pools, diagnostics, and generated object names.");
        private static readonly GUIContent PrefabLabel = new(
            "Prefab",
            "Prefab instantiated for accepted placements. Assigning it refreshes the placement bounds.");
        private static readonly GUIContent PlacementTypeLabel = new(
            "Placement Type",
            "Surface or volume target this asset can use: Floor, Wall, Ceiling, or Inside Space.");
        private static readonly GUIContent OrientationModeLabel = new(
            "Orientation",
            "None keeps the sampled orientation. Face Target uses a relative-placement anchor. Match Support Forward uses the direction provided by a Placement Surface Descriptor.");
        private static readonly GUIContent BoundsSizeLabel = new(
            "Size",
            "World-space size used for containment, spacing, overlap, and surface-fit validation.");
        private static readonly GUIContent BoundsCenterLabel = new(
            "Center Offset",
            "Offset from the prefab transform origin to the center of its placement bounds.");

        private SerializedProperty _prefab;
        private SerializedProperty _semanticTags;
        private SerializedProperty _anyTagCategories;
        private SerializedProperty _requiredSupportTags;
        private SerializedProperty _forbiddenSupportTags;
        private SerializedProperty _requiredSupportNoneCategories;
        private SerializedProperty _forbiddenSupportAnyCategories;
        private SerializedProperty _limitPlacements;
        private SerializedProperty _maxPlacements;
        private SerializedProperty _placementType;
        private SerializedProperty _wallVerticalPlacementMode;
        private SerializedProperty _placementHeight;
        private SerializedProperty _wallMinHeight;
        private SerializedProperty _wallMaxHeight;
        private SerializedProperty _boundsSize;
        private SerializedProperty _boundsCenterOffset;
        private SerializedProperty _orientationMode;
        private SerializedProperty _surfaceFitMode;
        private SerializedProperty _surfaceAlignmentMode;
        private SerializedProperty _surfaceHeightMode;
        private SerializedProperty _maxSurfaceHeightDifference;
        private SerializedProperty _minSurfaceSupport;
        private SerializedProperty _surfaceSinkOffset;
        private SerializedProperty _randomYawRotation;
        private SerializedProperty _randomPitchRotation;
        private SerializedProperty _randomRollRotation;
        private SerializedProperty _wallProximityMode;
        private SerializedProperty _wallDistance;

        private void OnEnable()
        {
            _prefab = serializedObject.FindProperty("prefab");
            _semanticTags = serializedObject.FindProperty("semanticTags");
            _anyTagCategories = serializedObject.FindProperty("anyTagCategories");
            _requiredSupportTags = serializedObject.FindProperty("requiredSupportTags");
            _forbiddenSupportTags = serializedObject.FindProperty("forbiddenSupportTags");
            _requiredSupportNoneCategories = serializedObject.FindProperty("requiredSupportNoneCategories");
            _forbiddenSupportAnyCategories = serializedObject.FindProperty("forbiddenSupportAnyCategories");
            _limitPlacements = serializedObject.FindProperty("limitPlacements");
            _maxPlacements = serializedObject.FindProperty("maxPlacements");
            _placementType = serializedObject.FindProperty("placementType");
            _wallVerticalPlacementMode = serializedObject.FindProperty("wallVerticalPlacementMode");
            _placementHeight = serializedObject.FindProperty("placementHeight");
            _wallMinHeight = serializedObject.FindProperty("wallMinHeight");
            _wallMaxHeight = serializedObject.FindProperty("wallMaxHeight");
            _boundsSize = serializedObject.FindProperty("boundsSize");
            _boundsCenterOffset = serializedObject.FindProperty("boundsCenterOffset");
            _orientationMode = serializedObject.FindProperty("orientationMode");
            _surfaceFitMode = serializedObject.FindProperty("surfaceFitMode");
            _surfaceAlignmentMode = serializedObject.FindProperty("surfaceAlignmentMode");
            _surfaceHeightMode = serializedObject.FindProperty("surfaceHeightMode");
            _maxSurfaceHeightDifference = serializedObject.FindProperty("maxSurfaceHeightDifference");
            _minSurfaceSupport = serializedObject.FindProperty("minSurfaceSupport");
            _surfaceSinkOffset = serializedObject.FindProperty("surfaceSinkOffset");
            _randomYawRotation = serializedObject.FindProperty("randomYawRotation");
            _randomPitchRotation = serializedObject.FindProperty("randomPitchRotation");
            _randomRollRotation = serializedObject.FindProperty("randomRollRotation");
            _wallProximityMode = serializedObject.FindProperty("wallProximityMode");
            _wallDistance = serializedObject.FindProperty("wallDistance");
        }

        /// <summary>Draws and applies the custom Inspector interface.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawAssetNameField();
            DrawPrefabSection();

            EditorGUILayout.Space(4f);

            DrawPlacementSection();

            EditorGUILayout.Space(4f);

            DrawSupportSurfaceSection();

            EditorGUILayout.Space(4f);

            DrawBoundsSection();

            EditorGUILayout.Space(6f);

            DrawSemanticTagsSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAssetNameField()
        {
            EditorGUI.BeginChangeCheck();

            string assetName = EditorGUILayout.DelayedTextField(AssetNameLabel, target.name);

            if (!EditorGUI.EndChangeCheck())
                return;

            AssetCatalogService.Rename(
                target,
                assetName,
                "New Genix Asset");

            serializedObject.Update();
        }

        private void DrawPrefabSection()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(_prefab, PrefabLabel);

            if (EditorGUI.EndChangeCheck())
                UpdateBoundsFromPrefab();
        }

        private void DrawPlacementSection()
        {
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_placementType, PlacementTypeLabel);
            EditorGUILayout.PropertyField(_limitPlacements, new GUIContent(
                "Limit Placements",
                "Restrict how often this asset may be accepted in one generation run."));

            if (_limitPlacements.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    int maximum = EditorGUILayout.IntField(new GUIContent(
                        "Max Placements",
                        "Maximum accepted instances of this asset per generation run."),
                        _maxPlacements.intValue);
                    if (EditorGUI.EndChangeCheck())
                        _maxPlacements.intValue = Mathf.Max(1, maximum);
                }
            }

            if (IsWallPlacementType())
            {
                DrawWallHeightSection();

                EditorGUILayout.PropertyField(_randomRollRotation, new GUIContent(
                    "Random Roll",
                    "Try deterministic rotations around the wall normal while keeping the asset flush with the wall."));
                DrawSurfaceFitSection();
            }
            else if (IsInsideSpacePlacementType())
            {
                EditorGUILayout.PropertyField(_randomYawRotation, RotationLabel("Yaw", "vertical axis"));
                EditorGUILayout.PropertyField(_randomPitchRotation, RotationLabel("Pitch", "side axis"));
                EditorGUILayout.PropertyField(_randomRollRotation, RotationLabel("Roll", "forward axis"));
            }
            else
            {
                EditorGUILayout.PropertyField(_randomYawRotation, RotationLabel("Yaw", "surface normal"));
                DrawSurfaceFitSection();
            }

            EditorGUILayout.PropertyField(_orientationMode, OrientationModeLabel);

            if (IsFloorOrCeilingPlacementType())
                DrawWallProximitySection();

            if (UsesSupportForward() && (IsWallPlacementType() || IsInsideSpacePlacementType()))
            {
                EditorGUILayout.HelpBox(
                    IsWallPlacementType()
                        ? "Wall assets already face the sampled wall normal. Use Random Roll to vary their rotation instead of Match Support Forward."
                        : "Inside Space has no supporting surface. Match Support Forward therefore cannot resolve a direction.",
                    MessageType.Warning);
            }
        }

        private void DrawSupportSurfaceSection()
        {
            EditorGUILayout.LabelField("Support Surface", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Required defaults to Any and restricts surfaces only when tags are selected. Forbidden defaults to None and always takes precedence. Selecting None under Required or Any under Forbidden intentionally blocks this asset.",
                MessageType.Info);

            if (IsInsideSpacePlacementType())
            {
                EditorGUILayout.HelpBox(
                    "Inside Space assets do not use a support collider, so support tags are ignored.",
                    MessageType.Info);
            }

            DrawSupportTagList(
                _requiredSupportTags,
                _requiredSupportNoneCategories,
                true,
                "Required Tags",
                "Any adds no restriction. None deliberately disables placement. Otherwise at least one selected tag must match.");
            DrawSupportTagList(
                _forbiddenSupportTags,
                _forbiddenSupportAnyCategories,
                false,
                "Forbidden Tags",
                "None adds no restriction. Any rejects every surface. Otherwise each selected matching tag rejects the surface.");

            List<SemanticTag> conflicts = GetTags(_requiredSupportTags)
                .Intersect(GetTags(_forbiddenSupportTags))
                .ToList();

            if (conflicts.Count > 0)
            {
                string names = string.Join(", ", conflicts.Select(tag => tag.DisplayName));
                EditorGUILayout.HelpBox(
                    $"Required and Forbidden contain the same tag(s): {names}. Forbidden takes precedence, so those surfaces will be rejected.",
                    MessageType.Warning);
            }

            List<TagCategory> requiredNone = GetCategories(_requiredSupportNoneCategories);
            List<TagCategory> forbiddenAny = GetCategories(_forbiddenSupportAnyCategories);

            if (requiredNone.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Required is set to None for: {string.Join(", ", requiredNone.Select(category => category.DisplayName))}. This asset cannot be placed until those categories are changed.",
                    MessageType.Warning);
            }

            if (forbiddenAny.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Forbidden is set to Any for: {string.Join(", ", forbiddenAny.Select(category => category.DisplayName))}. This asset cannot be placed until those categories are changed.",
                    MessageType.Warning);
            }
        }

        private void DrawSupportTagList(
            SerializedProperty property,
            SerializedProperty specialCategories,
            bool isRequired,
            string title,
            string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(title, tooltip), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(
                           property.arraySize == 0 && specialCategories.arraySize == 0))
                {
                    if (GUILayout.Button(new GUIContent(
                            "Reset",
                            isRequired
                                ? "Reset every category to Any."
                                : "Reset every category to None."), GUILayout.Width(52f)))
                    {
                        property.ClearArray();
                        specialCategories.ClearArray();
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
                    "Create a tag category with Usage set to Surface or Asset and Surface before assigning support tags.",
                    MessageType.Info);
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (TagCategory category in categories)
                {
                    List<SemanticTag> availableTags = catalog.Tags
                        .Where(tag => tag && tag.Category == category)
                        .OrderBy(tag => tag.DisplayName)
                        .ToList();
                    List<SemanticTag> selectedTags = GetTags(property)
                        .Where(tag => tag.Category == category)
                        .ToList();
                    bool specialSelected = ContainsCategory(specialCategories, category);
                    bool anySelected = isRequired
                        ? !specialSelected && selectedTags.Count == 0
                        : specialSelected;

                    TagSelectionField.Draw(
                        category.DisplayName,
                        category,
                        availableTags,
                        selectedTags,
                        null,
                        forceMultiSelect: true,
                        anySelected: anySelected,
                        onChangedWithSpecialSelection: (tags, specialSelection) =>
                            SetSupportSelection(
                                property,
                                specialCategories,
                                category,
                                tags,
                                specialSelection,
                                isRequired),
                        showNoneOption: true,
                        showAnyOption: true);
                }
            }
        }

        private static List<SemanticTag> GetTags(SerializedProperty property)
        {
            List<SemanticTag> tags = new();

            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue is SemanticTag tag && tag)
                    tags.Add(tag);
            }

            return tags;
        }

        private static List<TagCategory> GetCategories(SerializedProperty property)
        {
            List<TagCategory> categories = new();

            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue is TagCategory category && category)
                    categories.Add(category);
            }

            return categories;
        }

        private static bool ContainsCategory(SerializedProperty property, TagCategory category) =>
            GetCategories(property).Contains(category);

        private void SetSupportSelection(
            SerializedProperty property,
            SerializedProperty specialCategories,
            TagCategory category,
            IReadOnlyList<SemanticTag> selectedTags,
            TagSelectionField.SpecialSelection specialSelection,
            bool isRequired)
        {
            serializedObject.Update();

            for (int i = property.arraySize - 1; i >= 0; i--)
            {
                SemanticTag existing = property.GetArrayElementAtIndex(i).objectReferenceValue as SemanticTag;

                if (!existing || existing.Category == category)
                    property.DeleteArrayElementAtIndex(i);
            }

            for (int i = specialCategories.arraySize - 1; i >= 0; i--)
            {
                TagCategory existing = specialCategories
                    .GetArrayElementAtIndex(i).objectReferenceValue as TagCategory;

                if (!existing || existing == category)
                    specialCategories.DeleteArrayElementAtIndex(i);
            }

            bool selectSpecialCategory = selectedTags.Count == 0 &&
                                         (isRequired
                                             ? specialSelection == TagSelectionField.SpecialSelection.None
                                             : specialSelection == TagSelectionField.SpecialSelection.Any);

            if (selectSpecialCategory)
            {
                int specialIndex = specialCategories.arraySize;
                specialCategories.InsertArrayElementAtIndex(specialIndex);
                specialCategories.GetArrayElementAtIndex(specialIndex).objectReferenceValue = category;
            }

            foreach (SemanticTag tag in selectedTags.Where(tag => tag && tag.Category == category).Distinct())
            {
                int index = property.arraySize;
                property.InsertArrayElementAtIndex(index);
                property.GetArrayElementAtIndex(index).objectReferenceValue = tag;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void DrawWallProximitySection()
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.PropertyField(_wallProximityMode, new GUIContent(
                "Wall Relationship",
                "Any Distance disables wall checks. Near Wall uses a maximum bounds-to-wall distance. Away From Wall enforces minimum clearance."));

            WallProximityMode mode = (WallProximityMode)_wallProximityMode.enumValueIndex;
            if (mode == WallProximityMode.AnyDistance)
                return;

            DrawNonNegativeFloat(_wallDistance, new GUIContent(
                mode == WallProximityMode.NearWall ? "Max Wall Distance" : "Min Wall Distance",
                mode == WallProximityMode.NearWall
                    ? "Maximum horizontal gap between the asset bounds and the nearest detected wall."
                    : "Minimum horizontal clearance between the asset bounds and every detected wall."));
        }

        private void DrawWallHeightSection()
        {
            EditorGUILayout.PropertyField(_wallVerticalPlacementMode, new GUIContent(
                "Vertical Placement",
                "Full Wall uses all sampled wall heights. Fixed Height uses one level. Height Range distributes placements within a bounded vertical interval."));

            WallVerticalPlacementMode mode = (WallVerticalPlacementMode)_wallVerticalPlacementMode.enumValueIndex;

            if (mode == WallVerticalPlacementMode.FixedHeight)
            {
                DrawNonNegativeFloat(_placementHeight, new GUIContent(
                    "Fixed Height",
                    "Height of the asset's lower bound above the target area's lower bound. Zero rests the asset on that lower boundary."));
                return;
            }

            if (mode == WallVerticalPlacementMode.HeightRange)
            {
                DrawNonNegativeFloat(_wallMinHeight, new GUIContent(
                    "Min Height",
                    "Lowest permitted asset-bottom height above the target area's lower bound."));
                DrawNonNegativeFloat(_wallMaxHeight, new GUIContent(
                    "Max Height",
                    "Highest permitted asset-bottom height above the target area's lower bound."));

                if (_wallMaxHeight.floatValue < _wallMinHeight.floatValue)
                    _wallMaxHeight.floatValue = _wallMinHeight.floatValue;

                return;
            }

            EditorGUILayout.PropertyField(_placementHeight, new GUIContent(
                "Baseline Offset",
                "Additional vertical clearance above every sampled wall baseline. Zero keeps the asset's lower bound flush with each sampled level."));
        }

        private static void DrawNonNegativeFloat(SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.FloatField(label, property.floatValue);

            if (EditorGUI.EndChangeCheck())
                property.floatValue = Mathf.Max(0f, value);
        }

        private void DrawSurfaceFitSection()
        {
            bool isWall = IsWallPlacementType();
            EditorGUILayout.Space(2f);
            EditorGUILayout.PropertyField(_surfaceFitMode, new GUIContent(
                "Surface Fit",
                isWall
                    ? "Strict uses the sampled wall contact. Adaptive probes the complete wall-facing footprint and is recommended for uneven or curved walls."
                    : "Strict requires the footprint to fit its sampled region. Adaptive probes the real surface and is recommended for uneven terrain."));

            if (!IsAdaptiveSurfaceFit())
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_surfaceAlignmentMode, new GUIContent(
                    "Rotation",
                    isWall
                        ? "Align To Surface follows the fitted wall normal. Keep Upright follows its horizontal direction without tilting vertically."
                        : "Align To Surface follows the fitted normal. Keep Upright uses the fitted height without tilting."));

                if (!isWall)
                {
                    EditorGUILayout.PropertyField(_surfaceHeightMode, new GUIContent(
                        "Height",
                        "Choose the Average, Lowest, or Highest supported probe height as the placement height."));
                }

                EditorGUILayout.PropertyField(_maxSurfaceHeightDifference, new GUIContent(
                    isWall ? "Max Depth Difference" : "Max Height Difference",
                    isWall
                        ? "Reject the placement when supported wall probes vary more than this distance along the wall normal."
                        : "Reject the placement when supported footprint probes span a larger vertical range."));
                EditorGUILayout.PropertyField(_minSurfaceSupport, new GUIContent(
                    "Min Support",
                    isWall
                        ? "Minimum fraction of the wall-facing footprint that must find a compatible wall surface."
                        : "Minimum fraction of footprint probes that must find a compatible surface."));
                EditorGUILayout.PropertyField(_surfaceSinkOffset, new GUIContent(
                    "Sink Offset",
                    isWall
                        ? "Move the fitted asset into the wall by this distance to avoid visible gaps."
                        : "Move the fitted asset into the support surface by this distance to avoid visible gaps."));
            }
        }

        private void DrawBoundsSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Bounds", EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!_prefab.objectReferenceValue))
                {
                    if (GUILayout.Button(new GUIContent(
                            "Generate From Prefab",
                            "Recalculate placement size and center offset from the prefab renderers and colliders."),
                        GUILayout.Width(140f)))
                        UpdateBoundsFromPrefab();
                }
            }

            EditorGUILayout.PropertyField(_boundsSize, BoundsSizeLabel);
            EditorGUILayout.PropertyField(_boundsCenterOffset, BoundsCenterLabel);
        }

        private void DrawSemanticTagsSection()
        {
            DrawSectionHeader("Semantic Tags", () =>
            {
                using (new EditorGUI.DisabledScope(_semanticTags.arraySize == 0 && _anyTagCategories.arraySize == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                        ClearAssignedSemanticTags();
                }
            });

            DrawSemanticTagCategoryFields();
        }

        private void DrawSemanticTagCategoryFields()
        {
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            List<TagCategory> categories = catalog.Categories
                .Where(category => category && category.SupportsAssets)
                .OrderBy(category => category.DisplayName)
                .ToList();

            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No Asset or Asset and Surface tag categories are available.",
                    MessageType.Info);
                return;
            }

            foreach (TagCategory category in categories)
                DrawSemanticTagCategoryField(catalog, category);
        }

        private void DrawSemanticTagCategoryField(AssetCatalog catalog, TagCategory category)
        {
            List<SemanticTag> tags = catalog.Tags
                .Where(tag => tag && tag.Category == category)
                .OrderBy(tag => tag.DisplayName)
                .ToList();

            List<SemanticTag> assignedTags = GetAssignedTagsInCategory(category);
            bool anySelected = IsAnySelectedInCategory(category);

            TagSelectionField.Draw(
                category.DisplayName,
                category,
                tags,
                assignedTags,
                selectedTags => SetAssignedTagsForCategory(category, selectedTags),
                anySelected: anySelected,
                onChangedWithSpecialSelection: (selectedTags, specialSelection) =>
                    SetAssignedTagsForCategory(category, selectedTags, specialSelection == TagSelectionField.SpecialSelection.Any));
        }

        private List<SemanticTag> GetAssignedTagsInCategory(TagCategory category)
        {
            List<SemanticTag> tags = new();

            for (int i = 0; i < _semanticTags.arraySize; i++)
            {
                SemanticTag tag =
                    _semanticTags.GetArrayElementAtIndex(i).objectReferenceValue as SemanticTag;

                if (tag && tag.Category == category)
                    tags.Add(tag);
            }

            return tags;
        }

        private bool IsAnySelectedInCategory(TagCategory category)
        {
            for (int i = 0; i < _anyTagCategories.arraySize; i++)
            {
                TagCategory anyCategory =
                    _anyTagCategories.GetArrayElementAtIndex(i).objectReferenceValue as TagCategory;

                if (anyCategory == category)
                    return true;
            }

            return false;
        }

        private void SetAssignedTagsForCategory(
            TagCategory category,
            IReadOnlyList<SemanticTag> selectedTags,
            bool selectAny = false)
        {
            serializedObject.Update();

            RemoveAssignedTagsInCategory(category);
            RemoveAnyCategory(category);

            if (selectAny)
            {
                int anyIndex = _anyTagCategories.arraySize;
                _anyTagCategories.InsertArrayElementAtIndex(anyIndex);
                _anyTagCategories.GetArrayElementAtIndex(anyIndex).objectReferenceValue = category;

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                return;
            }

            List<SemanticTag> validTags = selectedTags
                .Where(tag => tag && tag.Category == category)
                .Distinct()
                .ToList();

            if (!category.AllowMultipleTags)
                validTags = validTags.Take(1).ToList();

            foreach (SemanticTag tag in validTags)
            {
                int index = _semanticTags.arraySize;
                _semanticTags.InsertArrayElementAtIndex(index);
                _semanticTags.GetArrayElementAtIndex(index).objectReferenceValue = tag;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void DrawSectionHeader(string title, Action drawButtons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, EditorStyles.boldLabel, GUILayout.ExpandWidth(false));

                GUILayout.Space(8f);
                GUILayout.FlexibleSpace();

                drawButtons?.Invoke();
            }
        }

        private void RemoveAssignedTagsInCategory(TagCategory category)
        {
            for (int i = _semanticTags.arraySize - 1; i >= 0; i--)
            {
                SemanticTag tag =
                    _semanticTags.GetArrayElementAtIndex(i).objectReferenceValue as SemanticTag;

                if (tag && tag.Category == category)
                    _semanticTags.DeleteArrayElementAtIndex(i);
            }
        }

        private void RemoveAnyCategory(TagCategory category)
        {
            for (int i = _anyTagCategories.arraySize - 1; i >= 0; i--)
            {
                TagCategory existingCategory =
                    _anyTagCategories.GetArrayElementAtIndex(i).objectReferenceValue as TagCategory;

                if (!existingCategory || existingCategory == category)
                    _anyTagCategories.DeleteArrayElementAtIndex(i);
            }
        }

        private void ClearAssignedSemanticTags()
        {
            _semanticTags.ClearArray();
            _anyTagCategories.ClearArray();

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            GUI.FocusControl(null);
        }

        private void UpdateBoundsFromPrefab()
        {
            GameObject prefab = _prefab.objectReferenceValue as GameObject;

            if (!AssetDefinitionFactory.TryGetPrefabBounds(prefab, out Vector3 boundsSize, out Vector3 boundsCenterOffset))
                return;

            _boundsSize.vector3Value = boundsSize;
            _boundsCenterOffset.vector3Value = boundsCenterOffset;
        }

        private bool IsWallPlacementType()
        {
            if (_placementType.enumValueIndex < 0 ||
                _placementType.enumValueIndex >= _placementType.enumNames.Length)
            {
                return false;
            }

            return _placementType.enumNames[_placementType.enumValueIndex] == nameof(PlacementType.Wall);
        }

        private bool IsInsideSpacePlacementType()
        {
            if (_placementType.enumValueIndex < 0 ||
                _placementType.enumValueIndex >= _placementType.enumNames.Length)
            {
                return false;
            }

            return _placementType.enumNames[_placementType.enumValueIndex] == nameof(PlacementType.InsideSpace);
        }

        private bool IsFloorOrCeilingPlacementType()
        {
            if (_placementType.enumValueIndex < 0 ||
                _placementType.enumValueIndex >= _placementType.enumNames.Length)
            {
                return false;
            }

            string placementType = _placementType.enumNames[_placementType.enumValueIndex];
            return placementType is nameof(PlacementType.Floor) or nameof(PlacementType.Ceiling);
        }

        private bool IsAdaptiveSurfaceFit()
        {
            if (_surfaceFitMode.enumValueIndex < 0 ||
                _surfaceFitMode.enumValueIndex >= _surfaceFitMode.enumNames.Length)
            {
                return false;
            }

            return _surfaceFitMode.enumNames[_surfaceFitMode.enumValueIndex] == nameof(SurfaceFitMode.Adaptive);
        }

        private bool UsesSupportForward()
        {
            if (_orientationMode.enumValueIndex < 0 ||
                _orientationMode.enumValueIndex >= _orientationMode.enumNames.Length)
            {
                return false;
            }

            return _orientationMode.enumNames[_orientationMode.enumValueIndex] ==
                   nameof(global::Genix.Orientation.OrientationMode.MatchSupportForward);
        }

        private static GUIContent RotationLabel(string name, string axis) =>
            new($"Random {name}", $"Apply a random rotation around the asset's {axis}.");
    }
}
