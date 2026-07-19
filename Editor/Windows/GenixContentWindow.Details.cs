using Genix.Assets;
using Genix.Editor.Drawers;
using Genix.Extensions;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixContentWindow
    {
        private void DrawSelectedObjectDetails()
        {
            if (!HasDetailsSelection())
                return;

            EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_tab == ContentTab.Tags)
                {
                    DrawTagDetails();
                    return;
                }

                Object selectedObject = GetSelectedObject();

                if (!selectedObject)
                    return;

                if (_tab == ContentTab.Assets && selectedObject is AssetDefinition selectedAsset)
                    DrawAssetPlacementPreview(selectedAsset);

                if (_selectedObjectEditorTarget != selectedObject)
                {
                    DestroySelectedObjectEditor();
                    _selectedObjectEditorTarget = selectedObject;
                }

                UnityEditor.Editor.CreateCachedEditor(selectedObject, null, ref _selectedObjectEditor);
                _selectedObjectEditor.OnInspectorGUI();
            }
        }

        private static void DrawAssetPlacementPreview(AssetDefinition asset)
        {
            EditorGUILayout.LabelField("Placement Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawAssetThumbnail(asset);

                using (new EditorGUILayout.VerticalScope())
                {
                    DrawAssetPreviewStat("Prefab", asset.Prefab ? asset.Prefab.name : "Missing");
                    DrawAssetPreviewStat("Placement", asset.PlacementType.ToDisplayName());
                    DrawAssetPreviewStat("Bounds", FormatVector(asset.BoundsSize));
                    DrawAssetPreviewStat("Center Offset", FormatVector(asset.BoundsCenterOffset));
                    DrawAssetPreviewStat("Random Yaw", asset.RandomYawRotation ? "On" : "Off");

                    if (asset.PlacementType == PlacementType.InsideSpace)
                    {
                        DrawAssetPreviewStat("Random Pitch", asset.RandomPitchRotation ? "On" : "Off");
                        DrawAssetPreviewStat("Random Roll", asset.RandomRollRotation ? "On" : "Off");
                    }
                }

                DrawAssetFootprintPreview(asset);
            }

            EditorGUILayout.Space(4f);
        }

        private static void DrawAssetThumbnail(AssetDefinition asset)
        {
            Rect rect = GUILayoutUtility.GetRect(88f, 88f, GUILayout.Width(88f), GUILayout.Height(88f));
            GUI.Box(rect, GUIContent.none);

            if (!asset.Prefab)
            {
                EditorGUI.LabelField(rect, "No Prefab", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Texture2D preview = AssetPreview.GetAssetPreview(asset.Prefab);
            if (!preview)
                preview = AssetPreview.GetMiniThumbnail(asset.Prefab);

            if (preview)
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
        }

        private static void DrawAssetFootprintPreview(AssetDefinition asset)
        {
            Rect rect = GUILayoutUtility.GetRect(100f, 88f, GUILayout.Width(110f), GUILayout.Height(88f));
            GUI.Box(rect, GUIContent.none);

            Rect inner = new(rect.x + 8f, rect.y + 18f, rect.width - 16f, rect.height - 28f);
            EditorGUI.DrawRect(inner, new Color(0f, 0f, 0f, 0.18f));

            Vector3 size = asset.BoundsSize;
            float max = Mathf.Max(0.01f, Mathf.Max(size.x, size.z));
            float width = inner.width * Mathf.Clamp01(size.x / max);
            float depth = inner.height * Mathf.Clamp01(size.z / max);
            Rect footprint = new(
                inner.center.x - width * 0.5f,
                inner.center.y - depth * 0.5f,
                width,
                depth);

            EditorGUI.DrawRect(footprint, new Color(0.2f, 0.65f, 1f, 0.45f));
            EditorGUI.LabelField(new Rect(rect.x, rect.y + 2f, rect.width, 16f), "Footprint", EditorStyles.centeredGreyMiniLabel);
        }

        private static void DrawAssetPreviewStat(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }

        private static string FormatVector(Vector3 value)
        {
            return $"{value.x:0.###}, {value.y:0.###}, {value.z:0.###}";
        }

        private bool HasDetailsSelection()
        {
            return _tab switch
            {
                ContentTab.Assets => _selectedAsset,
                ContentTab.Tags => _selectedTagCategory || _selectedSemanticTag,
                ContentTab.Locations => false,
                ContentTab.AssetPools => _selectedPool,
                _ => false
            };
        }

        private void DrawTagDetails()
        {
            if (!_selectedTagCategory && !_selectedSemanticTag)
                return;

            if (_selectedTagCategory)
            {
                UnityEditor.Editor.CreateCachedEditor(_selectedTagCategory, null, ref _selectedCategoryEditor);
                _selectedCategoryEditor.OnInspectorGUI();

                if (_selectedSemanticTag)
                    EditorGUILayout.Space(8f);
            }

            if (_selectedSemanticTag)
            {
                UnityEditor.Editor.CreateCachedEditor(_selectedSemanticTag, null, ref _selectedSemanticTagEditor);
                _selectedSemanticTagEditor.OnInspectorGUI();
            }
        }

        private string GetEmptyDetailsMessage()
        {
            return _tab switch
            {
                ContentTab.Assets => "Select an asset.",
                ContentTab.Tags => "Select a tag or category.",
                ContentTab.Locations => "Select a location.",
                ContentTab.AssetPools => "Select an asset pool.",
                _ => "Select an item."
            };
        }

    }
}
