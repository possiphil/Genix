using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.Genix.Editor.Common;
using Genix.Editor.Utilities;
using Genix.Extensions;
using Genix.Orientation;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixContentWindow
    {
        private void DrawAssetsTab(AssetCatalog catalog)
        {
            DrawPrefabCreationSection();

            EditorGUILayout.Space(4f);

            DrawAssetFilters(catalog);

            EditorGUILayout.Space(4f);

            List<AssetDefinition> filteredAssets = GetFilteredAssets(catalog);

            DrawAssetList(filteredAssets);

            EditorGUILayout.Space(4f);

            DrawStaticPoolAddSection(filteredAssets);
        }


        private void DrawPrefabCreationSection()
        {
            NormalizePrefabCreationList();

            DrawSectionHeader("Create Asset Definitions", () =>
            {
                using (new EditorGUI.DisabledScope(!HasValidCreationPrefabs()))
                {
                    if (GUILayout.Button("Create", GUILayout.Width(60f)))
                        CreatePrefabAssetsFromCreationList();

                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                    {
                        _prefabsToCreate.Clear();
                        NormalizePrefabCreationList();
                    }
                }
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawPrefabCreationList();
                DrawAssetCreationMessage();
            }
        }

        private void DrawPrefabCreationList()
        {
            const float buttonWidth = 24f;
            const float spacing = 4f;

            for (int i = 0; i < _prefabsToCreate.Count; i++)
            {
                GameObject prefab = _prefabsToCreate[i];

                Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

                Rect fieldRect = new(
                    rowRect.x,
                    rowRect.y,
                    rowRect.width - buttonWidth - spacing,
                    rowRect.height);

                Rect buttonRect = new(
                    fieldRect.xMax + spacing,
                    rowRect.y,
                    buttonWidth,
                    rowRect.height);

                if (!prefab)
                {
                    DrawAddPrefabField(fieldRect);
                    HandlePrefabCreationSlotClick(fieldRect, i);
                    HandlePrefabCreationSlotDragAndDrop(fieldRect, i);
                    HandlePrefabCreationPickerEvents();
                    continue;
                }

                HandlePrefabCreationSlotDragAndDrop(fieldRect, i);

                EditorGUI.BeginChangeCheck();

                GameObject newPrefab = (GameObject)EditorGUI.ObjectField(
                    fieldRect,
                    prefab,
                    typeof(GameObject),
                    false);

                if (EditorGUI.EndChangeCheck())
                {
                    SetPrefabCreationSlot(i, newPrefab);
                    GUI.FocusControl(null);
                    break;
                }

                if (GUI.Button(buttonRect, "-"))
                {
                    _prefabsToCreate.RemoveAt(i);
                    NormalizePrefabCreationList();
                    GUI.FocusControl(null);
                    break;
                }
            }
        }

        private static void DrawAddPrefabField(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.objectField);

            Rect textRect = new(
                rect.x + 4f,
                rect.y,
                rect.width - 8f,
                rect.height);

            GUIStyle style = new(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.75f, 0.75f, 0.75f)
                        : new Color(0.35f, 0.35f, 0.35f)
                }
            };

            EditorGUI.LabelField(textRect, "Add or drop Prefab(s) here", style);
        }

        private void HandlePrefabCreationSlotClick(Rect rect, int slotIndex)
        {
            Event currentEvent = Event.current;

            if (currentEvent.type != EventType.MouseDown ||
                currentEvent.button != 0 ||
                !rect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            _prefabCreationSlotPickerControlId = GUIUtility.GetControlID(FocusType.Passive);

            EditorGUIUtility.ShowObjectPicker<GameObject>(
                null,
                false,
                "t:Prefab",
                _prefabCreationSlotPickerControlId);

            currentEvent.Use();
        }

        private void HandlePrefabCreationPickerEvents()
        {
            Event currentEvent = Event.current;

            if (currentEvent.commandName != "ObjectSelectorClosed")
                return;

            if (EditorGUIUtility.GetObjectPickerControlID() != _prefabCreationSlotPickerControlId)
                return;

            GameObject selectedPrefab = EditorGUIUtility.GetObjectPickerObject() as GameObject;

            if (!AssetDefinitionFactory.IsPrefabAsset(selectedPrefab))
            {
                ShowAssetCreationMessage("Only prefab assets can be added.", MessageType.Warning);
                _prefabCreationSlotPickerControlId = -1;
                currentEvent.Use();
                return;
            }

            AddPrefabToCreationList(selectedPrefab);

            _prefabCreationSlotPickerControlId = -1;

            currentEvent.Use();
            Repaint();
        }

        private void AddPrefabToCreationList(GameObject prefab)
        {
            if (!AssetDefinitionFactory.IsPrefabAsset(prefab))
                return;

            if (_prefabsToCreate.Contains(prefab))
            {
                ShowAssetCreationMessage(
                    $"Prefab '{prefab.name}' is already in the list.",
                    MessageType.Warning);

                return;
            }

            _prefabsToCreate.RemoveAll(existingPrefab => !existingPrefab);
            _prefabsToCreate.Add(prefab);
            NormalizePrefabCreationList();
        }

        private void HandlePrefabCreationSlotDragAndDrop(Rect fieldRect, int slotIndex)
        {
            Event currentEvent = Event.current;

            if (!fieldRect.Contains(currentEvent.mousePosition))
                return;

            List<GameObject> prefabs = DragAndDrop.objectReferences
                .OfType<GameObject>()
                .Where(AssetDefinitionFactory.IsPrefabAsset)
                .Distinct()
                .ToList();

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = prefabs.Count > 0
                        ? DragAndDropVisualMode.Copy
                        : DragAndDropVisualMode.Rejected;

                    currentEvent.Use();
                    break;

                case EventType.DragPerform:
                    if (prefabs.Count == 0)
                        return;

                    DragAndDrop.AcceptDrag();

                    AddDraggedPrefabsToCreationList(
                        slotIndex,
                        prefabs,
                        out int addedCount,
                        out int duplicateCount);

                    ShowDraggedPrefabResultMessage(addedCount, duplicateCount);

                    currentEvent.Use();
                    Repaint();
                    break;
            }
        }



        private void AddDraggedPrefabsToCreationList(
            int slotIndex,
            IReadOnlyList<GameObject> prefabs,
            out int addedCount,
            out int duplicateCount)
        {
            addedCount = 0;
            duplicateCount = 0;

            if (prefabs == null || prefabs.Count == 0)
                return;

            _prefabsToCreate.RemoveAll(prefab => !prefab);

            List<GameObject> prefabsToAdd = new();

            foreach (GameObject prefab in prefabs)
            {
                if (!AssetDefinitionFactory.IsPrefabAsset(prefab))
                    continue;

                if (_prefabsToCreate.Contains(prefab) || prefabsToAdd.Contains(prefab))
                {
                    duplicateCount++;
                    continue;
                }

                prefabsToAdd.Add(prefab);
            }

            if (prefabsToAdd.Count == 0)
            {
                NormalizePrefabCreationList();
                return;
            }

            int insertIndex = Mathf.Clamp(slotIndex, 0, _prefabsToCreate.Count);

            if (insertIndex < _prefabsToCreate.Count)
                _prefabsToCreate.RemoveAt(insertIndex);

            foreach (GameObject prefab in prefabsToAdd)
            {
                _prefabsToCreate.Insert(insertIndex, prefab);
                insertIndex++;
                addedCount++;
            }

            NormalizePrefabCreationList();
        }

        private void ShowDraggedPrefabResultMessage(int addedCount, int duplicateCount)
        {
            if (duplicateCount > 0)
            {
                string message = addedCount > 0
                    ? $"Added {addedCount} prefab(s). {duplicateCount} prefab(s) were already in the list."
                    : $"{duplicateCount} prefab(s) were already in the list. Nothing was added.";

                ShowAssetCreationMessage(message, MessageType.Warning);
                return;
            }
        }

        private void NormalizePrefabCreationList()
        {
            _prefabsToCreate.RemoveAll(prefab => !prefab);

            for (int i = _prefabsToCreate.Count - 1; i >= 0; i--)
            {
                GameObject prefab = _prefabsToCreate[i];

                if (_prefabsToCreate.IndexOf(prefab) != i)
                    _prefabsToCreate.RemoveAt(i);
            }

            _prefabsToCreate.Add(null);
        }

        private void SetPrefabCreationSlot(int index, GameObject prefab)
        {
            if (index < 0 || index >= _prefabsToCreate.Count)
                return;

            if (!prefab)
            {
                _prefabsToCreate[index] = null;
                NormalizePrefabCreationList();
                return;
            }

            if (!AssetDefinitionFactory.IsPrefabAsset(prefab))
            {
                _prefabsToCreate[index] = null;
                ShowAssetCreationMessage("Only prefab assets can be added.", MessageType.Warning);
                NormalizePrefabCreationList();
                return;
            }

            if (_prefabsToCreate.Contains(prefab) && _prefabsToCreate[index] != prefab)
            {
                _prefabsToCreate[index] = null;
                ShowAssetCreationMessage($"Prefab '{prefab.name}' is already in the list.", MessageType.Warning);
                NormalizePrefabCreationList();
                return;
            }

            _prefabsToCreate[index] = prefab;
            NormalizePrefabCreationList();
        }

        private bool HasValidCreationPrefabs()
        {
            return _prefabsToCreate.Any(AssetDefinitionFactory.IsPrefabAsset);
        }

        private void CreatePrefabAssetsFromCreationList()
        {
            List<GameObject> prefabs = _prefabsToCreate
                .Where(AssetDefinitionFactory.IsPrefabAsset)
                .Distinct()
                .ToList();

            if (prefabs.Count == 0)
            {
                ShowAssetCreationMessage("No valid prefabs selected.", MessageType.Warning);
                return;
            }

            List<AssetDefinition> createdAssets = AssetDefinitionFactory.CreateAssetsFromPrefabs(prefabs);

            AssetCatalogService.Refresh();

            if (createdAssets.Count == 0)
            {
                ShowAssetCreationMessage("No asset definitions were created.", MessageType.Warning);
                return;
            }

            _prefabsToCreate.Clear();
            NormalizePrefabCreationList();

            AssetDefinition createdAsset = createdAssets[^1];
            SelectObject(createdAsset);

            ShowAssetCreationMessage(
                createdAssets.Count == 1
                    ? $"Created asset definition '{createdAsset.AssetName}'."
                    : $"Created {createdAssets.Count} asset definitions.",
                MessageType.Info);

            Repaint();
        }

        private void ShowAssetCreationMessage(string message, MessageType messageType)
        {
            _assetCreationMessage = message;
            _assetCreationMessageType = messageType;
            _assetCreationMessageUntil = EditorApplication.timeSinceStartup + 3.0;
        }



        private void DrawAssetCreationMessage()
        {
            if (string.IsNullOrWhiteSpace(_assetCreationMessage))
                return;

            if (EditorApplication.timeSinceStartup > _assetCreationMessageUntil)
                return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(_assetCreationMessage, _assetCreationMessageType);
        }



        private void DrawAssetFilters(AssetCatalog catalog)
        {
            DrawSectionHeader("Filters", () =>
            {
                if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                    ClearAssetFilters();
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _assetSearch = EditorGUILayout.TextField("Search", _assetSearch);

                _filterByPlacementType = EditorGUILayout.Toggle("Filter By Placement Type", _filterByPlacementType);

                if (_filterByPlacementType)
                {
                    using (new EditorGUI.IndentLevelScope())
                        _placementTypeFilter = (PlacementType)EditorGUILayout.EnumPopup("Placement Type", _placementTypeFilter);
                }

                _filterByOrientationMode = EditorGUILayout.Toggle("Filter By Orientation Mode", _filterByOrientationMode);

                if (_filterByOrientationMode)
                {
                    using (new EditorGUI.IndentLevelScope())
                        _orientationModeFilter = (OrientationMode)EditorGUILayout.EnumPopup("Orientation Mode", _orientationModeFilter);
                }

                DrawCategoryAssetFilters(catalog);
            }
        }

        private void DrawCategoryAssetFilters(AssetCatalog catalog)
        {
            foreach (TagCategory category in catalog.Categories
                         .Where(category => category)
                         .OrderBy(category => category.DisplayName))
            {
                DrawCategoryAssetFilter(catalog, category);
            }
        }

        private void DrawCategoryAssetFilter(AssetCatalog catalog, TagCategory category)
        {
            List<SemanticTag> tags = catalog.Tags
                .Where(tag => tag && tag.Category == category)
                .OrderBy(tag => tag.DisplayName)
                .ToList();

            IReadOnlyList<SemanticTag> selectedTags = GetSelectedCategoryFilterTags(category);

            TagSelectionField.Draw(
                category.DisplayName,
                category,
                tags,
                selectedTags,
                newSelection => SetCategoryFilter(category, newSelection),
                forceMultiSelect: true,
                showNoneOption: false);
        }

        private IReadOnlyList<SemanticTag> GetSelectedCategoryFilterTags(TagCategory category)
        {
            if (!category)
                return Array.Empty<SemanticTag>();

            if (!_assetCategoryFilters.TryGetValue(category, out List<SemanticTag> selectedTags))
                return Array.Empty<SemanticTag>();

            return selectedTags
                .Where(tag => tag && tag.Category == category)
                .Distinct()
                .ToList();
        }

        private void DrawStaticPoolAddSection(IReadOnlyList<AssetDefinition> filteredAssets)
        {
            EditorGUILayout.LabelField("Add To Static Asset Pool", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStaticPoolTargetSelector();

                using (new EditorGUI.DisabledScope(!_targetStaticPool || !_selectedAsset))
                {
                    if (GUILayout.Button("Add Selected", GUILayout.Width(110f)))
                        AddSelectedAssetToTargetPool();
                }

                using (new EditorGUI.DisabledScope(!_targetStaticPool || filteredAssets.Count == 0))
                {
                    if (GUILayout.Button("Add Filtered", GUILayout.Width(100f)))
                        AddFilteredAssetsToTargetPool(filteredAssets);
                }
            }

            DrawStaticPoolMessage();
        }

        private void ShowStaticPoolMessage(string message, MessageType messageType)
        {
            _staticPoolMessage = message;
            _staticPoolMessageType = messageType;
            _staticPoolMessageUntil = EditorApplication.timeSinceStartup + 3.0;
        }

        private void DrawStaticPoolMessage()
        {
            if (string.IsNullOrWhiteSpace(_staticPoolMessage))
                return;

            if (EditorApplication.timeSinceStartup > _staticPoolMessageUntil)
                return;

            EditorGUILayout.Space(3f);
            EditorGUILayout.HelpBox(_staticPoolMessage, _staticPoolMessageType);
        }

        private void DrawStaticPoolTargetSelector()
        {
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            List<AssetPool> staticAssetPools = catalog.AssetPools
                .Where(pool => pool && pool.IsStatic)
                .OrderBy(pool => pool.name)
                .ToList();

            if (staticAssetPools.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Popup(0, new[] { "No static asset pools available" }, GUILayout.Width(220f));

                _targetStaticPool = null;
                return;
            }

            int selectedIndex = _targetStaticPool
                ? staticAssetPools.IndexOf(_targetStaticPool)
                : -1;

            if (selectedIndex < 0)
                selectedIndex = 0;

            string[] options = staticAssetPools
                .Select(pool => pool.name)
                .ToArray();

            EditorGUI.BeginChangeCheck();

            int newIndex = EditorGUILayout.Popup(
                selectedIndex,
                options,
                GUILayout.Width(220f));

            if (!EditorGUI.EndChangeCheck())
                return;

            _targetStaticPool = staticAssetPools[newIndex];
        }

        private void DrawAssetList(IReadOnlyList<AssetDefinition> assets)
        {
            DrawSectionHeader($"Assets ({assets.Count})", () =>
            {
                DrawAssetSortDropdown();

                using (new EditorGUI.DisabledScope(!_selectedAsset))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                        DeleteSelectedAsset();
                }

                using (new EditorGUI.DisabledScope(assets.Count == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                        ClearAssets();
                }
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(ListHeight)))
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

                if (assets.Count == 0)
                {
                    EditorGUILayout.HelpBox("No assets match the current filters.", MessageType.Info);
                }
                else
                {
                    foreach (AssetDefinition asset in assets)
                        DrawAssetListItem(asset);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawAssetSortDropdown()
        {
            AssetSortMode[] modes =
            {
                AssetSortMode.AlphabeticalAscending,
                AssetSortMode.AlphabeticalDescending,
                AssetSortMode.SizeDescending,
                AssetSortMode.SizeAscending,
                AssetSortMode.PlacementType,
                AssetSortMode.TagCountDescending,
                AssetSortMode.TagCountAscending
            };

            string[] labels =
            {
                "Alphabetical Ascending",
                "Alphabetical Descending",
                "Size Descending",
                "Size Ascending",
                "Placement Type",
                "Tag Count Descending",
                "Tag Count Ascending"
            };

            _assetSortMode = DrawSortDropdown(_assetSortMode, modes, labels);
        }

        private static T DrawSortDropdown<T>(T currentMode, T[] modes, string[] labels, float width = 180f)
        {
            int selectedIndex = Array.IndexOf(modes, currentMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            GUILayout.Label("Sort by", EditorStyles.label, GUILayout.Width(42f));

            selectedIndex = EditorGUILayout.Popup(
                selectedIndex,
                labels,
                GUILayout.Width(width));

            return modes[selectedIndex];
        }

        private void DrawAssetListItem(AssetDefinition asset)
        {
            bool selected = GetSelectedObject() == asset;
            GUIStyle style = selected ? EditorStyles.helpBox : GUIStyle.none;

            using (new EditorGUILayout.VerticalScope(style))
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 40f);

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                    SelectObject(asset);

                Rect titleRect = new(rowRect.x, rowRect.y, rowRect.width, 18f);
                Rect infoRect = new(rowRect.x, rowRect.y + 18f, rowRect.width, 18f);

                EditorGUI.LabelField(titleRect, asset.AssetName, EditorStyles.boldLabel);
                EditorGUI.LabelField(infoRect, GetAssetInfo(asset));
            }

            EditorGUILayout.Space(2f);
        }

    }
}
