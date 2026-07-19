using System;
using System.Collections.Generic;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Extensions;
using Genix.Placement.Providers;
using Genix.Sampling;
using UnityEngine;

namespace Genix.Placement
{
    internal static class CandidateSeedFactory
    {
        public static List<CandidateSeed> Create(
            GenerationContext context,
            IDiagnosticsSink diagnostics,
            PlacementTarget? targets)
        {
            string cacheKey = CreateCacheKey(context, targets);

            if (CandidateSeedCache.TryGet(cacheKey, out CandidateSeedCacheEntry cached))
            {
                context.Random.State = cached.RandomStateAfterGeneration;
                List<CandidateSeed> copy = new(cached.Seeds);
                diagnostics.RecordCandidatePool(context.Count, copy);
                return copy;
            }

            List<CandidateSeed> seeds = new();

            foreach (ICandidateProvider provider in CreateProviders(context, targets))
                seeds.AddRange(provider.CreateCandidateSeeds(context, diagnostics));

            if (context.StyleSettings.algorithm is SamplingAlgorithm.Grid or SamplingAlgorithm.JitteredGrid)
                context.Random.Shuffle(seeds);

            CandidateSeedCache.Store(cacheKey, seeds, context.Random.State);
            diagnostics.RecordCandidatePool(context.Count, seeds);
            return seeds;
        }

        private static IEnumerable<ICandidateProvider> CreateProviders(
            GenerationContext context,
            PlacementTarget? targets)
        {
            if (context.GenerationMode != GenerationMode.TargetPlacement)
                throw new ArgumentOutOfRangeException(
                    nameof(context.GenerationMode),
                    context.GenerationMode,
                    $"Unsupported generation mode: {context.GenerationMode.ToDisplayName()}.");

            yield return new PlacementTargetCandidateProvider(targets ?? context.PlacementTargets);
        }

        private static string CreateCacheKey(GenerationContext context, PlacementTarget? targets)
        {
            if (context == null || !context.UseRandomSeed)
                return string.Empty;

            Bounds bounds = context.TargetBounds;
            Genix.Styles.StyleSettings settings = context.StyleSettings;

            return string.Join("|",
                context.Area.SourceInfo.SourceId,
                context.Area.SourceInfo.SourceName,
                context.GenerationMode,
                targets ?? context.PlacementTargets,
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
                settings.poisson.attempts);
        }

        private static string VectorKey(Vector3 value) =>
            $"{FloatKey(value.x)},{FloatKey(value.y)},{FloatKey(value.z)}";

        private static int FloatKey(float value) => Mathf.RoundToInt(value * 10_000f);
    }

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
