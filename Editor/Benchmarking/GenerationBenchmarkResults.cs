using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Profiling;
using UnityEngine;

namespace Genix.Editor.Benchmarking
{
    /// <summary>Serializable raw result for one measured generation-core execution.</summary>
    [Serializable]
    internal sealed class GenerationBenchmarkRunRecord
    {
        public string scenario;
        public string scene;
        public string targetId;
        public string cacheCondition;
        public string measurement;
        public int objectCount;
        public int seed;
        public int repetition;
        public double elapsedMilliseconds;
        public bool succeeded;
        public bool complete;
        public int placedCount;
        public string message;
        public string resultHash;
        public bool hasPrimaryReference;
        public bool resultMatchesPrimary;
        public int gcGen0;
        public int gcGen1;
        public int gcGen2;
        public long managedMemoryBefore;
        public long managedMemoryAfter;
        public float assetFilterMilliseconds;
        public float areaBuildMilliseconds;
        public float candidateGenerationMilliseconds;
        public float planningMilliseconds;
        public float samplingMilliseconds;
        public float projectionMilliseconds;
        public float raycastMilliseconds;
        public float validationMilliseconds;
        public int rawSamples;
        public int candidateSeeds;
        public int assetAttempts;
        public int rejectedAttempts;
        public bool candidateCacheHit;

        public static GenerationBenchmarkRunRecord Create(
            GenerationBenchmarkWorkItem item,
            GenerationBenchmarkExecutionResult execution,
            string scenePath,
            string targetId,
            RuntimeSnapshot before,
            RuntimeSnapshot after)
        {
            GenerationBenchmarkRunRecord record = new()
            {
                scenario = item.Scenario.DisplayName,
                scene = scenePath,
                targetId = targetId ?? string.Empty,
                cacheCondition = item.CacheCondition.ToString(),
                measurement = item.Measurement.ToString(),
                objectCount = item.ObjectCount,
                seed = item.Seed,
                repetition = item.Repetition,
                elapsedMilliseconds = execution.ElapsedMilliseconds,
                succeeded = execution.Succeeded,
                complete = execution.Complete,
                placedCount = execution.PlacedCount,
                message = execution.Message ?? string.Empty,
                resultHash = execution.ResultHash ?? string.Empty,
                resultMatchesPrimary = item.Measurement == BenchmarkMeasurementKind.Primary,
                gcGen0 = after.GcGen0 - before.GcGen0,
                gcGen1 = after.GcGen1 - before.GcGen1,
                gcGen2 = after.GcGen2 - before.GcGen2,
                managedMemoryBefore = before.ManagedMemory,
                managedMemoryAfter = after.ManagedMemory
            };

            PopulateProfile(record, execution.Profile);
            return record;
        }

        private static void PopulateProfile(GenerationBenchmarkRunRecord record, GenerationProfile profile)
        {
            if (profile == null)
                return;

            record.assetFilterMilliseconds = profile.GetPhaseTime(GenerationProfilePhase.AssetFilter);
            record.areaBuildMilliseconds = profile.GetPhaseTime(GenerationProfilePhase.AreaBuild);
            record.candidateGenerationMilliseconds = profile.GetPhaseTime(GenerationProfilePhase.CandidateGeneration);
            record.planningMilliseconds = profile.GetPhaseTime(GenerationProfilePhase.Planning);
            record.candidateCacheHit = profile.CandidateCacheHit;

            foreach (GenerationTargetProfile target in profile.Targets)
            {
                record.samplingMilliseconds += target.SamplingMilliseconds;
                record.projectionMilliseconds += target.ProjectionMilliseconds;
                record.raycastMilliseconds += target.RaycastMilliseconds;
                record.validationMilliseconds += target.ValidationMilliseconds;
                record.rawSamples += target.RawSamples;
                record.candidateSeeds += target.CandidateSeeds;
                record.assetAttempts += target.AssetAttempts;
                record.rejectedAttempts += target.RejectedAttempts;
            }
        }
    }

    [Serializable]
    internal sealed class GenerationBenchmarkCampaignResult
    {
        public string suiteName;
        public string suiteAssetPath;
        public string createdAtUtc;
        public string unityVersion;
        public string operatingSystem;
        public string processor;
        public int processorCount;
        public int systemMemoryMb;
        public string graphicsDevice;
        public string projectRevisionHash;
        public List<GenerationBenchmarkRunRecord> runs = new();
    }

    internal readonly struct RuntimeSnapshot
    {
        public int GcGen0 { get; }
        public int GcGen1 { get; }
        public int GcGen2 { get; }
        public long ManagedMemory { get; }

        private RuntimeSnapshot(int gcGen0, int gcGen1, int gcGen2, long managedMemory)
        {
            GcGen0 = gcGen0;
            GcGen1 = gcGen1;
            GcGen2 = gcGen2;
            ManagedMemory = managedMemory;
        }

        public static RuntimeSnapshot Capture() => new(
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            GC.GetTotalMemory(false));
    }

    internal static class GenerationBenchmarkStatistics
    {
        public static double Median(IEnumerable<double> values) => Percentile(values, 0.5);
        public static double LowerQuartile(IEnumerable<double> values) => Percentile(values, 0.25);
        public static double UpperQuartile(IEnumerable<double> values) => Percentile(values, 0.75);
        public static double P95(IEnumerable<double> values) => Percentile(values, 0.95);

        public static double StandardDeviation(IReadOnlyList<double> values)
        {
            if (values.Count <= 1)
                return 0d;

            double mean = values.Average();
            double variance = values.Sum(value => Math.Pow(value - mean, 2d)) / (values.Count - 1);
            return Math.Sqrt(variance);
        }

        private static double Percentile(IEnumerable<double> values, double percentile)
        {
            double[] sorted = values.OrderBy(value => value).ToArray();

            if (sorted.Length == 0)
                return 0d;
            if (sorted.Length == 1)
                return sorted[0];

            double position = (sorted.Length - 1) * percentile;
            int lower = (int)Math.Floor(position);
            int upper = (int)Math.Ceiling(position);
            double fraction = position - lower;
            return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
        }
    }
}
