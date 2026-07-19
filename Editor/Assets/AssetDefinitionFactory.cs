using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Infrastructure;
using Genix.Geometry;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Genix.Editor.Assets
{
    public static class AssetDefinitionFactory
    {
        private const string DefaultAssetName = "New Asset";
        private static readonly Vector3 DefaultBoundsSize = Vector3.one;

        public static AssetDefinition CreateAssetFromPrefab(GameObject prefab)
        {
            if (!IsPrefabAsset(prefab))
            {
                Debug.LogWarning("Selected object is not a prefab asset.");
                return null;
            }

            AssetCatalogService.GetOrCreate();

            bool hasBounds = TryGetPrefabBounds(prefab, out Vector3 generatedBoundsSize, out Vector3 generatedBoundsCenterOffset);
            Vector3 boundsSize = hasBounds ? generatedBoundsSize : DefaultBoundsSize;
            Vector3 boundsCenterOffset = hasBounds ? generatedBoundsCenterOffset : Vector3.zero;

            string assetName = GetCleanAssetName(prefab.name);
            string path = AssetFileService.UniqueAssetPath(ProjectContentPaths.AssetDefinitions, assetName);

            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.name = assetName;
            asset.Initialize(prefab, boundsSize, boundsCenterOffset);

            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);

            AssetCatalogService.RegisterAsset(asset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return asset;
        }

        public static List<AssetDefinition> CreateAssetsFromPrefabs(IEnumerable<GameObject> prefabs)
        {
            List<AssetDefinition> createdAssets = new();

            if (prefabs == null)
                return createdAssets;

            foreach (GameObject prefab in prefabs)
            {
                AssetDefinition createdAsset = CreateAssetFromPrefab(prefab);

                if (createdAsset)
                    createdAssets.Add(createdAsset);
            }

            return createdAssets;
        }

        public static List<AssetDefinition> CreateAssetsFromSelectedPrefabs()
        {
            return CreateAssetsFromPrefabs(GetSelectedPrefabAssets());
        }

        public static List<GameObject> GetSelectedPrefabAssets()
        {
            return Selection.objects.OfType<GameObject>().Where(IsPrefabAsset).Distinct().ToList();
        }

        public static bool HasSelectedPrefabAssets()
        {
            return GetSelectedPrefabAssets().Count > 0;
        }

        public static bool TryGetPrefabBounds(GameObject prefab, out Vector3 boundsSize, out Vector3 boundsCenterOffset)
        {
            boundsSize = default;
            boundsCenterOffset = default;

            if (!prefab)
                return false;

            if (!BoundsUtility.TryGetRendererBounds(prefab.transform, out Bounds bounds, true, false) &&
                !BoundsUtility.TryGetColliderBounds(prefab.transform, out bounds, true, false))
            {
                return false;
            }

            boundsSize = new Vector3(Mathf.Max(0.01f, bounds.size.x), Mathf.Max(0.01f, bounds.size.y), Mathf.Max(0.01f, bounds.size.z));
            boundsCenterOffset = prefab.transform.InverseTransformPoint(bounds.center);

            return true;
        }

        public static bool IsPrefabAsset(GameObject gameObject)
        {
            if (!gameObject)
                return false;

            if (!AssetDatabase.Contains(gameObject))
                return false;

            return PrefabUtility.GetPrefabAssetType(gameObject) != PrefabAssetType.NotAPrefab;
        }

        private static string GetCleanAssetName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? DefaultAssetName : value.Trim();
        }

    }
}
