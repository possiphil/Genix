using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Styles;
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

    /// <summary>Serializable relative-placement subset supported by unattended scene benchmarks.</summary>
    [Serializable]
    public sealed class BenchmarkRelativePlacement
    {
        [SerializeField] private RelativePlacementSource source = RelativePlacementSource.None;
        [SerializeField, Min(0.01f)] private float radius = 2f;
        [SerializeField] private LayerMask sceneLayers = ~0;

        /// <summary>Creates immutable runtime relative-placement settings.</summary>
        public RelativePlacementSettings CreateSettings() => new(source, radius, sceneLayers, Array.Empty<Transform>());
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
        [SerializeField] private AssetPool assetPool = null;
        [SerializeField] private StylePreset stylePreset = null;
        [SerializeField] private PlacementTarget placementTargets = PlacementTarget.InsideSpace;
        [SerializeField] private TargetDistributionMode targetDistributionMode = TargetDistributionMode.Random;
        [SerializeField] private TargetDistributionWeights targetDistributionWeights = new(1, 1, 1, 1);
        [SerializeField] private AreaBuildSettings areaBuildSettings = new(
            AreaDecompositionMode.Precise,
            ~0,
            surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);
        [SerializeField] private BenchmarkRelativePlacement relativePlacement = new();
        [SerializeField] private bool bestEffort = true;
        [SerializeField] private List<int> objectCounts = new() { 100, 1000, 5000, 10000 };

        /// <summary>Indicates whether the scenario participates in Run All Enabled.</summary>
        public bool Enabled => enabled;
        /// <summary>Gets the scenario label.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Unnamed Scenario" : displayName.Trim();
        /// <summary>Gets the referenced benchmark scene.</summary>
        public SceneAsset Scene => scene;
        /// <summary>Gets the target-area provider identifier.</summary>
        public string AreaProviderId => areaProviderId ?? string.Empty;
        /// <summary>Gets the stable target identifier, or an empty value when the scene has exactly one target.</summary>
        public string TargetId => targetId ?? string.Empty;
        /// <summary>Gets the configured asset pool.</summary>
        public AssetPool AssetPool => assetPool;
        /// <summary>Gets the configured style preset.</summary>
        public StylePreset StylePreset => stylePreset;
        /// <summary>Gets the selected placement targets.</summary>
        public PlacementTarget PlacementTargets => placementTargets & PlacementTarget.All;
        /// <summary>Gets target distribution mode.</summary>
        public TargetDistributionMode TargetDistributionMode => targetDistributionMode;
        /// <summary>Gets target distribution weights.</summary>
        public TargetDistributionWeights TargetDistributionWeights => targetDistributionWeights;
        /// <summary>Gets area construction settings.</summary>
        public AreaBuildSettings AreaBuildSettings => areaBuildSettings;
        /// <summary>Gets relative placement settings.</summary>
        public RelativePlacementSettings RelativePlacement => relativePlacement?.CreateSettings() ?? RelativePlacementSettings.Disabled;
        /// <summary>Indicates whether valid partial plans are retained.</summary>
        public bool BestEffort => bestEffort;
        /// <summary>Gets requested object-count variants.</summary>
        public IReadOnlyList<int> ObjectCounts => objectCounts;

        /// <summary>Creates a scenario initialized for an evaluation scene.</summary>
        public static GenerationBenchmarkScenario Create(string name, SceneAsset sceneAsset)
        {
            GenerationBenchmarkScenario scenario = new()
            {
                displayName = string.IsNullOrWhiteSpace(name) ? "Benchmark Scenario" : name.Trim(),
                scene = sceneAsset,
                targetDistributionWeights = TargetDistributionWeights.Default,
                areaBuildSettings = new AreaBuildSettings(
                    AreaDecompositionMode.Precise,
                    ~0,
                    surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume)
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
        public void AddScenario(string scenarioName, SceneAsset sceneAsset)
        {
            scenarios.Add(GenerationBenchmarkScenario.Create(scenarioName, sceneAsset));
        }

        /// <summary>Removes a benchmark scenario by index.</summary>
        public void RemoveScenarioAt(int index)
        {
            if (index >= 0 && index < scenarios.Count)
                scenarios.RemoveAt(index);
        }

        private void OnEnable()
        {
            EnsureSeeds();
        }

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
