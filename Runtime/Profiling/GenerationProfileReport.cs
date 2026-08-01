using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Genix.Areas;
using Genix.Extensions;
using Genix.Placement;
using UnityEngine;

namespace Genix.Profiling
{
    public sealed class GenerationProfileReport : ScriptableObject
    {
        [SerializeField, HideInInspector] private bool _initialized;
        [SerializeField, HideInInspector] private string _createdAt;
        [SerializeField, HideInInspector] private string _runId;
        [SerializeField, HideInInspector] private string _targetName;
        [SerializeField, HideInInspector] private string _styleName;
        [SerializeField, HideInInspector] private string _runType;
        [SerializeField, HideInInspector] private string _generationMode;
        [SerializeField, HideInInspector] private string _performanceMode;
        [SerializeField, HideInInspector] private string _placementTargets;
        [SerializeField, HideInInspector] private string _distributionMode;
        [SerializeField, HideInInspector] private string _samplingAlgorithm;
        [SerializeField, HideInInspector] private string _candidateSource;
        [SerializeField, HideInInspector] private string _stopReason;
        [SerializeField, HideInInspector] private int _requestedObjectCount;
        [SerializeField, HideInInspector] private int _placedObjectCount;
        [SerializeField, HideInInspector] private int _randomSeed;
        [SerializeField, HideInInspector] private bool _useRandomSeed;
        [SerializeField, HideInInspector] private float _planningUnattributedMilliseconds;
        [SerializeField, HideInInspector] private bool _hasManagedRuntimeStats;
        [SerializeField, HideInInspector] private int _garbageCollectionsGen0;
        [SerializeField, HideInInspector] private int _garbageCollectionsGen1;
        [SerializeField, HideInInspector] private int _garbageCollectionsGen2;
        [SerializeField, HideInInspector] private long _managedMemoryBeforeBytes;
        [SerializeField, HideInInspector] private long _managedMemoryAfterBytes;

        [SerializeField, HideInInspector] private List<PhaseEntry> _phases = new();
        [SerializeField, HideInInspector] private List<AreaBuildStepEntry> _areaBuildSteps = new();
        [SerializeField, HideInInspector] private List<PlanningStepEntry> _planningSteps = new();
        [SerializeField, HideInInspector] private List<TargetEntry> _targets = new();

        public string CreatedAt => _createdAt;
        public string RunId => _runId;
        public string TargetName => _targetName;
        public string StyleName => _styleName;
        public string RunType => _runType;
        public string GenerationMode => _generationMode;
        public string PerformanceMode => _performanceMode;
        public string PlacementTargets => _placementTargets;
        public string DistributionMode => _distributionMode;
        public string SamplingAlgorithm => _samplingAlgorithm;
        public string CandidateSource => _candidateSource;
        public string StopReason => _stopReason;
        public int RequestedObjectCount => _requestedObjectCount;
        public int PlacedObjectCount => _placedObjectCount;
        public int RandomSeed => _randomSeed;
        public bool UseRandomSeed => _useRandomSeed;
        public float PlanningUnattributedMilliseconds => _planningUnattributedMilliseconds;
        public bool HasManagedRuntimeStats => _hasManagedRuntimeStats;
        public int GarbageCollectionsGen0 => _garbageCollectionsGen0;
        public int GarbageCollectionsGen1 => _garbageCollectionsGen1;
        public int GarbageCollectionsGen2 => _garbageCollectionsGen2;
        public long ManagedMemoryBeforeBytes => _managedMemoryBeforeBytes;
        public long ManagedMemoryAfterBytes => _managedMemoryAfterBytes;
        public long ManagedMemoryDeltaBytes => _managedMemoryAfterBytes - _managedMemoryBeforeBytes;
        public IReadOnlyList<AreaBuildStepEntry> AreaBuildSteps =>
            _areaBuildSteps != null ? _areaBuildSteps : Array.Empty<AreaBuildStepEntry>();
        public IReadOnlyList<PlanningStepEntry> PlanningSteps =>
            _planningSteps != null ? _planningSteps : Array.Empty<PlanningStepEntry>();
        public IReadOnlyList<TargetEntry> Targets => _targets;

        public void Initialize(GenerationProfile profile, DateTime createdAt)
        {
            if (_initialized)
                throw new InvalidOperationException("This profile report has already been initialized.");

            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            _initialized = true;
            _createdAt = createdAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            _runId = profile.RunId;
            _targetName = profile.TargetName;
            _styleName = profile.StyleName;
            _runType = profile.RunType;
            _generationMode = profile.GenerationMode;
            _performanceMode = profile.PerformanceMode;
            _placementTargets = profile.PlacementTargets;
            _distributionMode = profile.DistributionMode;
            _samplingAlgorithm = profile.SamplingAlgorithm;
            _candidateSource = GetCandidateSource(profile);
            _stopReason = profile.StopReason;
            _requestedObjectCount = profile.RequestedObjectCount;
            _placedObjectCount = profile.PlacedObjectCount;
            _randomSeed = profile.RandomSeed;
            _useRandomSeed = profile.UseRandomSeed;
            _planningUnattributedMilliseconds = profile.PlanningUnattributedMilliseconds;
            _hasManagedRuntimeStats = profile.HasManagedRuntimeStats;
            _garbageCollectionsGen0 = profile.GarbageCollectionsGen0;
            _garbageCollectionsGen1 = profile.GarbageCollectionsGen1;
            _garbageCollectionsGen2 = profile.GarbageCollectionsGen2;
            _managedMemoryBeforeBytes = profile.ManagedMemoryBeforeBytes;
            _managedMemoryAfterBytes = profile.ManagedMemoryAfterBytes;

            _phases = profile.PhaseTimes
                .OrderBy(entry => entry.Key)
                .Select(entry => new PhaseEntry(entry.Key, entry.Value))
                .ToList();
            _areaBuildSteps = profile.GetSortedAreaBuildSteps()
                .Select(step => new AreaBuildStepEntry(step.Step.ToString(), step.Milliseconds, step.Calls))
                .ToList();
            _planningSteps = profile.GetSortedPlanningSteps()
                .Select(step => new PlanningStepEntry(step.Step.ToString(), step.Milliseconds, step.Calls))
                .ToList();
            _targets = profile.GetSortedTargets()
                .Select(target => new TargetEntry(target))
                .ToList();
        }

        public float GetPhaseTime(GenerationProfilePhase phase)
        {
            foreach (PhaseEntry entry in _phases)
            {
                if (entry.Phase == phase)
                    return entry.Milliseconds;
            }

            return 0f;
        }

        private static string GetCandidateSource(GenerationProfile profile)
        {
            bool hasCandidateData =
                profile.GetPhaseTime(GenerationProfilePhase.CandidateGeneration) > 0f ||
                profile.Targets.Count > 0;

            if (!hasCandidateData)
                return "Not reached";

            return profile.CandidateCacheHit ? "Cache" : "Generated";
        }

        [Serializable]
        public sealed class AreaBuildStepEntry
        {
            [SerializeField] private string step;
            [SerializeField] private float milliseconds;
            [SerializeField] private int calls;

            public string Step => step;
            public float Milliseconds => milliseconds;
            public int Calls => calls;

            public AreaBuildStepEntry(string step, float milliseconds, int calls)
            {
                this.step = step;
                this.milliseconds = Mathf.Max(0f, milliseconds);
                this.calls = Mathf.Max(0, calls);
            }
        }

        [Serializable]
        public sealed class PhaseEntry
        {
            [SerializeField] private GenerationProfilePhase phase;
            [SerializeField] private float milliseconds;

            public GenerationProfilePhase Phase => phase;
            public float Milliseconds => milliseconds;

            public PhaseEntry(GenerationProfilePhase phase, float milliseconds)
            {
                this.phase = phase;
                this.milliseconds = Mathf.Max(0f, milliseconds);
            }
        }

        [Serializable]
        public sealed class PlanningStepEntry
        {
            [SerializeField] private string step;
            [SerializeField] private float milliseconds;
            [SerializeField] private int calls;

            public string Step => step;
            public float Milliseconds => milliseconds;
            public int Calls => calls;

            public PlanningStepEntry(string step, float milliseconds, int calls)
            {
                this.step = step;
                this.milliseconds = Mathf.Max(0f, milliseconds);
                this.calls = Mathf.Max(0, calls);
            }
        }

        [Serializable]
        public sealed class TargetEntry
        {
            [SerializeField] private string placementType;
            [SerializeField] private int rawSamples;
            [SerializeField] private int candidateSeeds;
            [SerializeField] private int testedSeeds;
            [SerializeField] private int projectionAttempts;
            [SerializeField] private int projectionHits;
            [SerializeField] private int projectionMisses;
            [SerializeField] private int raycastCalls;
            [SerializeField] private int raycastHits;
            [SerializeField] private int assetAttempts;
            [SerializeField] private int acceptedAttempts;
            [SerializeField] private int rejectedAttempts;
            [SerializeField] private float seedGenerationMilliseconds;
            [SerializeField] private float samplingMilliseconds;
            [SerializeField] private float projectionMilliseconds;
            [SerializeField] private float raycastMilliseconds;
            [SerializeField] private float validationMilliseconds;
            [SerializeField] private List<ValidationStepEntry> validationSteps = new();
            [SerializeField] private List<RejectionEntry> rejections = new();

            public string PlacementType => placementType;
            public int RawSamples => rawSamples;
            public int CandidateSeeds => candidateSeeds;
            public int TestedSeeds => testedSeeds;
            public int ProjectionAttempts => projectionAttempts;
            public int ProjectionHits => projectionHits;
            public int ProjectionMisses => projectionMisses;
            public int RaycastCalls => raycastCalls;
            public int RaycastHits => raycastHits;
            public int AssetAttempts => assetAttempts;
            public int AcceptedAttempts => acceptedAttempts;
            public int RejectedAttempts => rejectedAttempts;
            public float SeedGenerationMilliseconds => seedGenerationMilliseconds;
            public float SamplingMilliseconds => samplingMilliseconds;
            public float ProjectionMilliseconds => projectionMilliseconds;
            public float RaycastMilliseconds => raycastMilliseconds;
            public float ValidationMilliseconds => validationMilliseconds;
            public IReadOnlyList<ValidationStepEntry> ValidationSteps =>
                validationSteps != null ? validationSteps : Array.Empty<ValidationStepEntry>();

            public IReadOnlyList<RejectionEntry> Rejections =>
                rejections != null ? rejections : Array.Empty<RejectionEntry>();

            public TargetEntry(GenerationTargetProfile target)
            {
                placementType = target.PlacementType.ToDisplayName();
                rawSamples = target.RawSamples;
                candidateSeeds = target.CandidateSeeds;
                testedSeeds = target.TestedSeeds;
                projectionAttempts = target.ProjectionAttempts;
                projectionHits = target.ProjectionHits;
                projectionMisses = target.ProjectionMisses;
                raycastCalls = target.RaycastCalls;
                raycastHits = target.RaycastHits;
                assetAttempts = target.AssetAttempts;
                acceptedAttempts = target.AcceptedAttempts;
                rejectedAttempts = target.RejectedAttempts;
                seedGenerationMilliseconds = target.SeedGenerationMilliseconds;
                samplingMilliseconds = target.SamplingMilliseconds;
                projectionMilliseconds = target.ProjectionMilliseconds;
                raycastMilliseconds = target.RaycastMilliseconds;
                validationMilliseconds = target.ValidationMilliseconds;
                validationSteps = target.ValidationSteps
                    .OrderBy(entry => entry.Step)
                    .Select(entry => new ValidationStepEntry(
                        entry.Step.ToString(),
                        entry.Milliseconds,
                        entry.Calls))
                    .ToList();
                rejections = target.RejectionCounts
                    .OrderByDescending(entry => entry.Value)
                    .Select(entry => new RejectionEntry(entry.Key.ToDisplayName(), entry.Value))
                    .ToList();
            }
        }

        [Serializable]
        public sealed class ValidationStepEntry
        {
            [SerializeField] private string step;
            [SerializeField] private float milliseconds;
            [SerializeField] private int calls;

            public string Step => step;
            public float Milliseconds => milliseconds;
            public int Calls => calls;

            public ValidationStepEntry(string step, float milliseconds, int calls)
            {
                this.step = step;
                this.milliseconds = Mathf.Max(0f, milliseconds);
                this.calls = Mathf.Max(0, calls);
            }
        }

        [Serializable]
        public sealed class RejectionEntry
        {
            [SerializeField] private string reason;
            [SerializeField] private int count;

            public string Reason => reason;
            public int Count => count;

            public RejectionEntry(string reason, int count)
            {
                this.reason = reason;
                this.count = Mathf.Max(0, count);
            }
        }
    }
}
