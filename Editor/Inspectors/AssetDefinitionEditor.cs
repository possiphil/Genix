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
        private static readonly GUIContent PrefabRotationOffsetLabel = new(
            "Rotation Offset",
            "Correct imported prefab axes without modifying or wrapping the prefab. Genix applies this local Euler rotation after surface alignment and uses it consistently for bounds, clearance, preview, and generation. For wall assets, adjust it until the visible front follows Genix +Z; Random Roll remains available for placement variation.");
        private static readonly GUIContent PlacementTypeLabel = new(
            "Placement Type",
            "Surface or volume target this asset can use: Floor, Wall, Ceiling, or Inside Space.");
        private static readonly GUIContent OrientationModeLabel = new(
            "Orientation",
            "None keeps the sampled orientation. Face Target uses a relative-placement anchor. Match Support Forward automatically uses the support object's local Z direction, or local X when Z is perpendicular to the surface.");
        private static readonly GUIContent BoundsSizeLabel = new(
            "Size",
            "Source-prefab local bounds before Rotation Offset. Genix derives corrected placement dimensions for containment, spacing, overlap, and surface fit.");
        private static readonly GUIContent BoundsCenterLabel = new(
            "Center Offset",
            "Source-prefab local offset to the bounds center. Prefab scale and Rotation Offset are applied automatically during placement.");
        private static readonly string[] WallDepthModeLabels =
        {
            "Average Depth",
            "Deepest",
            "Outermost"
        };

        private SerializedProperty _prefab;
        private SerializedProperty _semanticTags;
        private SerializedProperty _anyTagCategories;
        private SerializedProperty _requiredSupportTags;
        private SerializedProperty _forbiddenSupportTags;
        private SerializedProperty _requiredSupportNoneCategories;
        private SerializedProperty _forbiddenSupportAnyCategories;
        private SerializedProperty _limitPlacements;
        private SerializedProperty _maxPlacements;
        private SerializedProperty _spacingRules;
        private SerializedProperty _assetRelativePlacement;
        private SerializedProperty _pathPlacement;
        private SerializedProperty _placementType;
        private SerializedProperty _wallVerticalPlacementMode;
        private SerializedProperty _placementHeight;
        private SerializedProperty _wallMinHeight;
        private SerializedProperty _wallMaxHeight;
        private SerializedProperty _prefabRotationOffset;
        private SerializedProperty _boundsSize;
        private SerializedProperty _boundsCenterOffset;
        private SerializedProperty _reserveClearance;
        private SerializedProperty _clearanceSize;
        private SerializedProperty _clearanceCenterOffset;
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
            _spacingRules = serializedObject.FindProperty("spacingRules");
            _assetRelativePlacement = serializedObject.FindProperty("assetRelativePlacement");
            _pathPlacement = serializedObject.FindProperty("pathPlacement");
            _placementType = serializedObject.FindProperty("placementType");
            _wallVerticalPlacementMode = serializedObject.FindProperty("wallVerticalPlacementMode");
            _placementHeight = serializedObject.FindProperty("placementHeight");
            _wallMinHeight = serializedObject.FindProperty("wallMinHeight");
            _wallMaxHeight = serializedObject.FindProperty("wallMaxHeight");
            _prefabRotationOffset = serializedObject.FindProperty("prefabRotationOffset");
            _boundsSize = serializedObject.FindProperty("boundsSize");
            _boundsCenterOffset = serializedObject.FindProperty("boundsCenterOffset");
            _reserveClearance = serializedObject.FindProperty("reserveClearance");
            _clearanceSize = serializedObject.FindProperty("clearanceSize");
            _clearanceCenterOffset = serializedObject.FindProperty("clearanceCenterOffset");
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

            EditorGUILayout.PropertyField(_prefabRotationOffset, PrefabRotationOffsetLabel);
        }

        private void DrawPlacementSection()
        {
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_placementType, PlacementTypeLabel);
            EditorGUILayout.PropertyField(_limitPlacements, new GUIContent(
                "Limit Placements",
                "Restrict how often this asset may exist in the generated output across repeated runs."));

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

            DrawAssetSpacingRules();
            DrawAssetRelativePlacement();
            DrawPathPlacement();

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
            EditorGUILayout.LabelField(new GUIContent(
                    "Support Surface",
                    IsInsideSpacePlacementType()
                        ? "Inside Space assets do not use a support collider, so support tags are ignored."
                        : "Required defaults to Any and restricts surfaces only when tags are selected. Forbidden defaults to None and always takes precedence. Selecting None under Required or Any under Forbidden intentionally blocks this asset."),
                EditorStyles.boldLabel);

            DrawSupportTagList(
                _requiredSupportTags,
                _requiredSupportNoneCategories,
                true,
                "Required Tags",
                "Any adds no restriction. None deliberately disables placement. Within one category, any selected tag may match; every category containing a selection must match.");
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
                    MessageType.Warning);
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

            if (IsAdaptiveSurfaceFit())
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_surfaceAlignmentMode, new GUIContent(
                        "Rotation",
                        isWall
                            ? "Align To Surface follows the fitted wall normal. Keep Upright follows its horizontal direction without tilting vertically."
                            : "Align To Surface follows the fitted normal. Keep Upright uses the fitted height without tilting."));

                    if (isWall)
                    {
                        int selectedDepth = EditorGUILayout.Popup(
                            new GUIContent(
                                "Depth",
                                "Average Depth uses the mean supported wall depth. Deepest embeds the asset at the most recessed supported probe to avoid visible gaps. Outermost uses the most protruding probe to minimize wall penetration."),
                            _surfaceHeightMode.enumValueIndex,
                            WallDepthModeLabels);
                        _surfaceHeightMode.enumValueIndex = selectedDepth;
                    }
                    else
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
                }
            }

            EditorGUILayout.PropertyField(_surfaceSinkOffset, new GUIContent(
                "Sink Offset",
                isWall
                    ? "Move the asset into the wall by this distance to compensate for pivots, mounts, or tiny visible gaps."
                    : "Move the asset into the support surface by this distance to compensate for pivots or tiny visible gaps."));
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

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(_reserveClearance, new GUIContent(
                "Reserve Clearance",
                "Reserve an additional invisible volume that fixed geometry and other generated objects may not enter. Clearance is bidirectional: other visuals cannot enter it, and it cannot overlap other visuals, clearances, or fixed colliders."));

            if (_reserveClearance.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_clearanceSize, new GUIContent(
                        "Clearance Size",
                        "Full local-space size of the reserved volume. It rotates with the asset and creates no gameplay collider."));
                    EditorGUILayout.PropertyField(_clearanceCenterOffset, new GUIContent(
                        "Center Offset",
                        "Clearance center relative to the prefab transform origin."));

                    if (GUILayout.Button(new GUIContent(
                            "Start From Placement Bounds",
                            "Copy the placement bounds into the clearance volume, then adjust the sides that need extra room.")))
                    {
                        _clearanceSize.vector3Value = _boundsSize.vector3Value;
                        _clearanceCenterOffset.vector3Value = _boundsCenterOffset.vector3Value;
                    }
                }
            }
        }

        private void DrawAssetSpacingRules()
        {
            EditorGUILayout.Space(3f);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(
                    "Asset Spacing",
                    "Optional center-to-center distances from one asset or every asset carrying a selected tag. Distances are symmetric and the larger matching requirement wins. Floor and Ceiling use horizontal distance; Wall and Inside Space use 3D distance."),
                    EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Add", GUILayout.Width(44f)))
                {
                    int index = _spacingRules.arraySize;
                    _spacingRules.InsertArrayElementAtIndex(index);
                    SerializedProperty added = _spacingRules.GetArrayElementAtIndex(index);
                    added.FindPropertyRelative("scope").enumValueIndex = (int)AssetSpacingRuleScope.AssetTag;
                    added.FindPropertyRelative("asset").objectReferenceValue = null;
                    added.FindPropertyRelative("assetTag").objectReferenceValue = null;
                    added.FindPropertyRelative("minimumDistance").floatValue = 1f;
                }
            }

            if (_spacingRules.arraySize == 0)
                return;

            for (int i = 0; i < _spacingRules.arraySize; i++)
            {
                SerializedProperty rule = _spacingRules.GetArrayElementAtIndex(i);
                SerializedProperty scope = rule.FindPropertyRelative("scope");
                SerializedProperty targetAsset = rule.FindPropertyRelative("asset");
                SerializedProperty assetTag = rule.FindPropertyRelative("assetTag");
                SerializedProperty distance = rule.FindPropertyRelative("minimumDistance");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(scope, new GUIContent("Match By"));

                        if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus"), GUILayout.Width(24f)))
                        {
                            _spacingRules.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    if ((AssetSpacingRuleScope)scope.enumValueIndex == AssetSpacingRuleScope.Asset)
                    {
                        EditorGUILayout.PropertyField(targetAsset, new GUIContent(
                            "Neighbor Asset",
                            "Concrete asset definition whose instances must keep this distance."));
                    }
                    else
                    {
                        DrawAssetSpacingTagField(assetTag);
                    }

                    EditorGUI.BeginChangeCheck();
                    float value = EditorGUILayout.FloatField(new GUIContent(
                        "Minimum Distance",
                        "Required center-to-center distance in world units."),
                        distance.floatValue);
                    if (EditorGUI.EndChangeCheck())
                        distance.floatValue = Mathf.Max(0f, value);
                }
            }
        }

        private static void DrawAssetSpacingTagField(SerializedProperty property)
        {
            DrawAssetTagField(
                property,
                "Neighbor Tag",
                "Every neighboring asset carrying this asset-compatible semantic tag matches the rule.");
        }

        private static void DrawAssetTagField(
            SerializedProperty property,
            string fieldLabel,
            string tooltip)
        {
            SemanticTag current = property.objectReferenceValue as SemanticTag;
            string label = current ? current.DisplayName : "Select Asset Tag";
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(fieldLabel, tooltip));

            if (!EditorGUI.DropdownButton(rect, new GUIContent(label), FocusType.Keyboard))
                return;

            GenericMenu menu = new();
            menu.AddItem(new GUIContent("None"), !current, () =>
            {
                property.serializedObject.Update();
                property.objectReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
            });
            menu.AddSeparator(string.Empty);

            AssetCatalog catalog = AssetCatalogService.GetOrCreate();
            foreach (SemanticTag tag in catalog.Tags
                         .Where(tag => tag && tag.Category && tag.Category.SupportsAssets)
                         .OrderBy(tag => tag.Category.DisplayName)
                         .ThenBy(tag => tag.DisplayName))
            {
                SemanticTag captured = tag;
                menu.AddItem(
                    new GUIContent($"{tag.Category.DisplayName}/{tag.DisplayName}"),
                    current == tag,
                    () =>
                    {
                        property.serializedObject.Update();
                        property.objectReferenceValue = captured;
                        property.serializedObject.ApplyModifiedProperties();
                    });
            }

            menu.DropDown(rect);
        }

        private void DrawAssetRelativePlacement()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Asset-Relative Placement", EditorStyles.miniBoldLabel);
            SerializedProperty enabled = _assetRelativePlacement.FindPropertyRelative("enabled");
            EditorGUILayout.PropertyField(enabled, new GUIContent(
                "Enabled",
                "Require this asset to be positioned relative to a matching generated asset or explicit scene anchor."));

            if (!enabled.boolValue)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty source = _assetRelativePlacement.FindPropertyRelative("source");
                SerializedProperty scope = _assetRelativePlacement.FindPropertyRelative("targetScope");
                SerializedProperty targetAsset = _assetRelativePlacement.FindPropertyRelative("targetAsset");
                SerializedProperty targetTag = _assetRelativePlacement.FindPropertyRelative("targetTag");
                SerializedProperty side = _assetRelativePlacement.FindPropertyRelative("side");
                SerializedProperty additionalSides = _assetRelativePlacement.FindPropertyRelative("additionalSides");
                SerializedProperty alignment = _assetRelativePlacement.FindPropertyRelative("alignment");
                SerializedProperty sameSupport = _assetRelativePlacement.FindPropertyRelative("requireSameSupportSurface");
                SerializedProperty insideAnchor = _assetRelativePlacement.FindPropertyRelative("requireInsideAnchorBounds");
                SerializedProperty minimum = _assetRelativePlacement.FindPropertyRelative("minimumDistance");
                SerializedProperty maximum = _assetRelativePlacement.FindPropertyRelative("maximumDistance");
                SerializedProperty facing = _assetRelativePlacement.FindPropertyRelative("facing");
                SerializedProperty facingVariation = _assetRelativePlacement.FindPropertyRelative("facingVariationDegrees");
                SerializedProperty cardinalityMode = _assetRelativePlacement.FindPropertyRelative("cardinalityMode");
                SerializedProperty cardinalityCount = _assetRelativePlacement.FindPropertyRelative("cardinalityCount");
                SerializedProperty cardinalityMaximumCount =
                    _assetRelativePlacement.FindPropertyRelative("cardinalityMaximumCount");
                SerializedProperty usePathStations = _assetRelativePlacement.FindPropertyRelative("usePathStations");
                SerializedProperty pathStationSides = _assetRelativePlacement.FindPropertyRelative("pathStationSides");
                SerializedProperty pathStationSpacing = _assetRelativePlacement.FindPropertyRelative("pathStationSpacing");
                SerializedProperty pathStationLateralOffset = _assetRelativePlacement.FindPropertyRelative("pathStationLateralOffset");
                SerializedProperty pathStationEndpointMargin = _assetRelativePlacement.FindPropertyRelative("pathStationEndpointMargin");
                SerializedProperty pathStationMaximumCount = _assetRelativePlacement.FindPropertyRelative("pathStationMaximumCount");

                EditorGUILayout.PropertyField(source, new GUIContent(
                    "Anchor Source",
                    "Generated Objects uses current and previous Genix output. Scene Anchors uses explicit Asset Relation Anchor components, which can be added through GameObject > Genix > Add Asset Relation Anchor. Any uses both."));
                EditorGUILayout.PropertyField(scope, new GUIContent(
                    "Match By",
                    "Match one exact Asset Definition or any anchor carrying an asset-compatible semantic tag."));

                if ((AssetRelativeTargetScope)scope.enumValueIndex == AssetRelativeTargetScope.Asset)
                {
                    EditorGUILayout.PropertyField(targetAsset, new GUIContent(
                        "Target Asset",
                        "Concrete generated asset or scene anchor with the same Represented Asset."));
                }
                else
                {
                    DrawAssetTagField(
                        targetTag,
                        "Target Tag",
                        "Generated assets and scene anchors carrying this asset-compatible tag may satisfy the relation.");
                }

                bool canUsePathStations =
                    (AssetRelativeTargetScope)scope.enumValueIndex == AssetRelativeTargetScope.AssetTag &&
                    (AssetRelativeAnchorSource)source.enumValueIndex is
                        AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.SceneAnchors;
                if (canUsePathStations)
                {
                    EditorGUILayout.PropertyField(usePathStations, new GUIContent(
                        "Regular Path Stations",
                        "Derive virtual anchors from every matching Path Placement Source instead of authoring one scene anchor per object. Use Exactly 1 with Both Sides for paired roadside objects."));
                    if (usePathStations.boolValue)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            DrawPathStationSides(pathStationSides);
                            pathStationSpacing.floatValue = Mathf.Max(0.1f, EditorGUILayout.FloatField(
                                new GUIContent("Station Spacing", "Distance along the path between station groups."),
                                pathStationSpacing.floatValue));
                            pathStationLateralOffset.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(
                                new GUIContent("Lateral Offset", "Horizontal distance from the path centerline."),
                                pathStationLateralOffset.floatValue));
                            pathStationEndpointMargin.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(
                                new GUIContent("Endpoint Margin", "Path length ignored at both ends."),
                                pathStationEndpointMargin.floatValue));
                            pathStationMaximumCount.intValue = Mathf.Max(1, EditorGUILayout.IntField(
                                new GUIContent("Max Station Groups", "Maximum regular station groups across all matching paths in the target area."),
                                pathStationMaximumCount.intValue));
                        }
                    }
                }
                else
                {
                    usePathStations.boolValue = false;
                }

                DrawAssetRelativeSides(side, additionalSides);
                DrawAssetRelativeAlignment(
                    alignment,
                    GetAssetRelativeSides(side, additionalSides));
                EditorGUILayout.PropertyField(sameSupport, new GUIContent(
                    "Require Same Support Surface",
                    "Require candidate and anchor to reference the same Placement Surface Descriptor. Use this to keep a keyboard and monitor on the same desk. Both placements need a descriptor reference; for fixed scene anchors, assign Support Surface on the Asset Relation Anchor."));
                EditorGUILayout.PropertyField(insideAnchor, new GUIContent(
                    "Require Inside Anchor Bounds",
                    "Keep the complete generated asset inside the matched anchor bounds while still projecting it onto its normal placement surface. Use semantic regions such as parking areas, rest areas, habitat zones, or work cells without turning those regions into artificial support surfaces."));

                EditorGUI.BeginChangeCheck();
                float minValue = EditorGUILayout.FloatField(new GUIContent(
                    "Minimum Distance",
                    "Minimum 3D distance from the nearest point on the anchor bounds."),
                    minimum.floatValue);
                float maxValue = EditorGUILayout.FloatField(new GUIContent(
                    "Maximum Distance",
                    "Maximum 3D distance from the nearest point on the anchor bounds."),
                    maximum.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    minimum.floatValue = Mathf.Max(0f, minValue);
                    maximum.floatValue = Mathf.Max(minimum.floatValue, maxValue);
                }

                EditorGUILayout.PropertyField(facing, new GUIContent(
                    "Facing",
                    "Any keeps the normal orientation. Toward/Away face relative to the anchor center. Match Forward copies the anchor's local +Z direction. Asset-relative Facing takes precedence over the global Face Target orientation."));
                if ((AssetRelativeFacing)facing.enumValueIndex != AssetRelativeFacing.Any)
                {
                    facingVariation.floatValue = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent(
                        "Max Facing Deviation",
                        "Maximum deterministic yaw variation in either direction from the resolved facing. Zero faces exactly; 45 allows an angle from -45 to +45 degrees."),
                        facingVariation.floatValue), 0f, 180f);
                }

                EditorGUILayout.PropertyField(cardinalityMode, new GUIContent(
                    "Per Anchor Count",
                    "Unlimited adds no count rule. At Most limits optional dependents. At Least, Exactly, and Between actively complete their required minimum locally for every matching anchor; all generated parts count toward Object Count. Required dependents are planned immediately after their anchor, including transitive requirements. Genix reserves their slots and rolls back an incomplete new group."));
                AssetRelativeCardinalityMode selectedCardinality =
                    (AssetRelativeCardinalityMode)cardinalityMode.enumValueIndex;
                if (selectedCardinality != AssetRelativeCardinalityMode.Unlimited)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        if (selectedCardinality == AssetRelativeCardinalityMode.Between)
                        {
                            cardinalityCount.intValue = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent(
                                "Minimum Count",
                                "Minimum instances generation must complete for each matching anchor."),
                                cardinalityCount.intValue));
                            cardinalityMaximumCount.intValue = Mathf.Max(
                                cardinalityCount.intValue,
                                EditorGUILayout.IntField(new GUIContent(
                                    "Maximum Count",
                                    "Maximum instances allowed per matching anchor, including existing generated output."),
                                    cardinalityMaximumCount.intValue));
                        }
                        else
                        {
                            cardinalityCount.intValue = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent(
                                "Count",
                                selectedCardinality switch
                                {
                                    AssetRelativeCardinalityMode.AtMost =>
                                        "Maximum optional instances assigned to each matching anchor. The count includes previous generated output.",
                                    AssetRelativeCardinalityMode.AtLeast =>
                                        "Minimum instances generation must complete for each matching anchor. Additional instances remain possible.",
                                    AssetRelativeCardinalityMode.Exactly =>
                                        "Exact instances generation must complete for each matching anchor. This is both a minimum and a maximum.",
                                    _ => string.Empty
                                }),
                                cardinalityCount.intValue));
                            cardinalityMaximumCount.intValue = cardinalityCount.intValue;
                        }
                    }
                }

                AssetRelativeFacing selectedFacing = (AssetRelativeFacing)facing.enumValueIndex;
                if (IsWallPlacementType() && selectedFacing != AssetRelativeFacing.Any)
                {
                    EditorGUILayout.HelpBox(
                        "Wall assets must remain flush with their support, so asset-relative Facing is ignored. Positional side and distance constraints still apply.",
                        MessageType.Warning);
                }
                bool missingTarget = (AssetRelativeTargetScope)scope.enumValueIndex == AssetRelativeTargetScope.Asset
                    ? !targetAsset.objectReferenceValue
                    : !targetTag.objectReferenceValue;
                if (missingTarget)
                {
                    EditorGUILayout.HelpBox(
                        "Select a target before this relationship can be satisfied.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawPathPlacement()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Path Placement", EditorStyles.miniBoldLabel);
            SerializedProperty enabled = _pathPlacement.FindPropertyRelative("enabled");
            EditorGUILayout.PropertyField(enabled, new GUIContent(
                "Enabled",
                "Constrain this asset by horizontal distance, side, and facing relative to the nearest matching Path Placement Source. This composes with Asset-Relative Placement and semantic regions."));
            if (!enabled.boolValue)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty tag = _pathPlacement.FindPropertyRelative("pathTag");
                SerializedProperty minimum = _pathPlacement.FindPropertyRelative("minimumDistance");
                SerializedProperty maximum = _pathPlacement.FindPropertyRelative("maximumDistance");
                SerializedProperty endpointMargin = _pathPlacement.FindPropertyRelative("endpointMargin");
                SerializedProperty side = _pathPlacement.FindPropertyRelative("side");
                SerializedProperty facing = _pathPlacement.FindPropertyRelative("facing");
                SerializedProperty variation = _pathPlacement.FindPropertyRelative("facingVariationDegrees");

                DrawAssetTagField(
                    tag,
                    "Path Tag",
                    "Only Path Placement Sources carrying this asset-compatible semantic tag are considered.");
                EditorGUI.BeginChangeCheck();
                float minValue = EditorGUILayout.FloatField(new GUIContent(
                    "Minimum Distance",
                    "Minimum horizontal center distance from the nearest path centerline."), minimum.floatValue);
                float maxValue = EditorGUILayout.FloatField(new GUIContent(
                    "Maximum Distance",
                    "Maximum horizontal center distance from the nearest path centerline."), maximum.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    minimum.floatValue = Mathf.Max(0f, minValue);
                    maximum.floatValue = Mathf.Max(minimum.floatValue, maxValue);
                }
                endpointMargin.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent(
                    "Endpoint Margin",
                    "Path length excluded at both ends. Use this to keep objects away from path entrances, exits, junctions, or transitions."),
                    endpointMargin.floatValue));

                DrawPathConstraintSide(side);
                EditorGUILayout.PropertyField(facing, new GUIContent(
                    "Facing",
                    "Orient along, against, toward, or away from the nearest path. Any keeps the normal asset orientation."));
                if ((PathPlacementFacing)facing.enumValueIndex != PathPlacementFacing.Any)
                {
                    variation.floatValue = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent(
                        "Max Facing Deviation",
                        "Maximum deterministic yaw variation in either direction from the path-relative direction."),
                        variation.floatValue), 0f, 180f);
                }

                if (!tag.objectReferenceValue)
                {
                    EditorGUILayout.HelpBox(
                        "Select a Path Tag before this constraint can be satisfied.",
                        MessageType.Warning);
                }
            }
        }

        private static void DrawPathConstraintSide(SerializedProperty side)
        {
            PathPlacementSide current = (PathPlacementSide)side.enumValueIndex;
            int index = current switch
            {
                PathPlacementSide.Left => 1,
                PathPlacementSide.Right => 2,
                _ => 0
            };
            index = EditorGUILayout.Popup(
                new GUIContent(
                    "Side",
                    "Any accepts both sides. Left and Right follow the authored path direction."),
                index,
                new[] { "Any", "Left", "Right" });
            side.enumValueIndex = (int)(index switch
            {
                1 => PathPlacementSide.Left,
                2 => PathPlacementSide.Right,
                _ => PathPlacementSide.Any
            });
        }

        private static void DrawPathStationSides(SerializedProperty sides)
        {
            PathPlacementSide current = (PathPlacementSide)sides.enumValueIndex;
            int index = current switch
            {
                PathPlacementSide.Left => 0,
                PathPlacementSide.Right => 1,
                _ => 2
            };
            index = EditorGUILayout.Popup(
                new GUIContent(
                    "Station Sides",
                    "Create one virtual anchor on the left, right, or both sides of each station. Both Sides keeps pairs aligned along the path."),
                index,
                new[] { "Left", "Right", "Both Sides" });
            sides.enumValueIndex = (int)(index switch
            {
                0 => PathPlacementSide.Left,
                1 => PathPlacementSide.Right,
                _ => PathPlacementSide.BothSides
            });
        }

        private static void DrawAssetRelativeSides(
            SerializedProperty primarySide,
            SerializedProperty additionalSides)
        {
            List<AssetRelativeSide> selected = GetAssetRelativeSides(primarySide, additionalSides);
            string summary = selected.Count switch
            {
                0 => "Any",
                <= 2 => string.Join(", ", selected.Select(value => value.ToString())),
                _ => $"{selected[0]}, {selected[1]} +{selected.Count - 2}"
            };
            Rect row = EditorGUILayout.GetControlRect();
            Rect button = EditorGUI.PrefixLabel(row, new GUIContent(
                "Required Sides",
                "Accepted dominant-axis sectors around the anchor. Any disables the restriction; Front is local +Z, Back -Z, Left -X, Right +X, Above world +Y, and Below world -Y. Horizontal-only rules ignore height differences for backward compatibility."));

            if (!EditorGUI.DropdownButton(button, new GUIContent(summary), FocusType.Keyboard))
                return;

            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("Any"),
                selected.Count == 0,
                () => SetAssetRelativeSides(primarySide, additionalSides, Array.Empty<AssetRelativeSide>()));
            menu.AddSeparator(string.Empty);

            foreach (AssetRelativeSide side in new[]
                     {
                         AssetRelativeSide.Front,
                         AssetRelativeSide.Back,
                         AssetRelativeSide.Left,
                         AssetRelativeSide.Right,
                         AssetRelativeSide.Above,
                         AssetRelativeSide.Below
                     })
            {
                AssetRelativeSide captured = side;
                menu.AddItem(
                    new GUIContent(side.ToString()),
                    selected.Contains(side),
                    () =>
                    {
                        List<AssetRelativeSide> updated = GetAssetRelativeSides(primarySide, additionalSides);
                        if (!updated.Remove(captured))
                            updated.Add(captured);
                        SetAssetRelativeSides(primarySide, additionalSides, updated);
                    });
            }

            menu.DropDown(button);
        }

        private static List<AssetRelativeSide> GetAssetRelativeSides(
            SerializedProperty primarySide,
            SerializedProperty additionalSides)
        {
            List<AssetRelativeSide> sides = new();
            AssetRelativeSide primary = (AssetRelativeSide)primarySide.enumValueIndex;
            if (primary != AssetRelativeSide.Any)
                sides.Add(primary);

            for (int i = 0; i < additionalSides.arraySize; i++)
            {
                AssetRelativeSide side = (AssetRelativeSide)additionalSides
                    .GetArrayElementAtIndex(i)
                    .enumValueIndex;
                if (side != AssetRelativeSide.Any && !sides.Contains(side))
                    sides.Add(side);
            }

            return sides;
        }

        private static void DrawAssetRelativeAlignment(
            SerializedProperty alignment,
            IReadOnlyList<AssetRelativeSide> sides)
        {
            bool hasSingleHorizontalSide = sides.Count == 1 &&
                                           sides[0] is AssetRelativeSide.Front or AssetRelativeSide.Back or
                                               AssetRelativeSide.Left or AssetRelativeSide.Right;
            AssetRelativeAlignment current = (AssetRelativeAlignment)alignment.enumValueIndex;
            AssetRelativeAlignment[] values = sides.Count == 0
                ? new[] { AssetRelativeAlignment.Random }
                : hasSingleHorizontalSide
                ? new[]
                {
                    AssetRelativeAlignment.Random,
                    AssetRelativeAlignment.Center,
                    AssetRelativeAlignment.Start,
                    AssetRelativeAlignment.End
                }
                : new[]
                {
                    AssetRelativeAlignment.Random,
                    AssetRelativeAlignment.Center
                };
            string[] labels = sides.Count == 0
                ? new[] { "Random" }
                : hasSingleHorizontalSide
                ? sides[0] is AssetRelativeSide.Front or AssetRelativeSide.Back
                    ? new[] { "Random", "Center", "Left", "Right" }
                    : new[] { "Random", "Center", "Back", "Front" }
                : new[] { "Random", "Center" };
            int selected = Array.IndexOf(values, current);
            if (selected < 0)
                selected = 0;

            EditorGUI.BeginChangeCheck();
            selected = EditorGUILayout.Popup(new GUIContent(
                    "Alignment Within Side",
                    "Soft local preference after side and distance constraints. Random uses the fixed run seed. Center prefers the side midpoint. For one horizontal side, the remaining options prefer either local end. Above/Below and multi-side rules expose only Random or Center."),
                selected,
                labels);
            if (EditorGUI.EndChangeCheck())
                alignment.enumValueIndex = (int)values[selected];
        }

        private static void SetAssetRelativeSides(
            SerializedProperty primarySide,
            SerializedProperty additionalSides,
            IEnumerable<AssetRelativeSide> values)
        {
            List<AssetRelativeSide> sides = values
                .Where(value => value != AssetRelativeSide.Any)
                .Distinct()
                .ToList();
            SerializedObject serializedObject = primarySide.serializedObject;
            serializedObject.Update();
            primarySide.enumValueIndex = sides.Count > 0
                ? (int)sides[0]
                : (int)AssetRelativeSide.Any;
            additionalSides.ClearArray();
            for (int i = 1; i < sides.Count; i++)
            {
                int index = additionalSides.arraySize;
                additionalSides.InsertArrayElementAtIndex(index);
                additionalSides.GetArrayElementAtIndex(index).enumValueIndex = (int)sides[i];
            }
            serializedObject.ApplyModifiedProperties();
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
                return;

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
