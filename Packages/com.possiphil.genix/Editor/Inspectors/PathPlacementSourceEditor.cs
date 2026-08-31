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

        private void OnEnable()
        {
            _pathTags = serializedObject.FindProperty("pathTags");
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_pathTags, new GUIContent(
                "Path Identity Tags",
                "Tags that assets use to find this path for path placement or regular stations."), true);

            PathPlacementSource source = (PathPlacementSource)target;
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
