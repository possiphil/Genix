using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.UI;
using Genix.Extensions;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixContentWindow
    {
        private void DrawTagsTab(AssetCatalog catalog)
        {
            DrawTagCategoryFilters();

            EditorGUILayout.Space(6f);

            DrawCategoryList(catalog);

            EditorGUILayout.Space(6f);

            DrawTagList(catalog);
        }

        private void DrawTagCategoryFilters()
        {
            DrawSectionHeader("Category Filters", () =>
            {
                if (!string.IsNullOrWhiteSpace(_categorySearch) && GUILayout.Button("Reset", GUILayout.Width(60f)))
                    ClearCategoryFilters();
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _categorySearch = EditorGUILayout.TextField(
                    new GUIContent("Search", "Filter semantic categories by display name."),
                    _categorySearch);
            }
        }

        private void ClearCategoryFilters()
        {
            _categorySearch = string.Empty;
        }

        private void DrawCategoryList(AssetCatalog catalog)
        {
            List<TagCategory> categories = GetFilteredCategories(catalog);

            DrawSectionHeader($"Categories ({categories.Count})", () =>
            {
                if (GUILayout.Button("New", GUILayout.Width(48f)))
                    CreateCategory();

                using (new EditorGUI.DisabledScope(!_selectedTagCategory))
                {
                    if (GUILayout.Button("Delete…", GUILayout.Width(64f)))
                        DeleteCategory(_selectedTagCategory);
                }
            });

            Rect boxRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(ListHeight));

            _categoryScroll = EditorGUILayout.BeginScrollView(_categoryScroll);

            if (categories.Count == 0)
                DesignerTerminology.DrawEmptyState("No categories match the current filters.");
            else
            {
                foreach (TagCategory category in categories)
                    DrawCategoryListItem(catalog, category);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawCategoryListItem(
            AssetCatalog catalog,
            TagCategory category)
        {
            bool selected = _selectedTagCategory == category;
            GUIStyle style = selected ? EditorStyles.helpBox : GUIStyle.none;

            using (new EditorGUILayout.VerticalScope(style))
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 40f);

                Event currentEvent = Event.current;

                if (currentEvent.type == EventType.MouseDown &&
                    currentEvent.button == 0 &&
                    rowRect.Contains(currentEvent.mousePosition))
                {
                    SelectObject(category);
                    currentEvent.Use();
                }

                Rect titleRect = new(rowRect.x, rowRect.y, rowRect.width, 18f);
                Rect infoRect = new(rowRect.x, rowRect.y + 18f, rowRect.width, 18f);

                const float usageColumnWidth = 150f;
                const float modeColumnWidth = 120f;

                Rect usageRect = new(
                    infoRect.x,
                    infoRect.y,
                    usageColumnWidth,
                    infoRect.height);

                Rect modeRect = new(
                    infoRect.x + usageColumnWidth,
                    infoRect.y,
                    modeColumnWidth,
                    infoRect.height);

                Rect tagsRect = new(
                    infoRect.x + usageColumnWidth + modeColumnWidth,
                    infoRect.y,
                    infoRect.width - usageColumnWidth - modeColumnWidth,
                    infoRect.height);

                int tagCount = catalog.Tags.Count(tag => tag && tag.Category == category);
                string mode = category.AllowMultipleTags ? "Multiple" : "Single";

                EditorGUI.LabelField(titleRect, category.DisplayName, EditorStyles.boldLabel);
                if (infoRect.width < 390f)
                {
                    EditorGUI.LabelField(
                        infoRect,
                        $"{category.Usage.ToDisplayName()} · {mode} · {tagCount} tag(s)");
                }
                else
                {
                    EditorGUI.LabelField(usageRect, $"Available On: {category.Usage.ToDisplayName()}");
                    EditorGUI.LabelField(
                        modeRect,
                        new GUIContent($"Selection: {mode}", "Whether this category allows one or multiple tags to be selected."));
                    EditorGUI.LabelField(tagsRect, $"Tags: {tagCount}");
                }
            }
        }

        private void DrawTagList(AssetCatalog catalog)
        {
            TagCategory selectedCategory = GetSelectedTagCategory();
            bool filterBySelectedCategory = selectedCategory;

            List<SemanticTag> tags = SortTags(catalog.Tags
                    .Where(tag => tag)
                    .Where(tag => !filterBySelectedCategory || tag.Category == selectedCategory))
                .ToList();

            string title = filterBySelectedCategory
                ? $"Tags in {selectedCategory.DisplayName} ({tags.Count})"
                : $"Tags ({tags.Count})";

            DrawSectionHeader(title, () =>
            {
                using (new EditorGUI.DisabledScope(!filterBySelectedCategory))
                {
                    if (GUILayout.Button(
                            new GUIContent("Show All", "Clear the category filter and show tags from every category."),
                            GUILayout.Width(66f)))
                    {
                        ClearSelection();
                    }
                }

                using (new EditorGUI.DisabledScope(GetTargetCategoryForNewTag() == null))
                {
                    if (GUILayout.Button("New", GUILayout.Width(48f)))
                        CreateTag();
                }

                using (new EditorGUI.DisabledScope(!_selectedSemanticTag))
                {
                    if (GUILayout.Button("Delete…", GUILayout.Width(64f)))
                        DeleteTag(_selectedSemanticTag);
                }
            });

            Rect boxRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(ListHeight));

            _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll);

            if (tags.Count == 0)
                DesignerTerminology.DrawEmptyState("No tags match the selected category and filters.");
            else
            {
                foreach (SemanticTag tag in tags)
                    DrawTagListItem(tag, !filterBySelectedCategory);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTagListItem(SemanticTag tag, bool showCategoryPrefix)
        {
            bool selected = _selectedSemanticTag == tag;
            GUIStyle style = selected ? EditorStyles.helpBox : GUIStyle.none;

            using (new EditorGUILayout.VerticalScope(style))
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 22f);

                Event currentEvent = Event.current;

                if (currentEvent.type == EventType.MouseDown &&
                    currentEvent.button == 0 &&
                    rowRect.Contains(currentEvent.mousePosition))
                {
                    SelectObject(tag);
                    currentEvent.Use();
                }

                EditorGUI.LabelField(
                    rowRect,
                    GetTagListLabel(tag, showCategoryPrefix),
                    EditorStyles.boldLabel);
            }
        }

    }
}
