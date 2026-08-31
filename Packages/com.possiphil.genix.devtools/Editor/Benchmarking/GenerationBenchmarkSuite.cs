using System;
using System.Collections.Generic;
using Genix.Core;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Benchmarking
{
    /// <summary>Cache states measured by a scene benchmark campaign.</summary>
    [Flags]
    public enum BenchmarkCacheCondition
    {
        /// <summary>Clears Genix area, candidate, and scene-index caches before every measured run.</summary>
        Cold = 1,
        /// <summary>Primes reusable caches once and then measures runs with changing seeds.</summary>
        Warm = 2
    }

    /// <summary>Measurement variants emitted by a benchmark campaign.</summary>
    [Flags]
    public enum BenchmarkMeasurementKind
    {
        /// <summary>Measures the generation core with one external timer and no detailed profiler.</summary>
        Primary = 1,
        /// <summary>Repeats the same cases with detailed phase instrumentation enabled.</summary>
        Diagnostic = 2
    }

    internal static class BenchmarkMeasurementDisplay
    {
        public static string Name(BenchmarkMeasurementKind measurement) => measurement switch
        {
            BenchmarkMeasurementKind.Primary => "Runtime",
            BenchmarkMeasurementKind.Diagnostic => "Phase breakdown",
            _ => measurement.ToString()
        };

        public static string Name(string serializedMeasurement) =>
            Enum.TryParse(serializedMeasurement, out BenchmarkMeasurementKind measurement)
                ? Name(measurement)
                : serializedMeasurement ?? string.Empty;
    }

    /// <summary>One scene and generation configuration expanded across counts, seeds, and cache states.</summary>
    [Serializable]
    public sealed class GenerationBenchmarkScenario
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private string displayName = "Benchmark Scenario";
        [SerializeField] private SceneAsset scene;
        [SerializeField] private string areaProviderId = "space-foundation";
        [SerializeField] private string targetId = string.Empty;
        [SerializeField] private GenerationPreset generationPreset;
        [SerializeField] private List<int> objectCounts = new() { 100, 1000, 10000 };

        /// <summary>Indicates whether the full suite includes this scenario.</summary>
        public bool Enabled => enabled;
        /// <summary>Gets the scenario label.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Unnamed Scenario" : displayName.Trim();
        /// <summary>Gets the referenced benchmark scene.</summary>
        public SceneAsset Scene => scene;
        /// <summary>Gets the target-area provider identifier.</summary>
        public string AreaProviderId => areaProviderId ?? string.Empty;
        /// <summary>Gets the stable target identifier, or an empty value when the scene has exactly one target.</summary>
        public string TargetId => targetId ?? string.Empty;
        /// <summary>Gets the complete designer configuration measured by this scenario.</summary>
        public GenerationPreset GenerationPreset => generationPreset;
        /// <summary>Gets requested object-count variants.</summary>
        public IReadOnlyList<int> ObjectCounts => objectCounts;

        /// <summary>Creates a scenario initialized for an evaluation scene.</summary>
        public static GenerationBenchmarkScenario Create(
            string name,
            SceneAsset sceneAsset,
            GenerationPreset preset = null)
        {
            GenerationBenchmarkScenario scenario = new()
            {
                displayName = string.IsNullOrWhiteSpace(name) ? "Benchmark Scenario" : name.Trim(),
                scene = sceneAsset,
                generationPreset = preset
            };
            return scenario;
        }
    }

    /// <summary>Persistent, reproducible definition of one complete Genix benchmark campaign.</summary>
    [CreateAssetMenu(menuName = "Genix/Benchmark Suite", fileName = "GenerationBenchmarkSuite")]
    public sealed class GenerationBenchmarkSuite : ScriptableObject
    {
        [SerializeField] private BenchmarkCacheCondition cacheConditions = BenchmarkCacheCondition.Cold | BenchmarkCacheCondition.Warm;
        [SerializeField] private BenchmarkMeasurementKind measurements = BenchmarkMeasurementKind.Primary | BenchmarkMeasurementKind.Diagnostic;
        [SerializeField, Min(1)] private int coldSeedCount = 10;
        [SerializeField, Min(1)] private int warmSeedCount = 30;
        [SerializeField, Min(0)] private int warmupRuns = 1;
        [SerializeField, Min(1)] private int repetitions = 1;
        [SerializeField, Min(0)] private int settleFrames = 2;
        [SerializeField] private List<int> seeds = new();
        [SerializeField] private List<GenerationBenchmarkScenario> scenarios = new();

        /// <summary>Gets selected cache conditions.</summary>
        public BenchmarkCacheCondition CacheConditions => cacheConditions;
        /// <summary>Gets selected measurement variants.</summary>
        public BenchmarkMeasurementKind Measurements => measurements;
        /// <summary>Gets the number of cold-cache seeds used from the shared seed list.</summary>
        public int ColdSeedCount => Mathf.Max(1, coldSeedCount);
        /// <summary>Gets the number of warm-cache seeds used from the shared seed list.</summary>
        public int WarmSeedCount => Mathf.Max(1, warmSeedCount);
        /// <summary>Gets warm-up runs excluded from reported measurements.</summary>
        public int WarmupRuns => Mathf.Max(0, warmupRuns);
        /// <summary>Gets repetitions per seed.</summary>
        public int Repetitions => Mathf.Max(1, repetitions);
        /// <summary>Gets editor frames allowed to settle after opening a scene.</summary>
        public int SettleFrames => Mathf.Max(0, settleFrames);
        /// <summary>Gets deterministic campaign seeds.</summary>
        public IReadOnlyList<int> Seeds => seeds;
        /// <summary>Gets configured scenarios.</summary>
        public IReadOnlyList<GenerationBenchmarkScenario> Scenarios => scenarios;

        /// <summary>Adds a benchmark scenario for the supplied scene.</summary>
        public void AddScenario(
            string scenarioName,
            SceneAsset sceneAsset,
            GenerationPreset generationPreset = null)
        {
            scenarios.Add(GenerationBenchmarkScenario.Create(scenarioName, sceneAsset, generationPreset));
        }

        /// <summary>Removes a benchmark scenario by index.</summary>
        public void RemoveScenarioAt(int index)
        {
            if (index >= 0 && index < scenarios.Count)
                scenarios.RemoveAt(index);
        }

        private void OnEnable() => EnsureSeeds();

        private void OnValidate()
        {
            coldSeedCount = Mathf.Max(1, coldSeedCount);
            warmSeedCount = Mathf.Max(1, warmSeedCount);
            warmupRuns = Mathf.Max(0, warmupRuns);
            repetitions = Mathf.Max(1, repetitions);
            settleFrames = Mathf.Max(0, settleFrames);
            EnsureSeeds();
        }

        private void EnsureSeeds()
        {
            seeds ??= new List<int>();
            scenarios ??= new List<GenerationBenchmarkScenario>();
            int required = Mathf.Max(ColdSeedCount, WarmSeedCount);
            uint state = 0x9E3779B9u;

            while (seeds.Count < required)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                seeds.Add(unchecked((int)state));
            }
        }
    }
}
