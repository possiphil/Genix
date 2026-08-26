using System;
using System.Collections.Generic;
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
    public sealed partial class GenixContentWindow
    {
        private void DrawLayoutsTab()
        {
            DrawLayoutFilters();

            EditorGUILayout.Space(4f);

            List<SavedLayout> layouts = GetFilteredLayouts();

            DrawLayoutList(layouts);
        }

        private void DrawLayoutFilters()
        {
            DrawSectionHeader("Filters", () =>
            {
                if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                    ClearLayoutFilters();
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _layoutSearch = EditorGUILayout.TextField(
                    new GUIContent("Search", "Filter saved layouts by name, notes, scene, or target area."),
                    _layoutSearch);
                _layoutScopeFilter = DrawLayoutScopePopup(_layoutScopeFilter);

                if (_layoutScopeFilter == LayoutScopeFilter.CurrentTargetArea)
                    _layoutTargetAreaSelector.Draw(new GUIContent(
                        "Target Area",
                        "Show layouts saved for this specific spatial area."));
            }
        }

        private List<SavedLayout> GetFilteredLayouts()
        {
            IEnumerable<SavedLayout> layouts = _layoutScopeFilter switch
            {
                LayoutScopeFilter.CurrentTargetArea => LayoutWorkflow.LoadLayoutsForArea(CreateLayoutAreaSource()),
                LayoutScopeFilter.AllScenes => LayoutWorkflow.LoadLayouts(),
                _ => LayoutWorkflow.LoadLayoutsForCurrentScene()
            };

            layouts = layouts
                .Where(layout => layout)
                .Where(MatchesLayoutSearch);

            return SortLayouts(layouts);
        }

        private void DrawLayoutList(IReadOnlyList<SavedLayout> layouts)
        {
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(layouts.Count / (float)LayoutPageSize));
            _layoutPage = Mathf.Clamp(_layoutPage, 0, pageCount - 1);

            DrawSectionHeader($"Layouts ({layouts.Count})", () =>
            {
                DrawLayoutSortDropdown();

                using (new EditorGUI.DisabledScope(layouts.Count == 0))
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                "Delete Matching",
                                "Deletes every layout matching the current search and scope filters, including locked evaluation layouts."),
                            GUILayout.Width(104f)))
                    {
                        DeleteMatchingLayouts(layouts);
                    }
                }

                using (new EditorGUI.DisabledScope(!_selectedLayout || _selectedLayout.Locked))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                        DeleteSelectedLayout();
                }
            });

            if (pageCount > 1)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_layoutPage == 0))
                    {
                        if (GUILayout.Button("Previous", GUILayout.Width(72f)))
                            _layoutPage--;
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"Page {_layoutPage + 1:N0} / {pageCount:N0}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(_layoutPage >= pageCount - 1))
                    {
                        if (GUILayout.Button("Next", GUILayout.Width(72f)))
                            _layoutPage++;
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(ListHeight)))
            {
                _layoutListScroll = EditorGUILayout.BeginScrollView(_layoutListScroll);

                if (layouts.Count == 0)
                {
                    GUILayout.Space(EditorGUIUtility.singleLineHeight);
                }
                else
                {
                    int first = _layoutPage * LayoutPageSize;
                    int last = Mathf.Min(first + LayoutPageSize, layouts.Count);
                    for (int i = first; i < last; i++)
                    {
                        SavedLayout layout = layouts[i];
                        DrawLayoutListItem(layout);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawLayoutListItem(SavedLayout layout)
        {
            bool selected = GetSelectedObject() == layout;
            GUIStyle style = selected ? EditorStyles.helpBox : GUIStyle.none;

            using (new EditorGUILayout.VerticalScope(style))
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 40f);

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                    SelectObject(layout);

                Rect titleRect = new(rowRect.x, rowRect.y, rowRect.width, 18f);
                Rect infoRect = new(rowRect.x, rowRect.y + 18f, rowRect.width, 18f);

                EditorGUI.LabelField(titleRect, layout.DisplayName, EditorStyles.boldLabel);
                EditorGUI.LabelField(infoRect, GetLayoutListInfo(layout));
            }

            EditorGUILayout.Space(2f);
        }

        private void DrawLayoutDetails(SavedLayout layout)
        {
            if (!layout)
                return;

            EditorGUILayout.LabelField("Layout Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawLayoutThumbnail(layout);

                using (new EditorGUILayout.VerticalScope())
                {
                    DrawLayoutPreviewStat("Scene", GetLayoutSceneLabel(layout));
                    DrawLayoutPreviewStat("Target Area", GetLayoutTargetLabel(layout));
                    DrawLayoutPreviewStat("Objects", layout.ObjectCount.ToString());
                    DrawLayoutPreviewStat("Targets", GetLayoutPlacementTargetLabel(layout));
                    DrawLayoutPreviewStat("Style", string.IsNullOrWhiteSpace(layout.StyleName) ? "No Style" : layout.StyleName);
                    DrawLayoutPreviewStat("Bounds", FormatVector(layout.Bounds.size));
                }
            }

            string assetSummary = GetLayoutAssetSummary(layout);

            if (!string.IsNullOrWhiteSpace(assetSummary))
                EditorGUILayout.LabelField(assetSummary, EditorStyles.miniLabel);

            EditorGUILayout.Space(4f);

            if (_layoutScopeFilter == LayoutScopeFilter.CurrentTargetArea)
            {
                IAreaSource areaSource = CreateLayoutAreaSource();
                DrawLayoutPreviewStat(
                    "Apply Target",
                    areaSource?.SourceInfo.SourceName ?? "No Target Area");
            }
            else
            {
                DrawLayoutApplyTargetSelector();
            }

            DrawLayoutActionButtons(layout);
        }

        private void DrawLayoutActionButtons(SavedLayout layout)
        {
            IAreaSource areaSource = CreateLayoutAreaSource();
            bool canApply = areaSource != null && LayoutWorkflow.MatchesArea(layout, areaSource);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview A"))
                    PreviewContentLayout(layout, LayoutPreviewSlot.A);

                if (GUILayout.Button("Preview B"))
                    PreviewContentLayout(layout, LayoutPreviewSlot.B);

                if (GUILayout.Button("Clear Preview"))
                    LayoutWorkflow.ClearPreview();

                using (new EditorGUI.DisabledScope(!canApply))
                {
                    if (GUILayout.Button("Apply"))
                        ApplyContentLayout(layout, areaSource);
                }

                using (new EditorGUI.DisabledScope(layout.Locked))
                {
                    if (GUILayout.Button(layout.Locked ? "Locked" : "Delete"))
                        DeleteSelectedLayout();
                }
            }

            if (areaSource != null && !canApply)
                EditorGUILayout.HelpBox("This layout belongs to a different scene or target area than the selected apply target.", MessageType.Warning);
        }

        private void DrawLayoutApplyTargetSelector()
        {
            EditorGUILayout.LabelField("Apply Target", EditorStyles.boldLabel);
            _layoutTargetAreaSelector.Draw("Target Area");
        }

        private void PreviewContentLayout(SavedLayout layout, LayoutPreviewSlot slot)
        {
            if (!LayoutWorkflow.PreviewLayout(layout, slot, out string error))
                Debug.LogWarning(error);
        }

        private void ApplyContentLayout(SavedLayout layout, IAreaSource areaSource)
        {
            if (areaSource == null)
                return;

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

        private void DeleteSelectedLayout()
        {
            if (!_selectedLayout)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Layout",
                $"Delete layout '{_selectedLayout.DisplayName}' and its saved prefab?",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            SavedLayout layout = _selectedLayout;
            _selectedLayout = null;
            DestroySelectedObjectEditor();

            if (!LayoutWorkflow.DeleteLayout(layout, out string error))
                Debug.LogWarning(error);

            Repaint();
        }

        private void DeleteMatchingLayouts(IReadOnlyList<SavedLayout> layouts)
        {
            SavedLayout[] targets = layouts.Where(layout => layout).Distinct().ToArray();
            if (targets.Length == 0)
                return;

            int lockedCount = targets.Count(layout => layout.Locked);
            string warning = lockedCount > 0
                ? $"\n\nThis includes {lockedCount:N0} locked evaluation layout(s). Reports that reference deleted layouts retain their numeric results, but those observations can no longer be applied or used as visual evidence."
                : string.Empty;
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Matching Layouts",
                $"Delete all {targets.Length:N0} layouts matching the current scope and search filters, together with their saved prefabs?{warning}\n\nThis cannot be undone.",
                $"Delete {targets.Length:N0}",
                "Cancel");
            if (!confirmed)
                return;

            _selectedLayout = null;
            DestroySelectedObjectEditor();
            if (!LayoutWorkflow.DeleteLayouts(targets, true, out int deletedCount, out string error))
            {
                Debug.LogWarning(error);
                return;
            }

            _layoutPage = 0;
            Debug.Log($"Deleted {deletedCount:N0} saved Genix layout(s) and their owned prefabs.");
            Repaint();
        }

        private void DrawLayoutSortDropdown()
        {
            LayoutSortMode[] modes =
            {
                LayoutSortMode.NewestFirst,
                LayoutSortMode.NameAscending,
                LayoutSortMode.TargetArea,
                LayoutSortMode.ObjectCountDescending
            };

            string[] labels =
            {
                "Newest First",
                "Name (A-Z)",
                "Target Area",
                "Most Objects First"
            };

            _layoutSortMode = DrawSortDropdown(_layoutSortMode, modes, labels);
        }

        private static LayoutScopeFilter DrawLayoutScopePopup(LayoutScopeFilter current)
        {
            LayoutScopeFilter[] modes =
            {
                LayoutScopeFilter.CurrentScene,
                LayoutScopeFilter.CurrentTargetArea,
                LayoutScopeFilter.AllScenes
            };

            string[] labels =
            {
                "Current Scene",
                "Current Target Area",
                "All Scenes"
            };

            int selectedIndex = Array.IndexOf(modes, current);

            if (selectedIndex < 0)
                selectedIndex = 0;

            selectedIndex = EditorGUILayout.Popup(
                new GUIContent("Scope", "Current Scene is the usual focused view; Current Target Area narrows results spatially; All Scenes searches the whole project."),
                selectedIndex,
                labels);
            return modes[Mathf.Clamp(selectedIndex, 0, modes.Length - 1)];
        }

        private void ClearLayoutFilters()
        {
            _layoutSearch = string.Empty;
            _layoutScopeFilter = LayoutScopeFilter.CurrentScene;
            _layoutSortMode = LayoutSortMode.NewestFirst;
            _layoutPage = 0;
        }

        private bool MatchesLayoutSearch(SavedLayout layout)
        {
            if (string.IsNullOrWhiteSpace(_layoutSearch))
                return true;

            string search = _layoutSearch.Trim();

            return layout.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (layout.TargetAreaName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (layout.SceneName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (layout.StyleName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   layout.AssetPool && layout.AssetPool.name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   layout.AssetSummaries.Any(summary => (summary.AssetName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        private List<SavedLayout> SortLayouts(IEnumerable<SavedLayout> layouts)
        {
            return _layoutSortMode switch
            {
                LayoutSortMode.NameAscending => layouts
                    .OrderBy(layout => layout.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                LayoutSortMode.TargetArea => layouts
                    .OrderBy(layout => layout.TargetAreaName, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(layout => layout.CreatedAt, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                LayoutSortMode.ObjectCountDescending => layouts
                    .OrderByDescending(layout => layout.ObjectCount)
                    .ThenByDescending(layout => layout.CreatedAt, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                _ => layouts
                    .OrderByDescending(layout => layout.Favorite)
                    .ThenByDescending(layout => layout.CreatedAt, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(layout => layout.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private IAreaSource CreateLayoutAreaSource()
        {
            return _layoutTargetAreaSelector.CreateAreaSource();
        }

        private static void DrawLayoutThumbnail(SavedLayout layout)
        {
            Rect rect = GUILayoutUtility.GetRect(88f, 88f, GUILayout.Width(88f), GUILayout.Height(88f));
            GUI.Box(rect, GUIContent.none);

            if (!layout.Prefab)
            {
                EditorGUI.LabelField(rect, "No Prefab", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Texture2D preview = AssetPreview.GetAssetPreview(layout.Prefab);
            if (!preview)
                preview = AssetPreview.GetMiniThumbnail(layout.Prefab);

            if (preview)
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
        }

        private static void DrawLayoutPreviewStat(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }

        private static string GetLayoutListInfo(SavedLayout layout)
        {
            return $"{GetLayoutSceneLabel(layout)}    {GetLayoutTargetLabel(layout)}    {layout.ObjectCount} objects    {layout.CreatedAt}";
        }

        private static string GetLayoutSceneLabel(SavedLayout layout)
        {
            return string.IsNullOrWhiteSpace(layout.SceneName) ? "Unknown Scene" : layout.SceneName;
        }

        private static string GetLayoutTargetLabel(SavedLayout layout)
        {
            return string.IsNullOrWhiteSpace(layout.TargetAreaName) ? "Unknown Target Area" : layout.TargetAreaName;
        }

        private static string GetLayoutPlacementTargetLabel(SavedLayout layout)
        {
            return FormatPlacementTargets(layout.PlacementTargets);
        }

        private static string FormatPlacementTargets(PlacementTarget targets)
        {
            targets &= PlacementTarget.All;

            if (targets == PlacementTarget.All)
                return "Any";

            if (targets == PlacementTarget.None)
                return "None";

            List<string> labels = new();

            if ((targets & PlacementTarget.Floor) != 0)
                labels.Add("Floor");

            if ((targets & PlacementTarget.Wall) != 0)
                labels.Add("Wall");

            if ((targets & PlacementTarget.Ceiling) != 0)
                labels.Add("Ceiling");

            if ((targets & PlacementTarget.InsideSpace) != 0)
                labels.Add("Inside Space");

            return string.Join(", ", labels);
        }

        private static string GetLayoutAssetSummary(SavedLayout layout)
        {
            if (layout.AssetSummaries == null || layout.AssetSummaries.Count == 0)
                return string.Empty;

            const int maxShown = 6;

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
