using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.Genix.Editor.Common;
using Genix.Editor.Utilities;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    public sealed partial class AssetPoolEditor
    {
        private void DrawDynamicPool()
        {
            EditorGUILayout.LabelField("Placement Filters", EditorStyles.boldLabel);

            DrawPlacementTypeFilter();

            EditorGUILayout.Space(4f);

            DrawOrientationModeFilter();

            EditorGUILayout.Space(4f);

            DrawDynamicCategoryFilters();

            EditorGUILayout.Space(6f);

            DrawPreview();
        }

        private void DrawPlacementTypeFilter()
        {
            EditorGUILayout.PropertyField(_filterByPlacementType, new GUIContent("Filter By Placement Type"));

            if (_filterByPlacementType.boolValue)
                EditorGUILayout.PropertyField(_placementType, new GUIContent("Placement Type"));
        }

        private void DrawOrientationModeFilter()
        {
            EditorGUILayout.PropertyField(_filterByOrientationMode, new GUIContent("Filter By Orientation Mode"));

            if (_filterByOrientationMode.boolValue)
                EditorGUILayout.PropertyField(_orientationMode, new GUIContent("Orientation Mode"));
        }

        private void DrawDynamicCategoryFilters()
        {
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            List<TagCategory> categories = catalog.Categories
                .Where(category => category)
                .OrderBy(category => category.DisplayName)
                .ToList();

            DrawSemanticTagFiltersHeader();

            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox("No tag categories available.", MessageType.Info);
                return;
            }

            foreach (TagCategory category in categories)
                DrawDynamicCategoryFilter(catalog, category);
        }

        private void DrawSemanticTagFiltersHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Semantic Tag Filters", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));

                GUILayout.Space(8f);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(_categoryFilters.arraySize == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                        ClearDynamicCategoryFilters();
                }
            }
        }

        private void ClearDynamicCategoryFilters()
        {
            serializedObject.Update();

            _categoryFilters.ClearArray();

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            GUI.FocusControl(null);
            Repaint();
        }

        private void DrawDynamicCategoryFilter(
            AssetCatalog catalog,
            TagCategory category)
        {
            List<SemanticTag> tags = catalog.Tags
                .Where(tag => tag && tag.Category == category)
                .OrderBy(tag => tag.DisplayName)
                .ToList();

            IReadOnlyList<SemanticTag> selectedTags =
                GetSelectedDynamicCategoryFilterTags(category);

            TagSelectionField.Draw(
                category.DisplayName,
                category,
                tags,
                selectedTags,
                newSelection => SetDynamicCategoryFilter(category, newSelection),
                forceMultiSelect: true,
                showNoneOption: false);
        }

        private IReadOnlyList<SemanticTag> GetSelectedDynamicCategoryFilterTags(
            TagCategory category)
        {
            int index = FindDynamicCategoryFilterIndex(category);

            if (index < 0)
                return Array.Empty<SemanticTag>();

            SerializedProperty filterProperty = _categoryFilters.GetArrayElementAtIndex(index);
            SerializedProperty tagsProperty = filterProperty.FindPropertyRelative("tags");

            List<SemanticTag> tags = new();

            for (int i = 0; i < tagsProperty.arraySize; i++)
            {
                SemanticTag tag =
                    tagsProperty.GetArrayElementAtIndex(i).objectReferenceValue as SemanticTag;

                if (tag && tag.Category == category)
                    tags.Add(tag);
            }

            return tags;
        }

        private void SetDynamicCategoryFilter(
            TagCategory category,
            IReadOnlyList<SemanticTag> selectedTags)
        {
            if (!category)
                return;

            serializedObject.Update();

            List<SemanticTag> validTags = selectedTags
                .Where(tag => tag && tag.Category == category)
                .Distinct()
                .ToList();

            int filterIndex = FindDynamicCategoryFilterIndex(category);

            if (validTags.Count == 0)
            {
                if (filterIndex >= 0)
                    _categoryFilters.DeleteArrayElementAtIndex(filterIndex);

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                return;
            }

            if (filterIndex < 0)
            {
                filterIndex = _categoryFilters.arraySize;
                _categoryFilters.InsertArrayElementAtIndex(filterIndex);

                SerializedProperty newFilter = _categoryFilters.GetArrayElementAtIndex(filterIndex);
                newFilter.FindPropertyRelative("category").objectReferenceValue = category;
            }

            SerializedProperty filterProperty = _categoryFilters.GetArrayElementAtIndex(filterIndex);
            SerializedProperty tagsProperty = filterProperty.FindPropertyRelative("tags");

            tagsProperty.ClearArray();

            for (int i = 0; i < validTags.Count; i++)
            {
                tagsProperty.InsertArrayElementAtIndex(i);
                tagsProperty.GetArrayElementAtIndex(i).objectReferenceValue = validTags[i];
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private int FindDynamicCategoryFilterIndex(TagCategory category)
        {
            if (!category)
                return -1;

            for (int i = 0; i < _categoryFilters.arraySize; i++)
            {
                SerializedProperty filterProperty = _categoryFilters.GetArrayElementAtIndex(i);
                SerializedProperty categoryProperty = filterProperty.FindPropertyRelative("category");

                if (categoryProperty.objectReferenceValue == category)
                    return i;
            }

            return -1;
        }

        private void DrawPreview()
        {
            AssetPool pool = (AssetPool)target;
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            List<AssetDefinition> assets = pool
                .ResolveAssets(catalog)
                .Where(asset => asset)
                .ToList();

            _showPreview = EditorGUILayout.Foldout(
                _showPreview,
                $"Dynamic Assets ({assets.Count})",
                true);

            if (!_showPreview)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (assets.Count == 0)
                {
                    EditorGUILayout.HelpBox("This pool currently contains no assets.", MessageType.Warning);
                    return;
                }

                foreach (AssetDefinition asset in assets)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(asset, typeof(AssetDefinition), false);
                    }
                }
            }
        }

    }
}
