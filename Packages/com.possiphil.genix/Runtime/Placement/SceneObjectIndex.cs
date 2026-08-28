using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Extensions;
using Genix.Geometry;
using Genix.Layouts;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>
    /// Broad-phase index of existing generated or fixed scene objects used by overlap and spacing validation.
    /// </summary>
    /// <remarks>Collection caches are invalidated explicitly when hierarchy or project state changes.</remarks>
    internal sealed class SceneObjectIndex
    {
        private const int MaxCachedEntries = 16;

        private static readonly Dictionary<string, SceneObjectIndex> GeneratedCache = new();
        private static readonly Dictionary<string, SceneObjectIndex> FixedCache = new();

        private readonly List<Entry> _entries = new();
        private readonly SpatialBoundsIndex _spatialIndex = new();
        private readonly SpatialBoundsIndex _clearanceSpatialIndex = new();
        private readonly Dictionary<PlacementSurfaceDescriptor, int> _supportCounts = new();
        private readonly Dictionary<PlacementSurfaceDescriptor, Dictionary<AssetDefinition, int>> _supportAssetCounts = new();
        private readonly Dictionary<PlacementSurfaceDescriptor, Dictionary<SemanticTag, int>> _supportTagCounts = new();
        private readonly HashSet<AssetDefinition> _assets = new();
        private readonly HashSet<SemanticTag> _assetTags = new();
        private readonly Dictionary<AssetDefinition, int> _assetCounts = new();
        private readonly Dictionary<SemanticTag, int> _assetTagCounts = new();
        private Bounds _bounds;
        private bool _hasBounds;
        private float _maxAssetSpacingDistance;

        public static SceneObjectIndex Empty { get; } = new();
        public int Count => _entries.Count;
        public bool HasBounds => _hasBounds;
        public Bounds Bounds => _hasBounds ? _bounds : default;
        public bool HasClearanceBounds => _clearanceSpatialIndex.Count > 0;
        public float MaxAssetSpacingDistance => _maxAssetSpacingDistance;
        internal IReadOnlyList<Entry> Entries => _entries;

        public static void ClearCache()
        {
            GeneratedCache.Clear();
            FixedCache.Clear();
        }

        /// <summary>Returns a cached generated-object index for a hierarchy parent when still valid.</summary>
        public static SceneObjectIndex CollectGeneratedCached(Transform generatedParent)
        {
            string cacheKey = CreateGeneratedCacheKey(generatedParent);

            if (!string.IsNullOrEmpty(cacheKey) &&
                GeneratedCache.TryGetValue(cacheKey, out SceneObjectIndex cached))
            {
                return cached;
            }

            SceneObjectIndex index = CollectGenerated(generatedParent);
            StoreCached(GeneratedCache, cacheKey, index);
            return index;
        }

        public static SceneObjectIndex CollectGenerated(Transform generatedParent)
        {
            SceneObjectIndex index = new();

            if (!generatedParent)
                return index;

            foreach (Transform child in generatedParent)
            {
                if (!child)
                    continue;

                GeneratedObjectMetadata metadata = child.GetComponent<GeneratedObjectMetadata>();
                AssetDefinition asset = metadata ? metadata.AssetDefinition : null;
                Bounds bounds;

                if (asset)
                {
                    Quaternion placementRotation = asset.RemovePrefabRotationOffset(child.rotation);
                    bounds = new OrientedBounds(
                            child.position + placementRotation * asset.BoundsCenterOffset,
                            asset.BoundsSize,
                            placementRotation)
                        .ToAxisAlignedBounds();
                }
                else if (!BoundsUtility.TryGetRendererBounds(child, out bounds))
                {
                    continue;
                }

                index.Add(new Entry(
                    bounds,
                    child.name,
                    null,
                    child,
                    metadata ? metadata.SupportSurface : null,
                    asset));
            }

            return index;
        }

        public static SceneObjectIndex CollectFixed(
            IAreaSource areaSource,
            Transform generatedParent)
        {
            return CollectFixed(areaSource, generatedParent, default, 0f, false);
        }

        /// <summary>Returns a cached fixed-object index scoped to the area source and generated hierarchy.</summary>
        public static SceneObjectIndex CollectFixedCached(
            IAreaSource areaSource,
            Transform generatedParent,
            Bounds targetBounds,
            float boundsExpansion)
        {
            string cacheKey = CreateFixedCacheKey(areaSource, generatedParent, targetBounds, boundsExpansion);

            if (!string.IsNullOrEmpty(cacheKey) &&
                FixedCache.TryGetValue(cacheKey, out SceneObjectIndex cached))
            {
                return cached;
            }

            SceneObjectIndex index = CollectFixed(areaSource, generatedParent, targetBounds, boundsExpansion, true);
            StoreCached(FixedCache, cacheKey, index);
            return index;
        }

        public static SceneObjectIndex CollectFixed(
            IAreaSource areaSource,
            Transform generatedParent,
            Bounds targetBounds,
            float boundsExpansion)
        {
            return CollectFixed(areaSource, generatedParent, targetBounds, boundsExpansion, true);
        }

        private static SceneObjectIndex CollectFixed(
            IAreaSource areaSource,
            Transform generatedParent,
            Bounds targetBounds,
            float boundsExpansion,
            bool hasTargetBounds)
        {
            SceneObjectIndex index = new();
            Bounds searchBounds = targetBounds;

            if (hasTargetBounds)
                searchBounds.Expand(Mathf.Max(0f, boundsExpansion) * 2f);

            foreach (Collider collider in Object.FindObjectsByType<Collider>())
            {
                if (!IsUsableFixedCollider(collider, areaSource, generatedParent))
                    continue;

                Bounds colliderBounds = collider.bounds;

                if (hasTargetBounds && !colliderBounds.Intersects(searchBounds))
                    continue;

                index.Add(new Entry(colliderBounds, collider.name, collider, collider.transform, null, null));
            }

            return index;
        }

        public IEnumerable<Entry> Query(Bounds bounds)
        {
            foreach (int index in _spatialIndex.Query(bounds))
                yield return _entries[index];
        }

        public IEnumerable<Entry> QueryClearance(Bounds bounds)
        {
            foreach (int index in _clearanceSpatialIndex.Query(bounds))
                yield return _entries[index];
        }

        /// <summary>Returns how many indexed generated objects reference the supplied support surface.</summary>
        public int GetSupportCount(PlacementSurfaceDescriptor supportSurface)
        {
            return supportSurface && _supportCounts.TryGetValue(supportSurface, out int count) ? count : 0;
        }

        /// <summary>Returns how many indexed generated objects on one surface use the supplied asset.</summary>
        public int GetSupportAssetCount(PlacementSurfaceDescriptor supportSurface, AssetDefinition asset) =>
            GetNestedCount(_supportAssetCounts, supportSurface, asset);

        /// <summary>Returns how many indexed generated objects on one surface carry the supplied asset tag.</summary>
        public int GetSupportTagCount(PlacementSurfaceDescriptor supportSurface, SemanticTag tag) =>
            GetNestedCount(_supportTagCounts, supportSurface, tag);

        /// <summary>Determines whether any indexed generated object uses the supplied asset definition.</summary>
        public bool ContainsAsset(AssetDefinition asset) => asset && _assets.Contains(asset);

        /// <summary>Determines whether any indexed generated object carries the supplied asset-compatible tag.</summary>
        public bool ContainsAssetTag(SemanticTag tag) => tag && _assetTags.Contains(tag);

        /// <summary>Returns how many indexed generated objects use the supplied asset definition.</summary>
        public int GetAssetCount(AssetDefinition asset) =>
            asset && _assetCounts.TryGetValue(asset, out int count) ? count : 0;

        /// <summary>Returns how many indexed generated objects carry the supplied asset-compatible tag.</summary>
        public int GetAssetTagCount(SemanticTag tag) =>
            tag && _assetTagCounts.TryGetValue(tag, out int count) ? count : 0;

        private void Add(Entry entry)
        {
            _entries.Add(entry);
            int entryIndex = _entries.Count - 1;
            _spatialIndex.Add(entry.Bounds, entryIndex);

            if (entry.AssetDefinition)
            {
                _assets.Add(entry.AssetDefinition);
                IncrementCount(_assetCounts, entry.AssetDefinition);
                _maxAssetSpacingDistance = Mathf.Max(
                    _maxAssetSpacingDistance,
                    entry.AssetDefinition.MaxSpacingDistance);

                foreach (SemanticTag tag in entry.AssetDefinition.SemanticTags)
                {
                    if (tag && tag.SupportsAssets)
                    {
                        _assetTags.Add(tag);
                        IncrementCount(_assetTagCounts, tag);
                    }
                }

                if (entry.AssetDefinition.ReserveClearance && entry.Root)
                {
                    OrientedBounds clearance = entry.AssetDefinition.CreateClearanceBounds(
                        entry.Root.position,
                        entry.Root.rotation);
                    _clearanceSpatialIndex.Add(clearance.ToAxisAlignedBounds(), entryIndex);
                }
            }

            if (entry.SupportSurface)
            {
                _supportCounts[entry.SupportSurface] = GetSupportCount(entry.SupportSurface) + 1;

                if (entry.AssetDefinition)
                {
                    IncrementNestedCount(_supportAssetCounts, entry.SupportSurface, entry.AssetDefinition);

                    foreach (SemanticTag tag in entry.AssetDefinition.SemanticTags)
                    {
                        if (tag && tag.SupportsAssets)
                            IncrementNestedCount(_supportTagCounts, entry.SupportSurface, tag);
                    }
                }
            }

            if (!_hasBounds)
            {
                _bounds = entry.Bounds;
                _hasBounds = true;
            }
            else
            {
                _bounds.Encapsulate(entry.Bounds);
            }
        }

        private static void IncrementCount<TKey>(Dictionary<TKey, int> counts, TKey key)
        {
            if (key == null)
                return;

            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        private static void IncrementNestedCount<TKey>(
            Dictionary<PlacementSurfaceDescriptor, Dictionary<TKey, int>> counts,
            PlacementSurfaceDescriptor support,
            TKey key)
        {
            if (!support || key == null)
                return;

            if (!counts.TryGetValue(support, out Dictionary<TKey, int> supportCounts))
            {
                supportCounts = new Dictionary<TKey, int>();
                counts[support] = supportCounts;
            }

            supportCounts.TryGetValue(key, out int count);
            supportCounts[key] = count + 1;
        }

        private static int GetNestedCount<TKey>(
            Dictionary<PlacementSurfaceDescriptor, Dictionary<TKey, int>> counts,
            PlacementSurfaceDescriptor support,
            TKey key) =>
            support && key != null &&
            counts.TryGetValue(support, out Dictionary<TKey, int> supportCounts) &&
            supportCounts.TryGetValue(key, out int count)
                ? count
                : 0;

        private static bool IsUsableFixedCollider(
            Collider collider,
            IAreaSource areaSource,
            Transform generatedParent)
        {
            if (!collider ||
                !collider.enabled ||
                collider.isTrigger ||
                !collider.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (HasDontSaveHideFlags(collider.transform))
                return false;

            if (generatedParent && collider.transform.IsChildOf(generatedParent))
                return false;

            return areaSource?.IsSourceCollider(collider) != true;
        }

        private static bool HasDontSaveHideFlags(Transform transform)
        {
            while (transform)
            {
                if ((transform.gameObject.hideFlags & HideFlags.DontSave) != 0)
                    return true;

                transform = transform.parent;
            }

            return false;
        }

        private static void StoreCached(
            Dictionary<string, SceneObjectIndex> cache,
            string cacheKey,
            SceneObjectIndex index)
        {
            if (string.IsNullOrEmpty(cacheKey))
                return;

            if (cache.Count >= MaxCachedEntries)
                cache.Clear();

            cache[cacheKey] = index;
        }

        private static string CreateGeneratedCacheKey(Transform generatedParent)
        {
            if (!generatedParent)
                return string.Empty;

            return $"generated:{GetSceneKey(generatedParent)}:{generatedParent.GetLocalObjectId()}";
        }

        private static string CreateFixedCacheKey(
            IAreaSource areaSource,
            Transform generatedParent,
            Bounds targetBounds,
            float boundsExpansion)
        {
            if (areaSource == null)
                return string.Empty;

            string sourceId = areaSource.SourceInfo.SourceId;
            string sourceName = areaSource.SourceInfo.SourceName;
            string sourceType = areaSource.SourceInfo.SourceType;
            string sourceTransformId = areaSource.ParentTransform
                ? areaSource.ParentTransform.GetLocalObjectId()
                : string.Empty;
            string generatedParentId = generatedParent
                ? generatedParent.GetLocalObjectId()
                : string.Empty;

            return string.Join(
                ":",
                "fixed",
                GetSceneKey(areaSource.ParentTransform ? areaSource.ParentTransform : generatedParent),
                sourceType,
                sourceId,
                sourceName,
                sourceTransformId,
                generatedParentId,
                Quantize(targetBounds.center.x),
                Quantize(targetBounds.center.y),
                Quantize(targetBounds.center.z),
                Quantize(targetBounds.size.x),
                Quantize(targetBounds.size.y),
                Quantize(targetBounds.size.z),
                Quantize(Mathf.Max(0f, boundsExpansion)));
        }

        private static string GetSceneKey(Transform transform)
        {
            if (transform)
            {
                UnityEngine.SceneManagement.Scene scene = transform.gameObject.scene;
                return scene.IsValid()
                    ? $"{scene.handle}:{scene.path}:{scene.name}"
                    : string.Empty;
            }

            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return activeScene.IsValid()
                ? $"{activeScene.handle}:{activeScene.path}:{activeScene.name}"
                : string.Empty;
        }

        private static int Quantize(float value) =>
            Mathf.RoundToInt(value * 1000f);

        internal readonly struct Entry
        {
            public Bounds Bounds { get; }
            public string ObjectName { get; }
            public Collider Collider { get; }
            public Transform Root { get; }
            public PlacementSurfaceDescriptor SupportSurface { get; }
            public AssetDefinition AssetDefinition { get; }

            public Entry(
                Bounds bounds,
                string objectName,
                Collider collider,
                Transform root,
                PlacementSurfaceDescriptor supportSurface,
                AssetDefinition assetDefinition)
            {
                Bounds = bounds;
                ObjectName = string.IsNullOrWhiteSpace(objectName) ? "Scene Object" : objectName;
                Collider = collider;
                Root = root;
                SupportSurface = supportSurface;
                AssetDefinition = assetDefinition;
            }
        }
    }
}
