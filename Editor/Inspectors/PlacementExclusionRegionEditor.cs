using Genix.Core;
using Genix.Placement;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    /// <summary>Provides shape-aware authoring for collider-free placement exclusion regions.</summary>
    [CustomEditor(typeof(PlacementExclusionRegion))]
    public sealed class PlacementExclusionRegionEditor : UnityEditor.Editor
    {
        private SerializedProperty _shape;
        private SerializedProperty _center;
        private SerializedProperty _size;
        private SerializedProperty _radius;
        private SerializedProperty _affectedTargets;

        private void OnEnable()
        {
            _shape = serializedObject.FindProperty("shape");
            _center = serializedObject.FindProperty("center");
            _size = serializedObject.FindProperty("size");
            _radius = serializedObject.FindProperty("radius");
            _affectedTargets = serializedObject.FindProperty("affectedTargets");
        }

        /// <summary>Draws the custom region inspector.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Reserves placement space without creating a Collider, Trigger, or any gameplay physics behavior. Overlapping regions combine automatically.",
                MessageType.Info);

            EditorGUILayout.PropertyField(_shape, new GUIContent(
                "Shape",
                "Box is suited to paths and door clearances. Sphere is suited to radial safety or interaction zones."));
            EditorGUILayout.PropertyField(_center, new GUIContent(
                "Center",
                "Local offset from this object's transform. Transform scale is intentionally ignored."));

            ExclusionRegionShape shape = (ExclusionRegionShape)_shape.enumValueIndex;

            if (shape == ExclusionRegionShape.Box)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 size = EditorGUILayout.Vector3Field(new GUIContent(
                    "Size",
                    "Box dimensions in world units. Rotate the GameObject to orient the region."),
                    _size.vector3Value);

                if (EditorGUI.EndChangeCheck())
                {
                    _size.vector3Value = new Vector3(
                        Mathf.Max(0.01f, size.x),
                        Mathf.Max(0.01f, size.y),
                        Mathf.Max(0.01f, size.z));
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                float radius = EditorGUILayout.FloatField(new GUIContent(
                    "Radius",
                    "Sphere radius in world units."),
                    _radius.floatValue);

                if (EditorGUI.EndChangeCheck())
                    _radius.floatValue = Mathf.Max(0f, radius);
            }

            EditorGUILayout.PropertyField(_affectedTargets, new GUIContent(
                "Affected Targets",
                "Only candidates of these placement target types are rejected by this region."));

            if (((PlacementTarget)_affectedTargets.intValue & PlacementTarget.All) == PlacementTarget.None)
            {
                EditorGUILayout.HelpBox(
                    "No targets are selected, so this region currently has no effect.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("GameObject/Genix/Exclusion Region", false, 30)]
        private static void CreateExclusionRegion(MenuCommand command)
        {
            GameObject regionObject = new("Genix Exclusion Region");
            Undo.RegisterCreatedObjectUndo(regionObject, "Create Genix Exclusion Region");
            GameObjectUtility.SetParentAndAlign(regionObject, command.context as GameObject);
            regionObject.AddComponent<PlacementExclusionRegion>();
            Selection.activeGameObject = regionObject;
        }
    }
}
