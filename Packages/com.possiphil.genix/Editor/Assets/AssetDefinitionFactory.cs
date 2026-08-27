using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Infrastructure;
using Genix.Geometry;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Genix.Editor.Assets
{
    /// <summary>Creates asset definition instances.</summary>
    public static class AssetDefinitionFactory
    {
        private const string DefaultAssetName = "New Asset";
        private static readonly Vector3 DefaultBoundsSize = Vector3.one;

        /// <summary>Creates asset from prefab.</summary>
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

        /// <summary>Creates assets from prefabs.</summary>
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

        /// <summary>Creates assets from selected prefabs.</summary>
        public static List<AssetDefinition> CreateAssetsFromSelectedPrefabs()
        {
            return CreateAssetsFromPrefabs(GetSelectedPrefabAssets());
        }

        /// <summary>Returns selected prefab assets.</summary>
        public static List<GameObject> GetSelectedPrefabAssets()
        {
            return Selection.objects.OfType<GameObject>().Where(IsPrefabAsset).Distinct().ToList();
        }

        /// <summary>Determines whether selected prefab assets.</summary>
        public static bool HasSelectedPrefabAssets()
        {
            return GetSelectedPrefabAssets().Count > 0;
        }

        /// <summary>Attempts to get prefab bounds.</summary>
        public static bool TryGetPrefabBounds(GameObject prefab, out Vector3 boundsSize, out Vector3 boundsCenterOffset)
        {
            boundsSize = default;
            boundsCenterOffset = default;

            if (!prefab)
                return false;

            GameObject probe = UnityEngine.Object.Instantiate(prefab);
            try
            {
                probe.hideFlags = HideFlags.HideAndDontSave;
                probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Physics.SyncTransforms();

                if (!BoundsUtility.TryGetRendererBounds(probe.transform, out Bounds bounds, true, false) &&
                    !BoundsUtility.TryGetColliderBounds(probe.transform, out bounds, true, false))
                {
                    return false;
                }

                boundsSize = new Vector3(
                    Mathf.Max(0.01f, bounds.size.x),
                    Mathf.Max(0.01f, bounds.size.y),
                    Mathf.Max(0.01f, bounds.size.z));
                boundsCenterOffset = probe.transform.InverseTransformPoint(bounds.center);

                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>Determines whether prefab asset.</summary>
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
