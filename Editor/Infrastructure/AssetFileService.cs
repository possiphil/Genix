using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Infrastructure
{
    internal static class AssetFileService
    {
        public static string CleanName(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        public static string SanitizeName(string value, string fallback = "New Asset")
        {
            string result = CleanName(value, fallback);

            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
                result = result.Replace(invalidCharacter.ToString(), string.Empty);

            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        public static string UniqueAssetPath(string folderPath, string assetName)
        {
            EnsureFolder(folderPath);
            string path = $"{folderPath}/{SanitizeName(assetName)}.asset";
            return AssetDatabase.GenerateUniqueAssetPath(path);
        }

        public static void EnsureFolders(params string[] folderPaths)
        {
            foreach (string folderPath in folderPaths)
                EnsureFolder(folderPath);
        }

        public static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        public static List<T> FindAssets<T>(string rootPath)
            where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { rootPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset)
                .OrderBy(asset => asset.name)
                .ToList();
        }

        public static void Delete(Object asset)
        {
            string path = asset ? AssetDatabase.GetAssetPath(asset) : string.Empty;

            if (!string.IsNullOrWhiteSpace(path))
                AssetDatabase.DeleteAsset(path);
        }

        public static bool Move(Object asset, string targetFolder, string displayName, out string error)
        {
            error = null;
            string currentPath = asset ? AssetDatabase.GetAssetPath(asset) : string.Empty;

            if (string.IsNullOrWhiteSpace(currentPath))
                return false;

            EnsureFolder(targetFolder);
            string targetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{targetFolder}/{SanitizeName(displayName)}.asset");

            if (currentPath == targetPath)
                return true;

            error = AssetDatabase.MoveAsset(currentPath, targetPath);

            if (!string.IsNullOrWhiteSpace(error))
                return false;

            asset.name = Path.GetFileNameWithoutExtension(targetPath);
            SetDirty(asset);
            return true;
        }

        public static bool Rename(Object asset, string displayName, string fallback, out string error)
        {
            error = null;
            string currentPath = asset ? AssetDatabase.GetAssetPath(asset) : string.Empty;

            if (string.IsNullOrWhiteSpace(currentPath))
                return false;

            string cleanName = SanitizeName(displayName, fallback);

            if (asset.name == cleanName)
                return true;

            string folderPath = Path.GetDirectoryName(currentPath)?.Replace("\\", "/");
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{cleanName}.asset");
            string uniqueName = Path.GetFileNameWithoutExtension(targetPath);
            error = AssetDatabase.RenameAsset(currentPath, uniqueName);

            if (!string.IsNullOrWhiteSpace(error))
                return false;

            asset.name = uniqueName;
            SetDirty(asset);
            return true;
        }

        public static void SetDirty(Object asset)
        {
            if (asset)
                EditorUtility.SetDirty(asset);
        }
    }
}
