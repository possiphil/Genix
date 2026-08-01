using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Extensions;
using Genix.Placement;
using Genix.Sampling;
using UnityEngine;

namespace Genix.Profiling
{
    public enum GenerationProfilePhase
    {
        AssetFilter,
        AreaBuild,
        CandidateGeneration,
        Planning,
        Apply,
        Total,
        ContextSetup,
        PreviewPlanCopy,
        PreviewDiagnosticsHandoff,
        PreviewCleanup,
        PreviewLog
    }

    public enum ValidationProfileStep
    {
        Height,
        PlannedSpacing,
        SurfaceFit,
        Footprint,
        Volume,
        GeneratedOverlap,
        FixedOverlap,
        FixedSpacing,
        GeneratedSceneSpacing,
        Relative
    }

    public enum PlanningProfileStep
    {
        UsableTargetSelection,
        TargetSelection,
        AssetCatalog,
        AssetOrder,
        AssetPruning,
        CandidateIteration,
        CandidateBuild,
        CandidateValidation,
        DiagnosticsRecording,
        ObjectNaming,
        PlanRecording,
        TargetBudgetRecording
    }

    public sealed class GenerationProfile
    {
        private readonly Dictionary<GenerationProfilePhase, float> _phaseTimes = new();
        private readonly Dictionary<AreaBuildProfileStep, GenerationAreaBuildStepProfile> _areaBuildSteps = new();
        private readonly Dictionary<PlanningProfileStep, GenerationPlanningStepProfile> _planningSteps = new();
        private readonly Dictionary<PlacementType, GenerationTargetProfile> _targets = new();

        public string RunId { get; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; } = DateTime.Now;

        public string TargetName { get; private set; } = string.Empty;
        public string StyleName { get; private set; } = string.Empty;
        public string RunType { get; private set; } = string.Empty;
        public string GenerationMode { get; private set; } = string.Empty;
        public string PerformanceMode { get; private set; } = string.Empty;
        public string PlacementTargets { get; private set; } = string.Empty;
        public string DistributionMode { get; private set; } = string.Empty;
        public string SamplingAlgorithm { get; private set; } = string.Empty;
        public int RequestedObjectCount { get; private set; }
        public int PlacedObjectCount { get; set; }
        public int RandomSeed { get; private set; }
        public bool UseRandomSeed { get; private set; }
        public bool CandidateCacheHit { get; set; }
        public string StopReason { get; set; } = string.Empty;
        public float PlanningUnattributedMilliseconds { get; private set; }
        public bool HasManagedRuntimeStats { get; private set; }
        public int GarbageCollectionsGen0 { get; private set; }
        public int GarbageCollectionsGen1 { get; private set; }
        public int GarbageCollectionsGen2 { get; private set; }
        public long ManagedMemoryBeforeBytes { get; private set; }
        public long ManagedMemoryAfterBytes { get; private set; }
        public long ManagedMemoryDeltaBytes => ManagedMemoryAfterBytes - ManagedMemoryBeforeBytes;

        public IReadOnlyDictionary<GenerationProfilePhase, float> PhaseTimes => _phaseTimes;
        public IReadOnlyCollection<GenerationAreaBuildStepProfile> AreaBuildSteps => _areaBuildSteps.Values;
        public IReadOnlyCollection<GenerationPlanningStepProfile> PlanningSteps => _planningSteps.Values;
        public IReadOnlyCollection<GenerationTargetProfile> Targets => _targets.Values;

        public void Initialize(GenerationContext context, string styleName, bool dryRun)
        {
            if (context == null)
                return;

            TargetName = context.Area.SourceInfo.SourceName;
            StyleName = string.IsNullOrWhiteSpace(styleName)
                ? context.StyleSettings.algorithm.ToAlgorithmName()
                : styleName;
            RunType = dryRun ? "Preview Run" : "Generation";
            GenerationMode = context.GenerationMode.ToDisplayName();
            PerformanceMode = context.PerformanceMode.ToDisplayName();
            PlacementTargets = FormatPlacementTargets(context.PlacementTargets);
            DistributionMode = context.TargetDistributionMode.ToDisplayName();
            SamplingAlgorithm = context.StyleSettings.algorithm.ToAlgorithmName();
            RequestedObjectCount = context.Count;
            RandomSeed = context.RandomSeed;
            UseRandomSeed = context.UseRandomSeed;
        }

        public void AddPhaseTime(GenerationProfilePhase phase, float milliseconds)
        {
            if (!_phaseTimes.TryGetValue(phase, out float existing))
                existing = 0f;

            _phaseTimes[phase] = existing + Mathf.Max(0f, milliseconds);
        }

        public float GetPhaseTime(GenerationProfilePhase phase) =>
            _phaseTimes.TryGetValue(phase, out float milliseconds) ? milliseconds : 0f;

        public void AddAreaBuildStep(AreaBuildProfileStep step, float milliseconds, int calls = 1)
        {
            if (!_areaBuildSteps.TryGetValue(step, out GenerationAreaBuildStepProfile profile))
            {
                profile = new GenerationAreaBuildStepProfile(step);
                _areaBuildSteps[step] = profile;
            }

            profile.Add(milliseconds, calls);
        }

        public IEnumerable<GenerationAreaBuildStepProfile> GetSortedAreaBuildSteps() =>
            AreaBuildSteps.OrderByDescending(step => step.Milliseconds);

        public void AddPlanningStep(PlanningProfileStep step, float milliseconds, int calls = 1)
        {
            if (!_planningSteps.TryGetValue(step, out GenerationPlanningStepProfile profile))
            {
                profile = new GenerationPlanningStepProfile(step);
                _planningSteps[step] = profile;
            }

            profile.Add(milliseconds, calls);
        }

        public IEnumerable<GenerationPlanningStepProfile> GetSortedPlanningSteps() =>
            PlanningSteps.OrderByDescending(step => step.Milliseconds);

        public GenerationTargetProfile GetTarget(PlacementType placementType)
        {
            if (_targets.TryGetValue(placementType, out GenerationTargetProfile target))
                return target;

            target = new GenerationTargetProfile(placementType);
            _targets[placementType] = target;
            return target;
        }

        public IEnumerable<GenerationTargetProfile> GetSortedTargets() =>
            Targets.OrderBy(target => target.PlacementType);

        public void RecordPlanningUnattributedTime(float planningMilliseconds)
        {
            float attributedMilliseconds = 0f;
            float candidateGenerationMilliseconds = GetPhaseTime(GenerationProfilePhase.CandidateGeneration);

            foreach (GenerationPlanningStepProfile step in _planningSteps.Values)
            {
                attributedMilliseconds += step.Step == PlanningProfileStep.CandidateIteration
                    ? Mathf.Max(0f, step.Milliseconds - candidateGenerationMilliseconds)
                    : step.Milliseconds;
            }

            if (attributedMilliseconds <= 0f)
            {
                foreach (GenerationTargetProfile target in _targets.Values)
                    attributedMilliseconds += target.ValidationMilliseconds;
            }

            PlanningUnattributedMilliseconds = Mathf.Max(
                0f,
                planningMilliseconds - attributedMilliseconds);
        }

        public void RecordManagedRuntimeStats(
            int garbageCollectionsGen0,
            int garbageCollectionsGen1,
            int garbageCollectionsGen2,
            long managedMemoryBeforeBytes,
            long managedMemoryAfterBytes)
        {
            HasManagedRuntimeStats = true;
            GarbageCollectionsGen0 = Mathf.Max(0, garbageCollectionsGen0);
            GarbageCollectionsGen1 = Mathf.Max(0, garbageCollectionsGen1);
            GarbageCollectionsGen2 = Mathf.Max(0, garbageCollectionsGen2);
            ManagedMemoryBeforeBytes = managedMemoryBeforeBytes < 0L ? 0L : managedMemoryBeforeBytes;
            ManagedMemoryAfterBytes = managedMemoryAfterBytes < 0L ? 0L : managedMemoryAfterBytes;
        }

        private static string FormatPlacementTargets(PlacementTarget targets)
        {
            targets &= PlacementTarget.All;

            if (targets == PlacementTarget.All)
                return "Any";

            if (targets == PlacementTarget.None)
                return "None";

            List<string> labels = new();

            if ((targets & PlacementTarget.Floor) != 0)
                labels.Add("Floor");

            if ((targets & PlacementTarget.Wall) != 0)
                labels.Add("Wall");

            if ((targets & PlacementTarget.Ceiling) != 0)
                labels.Add("Ceiling");

            if ((targets & PlacementTarget.InsideSpace) != 0)
                labels.Add("Inside Space");

            return string.Join(", ", labels);
        }
    }

    public sealed class GenerationAreaBuildStepProfile
    {
        public AreaBuildProfileStep Step { get; }
        public int Calls { get; private set; }
        public float Milliseconds { get; private set; }

        public GenerationAreaBuildStepProfile(AreaBuildProfileStep step)
        {
            Step = step;
        }

        public void Add(float milliseconds, int calls)
        {
            Calls += Mathf.Max(0, calls);
            Milliseconds += Mathf.Max(0f, milliseconds);
        }
    }

    public sealed class GenerationPlanningStepProfile
    {
        public PlanningProfileStep Step { get; }
        public int Calls { get; private set; }
        public float Milliseconds { get; private set; }

        public GenerationPlanningStepProfile(PlanningProfileStep step)
        {
            Step = step;
        }

        public void Add(float milliseconds, int calls)
        {
            Calls += Mathf.Max(0, calls);
            Milliseconds += Mathf.Max(0f, milliseconds);
        }
    }

    public sealed class GenerationTargetProfile
    {
        private readonly Dictionary<RejectionReason, int> _rejectionCounts = new();
        private readonly Dictionary<ValidationProfileStep, ValidationStepProfile> _validationSteps = new();

        public PlacementType PlacementType { get; }
        public int RawSamples { get; private set; }
        public int CandidateSeeds { get; private set; }
        public int TestedSeeds { get; private set; }
        public int ProjectionAttempts { get; private set; }
        public int ProjectionHits { get; private set; }
        public int ProjectionMisses { get; private set; }
        public int RaycastCalls { get; private set; }
        public int RaycastHits { get; private set; }
        public int AssetAttempts { get; private set; }
        public int AcceptedAttempts { get; private set; }
        public int RejectedAttempts { get; private set; }
        public float SeedGenerationMilliseconds { get; private set; }
        public float SamplingMilliseconds { get; private set; }
        public float ProjectionMilliseconds { get; private set; }
        public float RaycastMilliseconds { get; private set; }
        public float ValidationMilliseconds { get; private set; }
        public IReadOnlyDictionary<RejectionReason, int> RejectionCounts => _rejectionCounts;
        public IReadOnlyCollection<ValidationStepProfile> ValidationSteps => _validationSteps.Values;

        public GenerationTargetProfile(PlacementType placementType)
        {
            PlacementType = placementType;
        }

        public void AddRawSamples(int count) => RawSamples += Mathf.Max(0, count);
        public void AddCandidateSeeds(int count) => CandidateSeeds += Mathf.Max(0, count);
        public void AddTestedSeed() => TestedSeeds++;
        public void AddSeedGenerationTime(float milliseconds) => SeedGenerationMilliseconds += Mathf.Max(0f, milliseconds);
        public void AddSamplingTime(float milliseconds) => SamplingMilliseconds += Mathf.Max(0f, milliseconds);

        public void AddProjection(bool hit, float milliseconds)
        {
            ProjectionAttempts++;

            if (hit)
                ProjectionHits++;
            else
                ProjectionMisses++;

            ProjectionMilliseconds += Mathf.Max(0f, milliseconds);
        }

        public void AddRaycast(int hitCount, float milliseconds)
        {
            RaycastCalls++;
            RaycastHits += Mathf.Max(0, hitCount);
            RaycastMilliseconds += Mathf.Max(0f, milliseconds);
        }

        public void AddAssetAttempt(bool accepted, RejectionReason rejectionReason, float validationMilliseconds)
        {
            AssetAttempts++;
            ValidationMilliseconds += Mathf.Max(0f, validationMilliseconds);

            if (accepted)
            {
                AcceptedAttempts++;
                return;
            }

            RejectedAttempts++;

            if (!_rejectionCounts.TryGetValue(rejectionReason, out int count))
                count = 0;

            _rejectionCounts[rejectionReason] = count + 1;
        }

        public void AddValidationStep(ValidationProfileStep step, float milliseconds)
        {
            if (!_validationSteps.TryGetValue(step, out ValidationStepProfile profile))
            {
                profile = new ValidationStepProfile(step);
                _validationSteps[step] = profile;
            }

            profile.Add(milliseconds);
        }
    }

    public sealed class ValidationStepProfile
    {
        public ValidationProfileStep Step { get; }
        public int Calls { get; private set; }
        public float Milliseconds { get; private set; }

        public ValidationStepProfile(ValidationProfileStep step)
        {
            Step = step;
        }

        public void Add(float milliseconds)
        {
            Calls++;
            Milliseconds += Mathf.Max(0f, milliseconds);
        }
    }
}
