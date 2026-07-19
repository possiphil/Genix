using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Infrastructure;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Genix.Editor.Assets
{
    public static class AssetCatalogService
    {
        public static AssetCatalog GetOrCreate()
        {
            EnsureFolders();
            AssetCatalog catalog = AssetDatabase.LoadAssetAtPath<AssetCatalog>(ProjectContentPaths.AssetCatalog);

            if (catalog)
                return catalog;

            catalog = ScriptableObject.CreateInstance<AssetCatalog>();
            AssetDatabase.CreateAsset(catalog, ProjectContentPaths.AssetCatalog);
            Refresh();
            return catalog;
        }

        public static void Refresh()
        {
            EnsureFolders();
            AssetCatalog catalog = AssetDatabase.LoadAssetAtPath<AssetCatalog>(ProjectContentPaths.AssetCatalog);

            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<AssetCatalog>();
                AssetDatabase.CreateAsset(catalog, ProjectContentPaths.AssetCatalog);
            }

            catalog.SetAssets(AssetFileService.FindAssets<AssetDefinition>(ProjectContentPaths.AssetsRoot));
            catalog.SetTags(AssetFileService.FindAssets<SemanticTag>(ProjectContentPaths.AssetsRoot));
            catalog.SetCategories(AssetFileService.FindAssets<TagCategory>(ProjectContentPaths.AssetsRoot));
            catalog.SetAssetPools(AssetFileService.FindAssets<AssetPool>(ProjectContentPaths.AssetsRoot));
            catalog.RemoveMissingReferences();
            Save(catalog);
        }

        public static TagCategory CreateCategory(string displayName, bool allowMultipleTags = true)
        {
            string name = AssetFileService.CleanName(displayName, "New Category");
            TagCategory category = ScriptableObject.CreateInstance<TagCategory>();
            category.name = name;
            category.Initialize(allowMultipleTags);
            AssetDatabase.CreateAsset(category, AssetFileService.UniqueAssetPath(ProjectContentPaths.TagCategories, name));

            AssetCatalog catalog = GetOrCreate();
            catalog.AddCategory(category);
            Save(category, catalog);
            return category;
        }

        public static SemanticTag CreateTag(string displayName, TagCategory category)
        {
            if (!category)
            {
                Debug.LogWarning("Cannot create a semantic tag without a category.");
                return null;
            }

            string name = AssetFileService.CleanName(displayName, "New Tag");
            string path = AssetFileService.UniqueAssetPath(GetTagFolder(category), name);
            SemanticTag tag = ScriptableObject.CreateInstance<SemanticTag>();
            tag.name = System.IO.Path.GetFileNameWithoutExtension(path);
            tag.Initialize(category);
            AssetDatabase.CreateAsset(tag, path);

            AssetCatalog catalog = GetOrCreate();
            catalog.AddTag(tag);
            Save(tag, catalog);
            return tag;
        }

        public static bool TryFindTagInCategory(TagCategory category, string displayName, out SemanticTag existingTag)
        {
            existingTag = null;

            if (!category || string.IsNullOrWhiteSpace(displayName))
                return false;

            string name = AssetFileService.CleanName(displayName, "New Tag");
            existingTag = GetOrCreate().Tags.FirstOrDefault(tag =>
                tag && tag.Category == category &&
                string.Equals(tag.DisplayName, name, StringComparison.OrdinalIgnoreCase));
            return existingTag;
        }

        public static bool TryRenameTag(SemanticTag tag, string displayName, out string error)
        {
            if (!tag)
            {
                error = "Missing semantic tag.";
                return false;
            }

            bool renamed = AssetFileService.Rename(tag, displayName, "New Tag", out error);
            SaveAndRefresh();
            return renamed;
        }

        public static bool TrySetTagCategory(SemanticTag tag, TagCategory category, out string error)
        {
            error = null;

            if (!tag || !category)
            {
                error = !tag ? "Missing semantic tag." : "Tags require a category.";
                return false;
            }

            if (tag.Category == category)
                return true;

            tag.SetCategory(category);
            AssetFileService.SetDirty(tag);

            if (!AssetFileService.Move(tag, GetTagFolder(category), tag.DisplayName, out error))
                return false;

            SaveAndRefresh();
            return true;
        }

        public static AssetPool CreateAssetPool(string displayName, AssetPoolMode mode)
        {
            string name = AssetFileService.CleanName(displayName, "New Asset Pool");
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            pool.Initialize(name, mode);
            AssetDatabase.CreateAsset(pool, AssetFileService.UniqueAssetPath(ProjectContentPaths.AssetPools, name));

            AssetCatalog catalog = GetOrCreate();
            catalog.AddAssetPool(pool);
            Save(pool, catalog);
            return pool;
        }

        public static void RegisterAsset(AssetDefinition asset)
        {
            if (!asset)
                return;

            AssetCatalog catalog = GetOrCreate();
            catalog.AddAsset(asset);
            Save(catalog);
        }

        public static void Rename(Object asset, string displayName, string fallbackName)
        {
            if (!AssetFileService.Rename(asset, displayName, fallbackName, out string error))
                Debug.LogWarning(error);

            SaveAndRefresh();
        }

        public static void DeleteTag(SemanticTag tag)
        {
            if (!tag)
                return;

            RemoveTagReferences(GetOrCreate(), tag);
            AssetFileService.Delete(tag);
            SaveAndRefresh();
        }

        public static void DeleteCategory(TagCategory category)
        {
            if (!category)
                return;

            AssetCatalog catalog = GetOrCreate();
            List<SemanticTag> categoryTags = catalog.Tags.Where(tag => tag && tag.Category == category).ToList();

            foreach (SemanticTag tag in categoryTags)
                RemoveTagReferences(catalog, tag);

            foreach (AssetPool pool in catalog.AssetPools)
            {
                if (!pool)
                    continue;

                pool.RemoveCategory(category);
                AssetFileService.SetDirty(pool);
            }

            foreach (SemanticTag tag in categoryTags)
                AssetFileService.Delete(tag);

            AssetFileService.Delete(category);
            SaveAndRefresh();
        }

        public static void DeleteAssetPool(AssetPool pool)
        {
            AssetFileService.Delete(pool);
            SaveAndRefresh();
        }

        public static void DeleteAsset(AssetDefinition asset)
        {
            if (!asset)
                return;

            AssetCatalog catalog = GetOrCreate();

            foreach (AssetPool pool in catalog.AssetPools)
            {
                if (!pool)
                    continue;

                pool.RemoveStaticAsset(asset);
                AssetFileService.SetDirty(pool);
            }

            AssetFileService.Delete(asset);
            SaveAndRefresh();
        }

        public static void ClearAssets()
        {
            foreach (AssetDefinition asset in GetOrCreate().Assets.Where(asset => asset).ToList())
                DeleteAsset(asset);
        }

        public static void ClearTags()
        {
            foreach (SemanticTag tag in GetOrCreate().Tags.Where(tag => tag).ToList())
                DeleteTag(tag);
        }

        public static void ClearTagsInCategory(TagCategory category)
        {
            if (!category)
                return;

            foreach (SemanticTag tag in GetOrCreate().Tags.Where(tag => tag && tag.Category == category).ToList())
                DeleteTag(tag);
        }

        public static void ClearCategories()
        {
            foreach (TagCategory category in GetOrCreate().Categories.Where(category => category).ToList())
                DeleteCategory(category);
        }

        public static void ClearAssetPools()
        {
            foreach (AssetPool pool in GetOrCreate().AssetPools.Where(pool => pool).ToList())
                DeleteAssetPool(pool);
        }

        public static void Clear()
        {
            ClearAssets();
            ClearCategories();
            ClearAssetPools();
            Refresh();
        }

        private static void RemoveTagReferences(AssetCatalog catalog, SemanticTag tag)
        {
            foreach (AssetDefinition asset in catalog.Assets)
            {
                if (!asset)
                    continue;

                asset.RemoveTag(tag);
                AssetFileService.SetDirty(asset);
            }

            foreach (AssetPool pool in catalog.AssetPools)
            {
                if (!pool)
                    continue;

                pool.RemoveTag(tag);
                AssetFileService.SetDirty(pool);
            }

            catalog.RemoveMissingReferences();
            Save(catalog);
        }

        private static string GetTagFolder(TagCategory category)
        {
            string categoryName = category ? category.DisplayName : "Uncategorized";
            return $"{ProjectContentPaths.TagValues}/{AssetFileService.SanitizeName(categoryName)}";
        }

        private static void EnsureFolders()
        {
            AssetFileService.EnsureFolders(ProjectContentPaths.AssetDefinitions, ProjectContentPaths.TagCategories, ProjectContentPaths.TagValues, ProjectContentPaths.AssetPools);
        }

        private static void Save(params Object[] assets)
        {
            foreach (Object asset in assets)
                AssetFileService.SetDirty(asset);

            AssetDatabase.SaveAssets();
        }

        private static void SaveAndRefresh()
        {
            AssetDatabase.SaveAssets();
            Refresh();
        }
    }
}
