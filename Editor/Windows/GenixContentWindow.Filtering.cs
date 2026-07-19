using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Extensions;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixContentWindow
    {
        private List<AssetDefinition> GetFilteredAssets(AssetCatalog catalog)
        {
            List<AssetDefinition> assets = catalog.Assets
                .Where(asset => asset)
                .Where(MatchesAssetSearch)
                .Where(MatchesAssetPlacementTypeFilter)
                .Where(MatchesAssetOrientationModeFilter)
                .Where(MatchesAssetCategoryFilters)
                .ToList();

            return SortAssets(catalog, assets);
        }

        private List<TagCategory> GetFilteredCategories(AssetCatalog catalog)
        {
            List<TagCategory> categories = catalog.Categories
                .Where(category => category)
                .Where(category => MatchesCategorySearch(catalog, category))
                .Where(MatchesCategoryModeFilter)
                .ToList();

            return SortCategories(catalog, categories);
        }

        private List<AssetPool> GetFilteredAssetPools(AssetCatalog catalog)
        {
            List<AssetPool> assetPools = catalog.AssetPools
                .Where(pool => pool)
                .Where(MatchesPoolSearch)
                .Where(MatchesPoolModeFilter)
                .Where(pool => MatchesPoolAssetStateFilter(catalog, pool))
                .ToList();

            return SortAssetPools(catalog, assetPools);
        }

        private List<AssetDefinition> SortAssets(AssetCatalog catalog, IEnumerable<AssetDefinition> assets)
        {
            return _assetSortMode switch
            {
                AssetSortMode.AlphabeticalDescending => assets
                    .OrderByDescending(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                AssetSortMode.SizeDescending => assets
                    .OrderByDescending(GetAssetBoundsVolume)
                    .ThenBy(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                AssetSortMode.SizeAscending => assets
                    .OrderBy(GetAssetBoundsVolume)
                    .ThenBy(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                AssetSortMode.PlacementType => assets
                    .OrderBy(asset => asset.PlacementType.ToString(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                AssetSortMode.TagCountDescending => assets
                    .OrderByDescending(asset => GetAssetSemanticTagCount(catalog, asset))
                    .ThenBy(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                AssetSortMode.TagCountAscending => assets
                    .OrderBy(asset => GetAssetSemanticTagCount(catalog, asset))
                    .ThenBy(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                _ => assets
                    .OrderBy(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private static float GetAssetBoundsVolume(AssetDefinition asset)
        {
            Vector3 size = asset.BoundsSize;
            return Mathf.Max(0.01f, size.x) * Mathf.Max(0.01f, size.y) * Mathf.Max(0.01f, size.z);
        }

        private static int GetAssetSemanticTagCount(AssetCatalog catalog, AssetDefinition asset)
        {
            int count = asset.SemanticTags.Count(tag => tag);

            if (!catalog)
                return count;

            foreach (TagCategory category in asset.AnyTagCategories.Where(category => category).Distinct())
                count += catalog.Tags.Count(tag => tag && tag.Category == category);

            return count;
        }

        private List<TagCategory> SortCategories(
            AssetCatalog catalog,
            IEnumerable<TagCategory> categories)
        {
            return _categorySortMode switch
            {
                CategorySortMode.AlphabeticalDescending => categories
                    .OrderByDescending(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                CategorySortMode.TagCountDescending => categories
                    .OrderByDescending(category => GetCategoryTagCount(catalog, category))
                    .ThenBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                CategorySortMode.TagCountAscending => categories
                    .OrderBy(category => GetCategoryTagCount(catalog, category))
                    .ThenBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                CategorySortMode.Mode => categories
                    .OrderBy(category => category.AllowMultipleTags ? 0 : 1)
                    .ThenBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                _ => categories
                    .OrderBy(category => category.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private List<SemanticTag> SortTags(IEnumerable<SemanticTag> tags)
        {
            return _tagSortMode switch
            {
                TagSortMode.AlphabeticalDescending => tags
                    .OrderByDescending(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                TagSortMode.CategoryDescending => tags
                    .OrderByDescending(GetTagCategoryName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                TagSortMode.AlphabeticalAscending => tags
                    .OrderBy(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                _ => tags
                    .OrderBy(GetTagCategoryName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(tag => tag.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private List<AssetPool> SortAssetPools(
            AssetCatalog catalog,
            IEnumerable<AssetPool> assetPools)
        {
            return _poolSortMode switch
            {
                PoolSortMode.AlphabeticalDescending => assetPools
                    .OrderByDescending(pool => pool.name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                PoolSortMode.AssetCountDescending => assetPools
                    .OrderByDescending(pool => GetPoolAssetCount(catalog, pool))
                    .ThenBy(pool => pool.name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                PoolSortMode.AssetCountAscending => assetPools
                    .OrderBy(pool => GetPoolAssetCount(catalog, pool))
                    .ThenBy(pool => pool.name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                PoolSortMode.Mode => assetPools
                    .OrderBy(pool => pool.Mode.ToString(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(pool => pool.name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                _ => assetPools
                    .OrderBy(pool => pool.name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private static int GetCategoryTagCount(AssetCatalog catalog, TagCategory category)
        {
            return catalog.Tags.Count(tag => tag && tag.Category == category);
        }

        private static string GetTagCategoryName(SemanticTag tag)
        {
            return tag.Category ? tag.Category.DisplayName : "Missing Category";
        }

        private static int GetPoolAssetCount(AssetCatalog catalog, AssetPool pool)
        {
            return pool.ResolveAssets(catalog).Count;
        }

        private bool MatchesAssetSearch(AssetDefinition asset)
        {
            if (string.IsNullOrWhiteSpace(_assetSearch))
                return true;

            string search = _assetSearch.Trim();

            return asset.AssetName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   asset.Prefab && asset.Prefab.name.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesAssetPlacementTypeFilter(AssetDefinition asset)
        {
            return !_filterByPlacementType || asset.PlacementType == _placementTypeFilter;
        }

        private bool MatchesAssetOrientationModeFilter(AssetDefinition asset)
        {
            return !_filterByOrientationMode || asset.OrientationMode == _orientationModeFilter;
        }

        private bool MatchesCategorySearch(AssetCatalog catalog, TagCategory category)
        {
            if (string.IsNullOrWhiteSpace(_categorySearch))
                return true;

            string search = _categorySearch.Trim();

            if (category.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;

            return catalog.Tags
                .Where(tag => tag && tag.Category == category)
                .Any(tag => tag.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        private bool MatchesCategoryModeFilter(TagCategory category)
        {
            return !_filterCategoriesByMode ||
                   category.AllowMultipleTags == _categoryModeFilterAllowsMultiple;
        }

        private bool MatchesPoolSearch(AssetPool pool)
        {
            if (string.IsNullOrWhiteSpace(_poolSearch))
                return true;

            string search = _poolSearch.Trim();

            return pool.name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   pool.Mode.ToDisplayName().Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesPoolModeFilter(AssetPool pool)
        {
            return !_filterAssetPoolsByMode || pool.Mode == _poolModeFilter;
        }

        private bool MatchesPoolAssetStateFilter(
            AssetCatalog catalog,
            AssetPool pool)
        {
            int assetCount = GetPoolAssetCount(catalog, pool);

            return _poolAssetStateFilter switch
            {
                PoolAssetStateFilter.HasAssets => assetCount > 0,
                PoolAssetStateFilter.Empty => assetCount == 0,
                _ => true
            };
        }

        private bool MatchesAssetCategoryFilters(AssetDefinition asset)
        {
            foreach (KeyValuePair<TagCategory, List<SemanticTag>> filter in _assetCategoryFilters)
            {
                List<SemanticTag> selectedTags = filter.Value
                    .Where(tag => tag && tag.Category == filter.Key)
                    .Distinct()
                    .ToList();

                if (selectedTags.Count == 0)
                    continue;

                bool matchesCategory = SemanticTagMatcher.MatchesFilterTags(asset, filter.Key, selectedTags);

                if (!matchesCategory)
                    return false;
            }

            return true;
        }

    }
}
