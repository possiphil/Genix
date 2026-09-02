using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Core;
using Genix.Editor.Layouts;
using Genix.Editor.UI;
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

            LayoutBrowserSnapshot snapshot = GetLayoutBrowserSnapshot();
            List<LayoutBrowserIndexEntry> layouts = GetFilteredLayouts(snapshot.Entries);

            DrawLayoutList(layouts, snapshot);
        }

        private void DrawLayoutFilters()
        {
            DrawSectionHeader("Filters", () =>
            {
                if ((!string.IsNullOrWhiteSpace(_layoutSearch) ||
                     _layoutScopeFilter != LayoutScopeFilter.CurrentScene ||
                     _layoutSortMode != LayoutSortMode.NewestFirst) &&
                    GUILayout.Button("Reset", GUILayout.Width(60f)))
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

        private LayoutBrowserSnapshot GetLayoutBrowserSnapshot()
        {
            return _layoutScopeFilter switch
            {
                LayoutScopeFilter.CurrentTargetArea => LayoutWorkflow.BrowseLayoutsForArea(CreateLayoutAreaSource()),
                LayoutScopeFilter.AllScenes => LayoutWorkflow.BrowseLayouts(),
                _ => LayoutWorkflow.BrowseLayoutsForCurrentScene()
            };
        }

        private List<LayoutBrowserIndexEntry> GetFilteredLayouts(
            IEnumerable<LayoutBrowserIndexEntry> layouts)
        {
            IEnumerable<LayoutBrowserIndexEntry> filteredLayouts = layouts
                .Where(layout => layout != null)
                .Where(MatchesLayoutSearch);

            return SortLayouts(filteredLayouts);
        }

        private void DrawLayoutList(
            IReadOnlyList<LayoutBrowserIndexEntry> layouts,
            LayoutBrowserSnapshot snapshot)
        {
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(layouts.Count / (float)LayoutPageSize));
            _layoutPage = Mathf.Clamp(_layoutPage, 0, pageCount - 1);

            DrawSectionHeader($"Layouts ({layouts.Count})", () =>
            {
                DrawLayoutSortDropdown();
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

                if (snapshot.IsLoading)
                {
                    Rect progressRect = EditorGUILayout.GetControlRect(false, 20f);
                    EditorGUI.ProgressBar(
                        progressRect,
                        snapshot.Progress,
                        $"Loading saved layouts... {snapshot.Progress:P0}");
                    Repaint();
                }
                else if (layouts.Count == 0)
                    DesignerTerminology.DrawEmptyState("No saved layouts match the current filters.");
                else
                {
                    int first = _layoutPage * LayoutPageSize;
                    int last = Mathf.Min(first + LayoutPageSize, layouts.Count);
                    for (int i = first; i < last; i++)
                    {
                        LayoutBrowserIndexEntry layout = layouts[i];
                        DrawLayoutListItem(layout);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawLayoutListItem(LayoutBrowserIndexEntry layout)
        {
            bool selected = _selectedLayout && string.Equals(
                AssetDatabase.GetAssetPath(_selectedLayout),
                layout.AssetPath,
                StringComparison.OrdinalIgnoreCase);
            GUIStyle style = selected ? EditorStyles.helpBox : GUIStyle.none;

            using (new EditorGUILayout.VerticalScope(style))
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 40f);

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    SavedLayout selectedLayout = layout.LoadAsset();
                    if (selectedLayout)
                        SelectObject(selectedLayout);
                }

                Rect titleRect = new(rowRect.x, rowRect.y, rowRect.width, 18f);
                Rect infoRect = new(rowRect.x, rowRect.y + 18f, rowRect.width, 18f);

                EditorGUI.LabelField(titleRect, layout.DisplayName, EditorStyles.boldLabel);
                string info = GetLayoutListInfo(layout);
                EditorGUI.LabelField(infoRect, new GUIContent(info, info));
            }

            EditorGUILayout.Space(2f);
        }

        private void DrawLayoutDetails(SavedLayout layout)
        {
            if (!layout)
                return;

            DrawSectionHeader("Layout Summary", () => DrawDeleteLayoutButton(layout));

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawLayoutThumbnail(layout);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawLayoutPreviewStat("Target Area", GetLayoutTargetLabel(layout));
                        DrawLayoutPreviewStat("Objects", layout.ObjectCount.ToString());
                        DrawLayoutPreviewStat("Placement", GetLayoutPlacementTargetLabel(layout));
                        DrawLayoutPreviewStat("Style", string.IsNullOrWhiteSpace(layout.StyleName) ? "No Style" : layout.StyleName);
                    }
                }

                EditorGUILayout.Space(3f);
                DrawLayoutAssets(layout);
            }

            EditorGUILayout.Space(6f);

            DrawLayoutMetadata(layout);

            EditorGUILayout.Space(6f);

            if (_layoutScopeFilter != LayoutScopeFilter.CurrentTargetArea)
                DrawLayoutApplyTargetSelector();

            DrawLayoutActionButtons(layout);
        }

        private void DrawLayoutMetadata(SavedLayout layout)
        {
            EditorGUILayout.LabelField("Organization", EditorStyles.boldLabel);

            string displayName = EditorGUILayout.DelayedTextField(
                new GUIContent("Layout Name", "Name shown in the layout browser."),
                layout.DisplayName);
            string notes = EditorGUILayout.DelayedTextField(
                new GUIContent("Notes", "Optional searchable notes about this layout."),
                layout.Notes);
            bool favorite = EditorGUILayout.Toggle(
                new GUIContent("Favorite", "Keep this layout ahead of other layouts when sorting by newest."),
                layout.Favorite);
            bool locked = EditorGUILayout.Toggle(
                new GUIContent("Protect Layout", "Prevent this layout from being removed by layout cleanup actions."),
                layout.Locked);

            if (displayName == layout.DisplayName &&
                notes == layout.Notes &&
                favorite == layout.Favorite &&
                locked == layout.Locked)
            {
                return;
            }

            Undo.RecordObject(layout, "Edit Saved Layout");
            layout.SetDesignerMetadata(displayName, notes, favorite, locked);
            EditorUtility.SetDirty(layout);
            LayoutWorkflow.RefreshLayoutMetadata(layout);
        }

        private void DrawLayoutActionButtons(SavedLayout layout)
        {
            IAreaSource areaSource = CreateLayoutAreaSource();
            bool canApply = areaSource != null && LayoutWorkflow.MatchesArea(layout, areaSource);

            EditorGUILayout.Space(4f);

            GUIStyle primaryButton = new(GUI.skin.button)
            {
                fixedHeight = 28f
            };

            using (new EditorGUILayout.HorizontalScope())
            {
                bool isPreviewing = LayoutWorkflow.IsPreviewing(layout);
                bool shouldPreview = GUILayout.Toggle(
                    isPreviewing,
                    new GUIContent(
                        isPreviewing ? "Hide Preview" : "Preview Layout",
                        "Show or hide this saved layout as a non-persistent Scene view preview."),
                    primaryButton,
                    GUILayout.ExpandWidth(true));
                if (shouldPreview != isPreviewing)
                    ToggleContentLayoutPreview(layout, shouldPreview);

                GUILayout.Space(4f);

                using (new EditorGUI.DisabledScope(!canApply))
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                "Apply Layout",
                                "Replace the generated objects in the selected target area with this layout."),
                            primaryButton,
                            GUILayout.ExpandWidth(true)))
                        ApplyContentLayout(layout, areaSource);
                }
            }

            if (areaSource != null && !canApply)
                EditorGUILayout.HelpBox("This layout belongs to a different scene or target area than the selected apply target.", MessageType.Warning);
        }

        private void DrawDeleteLayoutButton(SavedLayout layout)
        {
            using (new EditorGUI.DisabledScope(layout.Locked))
            {
                GUIContent deleteContent = new(
                    "Delete Layout…",
                    layout.Locked
                        ? "This layout is locked. Disable Protect Layout to delete it."
                        : "Delete this saved layout and its owned prefab.");
                if (GUILayout.Button(deleteContent, GUILayout.Width(96f)))
                    DeleteSelectedLayout();
            }
        }

        private void DrawLayoutApplyTargetSelector()
        {
            EditorGUILayout.LabelField("Apply Target", EditorStyles.boldLabel);
            _layoutTargetAreaSelector.Draw("Target Area");
        }

        private static void ToggleContentLayoutPreview(SavedLayout layout, bool show)
        {
            if (!show)
            {
                LayoutWorkflow.ClearPreview();
                return;
            }

            if (!LayoutWorkflow.PreviewLayout(layout, out string error))
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

        private void DeleteMatchingLayouts(IReadOnlyList<LayoutBrowserIndexEntry> layouts)
        {
            LayoutBrowserIndexEntry[] targetEntries = layouts
                .Where(layout => layout != null && !layout.Locked)
                .GroupBy(layout => layout.AssetPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
            if (targetEntries.Length == 0)
                return;

            int lockedCount = layouts.Count(layout => layout != null && layout.Locked);
            string lockedNote = lockedCount > 0
                ? $" {lockedCount:N0} locked layout(s) will be kept."
                : string.Empty;
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Filtered Layouts",
                $"Delete {targetEntries.Length:N0} unlocked layout(s) matching the current filters and their saved prefabs?{lockedNote}\n\nThis cannot be undone.",
                $"Delete {targetEntries.Length:N0}",
                "Cancel");
            if (!confirmed)
                return;

            SavedLayout[] targets = targetEntries
                .Select(entry => entry.LoadAsset())
                .Where(layout => layout)
                .ToArray();

            _selectedLayout = null;
            DestroySelectedObjectEditor();
            if (!LayoutWorkflow.DeleteLayouts(targets, false, out int deletedCount, out string error))
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

        private bool MatchesLayoutSearch(LayoutBrowserIndexEntry layout)
        {
            if (string.IsNullOrWhiteSpace(_layoutSearch))
                return true;

            string search = _layoutSearch.Trim();

            return layout.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (layout.TargetAreaName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (layout.SceneName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (layout.StyleName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (layout.AssetPoolName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   (layout.Notes ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   layout.AssetNames.Any(name =>
                       (name ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        private List<LayoutBrowserIndexEntry> SortLayouts(
            IEnumerable<LayoutBrowserIndexEntry> layouts)
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

        private static string GetLayoutListInfo(LayoutBrowserIndexEntry layout)
        {
            string target = string.IsNullOrWhiteSpace(layout.TargetAreaName)
                ? "Unknown Target Area"
                : layout.TargetAreaName;
            string createdAt = string.IsNullOrWhiteSpace(layout.CreatedAt)
                ? "Unknown Time"
                : layout.CreatedAt;
            return $"Target Area: {target}    Objects: {layout.ObjectCount}    Saved: {createdAt}";
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

        private void DrawLayoutAssets(SavedLayout layout)
        {
            if (layout.AssetSummaries == null || layout.AssetSummaries.Count == 0)
            {
                EditorGUILayout.LabelField("Assets", "None");
                return;
            }

            _showLayoutAssets = EditorGUILayout.Foldout(
                _showLayoutAssets,
                new GUIContent(
                    $"Assets ({layout.AssetSummaries.Count})",
                    "Show the asset types and counts captured in this layout."),
                true);
            if (!_showLayoutAssets)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (LayoutAssetSummary summary in layout.AssetSummaries)
                {
                    string assetName = string.IsNullOrWhiteSpace(summary.AssetName)
                        ? "Unknown Asset"
                        : summary.AssetName;
                    EditorGUILayout.LabelField(
                        new GUIContent(assetName, assetName),
                        new GUIContent(summary.Count.ToString()));
                }
            }
        }
    }
}
