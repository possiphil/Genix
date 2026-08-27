using Genix.Editor.Genix.Editor.Assets;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    /// <summary>Provides the custom Inspector for tag category.</summary>
    [CustomEditor(typeof(TagCategory))]
    public sealed class TagCategoryEditor : UnityEditor.Editor
    {
        private SerializedProperty _usage;
        private SerializedProperty _allowMultipleTags;

        private void OnEnable()
        {
            _usage = serializedObject.FindProperty("usage");
            _allowMultipleTags = serializedObject.FindProperty("allowMultipleTags");
        }

        /// <summary>Draws and applies the custom Inspector interface.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Tag Category", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawDisplayNameField();

            EditorGUILayout.PropertyField(
                _usage,
                new GUIContent(
                    "Usage",
                    "Asset categories appear in asset, pool, and spatial-context fields. Surface categories appear in support-surface fields. Asset and Surface appears in both."));

            EditorGUILayout.PropertyField(
                _allowMultipleTags,
                new GUIContent("Allow Multiple Tags",
                    "Allow an object or filter to select several tags from this category. Disable for mutually exclusive choices such as biome or room type."));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDisplayNameField()
        {
            EditorGUI.BeginChangeCheck();

            string displayName = EditorGUILayout.DelayedTextField(
                new GUIContent("Display Name", "Designer-facing category name shown in tag selectors and filters."),
                target.name);

            if (!EditorGUI.EndChangeCheck())
                return;

            AssetCatalogService.Rename(
                target,
                displayName,
                "New Category");

            serializedObject.Update();
        }
    }
}
