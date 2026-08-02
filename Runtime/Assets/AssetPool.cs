using System.Collections.Generic;
using System.Linq;
using Genix.Orientation;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    /// <summary>
    /// Supplies asset definitions either from an explicit static list or from dynamic catalog filters.
    /// </summary>
    public sealed class AssetPool : ScriptableObject
    {
        [SerializeField] private AssetPoolMode mode = AssetPoolMode.Static;

        [SerializeField] private List<AssetDefinition> staticAssets = new();

        [SerializeField] private bool filterByPlacementType;
        [SerializeField] private PlacementType placementType = PlacementType.Floor;

        [SerializeField] private bool filterByOrientationMode;
        [SerializeField] private OrientationMode orientationMode = OrientationMode.None;

        [SerializeField] private List<TagCategoryFilter> categoryFilters = new();

        /// <summary>Gets mode.</summary>
        public AssetPoolMode Mode => mode;
        /// <summary>Gets static assets.</summary>
        public IReadOnlyList<AssetDefinition> StaticAssets => staticAssets;

        /// <summary>Indicates whether filter by placement type.</summary>
        public bool FilterByPlacementType => filterByPlacementType;
        /// <summary>Gets placement type.</summary>
        public PlacementType PlacementType => placementType;

        /// <summary>Indicates whether filter by orientation mode.</summary>
        public bool FilterByOrientationMode => filterByOrientationMode;
        /// <summary>Gets orientation mode.</summary>
        public OrientationMode OrientationMode => orientationMode;

        /// <summary>Gets category filters.</summary>
        public IReadOnlyList<TagCategoryFilter> CategoryFilters => categoryFilters;

        /// <summary>Indicates whether static.</summary>
        public bool IsStatic => mode == AssetPoolMode.Static;
        /// <summary>Indicates whether dynamic.</summary>
        public bool IsDynamic => mode == AssetPoolMode.Dynamic;

        /// <summary>Indicates whether valid static assets.</summary>
        public bool HasValidStaticAssets => staticAssets.Any(asset => asset);

        /// <summary>Resolves the effective, distinct asset set against a catalog.</summary>
        public IReadOnlyList<AssetDefinition> ResolveAssets(AssetCatalog catalog)
        {
            if (!catalog)
                return new List<AssetDefinition>();

            return ResolveAssets(catalog.Assets);
        }

        /// <summary>Resolves the effective, distinct asset set against an arbitrary catalog sequence.</summary>
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

        /// <summary>Determines whether an asset satisfies every active dynamic-pool filter.</summary>
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

        /// <summary>Adds static asset.</summary>
        public void AddStaticAsset(AssetDefinition asset)
        {
            if (!asset || staticAssets.Contains(asset))
                return;

            staticAssets.Add(asset);
        }

        /// <summary>Adds static assets.</summary>
        public void AddStaticAssets(IEnumerable<AssetDefinition> assets)
        {
            if (assets == null)
                return;

            foreach (AssetDefinition asset in assets)
                AddStaticAsset(asset);
        }

        /// <summary>Removes static asset.</summary>
        public void RemoveStaticAsset(AssetDefinition asset)
        {
            staticAssets.Remove(asset);
        }

        /// <summary>Removes missing references.</summary>
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

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(string displayName, AssetPoolMode mode)
        {
            name = displayName;
            this.mode = mode;
        }

        /// <summary>Removes tag.</summary>
        public void RemoveTag(SemanticTag tag)
        {
            foreach (TagCategoryFilter filter in categoryFilters)
                filter?.RemoveTag(tag);
        }

        /// <summary>Removes category.</summary>
        public void RemoveCategory(TagCategory category)
        {
            categoryFilters.RemoveAll(filter => filter != null && filter.Category == category);
        }
    }
}
