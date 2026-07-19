using System;
using System.Linq;
using Genix.Areas;
using Genix.Core;
using Genix.Editor.Layouts;
using Genix.Extensions;
using Genix.Layouts;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixEditorWindow
    {
        private void DrawGeneratedLayoutsSection()
        {
            EditorGUILayout.Space(10);

            DrawGeneratedLayoutsHeader();

            if (!_showGeneratedLayouts)
                return;

            IAreaSource areaSource = CreateAreaSource();

            if (areaSource == null)
            {
                EditorGUILayout.HelpBox("Choose a target area to save, compare, or apply layouts.", MessageType.Info);
                return;
            }

            SavedLayout[] layouts = GetLayoutsForSelectedArea(areaSource);

            if (layouts.Length == 0)
            {
                EditorGUILayout.HelpBox("No saved layouts for this target area yet. Generate objects, then save the current layout.", MessageType.Info);
                return;
            }

            foreach (SavedLayout layout in layouts)
                DrawGeneratedLayoutItem(layout, areaSource);
        }

        private void DrawGeneratedLayoutsHeader()
        {
            const float foldoutWidth = 82f;
            const float buttonWidth = 100f;
            const float spacing = 4f;

            float controlHeight = EditorGUIUtility.singleLineHeight;
            float buttonHeight = controlHeight + 2f;
            Rect headerRect = EditorGUILayout.GetControlRect(false, buttonHeight);
            float foldoutY = headerRect.y + (headerRect.height - controlHeight) * 0.5f;

            Rect foldoutRect = new(headerRect.x, foldoutY, foldoutWidth, controlHeight);
            _showGeneratedLayouts = EditorGUI.Foldout(foldoutRect, _showGeneratedLayouts, "Layouts", true);

            float clearLayoutsX = headerRect.xMax - buttonWidth;
            float clearPreviewX = clearLayoutsX - spacing - buttonWidth;
            GUIStyle buttonStyle = GetLayoutHeaderButtonStyle();

            Rect clearPreviewRect = new(clearPreviewX, headerRect.y, buttonWidth, buttonHeight);
            if (GUI.Button(clearPreviewRect, "Clear Preview", buttonStyle))
                LayoutWorkflow.ClearPreview();

            Rect clearLayoutsRect = new(clearLayoutsX, headerRect.y, buttonWidth, buttonHeight);
            if (GUI.Button(clearLayoutsRect, "Clear Layouts", buttonStyle))
                ClearLayouts();
        }

        private static GUIStyle GetLayoutHeaderButtonStyle()
        {
            return new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 0, 0)
            };
        }

        private void DrawGeneratedLayoutItem(SavedLayout layout, IAreaSource areaSource)
        {
            if (!layout)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawLayoutDesignerMetadata(layout);
            EditorGUILayout.LabelField(GetLayoutMetaLabel(layout), EditorStyles.miniLabel);

            string assetSummary = GetLayoutAssetSummary(layout);
            if (!string.IsNullOrWhiteSpace(assetSummary))
                EditorGUILayout.LabelField(assetSummary, EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview A"))
                    PreviewLayout(layout, LayoutPreviewSlot.A);

                if (GUILayout.Button("Preview B"))
                    PreviewLayout(layout, LayoutPreviewSlot.B);

                if (GUILayout.Button("Apply"))
                    ApplyLayout(layout, areaSource);

                using (new EditorGUI.DisabledScope(layout.Locked))
                {
                    if (GUILayout.Button(layout.Locked ? "Locked" : "Delete"))
                        DeleteLayout(layout);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawLayoutDesignerMetadata(SavedLayout layout)
        {
            EditorGUI.BeginChangeCheck();

            string displayName;
            bool favorite = layout.Favorite;
            bool locked = layout.Locked;

            using (new EditorGUILayout.HorizontalScope())
            {
                displayName = EditorGUILayout.TextField("Name", layout.DisplayName);

                if (GUILayout.Button(GetFavoriteContent(layout.Favorite), GetLayoutIconButtonStyle(), GUILayout.Width(24f)))
                    favorite = !favorite;

                if (GUILayout.Button(GetLockContent(layout.Locked), GetLayoutIconButtonStyle(), GUILayout.Width(24f)))
                    locked = !locked;
            }

            string notes = EditorGUILayout.TextField("Notes", layout.Notes);

            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(layout, "Edited Genix Layout");
            layout.SetDesignerMetadata(displayName, notes, favorite, locked);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();
        }

        private static GUIContent GetFavoriteContent(bool favorite)
        {
            return new GUIContent(favorite ? "★" : "☆", favorite ? "Remove favorite" : "Mark as favorite");
        }

        private static GUIContent GetLockContent(bool locked)
        {
            return new GUIContent(GetLayoutLockIcon(locked), locked ? "Unlock layout" : "Lock layout");
        }

        private static GUIStyle GetLayoutIconButtonStyle()
        {
            return new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                fontSize = 13
            };
        }

        private static Texture2D GetLayoutLockIcon(bool locked)
        {
            if (locked)
                return _lockedLayoutIcon ? _lockedLayoutIcon : _lockedLayoutIcon = CreateLayoutLockIcon(true);

            return _unlockedLayoutIcon ? _unlockedLayoutIcon : _unlockedLayoutIcon = CreateLayoutLockIcon(false);
        }

        private static Texture2D CreateLayoutLockIcon(bool locked)
        {
            const int size = 16;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point
            };

            Color clear = new(0f, 0f, 0f, 0f);
            Color color = EditorGUIUtility.isProSkin
                ? new Color(0.82f, 0.82f, 0.82f, 1f)
                : new Color(0.18f, 0.18f, 0.18f, 1f);

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                    texture.SetPixel(x, y, clear);
            }

            if (locked)
                DrawFilledRect(texture, 4, 2, 11, 8, color);
            else
                DrawRectOutline(texture, 4, 2, 11, 8, color);

            DrawLine(texture, 5, 8, 5, 11, color);

            if (locked)
            {
                DrawLine(texture, 10, 8, 10, 11, color);
                DrawLine(texture, 5, 12, 10, 12, color);
            }
            else
            {
                DrawLine(texture, 5, 12, 10, 12, color);
                DrawLine(texture, 10, 11, 10, 12, color);
            }

            texture.Apply();
            return texture;
        }

        private static void DrawFilledRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color color)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                    texture.SetPixel(x, y, color);
            }
        }

        private static void DrawRectOutline(Texture2D texture, int minX, int minY, int maxX, int maxY, Color color)
        {
            DrawLine(texture, minX, minY, maxX, minY, color);
            DrawLine(texture, minX, maxY, maxX, maxY, color);
            DrawLine(texture, minX, minY, minX, maxY, color);
            DrawLine(texture, maxX, minY, maxX, maxY, color);
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx - dy;

            while (true)
            {
                texture.SetPixel(x0, y0, color);

                if (x0 == x1 && y0 == y1)
                    return;

                int doubledError = error * 2;

                if (doubledError > -dy)
                {
                    error -= dy;
                    x0 += sx;
                }

                if (doubledError < dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private void SaveCurrentLayout()
        {
            IAreaSource areaSource = CreateAreaSource();

            if (!LayoutWorkflow.SaveCurrentLayout(
                    areaSource,
                    _generationMode,
                    GetEffectivePlacementTargets(),
                    GetEffectiveTargetDistributionMode(),
                    GetEffectiveTargetDistributionWeights(),
                    _assetPool,
                    _selectedStylePreset ? _selectedStylePreset.name : string.Empty,
                    out SavedLayout layout,
                    out string error))
            {
                Debug.LogWarning(error);
                return;
            }

            RefreshGeneratedLayouts();
            Debug.Log($"Saved Genix layout '{layout.DisplayName}'.");
        }

        private static void PreviewLayout(SavedLayout layout, LayoutPreviewSlot slot)
        {
            if (!LayoutWorkflow.PreviewLayout(layout, slot, out string error))
                Debug.LogWarning(error);
        }

        private void ApplyLayout(SavedLayout layout, IAreaSource areaSource)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Apply Layout",
                $"Replace the currently generated objects for '{areaSource.SourceInfo.SourceName}' with layout '{layout.DisplayName}'?",
                "Apply",
                "Cancel");

            if (!confirmed)
                return;

            if (!LayoutWorkflow.ApplyLayout(layout, areaSource, out string error))
            {
                Debug.LogWarning(error);
                return;
            }

            Debug.Log($"Applied Genix layout '{layout.DisplayName}' to '{areaSource.SourceInfo.SourceName}'.");
        }

        private void DeleteLayout(SavedLayout layout)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Layout",
                $"Delete layout '{layout.DisplayName}' and its saved prefab?",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            if (!LayoutWorkflow.DeleteLayout(layout, out string error))
            {
                Debug.LogWarning(error);
                return;
            }

            RefreshGeneratedLayouts();
        }

        private void ClearLayouts()
        {
            int layoutCount = _generatedLayouts.Length;
            int lockedCount = _generatedLayouts.Count(layout => layout && layout.Locked);

            if (layoutCount == 0)
            {
                Debug.Log("No saved Genix layouts found.");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Layouts",
                lockedCount > 0
                    ? $"Delete all unlocked Genix layouts and their saved prefabs? {lockedCount} locked layout(s) will be kept."
                    : $"Delete all {layoutCount} saved Genix layout(s) and their saved prefabs?",
                "Clear Layouts",
                "Cancel");

            if (!confirmed)
                return;

            if (!LayoutWorkflow.ClearLayouts(out int deletedCount, out string error))
            {
                Debug.LogWarning(error);
                return;
            }

            RefreshGeneratedLayouts();
            Debug.Log($"Deleted {deletedCount} saved Genix layout(s).");
        }

        private SavedLayout[] GetLayoutsForSelectedArea(IAreaSource areaSource)
        {
            if (areaSource == null)
                return Array.Empty<SavedLayout>();

            return _generatedLayouts
                .Where(layout => LayoutWorkflow.MatchesArea(layout, areaSource))
                .ToArray();
        }

        private static string GetLayoutMetaLabel(SavedLayout layout)
        {
            string poolName = layout.AssetPool ? layout.AssetPool.name : "No Asset Pool";
            string styleName = string.IsNullOrWhiteSpace(layout.StyleName) ? "No Style" : layout.StyleName;
            string targets = layout.GenerationMode == GenerationMode.TargetPlacement
                ? $" | {GetPlacementTargetLabel(layout.PlacementTargets)} | {layout.TargetDistributionMode.ToDisplayName()}{GetLayoutWeightLabel(layout)}"
                : string.Empty;
            Vector3 size = layout.Bounds.size;
            string boundsSize = $" | Bounds {size.x:0.##} x {size.y:0.##} x {size.z:0.##}";

            return $"{layout.ObjectCount} objects | {layout.GenerationMode.ToDisplayName()}{targets}{boundsSize} | {poolName} | {styleName} | {layout.CreatedAt}";
        }

        private static string GetLayoutWeightLabel(SavedLayout layout)
        {
            if (layout.TargetDistributionMode != TargetDistributionMode.Weighted)
                return string.Empty;

            TargetDistributionWeights weights = layout.TargetDistributionWeights;
            return $" ({weights.Floor}/{weights.Wall}/{weights.Ceiling}/{weights.InsideSpace})";
        }

        private static string GetLayoutAssetSummary(SavedLayout layout)
        {
            if (layout.AssetSummaries == null || layout.AssetSummaries.Count == 0)
                return string.Empty;

            const int maxShown = 4;

            string[] labels = layout.AssetSummaries
                .Take(maxShown)
                .Select(summary => $"{summary.AssetName} x{summary.Count}")
                .ToArray();

            int remaining = layout.AssetSummaries.Count - labels.Length;

            return remaining > 0
                ? $"{string.Join(", ", labels)} +{remaining} more"
                : string.Join(", ", labels);
        }

    }
}
