using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Assets;
using Genix.Extensions;
using Genix.Layouts;
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

                TagCategory category = AssetCatalogService.CreateCategory(
                    "New Category",
                    allowMultipleTags: true,
                    TagCategoryUsage.Asset);
                AssetCatalogService.Refresh();

                SelectCreatedObject(category);
            };
        }

        private void CreateTag()
        {
            TagCategory defaultCategory = GetTargetCategoryForNewTag();

            EditorApplication.delayCall += () =>
            {
                if (!this || !defaultCategory)
                    return;

                SemanticTag tag = AssetCatalogService.CreateTag("New Tag", defaultCategory);
                AssetCatalogService.Refresh();

                SelectCreatedObject(tag);
            };
        }

        private void CreatePool(AssetPoolMode mode)
        {
            EditorApplication.delayCall += () =>
            {
                if (!this)
                    return;

                AssetPool pool = AssetCatalogService.CreateAssetPool("New Asset Pool", mode);
                AssetCatalogService.Refresh();

                SelectCreatedObject(pool);
            };
        }

        private void SelectCreatedObject(Object createdObject)
        {
            if (!createdObject)
                return;

            SelectObject(createdObject);
            _focusCreatedObjectName = true;
            Repaint();
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

            ClearSelection();
            AssetCatalogService.DeleteAssetPool(pool);
        }

        private TagCategory GetTargetCategoryForNewTag()
        {
            if (_selectedTagCategory)
                return _selectedTagCategory;

            if (_selectedSemanticTag && _selectedSemanticTag.Category)
                return _selectedSemanticTag.Category;

            return null;
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
                ContentTab.Layouts => _selectedLayout,
                ContentTab.SceneSetup => _selectedSceneSetupObject,
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

                case ContentTab.Layouts:
                    _selectedLayout = selectedObject as SavedLayout;
                    break;

                case ContentTab.SceneSetup:
                    _selectedSceneSetupObject = selectedObject;
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
                "Delete All Asset Definitions",
                "Delete all asset definitions?\n\nThis will also remove them from static asset pools.",
                "Delete All",
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
                "Delete All Asset Pools",
                "Delete every asset pool? This cannot be undone.",
                "Delete All",
                "Cancel");

            if (!confirmed)
                return;

            _selectedPool = null;
            DestroySelectedObjectEditor();

            AssetCatalogService.ClearAssetPools();
            Repaint();
        }

        private void ClearCatalog()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete All Genix Content",
                "Delete all assets, tags, categories, and asset pools?\n\nThis cannot be undone.",
                "Delete All",
                "Cancel");

            if (!confirmed)
                return;

            _selectedAsset = null;
            _selectedTagCategory = null;
            _selectedSemanticTag = null;
            _selectedPool = null;
            _selectedLayout = null;

            DestroySelectedObjectEditor();

            AssetCatalogService.Clear();
            Repaint();
        }

    }
}
