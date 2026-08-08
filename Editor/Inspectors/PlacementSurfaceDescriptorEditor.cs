using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.Genix.Editor.Common;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    /// <summary>Provides guided semantic surface, facing, and capacity authoring.</summary>
    [CustomEditor(typeof(PlacementSurfaceDescriptor))]
    public sealed class PlacementSurfaceDescriptorEditor : UnityEditor.Editor
    {
        private SerializedProperty _surfaceTags;
        private SerializedProperty _noneTagCategories;
        private SerializedProperty _usePreferredForward;
        private SerializedProperty _limitCapacity;
        private SerializedProperty _maxCapacity;

        private void OnEnable()
        {
            _surfaceTags = serializedObject.FindProperty("surfaceTags");
            _noneTagCategories = serializedObject.FindProperty("noneTagCategories");
            _usePreferredForward = serializedObject.FindProperty("usePreferredForward");
            _limitCapacity = serializedObject.FindProperty("limitCapacity");
            _maxCapacity = serializedObject.FindProperty("maxCapacity");
        }

        /// <summary>Draws the custom descriptor inspector.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            PlacementSurfaceDescriptor descriptor = (PlacementSurfaceDescriptor)target;

            EditorGUILayout.HelpBox(
                "Assign this component to a surface collider or one of its parents. Descendant colliders share these semantic and capacity rules.",
                MessageType.Info);

            if (!descriptor.GetComponentInChildren<Collider>())
            {
                EditorGUILayout.HelpBox(
                    "No collider exists on this object or its children, so Genix cannot sample this surface.",
                    MessageType.Warning);
            }

            DrawSurfaceTags();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Facing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_usePreferredForward, new GUIContent(
                "Use Preferred Forward",
                "Allows assets using Match Support Forward to align with this object's blue local Z axis."));

            if (_usePreferredForward.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Rotate this object so its blue local Z axis points toward the desired front. The direction is projected onto the sampled surface.",
                    MessageType.None);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Capacity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_limitCapacity, new GUIContent(
                "Limit Capacity",
                "Restricts how many generated objects may use this descriptor across generation runs."));

            if (_limitCapacity.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    int capacity = EditorGUILayout.IntField(new GUIContent(
                        "Max Capacity",
                        "Maximum number of generated objects supported by this descriptor. Zero blocks every placement."),
                        _maxCapacity.intValue);

                    if (EditorGUI.EndChangeCheck())
                        _maxCapacity.intValue = Mathf.Max(0, capacity);
                }

                if (_maxCapacity.intValue == 0)
                {
                    EditorGUILayout.HelpBox(
                        "This surface currently accepts no placements because Max Capacity is zero.",
                        MessageType.Warning);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSurfaceTags()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Surface Tags", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(
                           _surfaceTags.arraySize == 0 && _noneTagCategories.arraySize == 0))
                {
                    if (GUILayout.Button(new GUIContent(
                            "Reset",
                            "Reset every surface category to its default Any selection."), GUILayout.Width(52f)))
                    {
                        _surfaceTags.ClearArray();
                        _noneTagCategories.ClearArray();
                        GUI.FocusControl(null);
                    }
                }
            }

            AssetCatalog catalog = AssetCatalogService.GetOrCreate();
            List<TagCategory> categories = catalog.Categories
                .Where(category => category && category.SupportsSurfaces)
                .OrderBy(category => category.DisplayName)
                .ToList();

            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Create a tag category with Usage set to Surface or Asset and Surface before assigning surface tags.",
                    MessageType.Info);
                return;
            }

            foreach (TagCategory category in categories)
            {
                List<SemanticTag> availableTags = catalog.Tags
                    .Where(tag => tag && tag.Category == category)
                    .OrderBy(tag => tag.DisplayName)
                    .ToList();
                List<SemanticTag> selectedTags = GetTagsInCategory(category);

                TagSelectionField.Draw(
                    category.DisplayName,
                    category,
                    availableTags,
                    selectedTags,
                    null,
                    forceMultiSelect: true,
                    anySelected: ((PlacementSurfaceDescriptor)target).AcceptsAnyTag(category),
                    onChangedWithSpecialSelection: (tags, specialSelection) =>
                        SetCategorySelection(
                            category,
                            tags,
                            specialSelection == TagSelectionField.SpecialSelection.None),
                    showNoneOption: true,
                    showAnyOption: true);
            }
        }

        private List<SemanticTag> GetTagsInCategory(TagCategory category)
        {
            List<SemanticTag> tags = new();

            for (int i = 0; i < _surfaceTags.arraySize; i++)
            {
                SemanticTag tag = _surfaceTags.GetArrayElementAtIndex(i).objectReferenceValue as SemanticTag;

                if (tag && tag.Category == category)
                    tags.Add(tag);
            }

            return tags;
        }

        private void SetCategorySelection(
            TagCategory category,
            IReadOnlyList<SemanticTag> selectedTags,
            bool selectNone)
        {
            PlacementSurfaceDescriptor descriptor = (PlacementSurfaceDescriptor)target;
            Undo.RecordObject(descriptor, "Change Surface Tags");
            descriptor.SetCategorySelection(category, selectedTags, selectNone);
            EditorUtility.SetDirty(descriptor);
            serializedObject.Update();
        }

        [MenuItem("GameObject/Genix/Add Placement Surface Descriptor", false, 29)]
        private static void AddPlacementSurfaceDescriptor(MenuCommand command)
        {
            GameObject gameObject = command.context as GameObject ?? Selection.activeGameObject;

            if (!gameObject || gameObject.GetComponent<PlacementSurfaceDescriptor>())
                return;

            Undo.AddComponent<PlacementSurfaceDescriptor>(gameObject);
            Selection.activeGameObject = gameObject;
        }

        [MenuItem("GameObject/Genix/Add Placement Surface Descriptor", true)]
        private static bool CanAddPlacementSurfaceDescriptor()
        {
            GameObject gameObject = Selection.activeGameObject;
            return gameObject && !gameObject.GetComponent<PlacementSurfaceDescriptor>();
        }
    }
}
