using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Placement;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    /// <summary>Provides semantic and directional authoring for fixed scene relation anchors.</summary>
    [CustomEditor(typeof(AssetRelationAnchor))]
    public sealed class AssetRelationAnchorEditor : UnityEditor.Editor
    {
        private SerializedProperty _representedAsset;
        private SerializedProperty _assetTags;
        private SerializedProperty _supportSurface;
        private SerializedProperty _forwardYawOffset;
        private SerializedProperty _useCustomBounds;
        private SerializedProperty _boundsCenter;
        private SerializedProperty _boundsSize;
        private SerializedProperty _alwaysShowAnchor;

        private void OnEnable()
        {
            _representedAsset = serializedObject.FindProperty("representedAsset");
            _assetTags = serializedObject.FindProperty("assetTags");
            _supportSurface = serializedObject.FindProperty("supportSurface");
            _forwardYawOffset = serializedObject.FindProperty("forwardYawOffset");
            _useCustomBounds = serializedObject.FindProperty("useCustomBounds");
            _boundsCenter = serializedObject.FindProperty("boundsCenter");
            _boundsSize = serializedObject.FindProperty("boundsSize");
            _alwaysShowAnchor = serializedObject.FindProperty("alwaysShowAnchor");
        }

        /// <summary>Draws semantic identity, derived bounds, and forward-direction controls.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_representedAsset, new GUIContent(
                "Represented Asset",
                "Makes this fixed scene object available to asset-specific Relative Placement. Assign the optional concrete Asset Definition used by rules that match one exact asset."));
            DrawAssetTags();
            EditorGUILayout.PropertyField(_supportSurface, new GUIContent(
                "Support Surface",
                "Placement Surface Descriptor shared with dependent objects when Require Same Support Surface is enabled. A descriptor on this GameObject is used automatically."));
            EditorGUILayout.PropertyField(_forwardYawOffset, new GUIContent(
                "Front Yaw Offset",
                $"Front starts at local +Z; Left and Right rotate with it. Rotate semantic Front around local Y without rotating the visible scene object. Current semantic Front points along world {((AssetRelationAnchor)target).Forward:F2}. Enable Always Show Anchor to inspect the cyan arrow in the Scene view."));

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(_useCustomBounds, new GUIContent(
                "Use Custom Bounds",
                "Override bounds derived from child renderers and colliders."));

            if (_useCustomBounds.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_boundsCenter, new GUIContent("Local Center"));
                    EditorGUI.BeginChangeCheck();
                    Vector3 size = EditorGUILayout.Vector3Field(new GUIContent("Local Size"), _boundsSize.vector3Value);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _boundsSize.vector3Value = new Vector3(
                            Mathf.Max(0.01f, size.x),
                            Mathf.Max(0.01f, size.y),
                            Mathf.Max(0.01f, size.z));
                    }
                }
            }

            EditorGUILayout.PropertyField(_alwaysShowAnchor, new GUIContent(
                "Always Show Anchor",
                "Keep the cyan bounds and forward arrow visible when another object is selected."));

            if (!_representedAsset.objectReferenceValue && _assetTags.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Represented Asset or at least one Asset Tag. Otherwise no semantic relation rule can match this anchor.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAssetTags()
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
                "Asset Tags",
                "Additional asset-compatible tags used by tag-scoped relative-placement rules."));

            if (!EditorGUI.DropdownButton(button, new GUIContent(summary), FocusType.Keyboard))
                return;

            GenericMenu menu = new();
            menu.AddItem(new GUIContent("None"), selected.Count == 0, () => SetTags(Array.Empty<SemanticTag>()));
            menu.AddSeparator(string.Empty);
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();
            List<SemanticTag> available = catalog.Tags
                .Where(tag => tag && tag.Category && tag.Category.SupportsAssets)
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
            for (int i = 0; i < _assetTags.arraySize; i++)
            {
                if (_assetTags.GetArrayElementAtIndex(i).objectReferenceValue is SemanticTag tag && tag)
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
            _assetTags.ClearArray();
            foreach (SemanticTag tag in tags.Where(tag => tag).Distinct())
            {
                int index = _assetTags.arraySize;
                _assetTags.InsertArrayElementAtIndex(index);
                _assetTags.GetArrayElementAtIndex(index).objectReferenceValue = tag;
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        [MenuItem("GameObject/Genix/Add Asset Relation Anchor", false, 24)]
        private static void AddAnchor(MenuCommand command)
        {
            GameObject targetObject = command.context as GameObject ?? Selection.activeGameObject;

            if (!targetObject)
            {
                targetObject = new GameObject("Asset Relation Anchor");
                Undo.RegisterCreatedObjectUndo(targetObject, "Create Asset Relation Anchor");
                targetObject.transform.position = SceneView.lastActiveSceneView
                    ? SceneView.lastActiveSceneView.pivot
                    : Vector3.zero;
            }

            AssetRelationAnchor anchor = targetObject.GetComponent<AssetRelationAnchor>();
            if (!anchor)
                anchor = Undo.AddComponent<AssetRelationAnchor>(targetObject);

            Selection.activeObject = anchor;
        }
    }
}
