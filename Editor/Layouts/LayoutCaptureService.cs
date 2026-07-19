using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Generation;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.Infrastructure;
using Genix.Geometry;
using Genix.Layouts;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Layouts
{
    internal static class LayoutCaptureService
    {
        public static bool Save(
            IAreaSource areaSource,
            GenerationMode generationMode,
            PlacementTarget placementTargets,
            TargetDistributionMode distributionMode,
            TargetDistributionWeights distributionWeights,
            AssetPool assetPool,
            string styleName,
            out SavedLayout layout,
            out string error)
        {
            layout = null;

            if (areaSource == null)
            {
                error = "No Target Area is selected.";
                return false;
            }

            if (!GeneratedHierarchy.TryGet(areaSource, out Transform generatedParent))
            {
                error = $"No generated objects were found for '{areaSource.SourceInfo.SourceName}'.";
                return false;
            }

            List<Transform> generatedObjects = GetDirectChildren(generatedParent);

            if (generatedObjects.Count == 0)
            {
                error = $"The generated object group for '{areaSource.SourceInfo.SourceName}' is empty.";
                return false;
            }

            Object[] previousSelection = Selection.objects;
            string createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string areaName = areaSource.SourceInfo.SourceName;
            string safeAreaName = AssetFileService.SanitizeName(AreaName.ToUnitySafeDisplayName(areaName), "Target Area");
            string displayName = $"{areaName} Layout {timestamp}";
            string safeDisplayName = AssetFileService.SanitizeName(displayName, "Saved Layout");
            string targetFolder = $"{ProjectContentPaths.Layouts}/{safeAreaName}";
            string prefabFolder = $"{targetFolder}/Prefabs";
            AssetFileService.EnsureFolder(prefabFolder);

            GameObject temporaryRoot = CreatePrefabRoot(
                generatedObjects,
                displayName,
                areaName,
                areaSource.SourceInfo.SourceId,
                createdAt);

            try
            {
                string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{prefabFolder}/{safeDisplayName}.prefab");
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    temporaryRoot,
                    prefabPath,
                    out bool prefabSaved);

                if (!prefabSaved || !prefab)
                {
                    error = $"Could not save layout prefab at '{prefabPath}'.";
                    return false;
                }

                BoundsUtility.TryGetCombinedBounds(generatedParent, out Bounds bounds, true);
                layout = ScriptableObject.CreateInstance<SavedLayout>();
                layout.name = safeDisplayName;
                layout.Initialize(
                    displayName,
                    prefab,
                    areaName,
                    areaSource.SourceInfo.SourceId,
                    areaSource.SourceInfo.SourceType,
                    generationMode,
                    ResolvePlacementTargets(generatedObjects, placementTargets),
                    distributionMode,
                    distributionWeights,
                    assetPool,
                    styleName,
                    generatedObjects.Count,
                    bounds,
                    createdAt,
                    CreateAssetSummaries(generatedObjects));

                string layoutPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{targetFolder}/{safeDisplayName}.asset");
                AssetDatabase.CreateAsset(layout, layoutPath);
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                error = string.Empty;
                return true;
            }
            finally
            {
                Object.DestroyImmediate(temporaryRoot);
                Selection.objects = previousSelection.Where(item => item).ToArray();
            }
        }

        private static GameObject CreatePrefabRoot(
            IReadOnlyList<Transform> generatedObjects,
            string displayName,
            string areaName,
            string areaId,
            string createdAt)
        {
            GameObject root = new(displayName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            root.AddComponent<SavedLayoutRoot>().Initialize(
                displayName,
                areaName,
                areaId,
                createdAt,
                generatedObjects.Count);

            foreach (Transform generatedObject in generatedObjects)
            {
                GameObject clone = Object.Instantiate(generatedObject.gameObject, root.transform, false);
                clone.name = generatedObject.name;
                clone.transform.SetLocalPositionAndRotation(
                    generatedObject.localPosition,
                    generatedObject.localRotation);
                clone.transform.localScale = generatedObject.localScale;
            }

            return root;
        }

        private static PlacementTarget ResolvePlacementTargets(
            IReadOnlyList<Transform> generatedObjects,
            PlacementTarget fallback)
        {
            PlacementTarget result = PlacementTarget.None;
            Dictionary<GameObject, PlacementTarget> prefabTargets = null;

            foreach (Transform generatedObject in generatedObjects.Where(item => item))
            {
                GeneratedObjectMetadata metadata = generatedObject.GetComponent<GeneratedObjectMetadata>();

                if (metadata && metadata.PlacementTarget != PlacementTarget.None)
                {
                    result |= metadata.PlacementTarget;
                    continue;
                }

                prefabTargets ??= CreatePrefabTargetLookup();
                GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromSource(generatedObject.gameObject);

                if (sourcePrefab && prefabTargets.TryGetValue(sourcePrefab, out PlacementTarget target))
                    result |= target;
            }

            result &= PlacementTarget.All;
            return result != PlacementTarget.None ? result : fallback & PlacementTarget.All;
        }

        private static Dictionary<GameObject, PlacementTarget> CreatePrefabTargetLookup()
        {
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();
            Dictionary<GameObject, PlacementTarget> lookup = new();

            if (!catalog)
                return lookup;

            foreach (AssetDefinition asset in catalog.Assets.Where(asset => asset && asset.Prefab))
            {
                lookup[asset.Prefab] = asset.PlacementType switch
                {
                    PlacementType.Floor => PlacementTarget.Floor,
                    PlacementType.Wall => PlacementTarget.Wall,
                    PlacementType.Ceiling => PlacementTarget.Ceiling,
                    PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                    _ => PlacementTarget.None
                };
            }

            return lookup;
        }

        private static List<LayoutAssetSummary> CreateAssetSummaries(
            IReadOnlyList<Transform> generatedObjects)
        {
            Dictionary<string, AssetSummary> summaries = new();

            foreach (Transform generatedObject in generatedObjects)
            {
                GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(generatedObject.gameObject);
                string assetName = prefab ? prefab.name : generatedObject.name;
                string key = prefab ? AssetDatabase.GetAssetPath(prefab) : generatedObject.name;

                if (!summaries.TryGetValue(key, out AssetSummary summary))
                {
                    summary = new AssetSummary(assetName, prefab);
                    summaries.Add(key, summary);
                }

                summary.Count++;
            }

            return summaries.Values
                .OrderBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
                .Select(summary => new LayoutAssetSummary(summary.Name, summary.Count, summary.Prefab))
                .ToList();
        }

        private static List<Transform> GetDirectChildren(Transform parent)
        {
            List<Transform> children = new();

            foreach (Transform child in parent)
                children.Add(child);

            return children;
        }

        private sealed class AssetSummary
        {
            public string Name { get; }
            public GameObject Prefab { get; }
            public int Count { get; set; }

            public AssetSummary(string name, GameObject prefab)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Generated Object" : name;
                Prefab = prefab;
            }
        }
    }
}
