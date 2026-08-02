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
    /// <summary>Stores a serializable generation profile.</summary>
    public sealed class GenerationProfileReport : ScriptableObject
    {
        [SerializeField, HideInInspector] private bool _initialized;
        [SerializeField, HideInInspector] private string _createdAt;
        [SerializeField, HideInInspector] private string _runId;
        [SerializeField, HideInInspector] private string _targetName;
        [SerializeField, HideInInspector] private string _styleName;
        [SerializeField, HideInInspector] private string _runType;
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

        /// <summary>Gets created at.</summary>
        public string CreatedAt => _createdAt;
        /// <summary>Gets run id.</summary>
        public string RunId => _runId;
        /// <summary>Gets target name.</summary>
        public string TargetName => _targetName;
        /// <summary>Gets style name.</summary>
        public string StyleName => _styleName;
        /// <summary>Gets run type.</summary>
        public string RunType => _runType;
        /// <summary>Gets placement targets.</summary>
        public string PlacementTargets => _placementTargets;
        /// <summary>Gets distribution mode.</summary>
        public string DistributionMode => _distributionMode;
        /// <summary>Gets sampling algorithm.</summary>
        public string SamplingAlgorithm => _samplingAlgorithm;
        /// <summary>Gets candidate source.</summary>
        public string CandidateSource => _candidateSource;
        /// <summary>Gets stop reason.</summary>
        public string StopReason => _stopReason;
        /// <summary>Gets the number of requested object items.</summary>
        public int RequestedObjectCount => _requestedObjectCount;
        /// <summary>Gets the number of placed object items.</summary>
        public int PlacedObjectCount => _placedObjectCount;
        /// <summary>Gets random seed.</summary>
        public int RandomSeed => _randomSeed;
        /// <summary>Indicates whether fixed seed.</summary>
        public bool UseFixedSeed => _useRandomSeed;
        /// <summary>Gets the measured planning unattributed time in milliseconds.</summary>
        public float PlanningUnattributedMilliseconds => _planningUnattributedMilliseconds;
        /// <summary>Indicates whether managed runtime stats.</summary>
        public bool HasManagedRuntimeStats => _hasManagedRuntimeStats;
        /// <summary>Gets garbage collections gen0.</summary>
        public int GarbageCollectionsGen0 => _garbageCollectionsGen0;
        /// <summary>Gets garbage collections gen1.</summary>
        public int GarbageCollectionsGen1 => _garbageCollectionsGen1;
        /// <summary>Gets garbage collections gen2.</summary>
        public int GarbageCollectionsGen2 => _garbageCollectionsGen2;
        /// <summary>Gets managed memory before bytes.</summary>
        public long ManagedMemoryBeforeBytes => _managedMemoryBeforeBytes;
        /// <summary>Gets managed memory after bytes.</summary>
        public long ManagedMemoryAfterBytes => _managedMemoryAfterBytes;
        /// <summary>Gets managed memory delta bytes.</summary>
        public long ManagedMemoryDeltaBytes => _managedMemoryAfterBytes - _managedMemoryBeforeBytes;
        /// <summary>Gets area build steps.</summary>
        public IReadOnlyList<AreaBuildStepEntry> AreaBuildSteps =>
            _areaBuildSteps != null ? _areaBuildSteps : Array.Empty<AreaBuildStepEntry>();
        /// <summary>Gets planning steps.</summary>
        public IReadOnlyList<PlanningStepEntry> PlanningSteps =>
            _planningSteps != null ? _planningSteps : Array.Empty<PlanningStepEntry>();
        /// <summary>Gets targets.</summary>
        public IReadOnlyList<TargetEntry> Targets => _targets;

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
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
            _placementTargets = profile.PlacementTargets;
            _distributionMode = profile.DistributionMode;
            _samplingAlgorithm = profile.SamplingAlgorithm;
            _candidateSource = GetCandidateSource(profile);
            _stopReason = profile.StopReason;
            _requestedObjectCount = profile.RequestedObjectCount;
            _placedObjectCount = profile.PlacedObjectCount;
            _randomSeed = profile.RandomSeed;
            _useRandomSeed = profile.UseFixedSeed;
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

        /// <summary>Returns phase time.</summary>
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

        /// <summary>Stores one serialized area build step measurement.</summary>
        [Serializable]
        public sealed class AreaBuildStepEntry
        {
            [SerializeField] private string step;
            [SerializeField] private float milliseconds;
            [SerializeField] private int calls;

            /// <summary>Gets step.</summary>
            public string Step => step;
            /// <summary>Gets the measured  time in milliseconds.</summary>
            public float Milliseconds => milliseconds;
            /// <summary>Gets the number of recorded  calls.</summary>
            public int Calls => calls;

            /// <summary>Initializes a new instance of area build step entry.</summary>
            public AreaBuildStepEntry(string step, float milliseconds, int calls)
            {
                this.step = step;
                this.milliseconds = Mathf.Max(0f, milliseconds);
                this.calls = Mathf.Max(0, calls);
            }
        }

        /// <summary>Stores one serialized phase measurement.</summary>
        [Serializable]
        public sealed class PhaseEntry
        {
            [SerializeField] private GenerationProfilePhase phase;
            [SerializeField] private float milliseconds;

            /// <summary>Gets phase.</summary>
            public GenerationProfilePhase Phase => phase;
            /// <summary>Gets the measured  time in milliseconds.</summary>
            public float Milliseconds => milliseconds;

            /// <summary>Initializes a new instance of phase entry.</summary>
            public PhaseEntry(GenerationProfilePhase phase, float milliseconds)
            {
                this.phase = phase;
                this.milliseconds = Mathf.Max(0f, milliseconds);
            }
        }

        /// <summary>Stores one serialized planning step measurement.</summary>
        [Serializable]
        public sealed class PlanningStepEntry
        {
            [SerializeField] private string step;
            [SerializeField] private float milliseconds;
            [SerializeField] private int calls;

            /// <summary>Gets step.</summary>
            public string Step => step;
            /// <summary>Gets the measured  time in milliseconds.</summary>
            public float Milliseconds => milliseconds;
            /// <summary>Gets the number of recorded  calls.</summary>
            public int Calls => calls;

            /// <summary>Initializes a new instance of planning step entry.</summary>
            public PlanningStepEntry(string step, float milliseconds, int calls)
            {
                this.step = step;
                this.milliseconds = Mathf.Max(0f, milliseconds);
                this.calls = Mathf.Max(0, calls);
            }
        }

        /// <summary>Stores one serialized target measurement.</summary>
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

            /// <summary>Gets placement type.</summary>
            public string PlacementType => placementType;
            /// <summary>Gets raw samples.</summary>
            public int RawSamples => rawSamples;
            /// <summary>Gets candidate seeds.</summary>
            public int CandidateSeeds => candidateSeeds;
            /// <summary>Gets tested seeds.</summary>
            public int TestedSeeds => testedSeeds;
            /// <summary>Gets projection attempts.</summary>
            public int ProjectionAttempts => projectionAttempts;
            /// <summary>Gets projection hits.</summary>
            public int ProjectionHits => projectionHits;
            /// <summary>Gets projection misses.</summary>
            public int ProjectionMisses => projectionMisses;
            /// <summary>Gets the number of recorded raycast calls.</summary>
            public int RaycastCalls => raycastCalls;
            /// <summary>Gets raycast hits.</summary>
            public int RaycastHits => raycastHits;
            /// <summary>Gets asset attempts.</summary>
            public int AssetAttempts => assetAttempts;
            /// <summary>Gets accepted attempts.</summary>
            public int AcceptedAttempts => acceptedAttempts;
            /// <summary>Gets rejected attempts.</summary>
            public int RejectedAttempts => rejectedAttempts;
            /// <summary>Gets the measured seed generation time in milliseconds.</summary>
            public float SeedGenerationMilliseconds => seedGenerationMilliseconds;
            /// <summary>Gets the measured sampling time in milliseconds.</summary>
            public float SamplingMilliseconds => samplingMilliseconds;
            /// <summary>Gets the measured projection time in milliseconds.</summary>
            public float ProjectionMilliseconds => projectionMilliseconds;
            /// <summary>Gets the measured raycast time in milliseconds.</summary>
            public float RaycastMilliseconds => raycastMilliseconds;
            /// <summary>Gets the measured validation time in milliseconds.</summary>
            public float ValidationMilliseconds => validationMilliseconds;
            /// <summary>Gets validation steps.</summary>
            public IReadOnlyList<ValidationStepEntry> ValidationSteps =>
                validationSteps != null ? validationSteps : Array.Empty<ValidationStepEntry>();

            /// <summary>Gets rejections.</summary>
            public IReadOnlyList<RejectionEntry> Rejections =>
                rejections != null ? rejections : Array.Empty<RejectionEntry>();

            /// <summary>Initializes a new instance of target entry.</summary>
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

        /// <summary>Stores one serialized validation step measurement.</summary>
        [Serializable]
        public sealed class ValidationStepEntry
        {
            [SerializeField] private string step;
            [SerializeField] private float milliseconds;
            [SerializeField] private int calls;

            /// <summary>Gets step.</summary>
            public string Step => step;
            /// <summary>Gets the measured  time in milliseconds.</summary>
            public float Milliseconds => milliseconds;
            /// <summary>Gets the number of recorded  calls.</summary>
            public int Calls => calls;

            /// <summary>Initializes a new instance of validation step entry.</summary>
            public ValidationStepEntry(string step, float milliseconds, int calls)
            {
                this.step = step;
                this.milliseconds = Mathf.Max(0f, milliseconds);
                this.calls = Mathf.Max(0, calls);
            }
        }

        /// <summary>Stores one serialized rejection measurement.</summary>
        [Serializable]
        public sealed class RejectionEntry
        {
            [SerializeField] private string reason;
            [SerializeField] private int count;

            /// <summary>Gets reason.</summary>
            public string Reason => reason;
            /// <summary>Gets the number of stored items.</summary>
            public int Count => count;

            /// <summary>Initializes a new instance of rejection entry.</summary>
            public RejectionEntry(string reason, int count)
            {
                this.reason = reason;
                this.count = Mathf.Max(0, count);
            }
        }
    }
}
