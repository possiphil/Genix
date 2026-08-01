using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsFoundation = SpaceFoundationSystem.SpaceFoundation;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    internal static class SfsSubspaceProvider
    {
        private const int MaxFloodFillCells = 2_000_000;

        public static HashSet<Vector3Int> Resolve(SfsSpace space, SfsAnchor anchor) =>
            Resolve(space, anchor, out _);

        public static HashSet<Vector3Int> Resolve(
            SfsSpace space,
            SfsAnchor anchor,
            out SfsSubspaceResolutionInfo info)
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            HashSet<Vector3Int> liveSubspace = anchor.GetSubspace();

            if (liveSubspace != null && liveSubspace.Count > 0)
            {
                Stopwatch liveStoreStopwatch = Stopwatch.StartNew();
                LiveSubspaceStoreResult liveStoreResult = StoreLiveSubspace(space, anchor, liveSubspace);
                liveStoreStopwatch.Stop();
                totalStopwatch.Stop();
                info = SfsSubspaceResolutionInfo.Live(
                    liveSubspace.Count,
                    liveStoreStopwatch.ElapsedMilliseconds,
                    totalStopwatch.ElapsedMilliseconds,
                    liveStoreResult.StoreResult,
                    liveStoreResult.CacheKey);
                return liveSubspace;
            }

            if (!SfsPersistentDataReader.TryRead(space, anchor, out PersistentSubspaceData data))
            {
                if (TryGetLiveSnapshotCache(
                        space,
                        anchor,
                        totalStopwatch,
                        out HashSet<Vector3Int> liveSnapshot,
                        out info))
                {
                    return liveSnapshot;
                }

                totalStopwatch.Stop();
                info = SfsSubspaceResolutionInfo.Failed(
                    SfsSubspaceResolutionSource.MissingPersistentData,
                    totalStopwatch.ElapsedMilliseconds);
                return null;
            }

            if (data.Bounds.CellCount > MaxFloodFillCells)
            {
                totalStopwatch.Stop();
                info = SfsSubspaceResolutionInfo.Failed(
                    SfsSubspaceResolutionSource.BoundsTooLarge,
                    totalStopwatch.ElapsedMilliseconds,
                    data.Bounds.CellCount);
                return null;
            }

            PersistentSubspaceCacheKey key = PersistentSubspaceCacheKey.Create(data);
            string cacheKey = key.ToStableString();

            Stopwatch cacheStopwatch = Stopwatch.StartNew();

            if (PersistentSubspaceCache.TryGet(
                    key,
                    data.AnchorBorders.Count,
                    out HashSet<Vector3Int> cached,
                    out PersistentSubspaceCacheSource cacheSource))
            {
                cacheStopwatch.Stop();
                totalStopwatch.Stop();
                info = SfsSubspaceResolutionInfo.CacheHit(
                    cacheSource,
                    cached.Count,
                    data.Bounds.CellCount,
                    cacheStopwatch.ElapsedMilliseconds,
                    totalStopwatch.ElapsedMilliseconds,
                    cacheKey);
                return cached;
            }

            cacheStopwatch.Stop();

            Stopwatch floodFillStopwatch = Stopwatch.StartNew();
            HashSet<Vector3Int> subspace = VoxelFloodFill.Fill(
                data.Seed,
                data.AnchorId,
                data.BorderOwners,
                data.Bounds);
            floodFillStopwatch.Stop();

            if (subspace.Count < data.AnchorBorders.Count)
            {
                totalStopwatch.Stop();
                info = SfsSubspaceResolutionInfo.Failed(
                    SfsSubspaceResolutionSource.FloodFillFailed,
                    totalStopwatch.ElapsedMilliseconds,
                    data.Bounds.CellCount,
                    subspace.Count,
                    floodFillStopwatch.ElapsedMilliseconds,
                    cacheStopwatch.ElapsedMilliseconds,
                    cacheKey);
                return null;
            }

            Stopwatch storeStopwatch = Stopwatch.StartNew();
            PersistentSubspaceCacheStoreResult storeResult = PersistentSubspaceCache.Store(key, subspace);
            storeStopwatch.Stop();
            totalStopwatch.Stop();

            info = SfsSubspaceResolutionInfo.FloodFill(
                subspace.Count,
                data.Bounds.CellCount,
                cacheStopwatch.ElapsedMilliseconds,
                floodFillStopwatch.ElapsedMilliseconds,
                storeStopwatch.ElapsedMilliseconds,
                totalStopwatch.ElapsedMilliseconds,
                storeResult,
                cacheKey);
            return subspace;
        }

        private static LiveSubspaceStoreResult StoreLiveSubspace(
            SfsSpace space,
            SfsAnchor anchor,
            HashSet<Vector3Int> liveSubspace)
        {
            if (SfsPersistentDataReader.TryRead(space, anchor, out PersistentSubspaceData data))
            {
                PersistentSubspaceCacheKey key = PersistentSubspaceCacheKey.Create(data);
                return new LiveSubspaceStoreResult(
                    PersistentSubspaceCache.Store(key, liveSubspace),
                    key.ToStableString());
            }

            return TryCreateLiveSnapshotKey(space, anchor, out PersistentSubspaceCacheKey snapshotKey)
                ? new LiveSubspaceStoreResult(PersistentSubspaceCache.Store(snapshotKey, liveSubspace), snapshotKey.ToStableString())
                : new LiveSubspaceStoreResult(PersistentSubspaceCacheStoreResult.NotStored, string.Empty);
        }

        private static bool TryGetLiveSnapshotCache(
            SfsSpace space,
            SfsAnchor anchor,
            Stopwatch totalStopwatch,
            out HashSet<Vector3Int> subspace,
            out SfsSubspaceResolutionInfo info)
        {
            subspace = null;
            info = default;

            if (!TryCreateLiveSnapshotKey(space, anchor, out PersistentSubspaceCacheKey key))
                return false;

            Stopwatch cacheStopwatch = Stopwatch.StartNew();

            if (!PersistentSubspaceCache.TryGet(
                    key,
                    1,
                    out HashSet<Vector3Int> cached,
                    out PersistentSubspaceCacheSource cacheSource))
            {
                cacheStopwatch.Stop();
                return false;
            }

            cacheStopwatch.Stop();
            totalStopwatch.Stop();
            info = SfsSubspaceResolutionInfo.CacheHit(
                cacheSource,
                cached.Count,
                cached.Count,
                cacheStopwatch.ElapsedMilliseconds,
                totalStopwatch.ElapsedMilliseconds,
                key.ToStableString());
            subspace = cached;
            return true;
        }

        private static bool TryCreateLiveSnapshotKey(
            SfsSpace space,
            SfsAnchor anchor,
            out PersistentSubspaceCacheKey key)
        {
            key = default;

            if (!anchor)
                return false;

            string anchorId = anchor.GetUniqueId();
            SfsFoundation foundation = SfsFoundationUtility.Find(space, anchor);

            if (string.IsNullOrWhiteSpace(anchorId) || !foundation)
                return false;

            key = PersistentSubspaceCacheKey.CreateLiveSnapshot(foundation, anchorId);
            return true;
        }

        private readonly struct LiveSubspaceStoreResult
        {
            public PersistentSubspaceCacheStoreResult StoreResult { get; }
            public string CacheKey { get; }

            public LiveSubspaceStoreResult(
                PersistentSubspaceCacheStoreResult storeResult,
                string cacheKey)
            {
                StoreResult = storeResult;
                CacheKey = cacheKey ?? string.Empty;
            }
        }
    }

    internal readonly struct SfsSubspaceResolutionInfo
    {
        public SfsSubspaceResolutionSource Source { get; }
        public int CellCount { get; }
        public long BoundsCellCount { get; }
        public long CacheMilliseconds { get; }
        public long FloodFillMilliseconds { get; }
        public long StoreMilliseconds { get; }
        public long TotalMilliseconds { get; }
        public PersistentSubspaceCacheStoreResult StoreResult { get; }
        public string CacheKey { get; }
        public bool HasValue { get; }

        private SfsSubspaceResolutionInfo(
            SfsSubspaceResolutionSource source,
            int cellCount,
            long boundsCellCount,
            long cacheMilliseconds,
            long floodFillMilliseconds,
            long storeMilliseconds,
            long totalMilliseconds,
            PersistentSubspaceCacheStoreResult storeResult,
            string cacheKey = "")
        {
            Source = source;
            CellCount = cellCount;
            BoundsCellCount = boundsCellCount;
            CacheMilliseconds = cacheMilliseconds;
            FloodFillMilliseconds = floodFillMilliseconds;
            StoreMilliseconds = storeMilliseconds;
            TotalMilliseconds = totalMilliseconds;
            StoreResult = storeResult;
            CacheKey = cacheKey ?? string.Empty;
            HasValue = true;
        }

        public static SfsSubspaceResolutionInfo Live(
            int cellCount,
            long storeMilliseconds,
            long totalMilliseconds,
            PersistentSubspaceCacheStoreResult storeResult,
            string cacheKey) =>
            new(
                SfsSubspaceResolutionSource.Live,
                cellCount,
                cellCount,
                0L,
                0L,
                storeMilliseconds,
                totalMilliseconds,
                storeResult,
                cacheKey);

        public static SfsSubspaceResolutionInfo CacheHit(
            PersistentSubspaceCacheSource cacheSource,
            int cellCount,
            long boundsCellCount,
            long cacheMilliseconds,
            long totalMilliseconds,
            string cacheKey)
        {
            SfsSubspaceResolutionSource source = cacheSource == PersistentSubspaceCacheSource.Persistent
                ? SfsSubspaceResolutionSource.PersistentCache
                : SfsSubspaceResolutionSource.MemoryCache;

            return new SfsSubspaceResolutionInfo(
                source,
                cellCount,
                boundsCellCount,
                cacheMilliseconds,
                0L,
                0L,
                totalMilliseconds,
                PersistentSubspaceCacheStoreResult.NotStored,
                cacheKey);
        }

        public static SfsSubspaceResolutionInfo FloodFill(
            int cellCount,
            long boundsCellCount,
            long cacheMilliseconds,
            long floodFillMilliseconds,
            long storeMilliseconds,
            long totalMilliseconds,
            PersistentSubspaceCacheStoreResult storeResult,
            string cacheKey) =>
            new(
                SfsSubspaceResolutionSource.FloodFill,
                cellCount,
                boundsCellCount,
                cacheMilliseconds,
                floodFillMilliseconds,
                storeMilliseconds,
                totalMilliseconds,
                storeResult,
                cacheKey);

        public static SfsSubspaceResolutionInfo Failed(
            SfsSubspaceResolutionSource source,
            long totalMilliseconds,
            long boundsCellCount = 0L,
            int cellCount = 0,
            long floodFillMilliseconds = 0L,
            long cacheMilliseconds = 0L,
            string cacheKey = "") =>
            new(
                source,
                cellCount,
                boundsCellCount,
                cacheMilliseconds,
                floodFillMilliseconds,
                0L,
                totalMilliseconds,
                PersistentSubspaceCacheStoreResult.NotStored,
                cacheKey);
    }

    internal enum SfsSubspaceResolutionSource
    {
        Live,
        MemoryCache,
        PersistentCache,
        FloodFill,
        MissingPersistentData,
        BoundsTooLarge,
        FloodFillFailed
    }
}
