using Genix.Editor.Genix.Editor.Assets;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    [CustomEditor(typeof(TagCategory))]
    public sealed class TagCategoryEditor : UnityEditor.Editor
    {
        private SerializedProperty _allowMultipleTags;

        private void OnEnable()
        {
            _allowMultipleTags = serializedObject.FindProperty("allowMultipleTags");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Tag Category", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawDisplayNameField();

            EditorGUILayout.PropertyField(
                _allowMultipleTags,
                new GUIContent("Allow Multiple Tags"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDisplayNameField()
        {
            EditorGUI.BeginChangeCheck();

            string displayName = EditorGUILayout.DelayedTextField(
                "Display Name",
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
