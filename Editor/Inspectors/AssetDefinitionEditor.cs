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
    [CustomEditor(typeof(AssetDefinition))]
    public sealed class AssetDefinitionEditor : UnityEditor.Editor
    {
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
            _randomYawRotation = serializedObject.FindProperty("randomYawRotation");
            _randomPitchRotation = serializedObject.FindProperty("randomPitchRotation");
            _randomRollRotation = serializedObject.FindProperty("randomRollRotation");
        }

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

            string assetName = EditorGUILayout.DelayedTextField("Asset Name", target.name);

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

            EditorGUILayout.PropertyField(_prefab);

            if (EditorGUI.EndChangeCheck())
                UpdateBoundsFromPrefab();
        }

        private void DrawPlacementSection()
        {
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_placementType);

            if (IsWallPlacementType())
            {
                EditorGUILayout.PropertyField(_placementHeight);
                EditorGUILayout.PropertyField(_useHeightOffset);

                if (_useHeightOffset.boolValue)
                    EditorGUILayout.PropertyField(_maxHeightOffset);
            }
            else if (IsInsideSpacePlacementType())
            {
                EditorGUILayout.PropertyField(_randomYawRotation, new GUIContent("Random Yaw"));
                EditorGUILayout.PropertyField(_randomPitchRotation, new GUIContent("Random Pitch"));
                EditorGUILayout.PropertyField(_randomRollRotation, new GUIContent("Random Roll"));
            }
            else
            {
                EditorGUILayout.PropertyField(_randomYawRotation, new GUIContent("Random Yaw"));
            }

            EditorGUILayout.PropertyField(_orientationMode);
        }

        private void DrawBoundsSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Bounds", EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!_prefab.objectReferenceValue))
                {
                    if (GUILayout.Button("Generate From Prefab", GUILayout.Width(140f)))
                        UpdateBoundsFromPrefab();
                }
            }

            EditorGUILayout.PropertyField(_boundsSize, GUIContent.none);
            EditorGUILayout.PropertyField(_boundsCenterOffset, new GUIContent("Center Offset"));
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
    }
}
