using System.Collections.Generic;
using Genix.Assets;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Genix.Editor.Assets
{
    public static class AssetCreationMenu
    {
        [MenuItem("Assets/Genix/Create Asset Definition From Prefab", false, 20)]
        private static void CreateAssetDefinitionFromPrefab()
        {
            List<AssetDefinition> createdAssets = AssetDefinitionFactory.CreateAssetsFromSelectedPrefabs();

            if (createdAssets.Count == 0)
            {
                Debug.LogWarning("No prefab assets selected.");
                return;
            }

            Selection.activeObject = createdAssets[^1];
            EditorGUIUtility.PingObject(createdAssets[^1]);

            Debug.Log($"Created {createdAssets.Count} asset definition(s).");
        }

        [MenuItem("Assets/Genix/Create Asset Definition From Prefab", true)]
        private static bool CanCreateAssetDefinitionFromPrefab()
        {
            return AssetDefinitionFactory.HasSelectedPrefabAssets();
        }
    }
}
