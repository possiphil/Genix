using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Core;
using Genix.Editor.Assets;
using Genix.Editor.UI;
using Genix.Placement;
using Genix.Semantics;
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
        private SerializedProperty _exemptAssetTags;

        private void OnEnable()
        {
            _shape = serializedObject.FindProperty("shape");
            _center = serializedObject.FindProperty("center");
            _size = serializedObject.FindProperty("size");
            _radius = serializedObject.FindProperty("radius");
            _affectedTargets = serializedObject.FindProperty("affectedTargets");
            _exemptAssetTags = serializedObject.FindProperty("exemptAssetTags");
        }

        /// <summary>Draws the custom region inspector.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_shape, new GUIContent(
                "Shape",
                "Reserve a box or sphere without adding gameplay physics. Overlapping exclusion regions combine automatically."));
            ExclusionRegionShape shape = (ExclusionRegionShape)_shape.enumValueIndex;

            if (shape == ExclusionRegionShape.Box)
            {
                EditorGUILayout.PropertyField(_center, new GUIContent(
                    "Center (units)",
                    "Local offset from this object's transform. Transform scale is intentionally ignored."));
                EditorGUI.BeginChangeCheck();
                Vector3 size = EditorGUILayout.Vector3Field(new GUIContent(
                    "Size (units)",
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
            else if (shape == ExclusionRegionShape.Sphere)
            {
                EditorGUILayout.PropertyField(_center, new GUIContent(
                    "Center (units)",
                    "Local offset from this object's transform. Transform scale is intentionally ignored."));
                EditorGUI.BeginChangeCheck();
                float radius = EditorGUILayout.FloatField(new GUIContent(
                    "Radius (units)",
                    "Sphere radius in world units."),
                    _radius.floatValue);

                if (EditorGUI.EndChangeCheck())
                    _radius.floatValue = Mathf.Max(0f, radius);
            }

            DrawAffectedTargets();

            if (DesignerUiPreferences.IsAdvanced)
                DrawExemptAssetTags();

            if (((PlacementTarget)_affectedTargets.intValue & PlacementTarget.All) == PlacementTarget.None)
            {
                EditorGUILayout.HelpBox(
                    "No targets are selected, so this region currently has no effect.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawExemptAssetTags()
        {
            IReadOnlyList<SemanticTag> selected = GetTags();
            string summary = selected.Count switch
            {
                0 => "None",
                <= 2 => string.Join(", ", selected.Select(tag => tag.DisplayName)),
                _ => $"{selected[0].DisplayName}, {selected[1].DisplayName} +{selected.Count - 2}"
            };
            Rect row = EditorGUILayout.GetControlRect();
            Rect button = EditorGUI.PrefixLabel(row, new GUIContent(
                "Ignored Asset Tags",
                "Assets carrying any selected tag may overlap this region, such as path furniture on a path exclusion."));

            if (!EditorGUI.DropdownButton(button, new GUIContent(summary), FocusType.Keyboard))
                return;

            GenericMenu menu = new();
            menu.AddItem(new GUIContent("None"), selected.Count == 0, () => SetTags(Array.Empty<SemanticTag>()));
            menu.AddSeparator(string.Empty);
            List<SemanticTag> available = AssetCatalogService.GetOrCreate().Tags
                .Where(tag => tag && tag.SupportsAssets)
                .OrderBy(tag => tag.Category.DisplayName)
                .ThenBy(tag => tag.DisplayName)
                .ToList();

            foreach (SemanticTag tag in available)
            {
                SemanticTag captured = tag;
                menu.AddItem(
                    new GUIContent($"{tag.Category.DisplayName}/{tag.DisplayName}"),
                    selected.Contains(tag),
                    () => ToggleTag(captured));
            }

            if (available.Count == 0)
                menu.AddDisabledItem(new GUIContent("No asset-compatible tags available"));

            menu.DropDown(button);
        }

        private IReadOnlyList<SemanticTag> GetTags()
        {
            List<SemanticTag> tags = new();
            for (int i = 0; i < _exemptAssetTags.arraySize; i++)
            {
                if (_exemptAssetTags.GetArrayElementAtIndex(i).objectReferenceValue is SemanticTag tag && tag)
                    tags.Add(tag);
            }

            return tags;
        }

        private void ToggleTag(SemanticTag tag)
        {
            List<SemanticTag> tags = GetTags().ToList();
            if (!tags.Remove(tag))
                tags.Add(tag);
            SetTags(tags);
        }

        private void SetTags(IEnumerable<SemanticTag> tags)
        {
            serializedObject.Update();
            _exemptAssetTags.ClearArray();
            foreach (SemanticTag tag in tags.Where(tag => tag).Distinct())
            {
                int index = _exemptAssetTags.arraySize;
                _exemptAssetTags.InsertArrayElementAtIndex(index);
                _exemptAssetTags.GetArrayElementAtIndex(index).objectReferenceValue = tag;
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void DrawAffectedTargets()
        {
            PlacementTarget targets = PlacementTargetSelectionField.Normalize(
                (PlacementTarget)_affectedTargets.intValue);
            _affectedTargets.intValue = (int)targets;

            Rect controlRect = EditorGUILayout.GetControlRect();
            Rect dropdownRect = EditorGUI.PrefixLabel(
                controlRect,
                new GUIContent(
                    "Blocks Placement On",
                    "Reject only candidates using these placement target types."));

            if (!EditorGUI.DropdownButton(
                    dropdownRect,
                    new GUIContent(PlacementTargetSelectionField.GetLabel(targets, "None")),
                    FocusType.Keyboard))
            {
                return;
            }

            PlacementTargetSelectionField.Show(
                dropdownRect,
                targets,
                SetAffectedTargets,
                "Clear every affected target. The exclusion region then has no effect.",
                "Reject Floor, Wall, Ceiling, and Inside Space candidates.");
        }

        private void SetAffectedTargets(PlacementTarget targets)
        {
            serializedObject.Update();
            _affectedTargets.intValue = (int)PlacementTargetSelectionField.Normalize(targets);
            serializedObject.ApplyModifiedProperties();
            Repaint();
        }

        [MenuItem("GameObject/Genix/Add Exclusion Region", false, 30)]
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
