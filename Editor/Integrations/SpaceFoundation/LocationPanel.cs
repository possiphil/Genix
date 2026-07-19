using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Common;
using Genix.Editor.TargetAreas;
using Genix.Semantics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using SfsAnchor = SpaceFoundationSystem.Anchor;

namespace Genix.SpaceFoundation.Editor
{
    public sealed class LocationPanel : ILocationPanel
    {
        private const float ListHeight = 260f;

        private enum LocationSortMode
        {
            AlphabeticalAscending,
            AlphabeticalDescending,
            TagCountDescending,
            TagCountAscending,
            Hierarchy
        }

        private string _search = string.Empty;
        private LocationSortMode _locationSortMode = LocationSortMode.AlphabeticalAscending;
        private readonly Dictionary<TagCategory, List<SemanticTag>> _locationCategoryFilters = new();
        private Vector2 _scroll;
        private SfsAnchor _selectedAnchor;

        public string Title => "Space Foundation";

        public void Draw(AssetCatalog catalog)
        {
            DrawFilters(catalog);

            EditorGUILayout.Space(4f);

            List<SfsAnchor> anchors = GetFilteredLocations(catalog);

            DrawLocationList(anchors);

            EditorGUILayout.Space(6f);

            DrawLocationDetails(catalog);
        }

        private void DrawFilters(AssetCatalog catalog)
        {
            DrawSectionHeader("Filters", () =>
            {
                if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                    ClearFilters();
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _search = EditorGUILayout.TextField("Search", _search);
                DrawCategoryFilters(catalog);
            }
        }

        private void DrawCategoryFilters(AssetCatalog catalog)
        {
            foreach (TagCategory category in catalog.Categories
                         .Where(category => category)
                         .OrderBy(category => category.DisplayName))
            {
                DrawCategoryFilter(catalog, category);
            }
        }

        private void DrawCategoryFilter(AssetCatalog catalog, TagCategory category)
        {
            List<SemanticTag> tags = catalog.Tags
                .Where(tag => tag && tag.Category == category)
                .OrderBy(tag => tag.DisplayName)
                .ToList();

            IReadOnlyList<SemanticTag> selectedTags = GetSelectedFilterTags(category);

            TagSelectionField.Draw(
                category.DisplayName,
                category,
                tags,
                selectedTags,
                newSelection => SetCategoryFilter(category, newSelection),
                forceMultiSelect: true,
                showNoneOption: false);
        }

        private IReadOnlyList<SemanticTag> GetSelectedFilterTags(TagCategory category)
        {
            if (!category)
                return Array.Empty<SemanticTag>();

            if (!_locationCategoryFilters.TryGetValue(category, out List<SemanticTag> selectedTags))
                return Array.Empty<SemanticTag>();

            return selectedTags
                .Where(tag => tag && tag.Category == category)
                .Distinct()
                .ToList();
        }

        private void SetCategoryFilter(
            TagCategory category,
            IReadOnlyList<SemanticTag> selectedTags)
        {
            if (!category)
                return;

            List<SemanticTag> validTags = selectedTags
                .Where(tag => tag && tag.Category == category)
                .Distinct()
                .ToList();

            if (validTags.Count == 0)
                _locationCategoryFilters.Remove(category);
            else
                _locationCategoryFilters[category] = validTags;
        }

        private void ClearFilters()
        {
            _search = string.Empty;
            _locationCategoryFilters.Clear();
        }

        private void DrawLocationList(IReadOnlyList<SfsAnchor> anchors)
        {
            DrawSectionHeader($"Locations ({anchors.Count})", () =>
            {
                DrawLocationSortDropdown();

                using (new EditorGUI.DisabledScope(!_selectedAnchor))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                        DeleteSelectedLocation();
                }

                using (new EditorGUI.DisabledScope(anchors.Count == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                        DeleteLocations(anchors);
                }
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(ListHeight)))
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                if (anchors.Count == 0)
                {
                    bool hasLocations = Object.FindObjectsByType<SfsAnchor>()
                        .Any(anchor => anchor);
                    string message = hasLocations
                        ? "No locations match the current filters."
                        : "No SFS locations found in the current scene.";

                    EditorGUILayout.HelpBox(message, MessageType.Info);
                }
                else
                {
                    foreach (SfsAnchor anchor in anchors)
                        DrawLocationListItem(anchor);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawLocationSortDropdown()
        {
            LocationSortMode[] modes =
            {
                LocationSortMode.AlphabeticalAscending,
                LocationSortMode.AlphabeticalDescending,
                LocationSortMode.TagCountDescending,
                LocationSortMode.TagCountAscending,
                LocationSortMode.Hierarchy
            };

            string[] labels =
            {
                "Alphabetical Ascending",
                "Alphabetical Descending",
                "Tag Count Descending",
                "Tag Count Ascending",
                "Hierarchy"
            };

            int selectedIndex = Array.IndexOf(modes, _locationSortMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            GUILayout.Label("Sort by", EditorStyles.label, GUILayout.Width(42f));

            selectedIndex = EditorGUILayout.Popup(
                selectedIndex,
                labels,
                GUILayout.Width(180f));

            _locationSortMode = modes[selectedIndex];
        }

        private void DrawLocationListItem(SfsAnchor anchor)
        {
            bool selected = _selectedAnchor == anchor;
            GUIStyle style = selected ? EditorStyles.helpBox : GUIStyle.none;

            using (new EditorGUILayout.VerticalScope(style))
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 42f);
                Event currentEvent = Event.current;

                if (currentEvent.type == EventType.MouseDown &&
                    currentEvent.button == 0 &&
                    rowRect.Contains(currentEvent.mousePosition))
                {
                    _selectedAnchor = anchor;

                    if (currentEvent.clickCount == 2)
                        Selection.activeObject = anchor.gameObject;

                    currentEvent.Use();
                }

                Rect titleRect = new(rowRect.x, rowRect.y, rowRect.width, 18f);
                Rect infoRect = new(rowRect.x, rowRect.y + 18f, rowRect.width, 18f);

                EditorGUI.LabelField(titleRect, anchor.name, EditorStyles.boldLabel);
                EditorGUI.LabelField(infoRect, GetLocationTagsLabel(anchor));
            }

            EditorGUILayout.Space(2f);
        }

        private void DrawLocationDetails(AssetCatalog catalog)
        {
            if (!_selectedAnchor)
                return;

            DrawSectionHeader("Location Details", null);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawDisplayNameField(_selectedAnchor);
                EditorGUILayout.Space(6f);
                DrawSemanticTags(catalog, _selectedAnchor);
            }
        }

        private static void DrawDisplayNameField(SfsAnchor anchor)
        {
            EditorGUI.BeginChangeCheck();
            string displayName = EditorGUILayout.DelayedTextField("Display Name", anchor.name);

            if (!EditorGUI.EndChangeCheck())
                return;

            string cleanName = string.IsNullOrWhiteSpace(displayName)
                ? anchor.name
                : displayName.Trim();

            if (cleanName == anchor.name)
                return;

            Undo.RecordObject(anchor.gameObject, "Renamed Genix Location");
            anchor.gameObject.name = cleanName;
            EditorUtility.SetDirty(anchor.gameObject);
            EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
        }

        private static void DrawSemanticTags(AssetCatalog catalog, SfsAnchor anchor)
        {
            DrawSectionHeader("Semantic Tags", () =>
            {
                using (new EditorGUI.DisabledScope(!GetTagSet(anchor)))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                        ClearTags(anchor);
                }
            });

            List<TagCategory> categories = catalog.Categories
                .Where(category => category)
                .OrderBy(category => category.DisplayName)
                .ToList();

            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox("No tag categories available.", MessageType.Info);
                return;
            }

            SemanticTagSet tagSet = GetTagSet(anchor);

            foreach (TagCategory category in categories)
            {
                List<SemanticTag> tags = catalog.Tags
                    .Where(tag => tag && tag.Category == category)
                    .OrderBy(tag => tag.DisplayName)
                    .ToList();

                List<SemanticTag> assignedTags = GetAssignedTagsInCategory(tagSet, category);
                bool anySelected = IsAnySelectedInCategory(tagSet, category);

                TagSelectionField.Draw(
                    category.DisplayName,
                    category,
                    tags,
                    assignedTags,
                    selectedTags => SetTagsForCategory(anchor, category, selectedTags),
                    forceMultiSelect: true,
                    anySelected: anySelected,
                    onChangedWithSpecialSelection: (selectedTags, specialSelection) =>
                        SetTagsForCategory(anchor, category, selectedTags, specialSelection == TagSelectionField.SpecialSelection.Any),
                    showNoneOption: false);
            }
        }

        private static List<SemanticTag> GetAssignedTagsInCategory(
            SemanticTagSet tagSet,
            TagCategory category)
        {
            if (!tagSet || !category)
                return new List<SemanticTag>();

            return tagSet.SemanticTags
                .Where(tag => tag && tag.Category == category)
                .Distinct()
                .ToList();
        }

        private static bool IsAnySelectedInCategory(
            SemanticTagSet tagSet,
            TagCategory category)
        {
            return tagSet && tagSet.AnyTagCategories.Contains(category);
        }

        private static void SetTagsForCategory(
            SfsAnchor anchor,
            TagCategory category,
            IReadOnlyList<SemanticTag> selectedTags,
            bool selectAny = false)
        {
            SemanticTagSet tagSet = GetOrCreateTagSet(anchor);

            Undo.RecordObject(tagSet, "Changed Genix Location Tags");

            tagSet.SetTagsForCategory(category, selectedTags, forceAllowMultipleTags: true, selectAny: selectAny);

            EditorUtility.SetDirty(tagSet);
            EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
        }

        private static void ClearTags(SfsAnchor anchor)
        {
            SemanticTagSet tagSet = GetTagSet(anchor);

            if (!tagSet)
                return;

            Undo.RecordObject(tagSet, "Cleared Genix Location Tags");

            tagSet.Clear();

            EditorUtility.SetDirty(tagSet);
            EditorSceneManager.MarkSceneDirty(anchor.gameObject.scene);
        }

        private void DeleteLocations(IReadOnlyList<SfsAnchor> anchors)
        {
            List<SfsAnchor> validAnchors = anchors
                .Where(anchor => anchor)
                .Distinct()
                .ToList();

            if (validAnchors.Count == 0)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Locations",
                $"Delete {validAnchors.Count} filtered location{(validAnchors.Count == 1 ? string.Empty : "s")} from the scene?\n\nThis will delete the source GameObjects.",
                "Clear",
                "Cancel");

            if (!confirmed)
                return;

            UndoStep.ExecuteAsSingleStep("Cleared Genix Locations", () =>
            {
                if (_selectedAnchor && validAnchors.Contains(_selectedAnchor))
                    _selectedAnchor = null;

                foreach (SfsAnchor anchor in validAnchors)
                    Undo.DestroyObjectImmediate(anchor.gameObject);
            });
        }

        private void DeleteSelectedLocation()
        {
            if (!_selectedAnchor)
                return;

            GameObject target = _selectedAnchor.gameObject;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Location",
                $"Delete location '{target.name}' from the scene?\n\nThis will delete the source GameObject.",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            _selectedAnchor = null;
            Selection.activeObject = null;
            Undo.DestroyObjectImmediate(target);
        }

        private static SemanticTagSet GetTagSet(SfsAnchor anchor)
        {
            if (!anchor)
                return null;

            return anchor.TryGetComponent(out SemanticTagSet tagSet) ? tagSet : null;
        }

        private static SemanticTagSet GetOrCreateTagSet(SfsAnchor anchor)
        {
            if (anchor.TryGetComponent(out SemanticTagSet tagSet))
                return tagSet;

            return Undo.AddComponent<SemanticTagSet>(anchor.gameObject);
        }

        private static string GetLocationTagsLabel(SfsAnchor anchor)
        {
            SemanticTagSet tagSet = GetTagSet(anchor);

            if (!tagSet || tagSet.SemanticTags.Count == 0)
            {
                if (tagSet && tagSet.AnyTagCategories.Count > 0)
                    return string.Join(", ", tagSet.AnyTagCategories.Where(category => category).Select(category => $"{category.DisplayName}: Any"));

                return "Any";
            }

            List<string> labels = tagSet.SemanticTags
                .Where(tag => tag)
                .Select(GetTagDisplayLabel)
                .ToList();

            labels.AddRange(tagSet.AnyTagCategories
                .Where(category => category)
                .Select(category => $"{category.DisplayName}: Any"));

            return string.Join(", ", labels);
        }

        private static string GetTagDisplayLabel(SemanticTag tag)
        {
            if (!tag)
                return "Missing Tag";

            return tag.Category
                ? $"{tag.Category.DisplayName}: {tag.DisplayName}"
                : tag.DisplayName;
        }

        private List<SfsAnchor> GetFilteredLocations(AssetCatalog catalog)
        {
            List<SfsAnchor> anchors = Object.FindObjectsByType<SfsAnchor>()
                .Where(anchor => anchor)
                .Where(MatchesSearch)
                .Where(MatchesCategoryFilters)
                .ToList();

            return SortLocations(catalog, anchors);
        }

        private List<SfsAnchor> SortLocations(AssetCatalog catalog, IEnumerable<SfsAnchor> anchors)
        {
            return _locationSortMode switch
            {
                LocationSortMode.AlphabeticalDescending => anchors
                    .OrderByDescending(anchor => anchor.name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                LocationSortMode.TagCountDescending => anchors
                    .OrderByDescending(anchor => GetLocationTagCount(catalog, anchor))
                    .ThenBy(anchor => anchor.name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                LocationSortMode.TagCountAscending => anchors
                    .OrderBy(anchor => GetLocationTagCount(catalog, anchor))
                    .ThenBy(anchor => anchor.name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                LocationSortMode.Hierarchy => anchors
                    .OrderBy(GetHierarchyPath, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                _ => anchors
                    .OrderBy(anchor => anchor.name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private static int GetLocationTagCount(AssetCatalog catalog, SfsAnchor anchor)
        {
            SemanticTagSet tagSet = GetTagSet(anchor);
            return tagSet ? GetSemanticTagCount(catalog, tagSet.SemanticTags, tagSet.AnyTagCategories) : 0;
        }

        private static int GetSemanticTagCount(
            AssetCatalog catalog,
            IEnumerable<SemanticTag> tags,
            IEnumerable<TagCategory> anyCategories)
        {
            int count = tags.Count(tag => tag);

            if (catalog == null)
                return count;

            foreach (TagCategory category in anyCategories.Where(category => category).Distinct())
                count += catalog.Tags.Count(tag => tag && tag.Category == category);

            return count;
        }

        private static string GetHierarchyPath(SfsAnchor anchor)
        {
            List<string> names = new();
            Transform current = anchor.transform;

            while (current)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private bool MatchesSearch(SfsAnchor anchor)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            string search = _search.Trim();

            if (anchor.name.Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;

            SemanticTagSet tagSet = GetTagSet(anchor);

            return tagSet && tagSet.SemanticTags
                .Where(tag => tag)
                .Any(tag => tag.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        private bool MatchesCategoryFilters(SfsAnchor anchor)
        {
            SemanticTagSet tagSet = GetTagSet(anchor);

            foreach (KeyValuePair<TagCategory, List<SemanticTag>> filter in _locationCategoryFilters)
            {
                List<SemanticTag> selectedTags = filter.Value
                    .Where(tag => tag && tag.Category == filter.Key)
                    .Distinct()
                    .ToList();

                if (selectedTags.Count == 0)
                    continue;

                if (!tagSet)
                    return false;

                if (tagSet.AnyTagCategories.Contains(filter.Key))
                    continue;

                if (!selectedTags.Any(tag => tagSet.SemanticTags.Contains(tag)))
                    return false;
            }

            return true;
        }

        private static void DrawSectionHeader(string title, Action drawButtons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
                GUILayout.Space(8f);
                GUILayout.FlexibleSpace();
                drawButtons?.Invoke();
            }
        }
    }
}
