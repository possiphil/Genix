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
    /// <summary>Available generation profile phase values.</summary>
    public enum GenerationProfilePhase
    {
        /// <summary>Identifies the asset filter profiler step.</summary>
        AssetFilter,
        /// <summary>Identifies the area build profiler step.</summary>
        AreaBuild,
        /// <summary>Identifies the candidate generation profiler step.</summary>
        CandidateGeneration,
        /// <summary>Identifies the planning profiler step.</summary>
        Planning,
        /// <summary>Identifies the apply profiler step.</summary>
        Apply,
        /// <summary>Identifies the total profiler step.</summary>
        Total,
        /// <summary>Identifies the context setup profiler step.</summary>
        ContextSetup,
        /// <summary>Identifies the preview plan copy profiler step.</summary>
        PreviewPlanCopy,
        /// <summary>Identifies the preview diagnostics handoff profiler step.</summary>
        PreviewDiagnosticsHandoff,
        /// <summary>Identifies the preview cleanup profiler step.</summary>
        PreviewCleanup,
        /// <summary>Identifies the preview log profiler step.</summary>
        PreviewLog
    }

    /// <summary>Available validation profile step values.</summary>
    public enum ValidationProfileStep
    {
        /// <summary>Identifies the height profiler step.</summary>
        Height,
        /// <summary>Identifies the planned spacing profiler step.</summary>
        PlannedSpacing,
        /// <summary>Identifies the surface fit profiler step.</summary>
        SurfaceFit,
        /// <summary>Identifies the footprint profiler step.</summary>
        Footprint,
        /// <summary>Identifies the volume profiler step.</summary>
        Volume,
        /// <summary>Identifies the generated overlap profiler step.</summary>
        GeneratedOverlap,
        /// <summary>Identifies the fixed overlap profiler step.</summary>
        FixedOverlap,
        /// <summary>Identifies the fixed spacing profiler step.</summary>
        FixedSpacing,
        /// <summary>Identifies the generated scene spacing profiler step.</summary>
        GeneratedSceneSpacing,
        /// <summary>Identifies the relative profiler step.</summary>
        Relative
    }

    /// <summary>Available planning profile step values.</summary>
    public enum PlanningProfileStep
    {
        /// <summary>Identifies the usable target selection profiler step.</summary>
        UsableTargetSelection,
        /// <summary>Identifies the target selection profiler step.</summary>
        TargetSelection,
        /// <summary>Identifies the asset catalog profiler step.</summary>
        AssetCatalog,
        /// <summary>Identifies the asset order profiler step.</summary>
        AssetOrder,
        /// <summary>Identifies the asset pruning profiler step.</summary>
        AssetPruning,
        /// <summary>Identifies the candidate iteration profiler step.</summary>
        CandidateIteration,
        /// <summary>Identifies the candidate build profiler step.</summary>
        CandidateBuild,
        /// <summary>Identifies the candidate validation profiler step.</summary>
        CandidateValidation,
        /// <summary>Identifies the diagnostics recording profiler step.</summary>
        DiagnosticsRecording,
        /// <summary>Identifies the object naming profiler step.</summary>
        ObjectNaming,
        /// <summary>Identifies the plan recording profiler step.</summary>
        PlanRecording,
        /// <summary>Identifies the target budget recording profiler step.</summary>
        TargetBudgetRecording
    }

    /// <summary>Stores generation measurements.</summary>
    public sealed class GenerationProfile
    {
        private readonly Dictionary<GenerationProfilePhase, float> _phaseTimes = new();
        private readonly Dictionary<AreaBuildProfileStep, GenerationAreaBuildStepProfile> _areaBuildSteps = new();
        private readonly Dictionary<PlanningProfileStep, GenerationPlanningStepProfile> _planningSteps = new();
        private readonly Dictionary<PlacementType, GenerationTargetProfile> _targets = new();

        /// <summary>Gets run id.</summary>
        public string RunId { get; } = Guid.NewGuid().ToString();
        /// <summary>Gets created at.</summary>
        public DateTime CreatedAt { get; } = DateTime.Now;

        /// <summary>Gets target name.</summary>
        public string TargetName { get; private set; } = string.Empty;
        /// <summary>Gets style name.</summary>
        public string StyleName { get; private set; } = string.Empty;
        /// <summary>Gets run type.</summary>
        public string RunType { get; private set; } = string.Empty;
        /// <summary>Gets placement targets.</summary>
        public string PlacementTargets { get; private set; } = string.Empty;
        /// <summary>Gets distribution mode.</summary>
        public string DistributionMode { get; private set; } = string.Empty;
        /// <summary>Gets sampling algorithm.</summary>
        public string SamplingAlgorithm { get; private set; } = string.Empty;
        /// <summary>Gets the number of requested object items.</summary>
        public int RequestedObjectCount { get; private set; }
        /// <summary>Gets the number of placed object items.</summary>
        public int PlacedObjectCount { get; set; }
        /// <summary>Gets random seed.</summary>
        public int RandomSeed { get; private set; }
        /// <summary>Indicates whether fixed seed.</summary>
        public bool UseFixedSeed { get; private set; }
        /// <summary>Indicates whether candidate cache hit.</summary>
        public bool CandidateCacheHit { get; set; }
        /// <summary>Gets stop reason.</summary>
        public string StopReason { get; set; } = string.Empty;
        /// <summary>Gets the measured planning unattributed time in milliseconds.</summary>
        public float PlanningUnattributedMilliseconds { get; private set; }
        /// <summary>Indicates whether managed runtime stats.</summary>
        public bool HasManagedRuntimeStats { get; private set; }
        /// <summary>Gets garbage collections gen0.</summary>
        public int GarbageCollectionsGen0 { get; private set; }
        /// <summary>Gets garbage collections gen1.</summary>
        public int GarbageCollectionsGen1 { get; private set; }
        /// <summary>Gets garbage collections gen2.</summary>
        public int GarbageCollectionsGen2 { get; private set; }
        /// <summary>Gets managed memory before bytes.</summary>
        public long ManagedMemoryBeforeBytes { get; private set; }
        /// <summary>Gets managed memory after bytes.</summary>
        public long ManagedMemoryAfterBytes { get; private set; }
        /// <summary>Gets managed memory delta bytes.</summary>
        public long ManagedMemoryDeltaBytes => ManagedMemoryAfterBytes - ManagedMemoryBeforeBytes;

        /// <summary>Gets phase times.</summary>
        public IReadOnlyDictionary<GenerationProfilePhase, float> PhaseTimes => _phaseTimes;
        /// <summary>Gets area build steps.</summary>
        public IReadOnlyCollection<GenerationAreaBuildStepProfile> AreaBuildSteps => _areaBuildSteps.Values;
        /// <summary>Gets planning steps.</summary>
        public IReadOnlyCollection<GenerationPlanningStepProfile> PlanningSteps => _planningSteps.Values;
        /// <summary>Gets targets.</summary>
        public IReadOnlyCollection<GenerationTargetProfile> Targets => _targets.Values;

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(GenerationContext context, string styleName, bool dryRun)
        {
            if (context == null)
                return;

            TargetName = context.Area.SourceInfo.SourceName;
            StyleName = string.IsNullOrWhiteSpace(styleName)
                ? context.StyleSettings.algorithm.ToAlgorithmName()
                : styleName;
            RunType = dryRun ? "Preview Run" : "Generation";
            PlacementTargets = FormatPlacementTargets(context.PlacementTargets);
            DistributionMode = context.TargetDistributionMode.ToDisplayName();
            SamplingAlgorithm = context.StyleSettings.algorithm.ToAlgorithmName();
            RequestedObjectCount = context.Count;
            RandomSeed = context.RandomSeed;
            UseFixedSeed = context.UseFixedSeed;
        }

        /// <summary>Adds phase time.</summary>
        public void AddPhaseTime(GenerationProfilePhase phase, float milliseconds)
        {
            if (!_phaseTimes.TryGetValue(phase, out float existing))
                existing = 0f;

            _phaseTimes[phase] = existing + Mathf.Max(0f, milliseconds);
        }

        /// <summary>Returns phase time.</summary>
        public float GetPhaseTime(GenerationProfilePhase phase) =>
            _phaseTimes.TryGetValue(phase, out float milliseconds) ? milliseconds : 0f;

        /// <summary>Adds area build step.</summary>
        public void AddAreaBuildStep(AreaBuildProfileStep step, float milliseconds, int calls = 1)
        {
            if (!_areaBuildSteps.TryGetValue(step, out GenerationAreaBuildStepProfile profile))
            {
                profile = new GenerationAreaBuildStepProfile(step);
                _areaBuildSteps[step] = profile;
            }

            profile.Add(milliseconds, calls);
        }

        /// <summary>Returns sorted area build steps.</summary>
        public IEnumerable<GenerationAreaBuildStepProfile> GetSortedAreaBuildSteps() =>
            AreaBuildSteps.OrderByDescending(step => step.Milliseconds);

        /// <summary>Adds planning step.</summary>
        public void AddPlanningStep(PlanningProfileStep step, float milliseconds, int calls = 1)
        {
            if (!_planningSteps.TryGetValue(step, out GenerationPlanningStepProfile profile))
            {
                profile = new GenerationPlanningStepProfile(step);
                _planningSteps[step] = profile;
            }

            profile.Add(milliseconds, calls);
        }

        /// <summary>Returns sorted planning steps.</summary>
        public IEnumerable<GenerationPlanningStepProfile> GetSortedPlanningSteps() =>
            PlanningSteps.OrderByDescending(step => step.Milliseconds);

        /// <summary>Returns target.</summary>
        public GenerationTargetProfile GetTarget(PlacementType placementType)
        {
            if (_targets.TryGetValue(placementType, out GenerationTargetProfile target))
                return target;

            target = new GenerationTargetProfile(placementType);
            _targets[placementType] = target;
            return target;
        }

        /// <summary>Returns sorted targets.</summary>
        public IEnumerable<GenerationTargetProfile> GetSortedTargets() =>
            Targets.OrderBy(target => target.PlacementType);

        /// <summary>Records planning unattributed time.</summary>
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

        /// <summary>Records managed runtime stats.</summary>
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

    /// <summary>Stores generation area build step measurements.</summary>
    public sealed class GenerationAreaBuildStepProfile
    {
        /// <summary>Gets step.</summary>
        public AreaBuildProfileStep Step { get; }
        /// <summary>Gets the number of recorded  calls.</summary>
        public int Calls { get; private set; }
        /// <summary>Gets the measured  time in milliseconds.</summary>
        public float Milliseconds { get; private set; }

        /// <summary>Initializes a new instance of generation area build step profile.</summary>
        public GenerationAreaBuildStepProfile(AreaBuildProfileStep step)
        {
            Step = step;
        }

        /// <summary>Adds .</summary>
        public void Add(float milliseconds, int calls)
        {
            Calls += Mathf.Max(0, calls);
            Milliseconds += Mathf.Max(0f, milliseconds);
        }
    }

    /// <summary>Stores generation planning step measurements.</summary>
    public sealed class GenerationPlanningStepProfile
    {
        /// <summary>Gets step.</summary>
        public PlanningProfileStep Step { get; }
        /// <summary>Gets the number of recorded  calls.</summary>
        public int Calls { get; private set; }
        /// <summary>Gets the measured  time in milliseconds.</summary>
        public float Milliseconds { get; private set; }

        /// <summary>Initializes a new instance of generation planning step profile.</summary>
        public GenerationPlanningStepProfile(PlanningProfileStep step)
        {
            Step = step;
        }

        /// <summary>Adds .</summary>
        public void Add(float milliseconds, int calls)
        {
            Calls += Mathf.Max(0, calls);
            Milliseconds += Mathf.Max(0f, milliseconds);
        }
    }

    /// <summary>Stores generation target measurements.</summary>
    public sealed class GenerationTargetProfile
    {
        private readonly Dictionary<RejectionReason, int> _rejectionCounts = new();
        private readonly Dictionary<ValidationProfileStep, ValidationStepProfile> _validationSteps = new();

        /// <summary>Gets placement type.</summary>
        public PlacementType PlacementType { get; }
        /// <summary>Gets raw samples.</summary>
        public int RawSamples { get; private set; }
        /// <summary>Gets candidate seeds.</summary>
        public int CandidateSeeds { get; private set; }
        /// <summary>Gets tested seeds.</summary>
        public int TestedSeeds { get; private set; }
        /// <summary>Gets projection attempts.</summary>
        public int ProjectionAttempts { get; private set; }
        /// <summary>Gets projection hits.</summary>
        public int ProjectionHits { get; private set; }
        /// <summary>Gets projection misses.</summary>
        public int ProjectionMisses { get; private set; }
        /// <summary>Gets the number of recorded raycast calls.</summary>
        public int RaycastCalls { get; private set; }
        /// <summary>Gets raycast hits.</summary>
        public int RaycastHits { get; private set; }
        /// <summary>Gets asset attempts.</summary>
        public int AssetAttempts { get; private set; }
        /// <summary>Gets accepted attempts.</summary>
        public int AcceptedAttempts { get; private set; }
        /// <summary>Gets rejected attempts.</summary>
        public int RejectedAttempts { get; private set; }
        /// <summary>Gets the measured seed generation time in milliseconds.</summary>
        public float SeedGenerationMilliseconds { get; private set; }
        /// <summary>Gets the measured sampling time in milliseconds.</summary>
        public float SamplingMilliseconds { get; private set; }
        /// <summary>Gets the measured projection time in milliseconds.</summary>
        public float ProjectionMilliseconds { get; private set; }
        /// <summary>Gets the measured raycast time in milliseconds.</summary>
        public float RaycastMilliseconds { get; private set; }
        /// <summary>Gets the measured validation time in milliseconds.</summary>
        public float ValidationMilliseconds { get; private set; }
        /// <summary>Gets rejection counts.</summary>
        public IReadOnlyDictionary<RejectionReason, int> RejectionCounts => _rejectionCounts;
        /// <summary>Gets validation steps.</summary>
        public IReadOnlyCollection<ValidationStepProfile> ValidationSteps => _validationSteps.Values;

        /// <summary>Initializes a new instance of generation target profile.</summary>
        public GenerationTargetProfile(PlacementType placementType)
        {
            PlacementType = placementType;
        }

        /// <summary>Adds raw samples.</summary>
        public void AddRawSamples(int count) => RawSamples += Mathf.Max(0, count);
        /// <summary>Adds candidate seeds.</summary>
        public void AddCandidateSeeds(int count) => CandidateSeeds += Mathf.Max(0, count);
        /// <summary>Adds tested seed.</summary>
        public void AddTestedSeed() => TestedSeeds++;
        /// <summary>Adds seed generation time.</summary>
        public void AddSeedGenerationTime(float milliseconds) => SeedGenerationMilliseconds += Mathf.Max(0f, milliseconds);
        /// <summary>Adds sampling time.</summary>
        public void AddSamplingTime(float milliseconds) => SamplingMilliseconds += Mathf.Max(0f, milliseconds);

        /// <summary>Adds projection.</summary>
        public void AddProjection(bool hit, float milliseconds)
        {
            ProjectionAttempts++;

            if (hit)
                ProjectionHits++;
            else
                ProjectionMisses++;

            ProjectionMilliseconds += Mathf.Max(0f, milliseconds);
        }

        /// <summary>Adds raycast.</summary>
        public void AddRaycast(int hitCount, float milliseconds)
        {
            RaycastCalls++;
            RaycastHits += Mathf.Max(0, hitCount);
            RaycastMilliseconds += Mathf.Max(0f, milliseconds);
        }

        /// <summary>Adds asset attempt.</summary>
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

        /// <summary>Adds validation step.</summary>
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

    /// <summary>Stores validation step measurements.</summary>
    public sealed class ValidationStepProfile
    {
        /// <summary>Gets step.</summary>
        public ValidationProfileStep Step { get; }
        /// <summary>Gets the number of recorded  calls.</summary>
        public int Calls { get; private set; }
        /// <summary>Gets the measured  time in milliseconds.</summary>
        public float Milliseconds { get; private set; }

        /// <summary>Initializes a new instance of validation step profile.</summary>
        public ValidationStepProfile(ValidationProfileStep step)
        {
            Step = step;
        }

        /// <summary>Adds .</summary>
        public void Add(float milliseconds)
        {
            Calls++;
            Milliseconds += Mathf.Max(0f, milliseconds);
        }
    }
}
