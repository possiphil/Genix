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
            "None keeps the sampled orientation. Face Target turns the asset toward the nearest active relative-placement anchor.");
        private static readonly GUIContent BoundsSizeLabel = new(
            "Size",
            "World-space size used for containment, spacing, overlap, and surface-fit validation.");
        private static readonly GUIContent BoundsCenterLabel = new(
            "Center Offset",
            "Offset from the prefab transform origin to the center of its placement bounds.");

        private SerializedProperty _prefab;
        private SerializedProperty _semanticTags;
        private SerializedProperty _anyTagCategories;
        private SerializedProperty _placementType;
        private SerializedProperty _placementHeight;
        private SerializedProperty _useHeightOffset;
        private SerializedProperty _maxHeightOffset;
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

        private void OnEnable()
        {
            _prefab = serializedObject.FindProperty("prefab");
            _semanticTags = serializedObject.FindProperty("semanticTags");
            _anyTagCategories = serializedObject.FindProperty("anyTagCategories");
            _placementType = serializedObject.FindProperty("placementType");
            _placementHeight = serializedObject.FindProperty("placementHeight");
            _useHeightOffset = serializedObject.FindProperty("useHeightOffset");
            _maxHeightOffset = serializedObject.FindProperty("maxHeightOffset");
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

            if (IsWallPlacementType())
            {
                EditorGUILayout.PropertyField(_placementHeight, new GUIContent(
                    "Placement Height",
                    "Height above the sampled wall position at which the asset pivot is placed."));
                EditorGUILayout.PropertyField(_useHeightOffset, new GUIContent(
                    "Random Height Offset",
                    "Randomize the wall placement height within the configured maximum offset."));

                if (_useHeightOffset.boolValue)
                    EditorGUILayout.PropertyField(_maxHeightOffset, new GUIContent(
                        "Max Height Offset",
                        "Maximum absolute random offset from Placement Height."));
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
        }

        private void DrawSurfaceFitSection()
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.PropertyField(_surfaceFitMode, new GUIContent(
                "Surface Fit",
                "Strict requires the footprint to fit its sampled region. Adaptive probes the real surface and is recommended for uneven terrain."));

            if (!IsAdaptiveSurfaceFit())
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_surfaceAlignmentMode, new GUIContent(
                    "Rotation",
                    "Align To Surface follows the fitted normal. Keep Upright uses the fitted height without tilting."));
                EditorGUILayout.PropertyField(_surfaceHeightMode, new GUIContent(
                    "Height",
                    "Choose the Average, Lowest, or Highest supported probe height as the placement height."));
                EditorGUILayout.PropertyField(_maxSurfaceHeightDifference, new GUIContent(
                    "Max Height Difference",
                    "Reject the placement when supported footprint probes span a larger vertical range."));
                EditorGUILayout.PropertyField(_minSurfaceSupport, new GUIContent(
                    "Min Support",
                    "Minimum fraction of footprint probes that must find a compatible surface."));
                EditorGUILayout.PropertyField(_surfaceSinkOffset, new GUIContent(
                    "Sink Offset",
                    "Move the fitted asset into the support surface by this distance to avoid visible gaps."));
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
                .Where(category => category)
                .OrderBy(category => category.DisplayName)
                .ToList();

            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox("No tag categories available.", MessageType.Info);
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

        private bool IsAdaptiveSurfaceFit()
        {
            if (_surfaceFitMode.enumValueIndex < 0 ||
                _surfaceFitMode.enumValueIndex >= _surfaceFitMode.enumNames.Length)
            {
                return false;
            }

            return _surfaceFitMode.enumNames[_surfaceFitMode.enumValueIndex] == nameof(SurfaceFitMode.Adaptive);
        }

        private static GUIContent RotationLabel(string name, string axis) =>
            new($"Random {name}", $"Apply a random rotation around the asset's {axis}.");
    }
}
