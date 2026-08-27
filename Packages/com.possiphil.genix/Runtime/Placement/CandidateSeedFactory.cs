using System;
using System.Collections.Generic;
using System.Diagnostics;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Extensions;
using Genix.Placement.Providers;
using Genix.Profiling;
using Genix.Sampling;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>
    /// Allocates target-specific candidate budgets, invokes providers, and optionally reuses deterministic seed sets.
    /// </summary>
    /// <remarks>
    /// Cache keys include spatial identity, style, target distribution, surface settings, requested count, and random
    /// state. Reusing an entry also restores the random state after generation so downstream choices remain stable.
    /// </remarks>
    internal static class CandidateSeedFactory
    {
        private const int LazyBatchCandidateMultiplier = 2;
        private const int LazyMinimumBatchCandidateCount = 64;
        private const int LazyMaxBatchCount = 64;

        /// <summary>Creates one candidate pool containing all requested placement targets.</summary>
        public static CandidatePool CreatePool(
            GenerationContext context,
            IDiagnosticsSink diagnostics,
            PlacementTarget? targets,
            IGenerationProfiler profiler = null,
            IReadOnlyList<AssetDefinition> assets = null)
        {
            diagnostics ??= NullDiagnosticsSink.Instance;
            profiler ??= NullGenerationProfiler.Instance;

            if (!ShouldUseLazy(context))
                return new CandidatePool(Create(context, diagnostics, targets, profiler, assets));

            string cacheKey = CreateCacheKey(context, targets, assets);

            if (CandidateSeedCache.TryGet(cacheKey, out _))
                return new CandidatePool(Create(context, diagnostics, targets, profiler, assets));

            profiler.RecordCandidateCacheHit(false);
            int requestedCount = Mathf.Max(1, context.Count);
            int minimumCandidateCount = GetMinimumCandidateCount(context, requestedCount);
            int candidateBudget = GetCandidateBudget(context, requestedCount, minimumCandidateCount);
            int batchCandidateCount = GetLazyBatchCandidateCount(requestedCount);
            int maxBatchCount = GetLazyMaxBatchCount(candidateBudget, batchCandidateCount);
            IReadOnlyList<ICandidateProvider> providers = CreateProviderList(
                context,
                targets,
                requestedCount,
                minimumCandidateCount,
                batchCandidateCount,
                assets);

            return new CandidatePool(
                () => CreateLazyBatch(
                    context,
                    diagnostics,
                    providers,
                    profiler,
                    requestedCount),
                maxBatchCount,
                candidateBudget);
        }

        /// <summary>Creates independent pools for target-distribution policies that consume targets separately.</summary>
        public static Dictionary<PlacementType, CandidatePool> CreatePoolsByPlacementType(
            GenerationContext context,
            IDiagnosticsSink diagnostics,
            PlacementTarget? targets,
            IGenerationProfiler profiler = null,
            IReadOnlyList<AssetDefinition> assets = null)
        {
            diagnostics ??= NullDiagnosticsSink.Instance;
            profiler ??= NullGenerationProfiler.Instance;

            if (!ShouldUseLazy(context))
                return CreateEagerPoolsByPlacementType(context, diagnostics, targets, profiler, assets);

            PlacementTarget selectedTargets = (targets ?? context.PlacementTargets) & PlacementTarget.All;
            List<PlacementType> placementTypes = GetPlacementTypes(selectedTargets);
            Dictionary<PlacementType, CandidatePool> pools = new();

            profiler.RecordCandidateCacheHit(false);

            foreach (PlacementType placementType in placementTypes)
            {
                PlacementTarget target = ToPlacementTarget(placementType);
                int requestedCount = GetRequestedObjectCount(context, placementType, placementTypes);
                int minimumCandidateCount = GetMinimumCandidateCount(context, requestedCount);
                int candidateBudget = GetCandidateBudget(context, requestedCount, minimumCandidateCount);
                int batchCandidateCount = GetLazyBatchCandidateCount(requestedCount);
                int maxBatchCount = GetLazyMaxBatchCount(candidateBudget, batchCandidateCount);
                IReadOnlyList<ICandidateProvider> providers = CreateProviderList(
                    context,
                    target,
                    requestedCount,
                    minimumCandidateCount,
                    batchCandidateCount,
                    assets);

                pools[placementType] = new CandidatePool(
                    () => CreateLazyBatch(
                        context,
                        diagnostics,
                        providers,
                        profiler,
                        requestedCount),
                    maxBatchCount,
                    candidateBudget);
            }

            return pools;
        }

        /// <summary>Creates or retrieves deterministic candidate seeds for the requested target mask.</summary>
        public static List<CandidateSeed> Create(
            GenerationContext context,
            IDiagnosticsSink diagnostics,
            PlacementTarget? targets,
            IGenerationProfiler profiler = null,
            IReadOnlyList<AssetDefinition> assets = null)
        {
            profiler ??= NullGenerationProfiler.Instance;
            Stopwatch stopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            string cacheKey = CreateCacheKey(context, targets, assets);

            if (CandidateSeedCache.TryGet(cacheKey, out CandidateSeedCacheEntry cached))
            {
                context.Random.State = cached.RandomStateAfterGeneration;
                List<CandidateSeed> copy = new(cached.Seeds);
                profiler.RecordCandidateCacheHit(true);
                RecordCachedSeedCounts(copy, profiler);
                diagnostics.RecordCandidatePool(context.Count, copy);
                profiler.AddPhaseTime(GenerationProfilePhase.CandidateGeneration, StopAndReadMilliseconds(stopwatch));
                return copy;
            }

            List<CandidateSeed> seeds = new();

            foreach (ICandidateProvider provider in CreateProviders(context, targets, assets: assets))
                seeds.AddRange(provider.CreateCandidateSeeds(context, diagnostics, profiler));

            if (context.StyleSettings.algorithm is SamplingAlgorithm.Grid or SamplingAlgorithm.JitteredGrid)
                context.Random.Shuffle(seeds);

            CandidateSeedCache.Store(cacheKey, seeds, context.Random.State);
            profiler.RecordCandidateCacheHit(false);
            diagnostics.RecordCandidatePool(context.Count, seeds);
            profiler.AddPhaseTime(GenerationProfilePhase.CandidateGeneration, StopAndReadMilliseconds(stopwatch));
            return seeds;
        }

        private static Dictionary<PlacementType, CandidatePool> CreateEagerPoolsByPlacementType(
            GenerationContext context,
            IDiagnosticsSink diagnostics,
            PlacementTarget? targets,
            IGenerationProfiler profiler,
            IReadOnlyList<AssetDefinition> assets)
        {
            List<CandidateSeed> seeds = Create(context, diagnostics, targets, profiler, assets);
            Dictionary<PlacementType, List<CandidateSeed>> groupedSeeds = new();

            foreach (CandidateSeed seed in seeds)
            {
                if (!groupedSeeds.TryGetValue(seed.PlacementType, out List<CandidateSeed> group))
                {
                    group = new List<CandidateSeed>();
                    groupedSeeds[seed.PlacementType] = group;
                }

                group.Add(seed);
            }

            Dictionary<PlacementType, CandidatePool> pools = new();

            foreach (KeyValuePair<PlacementType, List<CandidateSeed>> entry in groupedSeeds)
                pools[entry.Key] = new CandidatePool(entry.Value);

            return pools;
        }

        private static List<CandidateSeed> CreateLazyBatch(
            GenerationContext context,
            IDiagnosticsSink diagnostics,
            IReadOnlyList<ICandidateProvider> providers,
            IGenerationProfiler profiler,
            int requestedCount)
        {
            Stopwatch stopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            List<CandidateSeed> seeds = new();

            foreach (ICandidateProvider provider in providers)
                seeds.AddRange(provider.CreateCandidateSeeds(context, diagnostics, profiler));

            if (context.StyleSettings.algorithm is SamplingAlgorithm.Grid or SamplingAlgorithm.JitteredGrid)
                context.Random.Shuffle(seeds);

            diagnostics.RecordCandidatePool(requestedCount, seeds);
            profiler.AddPhaseTime(GenerationProfilePhase.CandidateGeneration, StopAndReadMilliseconds(stopwatch));
            return seeds;
        }

        private static IReadOnlyList<ICandidateProvider> CreateProviderList(
            GenerationContext context,
            PlacementTarget? targets,
            int requestedCount,
            int minimumCandidateCount,
            int candidateCount,
            IReadOnlyList<AssetDefinition> assets)
        {
            List<ICandidateProvider> providers = new();

            foreach (ICandidateProvider provider in CreateProviders(
                         context,
                         targets,
                         requestedCount,
                         minimumCandidateCount,
                         candidateCount,
                         assets))
            {
                providers.Add(provider);
            }

            return providers;
        }

        private static float StopAndReadMilliseconds(Stopwatch stopwatch)
        {
            if (stopwatch == null)
                return 0f;

            stopwatch.Stop();
            return (float)stopwatch.Elapsed.TotalMilliseconds;
        }

        private static void RecordCachedSeedCounts(
            IEnumerable<CandidateSeed> seeds,
            IGenerationProfiler profiler)
        {
            if (profiler is not { IsEnabled: true } || seeds == null)
                return;

            Dictionary<PlacementType, int> counts = new();

            foreach (CandidateSeed seed in seeds)
            {
                counts.TryGetValue(seed.PlacementType, out int count);
                counts[seed.PlacementType] = count + 1;
            }

            foreach (KeyValuePair<PlacementType, int> entry in counts)
                profiler.RecordCandidateSeeds(entry.Key, entry.Value);
        }

        private static IEnumerable<ICandidateProvider> CreateProviders(
            GenerationContext context,
            PlacementTarget? targets,
            int requestedCount = -1,
            int minimumCandidateCount = -1,
            int candidateCount = -1,
            IReadOnlyList<AssetDefinition> assets = null)
        {
            yield return new PlacementTargetCandidateProvider(
                targets ?? context.PlacementTargets,
                requestedCount,
                minimumCandidateCount,
                candidateCount,
                assets);
        }

        private static bool ShouldUseLazy(GenerationContext context) =>
            context != null &&
            context.StyleSettings.algorithm is SamplingAlgorithm.Random or SamplingAlgorithm.BridsonPoissonDisk;

        private static int GetLazyBatchCandidateCount(int requestedCount) =>
            Mathf.Max(
                LazyMinimumBatchCandidateCount,
                Mathf.Max(1, requestedCount) * LazyBatchCandidateMultiplier);

        private static int GetLazyMaxBatchCount(int candidateBudget, int batchCandidateCount) =>
            Mathf.Clamp(
                Mathf.CeilToInt(candidateBudget / (float)Mathf.Max(1, batchCandidateCount)),
                1,
                LazyMaxBatchCount);

        private static int GetCandidateBudget(
            GenerationContext context,
            int requestedCount,
            int minimumCandidateCount) =>
            context.StyleSettings.candidates.GetBudget(requestedCount, minimumCandidateCount);

        private static int GetMinimumCandidateCount(GenerationContext context, int requestedCount)
        {
            int rootCount = Mathf.Max(1, context.Count);
            return Mathf.CeilToInt(context.StyleSettings.candidates.minimumCount * (requestedCount / (float)rootCount));
        }

        private static List<PlacementType> GetPlacementTypes(PlacementTarget targets)
        {
            List<PlacementType> result = new();

            if ((targets & PlacementTarget.Floor) != 0)
                result.Add(PlacementType.Floor);

            if ((targets & PlacementTarget.Wall) != 0)
                result.Add(PlacementType.Wall);

            if ((targets & PlacementTarget.Ceiling) != 0)
                result.Add(PlacementType.Ceiling);

            if ((targets & PlacementTarget.InsideSpace) != 0)
                result.Add(PlacementType.InsideSpace);

            return result;
        }

        private static int GetRequestedObjectCount(
            GenerationContext context,
            PlacementType placementType,
            IReadOnlyList<PlacementType> placementTypes)
        {
            if (placementTypes == null || placementTypes.Count <= 1)
                return Mathf.Max(1, context.Count);

            if (context.TargetDistributionMode != TargetDistributionMode.Weighted)
                return Mathf.CeilToInt(context.Count / (float)placementTypes.Count);

            int targetWeight = GetWeight(context, placementType);
            int totalWeight = 0;

            foreach (PlacementType type in placementTypes)
                totalWeight += GetWeight(context, type);

            if (targetWeight <= 0 || totalWeight <= 0)
                return Mathf.CeilToInt(context.Count / (float)placementTypes.Count);

            return Mathf.CeilToInt(context.Count * (targetWeight / (float)totalWeight));
        }

        private static int GetWeight(GenerationContext context, PlacementType placementType) =>
            context.TargetDistributionMode == TargetDistributionMode.Weighted
                ? context.TargetDistributionWeights.GetWeight(ToPlacementTarget(placementType))
                : 1;

        private static PlacementTarget ToPlacementTarget(PlacementType placementType) =>
            placementType switch
            {
                PlacementType.Floor => PlacementTarget.Floor,
                PlacementType.Wall => PlacementTarget.Wall,
                PlacementType.Ceiling => PlacementTarget.Ceiling,
                PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                _ => PlacementTarget.None
            };

        private static string CreateCacheKey(
            GenerationContext context,
            PlacementTarget? targets,
            IReadOnlyList<AssetDefinition> assets)
        {
            if (context == null || !context.UseFixedSeed)
                return string.Empty;

            Bounds bounds = context.TargetBounds;
            Genix.Styles.StyleSettings settings = context.StyleSettings;

            PlacementTarget selectedTargets = (targets ?? context.PlacementTargets) & PlacementTarget.All;
            string supportSamplingKey = (selectedTargets & PlacementTarget.Floor) != 0
                ? SupportSurfaceSampling.CreateCacheKey(context, assets, PlacementType.Floor)
                : "support:not-used";

            return string.Join("|",
                context.Area.SourceInfo.SourceId,
                context.Area.SourceInfo.SourceName,
                targets ?? context.PlacementTargets,
                context.TargetDistributionMode,
                context.TargetDistributionWeights.Floor,
                context.TargetDistributionWeights.Wall,
                context.TargetDistributionWeights.Ceiling,
                context.TargetDistributionWeights.InsideSpace,
                context.Count,
                context.RandomSeed,
                context.Area.SurfaceSettingsCacheKey,
                VectorKey(bounds.center),
                VectorKey(bounds.size),
                settings.algorithm,
                settings.candidates.multiplier,
                settings.candidates.minimumCount,
                settings.candidates.shuffle,
                FloatKey(settings.grid.cellSize),
                FloatKey(settings.grid.jitterAmount),
                settings.cluster.count,
                FloatKey(settings.cluster.radius),
                settings.cluster.useMinCenterDistance,
                FloatKey(settings.cluster.minCenterDistance),
                FloatKey(settings.poisson.minDistance),
                settings.poisson.attempts,
                supportSamplingKey);
        }

        private static string VectorKey(Vector3 value) =>
            $"{FloatKey(value.x)},{FloatKey(value.y)},{FloatKey(value.z)}";

        private static int FloatKey(float value) => Mathf.RoundToInt(value * 10_000f);
    }

    /// <summary>Immutable cached seeds plus the random-stream state immediately after their creation.</summary>
    internal sealed class CandidateSeedCacheEntry
    {
        public IReadOnlyList<CandidateSeed> Seeds { get; }
        public ulong RandomStateAfterGeneration { get; }

        public CandidateSeedCacheEntry(IEnumerable<CandidateSeed> seeds, ulong randomStateAfterGeneration)
        {
            Seeds = new List<CandidateSeed>(seeds);
            RandomStateAfterGeneration = randomStateAfterGeneration;
        }
    }

    /// <summary>Small process-local LRU cache for deterministic candidate generation results.</summary>
    internal static class CandidateSeedCache
    {
        private const int MaxEntries = 32;
        private static readonly Dictionary<string, CandidateSeedCacheEntry> Entries = new();

        public static bool TryGet(string key, out CandidateSeedCacheEntry entry)
        {
            if (string.IsNullOrEmpty(key))
            {
                entry = null;
                return false;
            }

            return Entries.TryGetValue(key, out entry);
        }

        public static void Store(string key, IEnumerable<CandidateSeed> seeds, ulong randomState)
        {
            if (string.IsNullOrEmpty(key) || seeds == null)
                return;

            if (Entries.Count >= MaxEntries && !Entries.ContainsKey(key))
                Entries.Clear();

            Entries[key] = new CandidateSeedCacheEntry(seeds, randomState);
        }

        public static void Clear() => Entries.Clear();
    }
}
