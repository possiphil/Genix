using System.Collections.Generic;
using System.Linq;
using Genix.Orientation;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    public sealed class AssetPool : ScriptableObject
    {
        [SerializeField] private AssetPoolMode mode = AssetPoolMode.Static;

        [SerializeField] private List<AssetDefinition> staticAssets = new();

        [SerializeField] private bool filterByPlacementType;
        [SerializeField] private PlacementType placementType = PlacementType.Floor;

        [SerializeField] private bool filterByOrientationMode;
        [SerializeField] private OrientationMode orientationMode = OrientationMode.None;

        [SerializeField] private List<TagCategoryFilter> categoryFilters = new();

        public AssetPoolMode Mode => mode;
        public IReadOnlyList<AssetDefinition> StaticAssets => staticAssets;

        public bool FilterByPlacementType => filterByPlacementType;
        public PlacementType PlacementType => placementType;

        public bool FilterByOrientationMode => filterByOrientationMode;
        public OrientationMode OrientationMode => orientationMode;

        public IReadOnlyList<TagCategoryFilter> CategoryFilters => categoryFilters;

        public bool IsStatic => mode == AssetPoolMode.Static;
        public bool IsDynamic => mode == AssetPoolMode.Dynamic;

        public bool HasValidStaticAssets => staticAssets.Any(asset => asset);

        public IReadOnlyList<AssetDefinition> ResolveAssets(AssetCatalog catalog)
        {
            if (!catalog)
                return new List<AssetDefinition>();

            return ResolveAssets(catalog.Assets);
        }

        public IReadOnlyList<AssetDefinition> ResolveAssets(IEnumerable<AssetDefinition> catalogAssets)
        {
            if (IsStatic)
                return GetValidStaticAssets();

            if (catalogAssets == null)
                return new List<AssetDefinition>();

            return catalogAssets
                .Where(MatchesAsset)
                .Distinct()
                .ToList();
        }

        public bool MatchesAsset(AssetDefinition asset)
        {
            if (!asset)
                return false;

            if (filterByPlacementType && asset.PlacementType != placementType)
                return false;

            if (filterByOrientationMode && asset.OrientationMode != orientationMode)
                return false;

            foreach (TagCategoryFilter categoryFilter in categoryFilters)
            {
                if (categoryFilter == null || !categoryFilter.IsActive)
                    continue;

                if (!categoryFilter.Matches(asset))
                    return false;
            }

            return true;
        }

        public void AddStaticAsset(AssetDefinition asset)
        {
            if (!asset || staticAssets.Contains(asset))
                return;

            staticAssets.Add(asset);
        }

        public void AddStaticAssets(IEnumerable<AssetDefinition> assets)
        {
            if (assets == null)
                return;

            foreach (AssetDefinition asset in assets)
                AddStaticAsset(asset);
        }

        public void RemoveStaticAsset(AssetDefinition asset)
        {
            staticAssets.Remove(asset);
        }

        public void RemoveMissingReferences()
        {
            staticAssets.RemoveAll(asset => !asset);

            foreach (TagCategoryFilter filter in categoryFilters)
                filter?.RemoveMissingTags();

            categoryFilters.RemoveAll(filter => filter == null || !filter.IsActive);
        }

        private IReadOnlyList<AssetDefinition> GetValidStaticAssets()
        {
            return staticAssets
                .Where(asset => asset)
                .Distinct()
                .ToList();
        }

        public void Initialize(string displayName, AssetPoolMode mode)
        {
            name = displayName;
            this.mode = mode;
        }

        public void RemoveTag(SemanticTag tag)
        {
            foreach (TagCategoryFilter filter in categoryFilters)
                filter?.RemoveTag(tag);
        }

        public void RemoveCategory(TagCategory category)
        {
            categoryFilters.RemoveAll(filter => filter != null && filter.Category == category);
        }
    }
}
