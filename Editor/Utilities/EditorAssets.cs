using System;
using System.Collections.Generic;
using UnityEditor;

namespace Genix.Editor.Utilities
{
    public static class EditorAssets
    {
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

        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

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

        public static string[] CreateAssetOptions<T>(IReadOnlyList<T> assets) where T : UnityEngine.Object
        {
            string[] options = new string[assets.Count];

            for (int i = 0; i < assets.Count; i++)
                options[i] = assets[i].name;

            return options;
        }

    }
}
