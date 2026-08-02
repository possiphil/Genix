using System;
using System.Collections.Generic;
using UnityEditor;

namespace Genix.Editor.Utilities
{
    /// <summary>Provides deterministic project-asset loading and selector helpers for editor workflows.</summary>
    public static class EditorAssets
    {
        /// <summary>Loads assets of the requested type from a project folder.</summary>
        /// <typeparam name="T">Unity asset type to load.</typeparam>
        /// <param name="folderPath">Project-relative folder path.</param>
        /// <param name="sortComparison">Optional ordering applied before returning the assets.</param>
        /// <returns>Matching assets, or an empty array when the folder does not exist.</returns>
        public static T[] LoadAssetsFromFolder<T>(string folderPath, Comparison<T> sortComparison = null) where T : UnityEngine.Object
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
                return Array.Empty<T>();

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });
            List<T> assets = new();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);

                if (asset)
                    assets.Add(asset);
            }

            if (sortComparison != null)
                assets.Sort(sortComparison);

            return assets.ToArray();
        }

        /// <summary>Loads an asset of the requested type at the project-relative path.</summary>
        /// <typeparam name="T">Unity asset type to load.</typeparam>
        /// <param name="assetPath">Project-relative asset path.</param>
        /// <returns>The loaded asset, or <see langword="null"/> when no matching asset exists.</returns>
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

        /// <summary>Determines whether the collection contains the supplied Unity asset.</summary>
        /// <typeparam name="T">Unity object type contained by the collection.</typeparam>
        /// <param name="assets">Assets to search.</param>
        /// <param name="selectedAsset">Asset reference to locate.</param>
        /// <returns><see langword="true"/> when the same Unity object reference is present.</returns>
        public static bool ContainsAsset<T>(IReadOnlyList<T> assets, T selectedAsset) where T : UnityEngine.Object
        {
            if (!selectedAsset)
                return false;

            foreach (T asset in assets)
            {
                if (asset == selectedAsset)
                    return true;
            }

            return false;
        }

        /// <summary>Returns the selector index of the supplied asset.</summary>
        /// <typeparam name="T">Unity object type contained by the collection.</typeparam>
        /// <param name="assets">Assets displayed by the selector.</param>
        /// <param name="selectedAsset">Currently selected asset.</param>
        /// <returns>The zero-based asset index, or zero when the selection is missing.</returns>
        public static int GetAssetDropdownIndex<T>(IReadOnlyList<T> assets, T selectedAsset) where T : UnityEngine.Object
        {
            if (!selectedAsset)
                return 0;

            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] == selectedAsset)
                    return i;
            }

            return 0;
        }

        /// <summary>Creates selector labels from Unity asset names.</summary>
        /// <typeparam name="T">Unity object type contained by the collection.</typeparam>
        /// <param name="assets">Assets to describe.</param>
        /// <returns>One display label per asset in the same order.</returns>
        public static string[] CreateAssetOptions<T>(IReadOnlyList<T> assets) where T : UnityEngine.Object
        {
            string[] options = new string[assets.Count];

            for (int i = 0; i < assets.Count; i++)
                options[i] = assets[i].name;

            return options;
        }

    }
}
