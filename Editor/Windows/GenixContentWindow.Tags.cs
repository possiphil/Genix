using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
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
                if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                    ClearCategoryFilters();
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _categorySearch = EditorGUILayout.TextField("Search", _categorySearch);
                _filterCategoriesByMode = EditorGUILayout.Toggle("Filter By Mode", _filterCategoriesByMode);

                if (_filterCategoriesByMode)
                {
                    string[] labels = { "Multiple Tags", "Single Tag" };
                    int selectedIndex = _categoryModeFilterAllowsMultiple ? 0 : 1;
                    selectedIndex = EditorGUILayout.Popup("Mode", selectedIndex, labels);
                    _categoryModeFilterAllowsMultiple = selectedIndex == 0;
                }
            }
        }

        private void ClearCategoryFilters()
        {
            _categorySearch = string.Empty;
            _filterCategoriesByMode = false;
            _categoryModeFilterAllowsMultiple = true;
        }

        private void DrawCategoryList(AssetCatalog catalog)
        {
            List<TagCategory> categories = GetFilteredCategories(catalog);

            DrawSectionHeader($"Categories ({categories.Count})", () =>
            {
                DrawCategorySortDropdown();

                if (GUILayout.Button("Create", GUILayout.Width(60f)))
                    CreateCategory();

                using (new EditorGUI.DisabledScope(!_selectedTagCategory))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                        DeleteCategory(_selectedTagCategory);
                }

                using (new EditorGUI.DisabledScope(categories.Count == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                        ClearCategories();
                }
            });

            Rect boxRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(ListHeight));

            _categoryScroll = EditorGUILayout.BeginScrollView(_categoryScroll);

            if (categories.Count == 0)
            {
                string message = catalog.Categories.Any(category => category)
                    ? "No categories match the current filters."
                    : "No categories created yet.";

                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
            else
            {
                foreach (TagCategory category in categories)
                    DrawCategoryListItem(catalog, category);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawCategorySortDropdown()
        {
            CategorySortMode[] modes =
            {
                CategorySortMode.AlphabeticalAscending,
                CategorySortMode.AlphabeticalDescending,
                CategorySortMode.TagCountDescending,
                CategorySortMode.TagCountAscending,
                CategorySortMode.Mode
            };

            string[] labels =
            {
                "Alphabetical Ascending",
                "Alphabetical Descending",
                "Tag Count Descending",
                "Tag Count Ascending",
                "Mode"
            };

            _categorySortMode = DrawSortDropdown(_categorySortMode, modes, labels);
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

                const float modeColumnWidth = 150f;

                Rect modeRect = new(
                    infoRect.x,
                    infoRect.y,
                    modeColumnWidth,
                    infoRect.height);

                Rect tagsRect = new(
                    infoRect.x + modeColumnWidth,
                    infoRect.y,
                    infoRect.width - modeColumnWidth,
                    infoRect.height);

                int tagCount = catalog.Tags.Count(tag => tag && tag.Category == category);
                string mode = category.AllowMultipleTags ? "Multiple Tags" : "Single Tag";

                EditorGUI.LabelField(titleRect, category.DisplayName, EditorStyles.boldLabel);
                EditorGUI.LabelField(modeRect, $"Mode: {mode}");
                EditorGUI.LabelField(tagsRect, $"Tags: {tagCount}");
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
                DrawTagSortDropdown();

                using (new EditorGUI.DisabledScope(GetTargetCategoryForNewTag() == null))
                {
                    if (GUILayout.Button("Create", GUILayout.Width(60f)))
                        CreateTag();
                }

                using (new EditorGUI.DisabledScope(!_selectedSemanticTag))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                        DeleteTag(_selectedSemanticTag);
                }

                using (new EditorGUI.DisabledScope(tags.Count == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                        ClearTags(filterBySelectedCategory ? selectedCategory : null);
                }
            });

            Rect boxRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(ListHeight));

            _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll);

            if (tags.Count == 0)
            {
                string message = filterBySelectedCategory
                    ? "No tags in this category yet."
                    : "No tags created yet.";

                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
            else
            {
                foreach (SemanticTag tag in tags)
                    DrawTagListItem(tag, !filterBySelectedCategory);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTagSortDropdown()
        {
            TagSortMode[] modes =
            {
                TagSortMode.AlphabeticalAscending,
                TagSortMode.AlphabeticalDescending,
                TagSortMode.CategoryAscending,
                TagSortMode.CategoryDescending
            };

            string[] labels =
            {
                "Alphabetical Ascending",
                "Alphabetical Descending",
                "Category Ascending",
                "Category Descending"
            };

            _tagSortMode = DrawSortDropdown(_tagSortMode, modes, labels);
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
