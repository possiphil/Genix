using System.Collections.Generic;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    public sealed class AssetCatalog : ScriptableObject
    {
        [SerializeField] private List<AssetDefinition> assets = new();
        [SerializeField] private List<SemanticTag> tags = new();
        [SerializeField] private List<TagCategory> categories = new();
        [SerializeField] private List<AssetPool> assetPools = new();

        public IReadOnlyList<AssetDefinition> Assets => assets;
        public IReadOnlyList<SemanticTag> Tags => tags;
        public IReadOnlyList<TagCategory> Categories => categories;
        public IReadOnlyList<AssetPool> AssetPools => assetPools;

        public void SetAssets(IEnumerable<AssetDefinition> assets)
        {
            ReplaceList(this.assets, assets);
        }

        public void SetTags(IEnumerable<SemanticTag> tags)
        {
            ReplaceList(this.tags, tags);
        }

        public void SetCategories(IEnumerable<TagCategory> categories)
        {
            ReplaceList(this.categories, categories);
        }

        public void SetAssetPools(IEnumerable<AssetPool> pools)
        {
            ReplaceList(assetPools, pools);
        }

        public void AddAsset(AssetDefinition asset)
        {
            AddUnique(assets, asset);
        }

        public void AddTag(SemanticTag tag)
        {
            AddUnique(tags, tag);
        }

        public void AddCategory(TagCategory category)
        {
            AddUnique(categories, category);
        }

        public void AddAssetPool(AssetPool pool)
        {
            AddUnique(assetPools, pool);
        }

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
