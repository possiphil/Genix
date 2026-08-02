using System.Collections.Generic;
using Genix.Areas;
using Genix.Core;
using Genix.Extensions;
using Genix.Geometry;
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
        private Bounds _bounds;
        private bool _hasBounds;

        public static SceneObjectIndex Empty { get; } = new();
        public int Count => _entries.Count;
        public bool HasBounds => _hasBounds;
        public Bounds Bounds => _hasBounds ? _bounds : default;

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
                if (!child ||
                    !BoundsUtility.TryGetRendererBounds(child, out Bounds bounds))
                {
                    continue;
                }

                index.Add(new Entry(bounds, child.name, null));
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

                index.Add(new Entry(colliderBounds, collider.name, collider));
            }

            return index;
        }

        public IEnumerable<Entry> Query(Bounds bounds)
        {
            foreach (int index in _spatialIndex.Query(bounds))
                yield return _entries[index];
        }

        private void Add(Entry entry)
        {
            _entries.Add(entry);
            _spatialIndex.Add(entry.Bounds, _entries.Count - 1);

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

            public Entry(Bounds bounds, string objectName, Collider collider)
            {
                Bounds = bounds;
                ObjectName = string.IsNullOrWhiteSpace(objectName) ? "Scene Object" : objectName;
                Collider = collider;
            }
        }
    }
}
