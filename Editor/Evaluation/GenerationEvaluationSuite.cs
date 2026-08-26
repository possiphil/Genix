using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Core;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Evaluation
{
    /// <summary>Purpose of one reproducible evaluation scenario.</summary>
    public enum EvaluationScenarioKind
    {
        /// <summary>Isolates one architectural capability or semantic mechanism.</summary>
        Isolated,
        /// <summary>Exercises several capabilities together in an integrated authored scene.</summary>
        RealWorld,
        /// <summary>Validates functional integrity under a performance-oriented workload.</summary>
        Performance
    }

    /// <summary>Automatic assertions applied to one generated evaluation result.</summary>
    [Flags]
    public enum EvaluationCheckSet
    {
        /// <summary>Performs no automatic quality assertion.</summary>
        None = 0,
        /// <summary>Requires generation to produce at least the configured completion ratio.</summary>
        Completion = 1 << 0,
        /// <summary>Requires generated metadata and asset definitions for every produced object.</summary>
        Metadata = 1 << 1,
        /// <summary>Requires generated placement bounds to remain inside the target volume.</summary>
        TargetContainment = 1 << 2,
        /// <summary>Requires generated object bounds and reserved clearances not to overlap.</summary>
        NonOverlap = 1 << 3,
        /// <summary>Requires every generated asset to use a compatible semantic support surface.</summary>
        SupportSemantics = 1 << 4,
        /// <summary>Requires configured generated-asset relations and per-anchor cardinalities.</summary>
        AssetRelations = 1 << 5,
        /// <summary>Requires generated placements to remain outside active exclusion regions.</summary>
        ExclusionRegions = 1 << 6,
        /// <summary>Requires asset and shared tag placement limits to be respected.</summary>
        PlacementLimits = 1 << 7,
        /// <summary>Requires the target adapter to use authoritative spatial data instead of a degraded fallback.</summary>
        SpatialSourceIntegrity = 1 << 8,
        /// <summary>Requires every result to satisfy the configured global relative-placement radius.</summary>
        RelativePlacement = 1 << 9,
        /// <summary>Requires accepted sample positions to preserve the configured Poisson minimum distance.</summary>
        SamplingSpacing = 1 << 10,
        /// <summary>Applies every structural check supported by the evaluator.</summary>
        AllStructural = Completion | Metadata | TargetContainment | NonOverlap |
                        SupportSemantics | AssetRelations | ExclusionRegions | PlacementLimits |
                        SpatialSourceIntegrity
    }

    /// <summary>One scene, generator preset, target, and assertion configuration.</summary>
    [Serializable]
    public sealed class GenerationEvaluationScenario
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool ready = true;
        [SerializeField] private string displayName = "Evaluation Scenario";
        [SerializeField] private EvaluationScenarioKind kind;
        [SerializeField] private SceneAsset scene;
        [SerializeField] private string areaProviderId = "space-foundation";
        [SerializeField] private string targetId;
        [SerializeField] private GenerationPreset generationPreset;
        [SerializeField] private EvaluationCheckSet checks = EvaluationCheckSet.AllStructural;
        [SerializeField, Range(0f, 1f)] private float minimumCompletionRatio = 1f;
        [SerializeField, Range(0f, 1f)] private float maximumCompletionRatio = 1f;
        [SerializeField] private bool saveLayouts = true;

        /// <summary>Indicates whether Run All includes this scenario.</summary>
        public bool Enabled => enabled;
        /// <summary>Indicates whether scene authoring is complete enough for final evaluation.</summary>
        public bool Ready => ready;
        /// <summary>Gets the designer-facing scenario name.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Unnamed Scenario" : displayName.Trim();
        /// <summary>Gets the methodological scenario category.</summary>
        public EvaluationScenarioKind Kind => kind;
        /// <summary>Gets the scene opened before each group of runs.</summary>
        public SceneAsset Scene => scene;
        /// <summary>Gets the installed target-area provider identifier.</summary>
        public string AreaProviderId => areaProviderId ?? string.Empty;
        /// <summary>Gets the stable target identifier, or an empty value for a single-target scene.</summary>
        public string TargetId => targetId ?? string.Empty;
        /// <summary>Gets the complete generator settings used for every seed.</summary>
        public GenerationPreset GenerationPreset => generationPreset;
        /// <summary>Gets enabled automatic quality assertions.</summary>
        public EvaluationCheckSet Checks => checks;
        /// <summary>Gets the minimum accepted placed/requested ratio.</summary>
        public float MinimumCompletionRatio => Mathf.Clamp01(minimumCompletionRatio);
        /// <summary>Gets the maximum accepted placed/requested ratio for deliberately capacity-limited scenarios.</summary>
        public float MaximumCompletionRatio => Mathf.Clamp(maximumCompletionRatio, MinimumCompletionRatio, 1f);
        /// <summary>Indicates whether every run is captured as a Saved Layout.</summary>
        public bool SaveLayouts => saveLayouts;

        /// <summary>Creates a scenario with thesis-oriented defaults.</summary>
        public static GenerationEvaluationScenario Create(
            string name,
            EvaluationScenarioKind scenarioKind,
            SceneAsset sceneAsset,
            GenerationPreset preset,
            string targetIdentifier = "",
            bool isReady = true,
            EvaluationCheckSet enabledChecks = EvaluationCheckSet.AllStructural,
            float completionRatio = 1f,
            float maximumCompletion = 1f,
            bool persistLayouts = true)
        {
            return new GenerationEvaluationScenario
            {
                displayName = string.IsNullOrWhiteSpace(name) ? "Evaluation Scenario" : name.Trim(),
                kind = scenarioKind,
                scene = sceneAsset,
                generationPreset = preset,
                targetId = targetIdentifier ?? string.Empty,
                ready = isReady,
                checks = enabledChecks,
                saveLayouts = persistLayouts,
                minimumCompletionRatio = Mathf.Clamp01(completionRatio),
                maximumCompletionRatio = Mathf.Clamp(maximumCompletion, Mathf.Clamp01(completionRatio), 1f)
            };
        }
    }

    /// <summary>Persistent, reproducible definition of one complete Genix quality evaluation.</summary>
    [CreateAssetMenu(menuName = "Genix/Evaluation Suite", fileName = "GenerationEvaluationSuite")]
    public sealed class GenerationEvaluationSuite : ScriptableObject
    {
        [SerializeField, Min(1)] private int runsPerScenario = 20;
        [SerializeField, Min(0)] private int settleFrames = 2;
        [SerializeField] private List<int> seeds = new();
        [SerializeField] private List<GenerationEvaluationScenario> scenarios = new();

        /// <summary>Gets independent deterministic runs performed per scenario.</summary>
        public int RunsPerScenario => Mathf.Max(1, runsPerScenario);
        /// <summary>Gets editor frames excluded after opening a scene.</summary>
        public int SettleFrames => Mathf.Max(0, settleFrames);
        /// <summary>Gets the shared deterministic seed sample.</summary>
        public IReadOnlyList<int> Seeds => seeds;
        /// <summary>Gets configured evaluation scenarios.</summary>
        public IReadOnlyList<GenerationEvaluationScenario> Scenarios => scenarios;

        /// <summary>Adds one scenario to the suite.</summary>
        public void AddScenario(
            string scenarioName,
            EvaluationScenarioKind kind,
            SceneAsset scene,
            GenerationPreset preset,
            string targetId = "",
            bool ready = true,
            EvaluationCheckSet checks = EvaluationCheckSet.AllStructural,
            float minimumCompletionRatio = 1f,
            float maximumCompletionRatio = 1f,
            bool saveLayouts = true)
        {
            scenarios.Add(GenerationEvaluationScenario.Create(
                scenarioName,
                kind,
                scene,
                preset,
                targetId,
                ready,
                checks,
                minimumCompletionRatio,
                maximumCompletionRatio,
                saveLayouts));
        }

        /// <summary>Replaces the scenario list while retaining the reproducible seed sample.</summary>
        public void ClearScenarios() => scenarios.Clear();

        /// <summary>Replaces the complete deterministic campaign configuration.</summary>
        /// <param name="runCount">Number of independent runs performed for every scenario.</param>
        /// <param name="sceneSettleFrames">Editor frames excluded after loading each scene.</param>
        /// <param name="deterministicSeeds">One unique seed for every configured run.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="runCount"/> is below one.</exception>
        /// <exception cref="ArgumentException">Thrown when the seed sample has the wrong size or contains duplicates.</exception>
        public void ConfigureCampaign(
            int runCount,
            int sceneSettleFrames,
            IEnumerable<int> deterministicSeeds)
        {
            if (runCount < 1)
                throw new ArgumentOutOfRangeException(nameof(runCount), "An evaluation campaign needs at least one run.");

            int[] configuredSeeds = deterministicSeeds?.ToArray() ?? Array.Empty<int>();
            if (configuredSeeds.Length != runCount)
            {
                throw new ArgumentException(
                    $"Expected exactly {runCount} deterministic seeds, but received {configuredSeeds.Length}.",
                    nameof(deterministicSeeds));
            }

            if (configuredSeeds.Distinct().Count() != configuredSeeds.Length)
                throw new ArgumentException("Deterministic evaluation seeds must be unique.", nameof(deterministicSeeds));

            runsPerScenario = runCount;
            settleFrames = Mathf.Max(0, sceneSettleFrames);
            seeds = new List<int>(configuredSeeds);
        }

        /// <summary>Removes one scenario by index.</summary>
        public void RemoveScenarioAt(int index)
        {
            if (index >= 0 && index < scenarios.Count)
                scenarios.RemoveAt(index);
        }

        private void OnEnable() => EnsureSeeds();

        private void OnValidate()
        {
            runsPerScenario = Mathf.Max(1, runsPerScenario);
            settleFrames = Mathf.Max(0, settleFrames);
            EnsureSeeds();
        }

        private void EnsureSeeds()
        {
            seeds ??= new List<int>();
            scenarios ??= new List<GenerationEvaluationScenario>();
            uint state = 0x6D2B79F5u;

            while (seeds.Count < RunsPerScenario)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                seeds.Add(unchecked((int)state));
            }
        }
    }
}
