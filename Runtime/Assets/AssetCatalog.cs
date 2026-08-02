using System.Collections.Generic;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Stores the asset definitions, semantic metadata, and pools available to Genix.</summary>
    public sealed class AssetCatalog : ScriptableObject
    {
        [SerializeField] private List<AssetDefinition> assets = new();
        [SerializeField] private List<SemanticTag> tags = new();
        [SerializeField] private List<TagCategory> categories = new();
        [SerializeField] private List<AssetPool> assetPools = new();

        /// <summary>Gets assets.</summary>
        public IReadOnlyList<AssetDefinition> Assets => assets;
        /// <summary>Gets tags.</summary>
        public IReadOnlyList<SemanticTag> Tags => tags;
        /// <summary>Gets categories.</summary>
        public IReadOnlyList<TagCategory> Categories => categories;
        /// <summary>Gets asset pools.</summary>
        public IReadOnlyList<AssetPool> AssetPools => assetPools;

        /// <summary>Sets assets.</summary>
        public void SetAssets(IEnumerable<AssetDefinition> assets)
        {
            ReplaceList(this.assets, assets);
        }

        /// <summary>Sets tags.</summary>
        public void SetTags(IEnumerable<SemanticTag> tags)
        {
            ReplaceList(this.tags, tags);
        }

        /// <summary>Sets categories.</summary>
        public void SetCategories(IEnumerable<TagCategory> categories)
        {
            ReplaceList(this.categories, categories);
        }

        /// <summary>Sets asset pools.</summary>
        public void SetAssetPools(IEnumerable<AssetPool> pools)
        {
            ReplaceList(assetPools, pools);
        }

        /// <summary>Adds asset.</summary>
        public void AddAsset(AssetDefinition asset)
        {
            AddUnique(assets, asset);
        }

        /// <summary>Adds tag.</summary>
        public void AddTag(SemanticTag tag)
        {
            AddUnique(tags, tag);
        }

        /// <summary>Adds category.</summary>
        public void AddCategory(TagCategory category)
        {
            AddUnique(categories, category);
        }

        /// <summary>Adds asset pool.</summary>
        public void AddAssetPool(AssetPool pool)
        {
            AddUnique(assetPools, pool);
        }

        /// <summary>Removes missing references.</summary>
        public void RemoveMissingReferences()
        {
            assets.RemoveAll(asset => !asset);
            tags.RemoveAll(tag => !tag);
            categories.RemoveAll(category => !category);
            assetPools.RemoveAll(pool => !pool);

            foreach (AssetDefinition asset in assets)
                asset?.RemoveMissingTags();

            foreach (AssetPool pool in assetPools)
                pool?.RemoveMissingReferences();
        }

        private static void ReplaceList<T>(List<T> target, IEnumerable<T> source)
            where T : Object
        {
            target.Clear();

            if (source == null)
                return;

            foreach (T item in source)
                AddUnique(target, item);
        }

        private static void AddUnique<T>(List<T> target, T item)
            where T : Object
        {
            if (!item || target.Contains(item))
                return;

            target.Add(item);
        }
    }
}
