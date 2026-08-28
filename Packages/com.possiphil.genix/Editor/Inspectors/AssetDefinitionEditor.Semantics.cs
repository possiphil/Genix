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
    }
}
