using Genix.Editor.UI;
using Genix.Placement;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    /// <summary>Explains and exposes reusable semantic path data without displaying instructional help boxes.</summary>
    [CustomEditor(typeof(PathPlacementSource))]
    public sealed class PathPlacementSourceEditor : UnityEditor.Editor
    {
        private SerializedProperty _pathTags;
        private SerializedProperty _alwaysShowPath;

        private void OnEnable()
        {
            _pathTags = serializedObject.FindProperty("pathTags");
            _alwaysShowPath = serializedObject.FindProperty("alwaysShowPath");
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_pathTags, new GUIContent(
                "Path Tags",
                "Asset-compatible semantic tags used by Path Placement and Regular Path Stations."), true);

            if (!DesignerUiPreferences.IsAdvanced && _alwaysShowPath.boolValue)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.FlexibleSpace();
                    DesignerUiPreferences.DrawAdvancedActiveIndicator(
                        true,
                        "This path has advanced visualization enabled. It remains active in Basic mode.");
                }
            }

            PathPlacementSource source = (PathPlacementSource)target;
            if (DesignerUiPreferences.IsAdvanced)
            {
                EditorGUILayout.PropertyField(_alwaysShowPath, new GUIContent(
                    "Always Show Path",
                    "Keep the sampled centerline visible in the Scene view when this object is not selected."));
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.IntField(new GUIContent("Sampled Points", "Ordered centerline points stored by the authoring integration."), source.PointCount);
            }

            if (source.PointCount < 2)
            {
                EditorGUILayout.HelpBox(
                    "This source has no usable centerline. Rebuild it through the path authoring integration.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
