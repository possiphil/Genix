using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Assets;
using Genix.Editor.Common;
using Genix.Editor.UI;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    public sealed partial class AssetDefinitionEditor
    {
        private void DrawSupportSurfaceSection()
        {
            EditorGUILayout.LabelField(new GUIContent(
                    "Support Surface",
                    IsInsideSpacePlacementType()
                        ? "Inside Space assets do not use a support collider, so support tags are ignored."
                        : "Required defaults to Any and restricts surfaces only when tags are selected. Blocked defaults to None and always takes precedence. Selecting None under Required or Any under Blocked intentionally disables placement."),
                EditorStyles.boldLabel);

            DrawSupportTagList(
                _requiredSupportTags,
                _requiredSupportNoneCategories,
                true,
                "Required Support Tags",
                "Any adds no restriction. None deliberately disables placement. Within one category, any selected tag may match; every category containing a selection must match.");
            DrawSupportTagList(
                _forbiddenSupportTags,
                _forbiddenSupportAnyCategories,
                false,
                "Blocked Support Tags",
                "None adds no restriction. Any rejects every surface. Otherwise each selected matching tag rejects the surface.");

            List<SemanticTag> conflicts = GetTags(_requiredSupportTags)
                .Intersect(GetTags(_forbiddenSupportTags))
                .ToList();

            if (conflicts.Count > 0)
            {
                string names = string.Join(", ", conflicts.Select(tag => tag.DisplayName));
                EditorGUILayout.HelpBox(
                    $"Required and Blocked contain the same tag(s): {names}. Blocked takes precedence, so those surfaces will be rejected.",
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
                    $"Blocked is set to Any for: {string.Join(", ", forbiddenAny.Select(category => category.DisplayName))}. This asset cannot be placed until those categories are changed.",
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
                string label = categories.Count == 1
                    ? title
                    : $"{title}: {category.DisplayName}";

                TagSelectionField.Draw(
                    label,
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
                    showAnyOption: true,
                    tooltip: tooltip);
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
                mode == WallProximityMode.NearWall ? "Max Wall Distance (units)" : "Min Wall Distance (units)",
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
                    "Fixed Height (units)",
                    "Height of the asset's lower bound above the target area's lower bound. Zero rests the asset on that lower boundary."));
                return;
            }

            if (mode == WallVerticalPlacementMode.HeightRange)
            {
                DrawNonNegativeFloat(_wallMinHeight, new GUIContent(
                    "Min Height (units)",
                    "Lowest permitted asset-bottom height above the target area's lower bound."));
                DrawNonNegativeFloat(_wallMaxHeight, new GUIContent(
                    "Max Height (units)",
                    "Highest permitted asset-bottom height above the target area's lower bound."));

                if (_wallMaxHeight.floatValue < _wallMinHeight.floatValue)
                    _wallMaxHeight.floatValue = _wallMinHeight.floatValue;

                return;
            }

            EditorGUILayout.PropertyField(_placementHeight, new GUIContent(
                "Baseline Offset (units)",
                "Additional vertical clearance above every sampled wall baseline. Zero keeps the asset's lower bound flush with each sampled level."));
        }

        private static void DrawNonNegativeFloat(SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.FloatField(label, property.floatValue);

            if (EditorGUI.EndChangeCheck())
                property.floatValue = Mathf.Max(0f, value);
        }
    }
}
