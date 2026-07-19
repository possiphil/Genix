using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    [CustomEditor(typeof(SemanticTag))]
    public sealed class SemanticTagEditor : UnityEditor.Editor
    {
        private SerializedProperty _category;

        private void OnEnable()
        {
            _category = serializedObject.FindProperty("category");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Semantic Tag", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawDisplayNameField();
            DrawCategoryDropdown();

            DrawWarnings();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDisplayNameField()
        {
            EditorGUI.BeginChangeCheck();

            string displayName = EditorGUILayout.DelayedTextField("Display Name", target.name);

            if (!EditorGUI.EndChangeCheck())
                return;

            if (!AssetCatalogService.TryRenameTag(
                    (SemanticTag)target,
                    displayName,
                    out string error))
            {
                Debug.LogWarning(error);
                return;
            }

            serializedObject.Update();
        }

        private void DrawCategoryDropdown()
        {
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            List<TagCategory> categories = catalog.Categories
                .Where(category => category)
                .OrderBy(category => category.DisplayName)
                .ToList();

            if (categories.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Popup("Category", 0, new[] { "No categories available" });

                return;
            }

            TagCategory currentCategory = _category.objectReferenceValue as TagCategory;
            bool hasValidCategory = currentCategory && categories.Contains(currentCategory);

            string[] options = hasValidCategory
                ? categories.Select(category => category.DisplayName).ToArray()
                : new[] { "Missing Category" }
                    .Concat(categories.Select(category => category.DisplayName))
                    .ToArray();

            int selectedIndex = hasValidCategory
                ? categories.IndexOf(currentCategory)
                : 0;

            EditorGUI.BeginChangeCheck();

            int newIndex = EditorGUILayout.Popup("Category", selectedIndex, options);

            if (!EditorGUI.EndChangeCheck())
                return;

            TagCategory selectedCategory = hasValidCategory
                ? categories[newIndex]
                : newIndex == 0
                    ? null
                    : categories[newIndex - 1];

            if (!selectedCategory)
                return;

            SetCategory(selectedCategory);
        }

        private void SetCategory(TagCategory category)
        {
            if (!AssetCatalogService.TrySetTagCategory(
                    (SemanticTag)target,
                    category,
                    out string error))
            {
                Debug.LogWarning(error);
                return;
            }

            serializedObject.Update();
            EditorUtility.SetDirty(target);
        }

        private void DrawWarnings()
        {
            if (_category.objectReferenceValue)
                return;

            EditorGUILayout.Space(4f);

            EditorGUILayout.HelpBox(
                "This tag has no category. Assign a category before using it in assets or asset pools.",
                MessageType.Warning);
        }
    }
}
