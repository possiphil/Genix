using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.Utilities;
using Genix.Extensions;
using Genix.Orientation;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixContentWindow
    {
        private void CreateCategory()
        {
            EditorApplication.delayCall += () =>
            {
                if (!this)
                    return;

                CreateCategoryDialog.Open((displayName, allowMultipleTags) =>
                {
                    TagCategory category = AssetCatalogService.CreateCategory(displayName, allowMultipleTags);
                    AssetCatalogService.Refresh();

                    SelectObject(category);
                    Repaint();
                });
            };
        }

        private void CreateTag()
        {
            TagCategory defaultCategory = GetTargetCategoryForNewTag();

            EditorApplication.delayCall += () =>
            {
                if (!this)
                    return;

                CreateTagDialog.Open(defaultCategory, (displayName, category) =>
                {
                    SemanticTag tag = AssetCatalogService.CreateTag(displayName, category);
                    AssetCatalogService.Refresh();

                    SelectObject(tag);
                    Repaint();
                });
            };
        }

        private void CreatePool(AssetPoolMode mode)
        {
            EditorApplication.delayCall += () =>
            {
                if (!this)
                    return;

                CreateAssetPoolDialog.Open(mode, (displayName, poolMode) =>
                {
                    AssetPool pool = AssetCatalogService.CreateAssetPool(displayName, poolMode);
                    AssetCatalogService.Refresh();

                    SelectObject(pool);

                    if (pool.IsStatic)
                        _targetStaticPool = pool;

                    Repaint();
                });
            };
        }

        private void DeleteSelectedAsset()
        {
            if (GetSelectedObject() is not AssetDefinition asset)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Asset",
                $"Delete asset '{asset.name}'?\n\nThis cannot be undone.",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            ClearSelection();
            AssetCatalogService.DeleteAsset(asset);
        }

        private void DeleteTag(SemanticTag tag)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Semantic Tag",
                $"Delete tag '{tag.DisplayName}'?\n\nThis will remove it from all assets and asset pools.",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            _selectedSemanticTag = null;
            DestroySelectedObjectEditor();

            AssetCatalogService.DeleteTag(tag);
        }

        private void DeleteCategory(TagCategory category)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Tag Category",
                $"Delete category '{category.DisplayName}'?\n\nThis will also delete all tags in this category and remove them from assets and asset pools.",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            ClearSelection();
            AssetCatalogService.DeleteCategory(category);
        }

        private void DeleteSelectedPool()
        {
            if (GetSelectedObject() is not AssetPool pool)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Asset Pool",
                $"Delete pool '{pool.name}'?\n\nThis cannot be undone.",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            if (_targetStaticPool == pool)
                _targetStaticPool = null;

            ClearSelection();
            AssetCatalogService.DeleteAssetPool(pool);
        }

        private void AddSelectedAssetToTargetPool()
        {
            if (!_targetStaticPool)
            {
                ShowStaticPoolMessage("No target static pool selected.", MessageType.Warning);
                return;
            }

            if (!_selectedAsset)
            {
                ShowStaticPoolMessage("No asset selected.", MessageType.Warning);
                return;
            }

            if (_targetStaticPool.StaticAssets.Contains(_selectedAsset))
            {
                ShowStaticPoolMessage(
                    $"'{_selectedAsset.AssetName}' is already in '{_targetStaticPool.name}'.",
                    MessageType.Warning);

                return;
            }

            _targetStaticPool.AddStaticAsset(_selectedAsset);
            EditorUtility.SetDirty(_targetStaticPool);
            AssetDatabase.SaveAssets();

            ShowStaticPoolMessage(
                $"Added '{_selectedAsset.AssetName}' to '{_targetStaticPool.name}'.",
                MessageType.Info);
        }

        private void AddFilteredAssetsToTargetPool(IReadOnlyList<AssetDefinition> filteredAssets)
        {
            if (!_targetStaticPool)
            {
                ShowStaticPoolMessage("No target static pool selected.", MessageType.Warning);
                return;
            }

            List<AssetDefinition> candidates = filteredAssets
                .Where(asset => asset)
                .Distinct()
                .ToList();

            if (candidates.Count == 0)
            {
                ShowStaticPoolMessage("No filtered assets to add.", MessageType.Warning);
                return;
            }

            List<AssetDefinition> newAssets = candidates
                .Where(asset => !_targetStaticPool.StaticAssets.Contains(asset))
                .ToList();

            int duplicateCount = candidates.Count - newAssets.Count;

            if (newAssets.Count == 0)
            {
                ShowStaticPoolMessage(
                    $"All {candidates.Count} filtered assets are already in '{_targetStaticPool.name}'.",
                    MessageType.Warning);

                return;
            }

            _targetStaticPool.AddStaticAssets(newAssets);
            EditorUtility.SetDirty(_targetStaticPool);
            AssetDatabase.SaveAssets();

            if (duplicateCount > 0)
            {
                ShowStaticPoolMessage(
                    $"Added {newAssets.Count} assets to '{_targetStaticPool.name}'. {duplicateCount} were already included.",
                    MessageType.Warning);

                return;
            }

            ShowStaticPoolMessage(
                $"Added {newAssets.Count} assets to '{_targetStaticPool.name}'.",
                MessageType.Info);
        }

        private TagCategory GetTargetCategoryForNewTag()
        {
            if (_selectedTagCategory)
                return _selectedTagCategory;

            if (_selectedSemanticTag && _selectedSemanticTag.Category)
                return _selectedSemanticTag.Category;

            AssetCatalog catalog = AssetCatalogService.GetOrCreate();
            return catalog.Categories.FirstOrDefault(category => category);
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
            {
                _assetCategoryFilters.Remove(category);
            }
            else
            {
                _assetCategoryFilters[category] = validTags;
            }

            Repaint();
        }

        private void ClearAssetFilters()
        {
            _assetSearch = string.Empty;
            _filterByPlacementType = false;
            _placementTypeFilter = PlacementType.Floor;
            _filterByOrientationMode = false;
            _orientationModeFilter = OrientationMode.None;
            _assetCategoryFilters.Clear();
        }

        private Object GetSelectedObject()
        {
            return _tab switch
            {
                ContentTab.Assets => _selectedAsset,
                ContentTab.Tags => _selectedSemanticTag ? _selectedSemanticTag : _selectedTagCategory,
                ContentTab.Locations => null,
                ContentTab.AssetPools => _selectedPool,
                _ => null
            };
        }

        private void SelectObject(Object selectedObject)
        {
            Object currentSelectedObject = GetSelectedObject();

            if (currentSelectedObject == selectedObject)
                return;

            SetSelectedObjectForCurrentTab(selectedObject);
            DestroySelectedObjectEditor();

            GUI.FocusControl(null);

            Repaint();
        }

        private void SetSelectedObjectForCurrentTab(Object selectedObject)
        {
            switch (_tab)
            {
                case ContentTab.Assets:
                    _selectedAsset = selectedObject as AssetDefinition;
                    break;

                case ContentTab.Tags:
                    if (selectedObject is TagCategory category)
                    {
                        _selectedTagCategory = category;

                        if (!_selectedSemanticTag || _selectedSemanticTag.Category != category)
                            _selectedSemanticTag = GetFirstTagInCategory(category);
                    }
                    else if (selectedObject is SemanticTag tag)
                    {
                        _selectedSemanticTag = tag;
                        _selectedTagCategory = tag.Category;
                    }
                    else
                    {
                        _selectedTagCategory = null;
                        _selectedSemanticTag = null;
                    }

                    break;

                case ContentTab.Locations:
                    break;

                case ContentTab.AssetPools:
                    _selectedPool = selectedObject as AssetPool;
                    break;
            }
        }

        private SemanticTag GetFirstTagInCategory(TagCategory category)
        {
            if (!category)
                return null;

            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            return catalog.Tags
                .Where(tag => tag && tag.Category == category)
                .OrderBy(tag => tag.DisplayName)
                .FirstOrDefault();
        }

        private void ClearSelection()
        {
            SetSelectedObjectForCurrentTab(null);
            DestroySelectedObjectEditor();
            Repaint();
        }

        private void DestroySelectedObjectEditor()
        {
            if (_selectedObjectEditor)
                DestroyImmediate(_selectedObjectEditor);

            if (_selectedCategoryEditor)
                DestroyImmediate(_selectedCategoryEditor);

            if (_selectedSemanticTagEditor)
                DestroyImmediate(_selectedSemanticTagEditor);

            _selectedObjectEditor = null;
            _selectedObjectEditorTarget = null;
            _selectedCategoryEditor = null;
            _selectedSemanticTagEditor = null;
        }

        private static string GetAssetInfo(AssetDefinition asset)
        {
            return $"Placement: {asset.PlacementType.ToDisplayName()}    Tags: {GetAssetTagsLabel(asset)}";
        }

        private static string GetAssetTagsLabel(AssetDefinition asset)
        {
            List<string> labels = asset.SemanticTags
                .Where(tag => tag)
                .Select(GetTagLabel)
                .ToList();

            labels.AddRange(asset.AnyTagCategories
                .Where(category => category)
                .Select(category => $"{category.DisplayName}: Any"));

            if (labels.Count == 0)
                return "None";

            return string.Join(", ", labels);
        }

        private static string GetTagLabel(SemanticTag tag)
        {
            if (!tag)
                return "Missing Tag";

            string category = tag.Category
                ? tag.Category.DisplayName
                : "Missing Category";

            return $"{category}: {tag.DisplayName}";
        }

        private TagCategory GetSelectedTagCategory()
        {
            return _selectedTagCategory;
        }

        private static string GetTagListLabel(SemanticTag tag, bool showCategoryPrefix)
        {
            if (!tag)
                return "Missing Tag";

            return showCategoryPrefix
                ? GetTagLabel(tag)
                : tag.DisplayName;
        }

        private void ClearAssets()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Genix Assets",
                "Delete all asset definitions?\n\nThis will also remove them from static asset pools.",
                "Clear",
                "Cancel");

            if (!confirmed)
                return;

            _selectedAsset = null;
            DestroySelectedObjectEditor();

            AssetCatalogService.ClearAssets();
            Repaint();
        }

        private void ClearTags(TagCategory category)
        {
            bool clearCategoryOnly = category;

            string title = clearCategoryOnly
                ? "Clear Tags In Category"
                : "Clear Tags";

            string message = clearCategoryOnly
                ? $"Delete all tags in category '{category.DisplayName}'?\n\nThis will remove them from all assets and asset pools."
                : "Delete all semantic tags?\n\nThis will remove them from all assets and asset pools.";

            bool confirmed = EditorUtility.DisplayDialog(
                title,
                message,
                "Clear",
                "Cancel");

            if (!confirmed)
                return;

            _selectedSemanticTag = null;
            DestroySelectedObjectEditor();

            if (clearCategoryOnly)
                AssetCatalogService.ClearTagsInCategory(category);
            else
                AssetCatalogService.ClearTags();

            Repaint();
        }

        private void ClearCategories()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Categories",
                "Delete all categories and all tags?\n\nTags require categories, so all semantic tags will also be deleted.",
                "Clear",
                "Cancel");

            if (!confirmed)
                return;

            _selectedTagCategory = null;
            _selectedSemanticTag = null;
            DestroySelectedObjectEditor();

            AssetCatalogService.ClearCategories();
            Repaint();
        }

        private void ClearAssetPools()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Asset Pools",
                "Delete all asset pools?",
                "Clear",
                "Cancel");

            if (!confirmed)
                return;

            _selectedPool = null;
            _targetStaticPool = null;
            DestroySelectedObjectEditor();

            AssetCatalogService.ClearAssetPools();
            Repaint();
        }

        private void ClearCatalog()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Asset Catalog",
                "Delete all assets, tags, categories, and asset pools?\n\nThis cannot be undone.",
                "Clear",
                "Cancel");

            if (!confirmed)
                return;

            _selectedAsset = null;
            _selectedTagCategory = null;
            _selectedSemanticTag = null;
            _selectedPool = null;
            _targetStaticPool = null;

            DestroySelectedObjectEditor();

            AssetCatalogService.Clear();
            Repaint();
        }

    }
}
