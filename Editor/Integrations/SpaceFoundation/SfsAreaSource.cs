using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Diagnostics;
using Genix.Semantics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    public sealed class SfsAreaSource : IAreaSource, IAreaCacheControl
    {
        private readonly SfsSpace _space;

        private SfsAnchor Anchor => _space ? _space.anchor : null;

        public SfsAreaSource(SfsSpace space)
        {
            _space = space;
        }

        public Transform ParentTransform => _space ? _space.transform : null;

        public string ClearCacheLabel => "Clear SFS Cache";

        public string ClearCacheTooltip =>
            "Clear Genix's cached Space Foundation subspaces and derived placement areas.";

        public SpatialSourceInfo SourceInfo => new(
            "Space Foundation location",
            _space ? AreaName.ToDesignerName(_space.name) : "Missing Location",
            Anchor ? Anchor.GetUniqueId() : string.Empty);

        public IReadOnlyList<SemanticTag> SemanticTags =>
            GetSemanticTagSet()?.SemanticTags ?? Array.Empty<SemanticTag>();

        public IReadOnlyList<TagCategory> AnyTagCategories =>
            GetSemanticTagSet()?.AnyTagCategories ?? Array.Empty<TagCategory>();

        public bool TryBuildArea(AreaBuildSettings settings, out PlacementArea area, out string error)
        {
            area = null;

            if (!_space)
            {
                error = "The selected Space Foundation location no longer exists.";
                return false;
            }

            if (!Anchor)
            {
                error = $"Location '{_space.name}' has no Space Foundation anchor.";
                return false;
            }

            float voxelSize = SfsFoundationUtility.GetVoxelSize(SfsFoundationUtility.Find(_space, Anchor));
            Stopwatch memoryCacheLookupStopwatch = Stopwatch.StartNew();

            if (SfsAreaCache.TryGetMemory(
                    _space,
                    Anchor,
                    settings,
                    voxelSize,
                    out area))
            {
                memoryCacheLookupStopwatch.Stop();
                settings.profile?.AddStepTime(
                    AreaBuildProfileStep.AreaCacheLookup,
                    (float)memoryCacheLookupStopwatch.Elapsed.TotalMilliseconds);
                error = string.Empty;
                return true;
            }

            memoryCacheLookupStopwatch.Stop();

            Stopwatch subspaceStopwatch = Stopwatch.StartNew();
            HashSet<Vector3Int> subspace = SfsSubspaceProvider.Resolve(
                _space,
                Anchor,
                out SfsSubspaceResolutionInfo subspaceInfo);
            subspaceStopwatch.Stop();
            settings.profile?.AddStepTime(
                AreaBuildProfileStep.SubspaceResolve,
                Mathf.Max(0f, (float)subspaceStopwatch.Elapsed.TotalMilliseconds - subspaceInfo.StoreMilliseconds));
            settings.profile?.AddStepTime(
                AreaBuildProfileStep.LiveCacheStore,
                subspaceInfo.StoreMilliseconds);

            LogSubspaceResolution(_space.name, subspaceInfo);

            if (subspace != null && subspace.Count > 0)
            {
                Stopwatch cacheLookupStopwatch = Stopwatch.StartNew();

                if (SfsAreaCache.TryGet(
                        _space,
                        Anchor,
                        SourceInfo,
                        settings,
                        subspaceInfo,
                        subspace,
                        subspace.Count,
                        voxelSize,
                        IsSourceCollider,
                        out area))
                {
                    cacheLookupStopwatch.Stop();
                    settings.profile?.AddStepTime(
                        AreaBuildProfileStep.AreaCacheLookup,
                        (float)cacheLookupStopwatch.Elapsed.TotalMilliseconds);
                    error = string.Empty;
                    return true;
                }

                cacheLookupStopwatch.Stop();
                settings.profile?.AddStepTime(
                    AreaBuildProfileStep.AreaCacheLookup,
                    (float)cacheLookupStopwatch.Elapsed.TotalMilliseconds);

                return SfsAreaBuilder.TryBuild(
                    _space,
                    Anchor,
                    SourceInfo,
                    subspace,
                    settings,
                    IsSourceCollider,
                    out area,
                    out error) &&
                       StoreBuiltArea(
                           _space,
                           Anchor,
                           settings,
                           subspaceInfo,
                           subspace.Count,
                           voxelSize,
                           area);
            }

            return BoundsAreaFallback.TryBuild(
                _space.gameObject,
                SourceInfo,
                settings,
                IsSourceCollider,
                out area,
                out error);
        }

        public static void ClearPersistentSubspaceCache()
        {
            PersistentSubspaceCache.Clear();
            SfsAreaCache.Clear();
        }

        public void ClearCache()
        {
            ClearPersistentSubspaceCache();
        }

        private static bool StoreBuiltArea(
            SfsSpace space,
            SfsAnchor anchor,
            AreaBuildSettings settings,
            SfsSubspaceResolutionInfo subspaceInfo,
            int subspaceCellCount,
            float voxelSize,
            PlacementArea area)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            SfsAreaCache.Store(space, anchor, settings, subspaceInfo, subspaceCellCount, voxelSize, area);
            stopwatch.Stop();
            settings.profile?.AddStepTime(
                AreaBuildProfileStep.AreaCacheStore,
                (float)stopwatch.Elapsed.TotalMilliseconds);
            return true;
        }

        private static void LogSubspaceResolution(string locationName, SfsSubspaceResolutionInfo info)
        {
            if (!info.HasValue)
                return;

            string target = string.IsNullOrWhiteSpace(locationName) ? "selected location" : locationName;

            switch (info.Source)
            {
                case SfsSubspaceResolutionSource.Live:
                    Debug.Log(
                        $"Genix SFS live subspace for '{target}': {FormatCount(info.CellCount)} cells, " +
                        $"store {info.StoreMilliseconds} ms ({FormatStoreResult(info.StoreResult)}), total {info.TotalMilliseconds} ms.");
                    break;

                case SfsSubspaceResolutionSource.MemoryCache:
                case SfsSubspaceResolutionSource.PersistentCache:
                    Debug.Log(
                        $"Genix SFS subspace cache hit for '{target}': {FormatSource(info.Source)}, " +
                        $"{FormatCount(info.CellCount)} cells, cache {info.CacheMilliseconds} ms, total {info.TotalMilliseconds} ms.");
                    break;

                case SfsSubspaceResolutionSource.FloodFill:
                    Debug.Log(
                        $"Genix SFS subspace flood fill for '{target}': {FormatCount(info.CellCount)} cells " +
                        $"inside {FormatCount(info.BoundsCellCount)} bounds cells, flood {info.FloodFillMilliseconds} ms, " +
                        $"store {info.StoreMilliseconds} ms ({FormatStoreResult(info.StoreResult)}), total {info.TotalMilliseconds} ms.");
                    break;

                default:
                    Debug.LogWarning(
                        $"Genix SFS subspace could not be resolved for '{target}' via cache/flood fill: " +
                        $"{FormatSource(info.Source)}, bounds {FormatCount(info.BoundsCellCount)} cells, total {info.TotalMilliseconds} ms. " +
                        "Genix will use the bounds fallback for this location.");
                    break;
            }
        }

        private static string FormatSource(SfsSubspaceResolutionSource source)
        {
            return source switch
            {
                SfsSubspaceResolutionSource.MemoryCache => "memory cache",
                SfsSubspaceResolutionSource.PersistentCache => "persistent cache",
                SfsSubspaceResolutionSource.Live => "live subspace",
                SfsSubspaceResolutionSource.FloodFill => "flood fill",
                SfsSubspaceResolutionSource.MissingPersistentData => "missing persistent data",
                SfsSubspaceResolutionSource.BoundsTooLarge => "bounds too large",
                SfsSubspaceResolutionSource.FloodFillFailed => "flood fill failed",
                _ => source.ToString()
            };
        }

        private static string FormatStoreResult(PersistentSubspaceCacheStoreResult result)
        {
            return result switch
            {
                PersistentSubspaceCacheStoreResult.AlreadyCached => "already cached",
                PersistentSubspaceCacheStoreResult.MemoryAndPersistent => "memory + persistent cache",
                PersistentSubspaceCacheStoreResult.MemoryOnly => "memory cache only",
                _ => "not cached"
            };
        }

        private static string FormatCount(long count) => count.ToString("N0");

        private SemanticTagSet GetSemanticTagSet()
        {
            if (Anchor && Anchor.TryGetComponent(out SemanticTagSet anchorTags))
                return anchorTags;

            return _space && _space.TryGetComponent(out SemanticTagSet spaceTags)
                ? spaceTags
                : null;
        }

        public bool IsSourceCollider(Collider collider)
        {
            return collider && collider.GetComponentInParent<SfsSpace>() != null;
        }
    }
}
