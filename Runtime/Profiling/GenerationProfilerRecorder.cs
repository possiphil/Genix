using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;

namespace Genix.Profiling
{
    /// <summary>Accumulates phase timings and counters for a profiled generation run.</summary>
    public sealed class GenerationProfilerRecorder : IGenerationProfiler
    {
        /// <summary>Indicates whether enabled.</summary>
        public bool IsEnabled => true;
        /// <summary>Gets profile.</summary>
        public GenerationProfile Profile { get; } = new();

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(GenerationContext context, string styleName, bool dryRun) =>
            Profile.Initialize(context, styleName, dryRun);

        /// <summary>Adds phase time.</summary>
        public void AddPhaseTime(GenerationProfilePhase phase, float milliseconds) =>
            Profile.AddPhaseTime(phase, milliseconds);

        /// <summary>Records area build step.</summary>
        public void RecordAreaBuildStep(AreaBuildProfileStep step, float milliseconds, int calls = 1) =>
            Profile.AddAreaBuildStep(step, milliseconds, calls);

        /// <summary>Records planning step.</summary>
        public void RecordPlanningStep(PlanningProfileStep step, float milliseconds, int calls = 1) =>
            Profile.AddPlanningStep(step, milliseconds, calls);

        /// <summary>Records candidate cache hit.</summary>
        public void RecordCandidateCacheHit(bool cacheHit) =>
            Profile.CandidateCacheHit = cacheHit;

        /// <summary>Records raw samples.</summary>
        public void RecordRawSamples(PlacementType placementType, int count) =>
            Profile.GetTarget(placementType).AddRawSamples(count);

        /// <summary>Records candidate seeds.</summary>
        public void RecordCandidateSeeds(PlacementType placementType, int count) =>
            Profile.GetTarget(placementType).AddCandidateSeeds(count);

        /// <summary>Records tested seed.</summary>
        public void RecordTestedSeed(PlacementType placementType) =>
            Profile.GetTarget(placementType).AddTestedSeed();

        /// <summary>Adds seed generation time.</summary>
        public void AddSeedGenerationTime(PlacementType placementType, float milliseconds) =>
            Profile.GetTarget(placementType).AddSeedGenerationTime(milliseconds);

        /// <summary>Adds sampling time.</summary>
        public void AddSamplingTime(PlacementType placementType, float milliseconds) =>
            Profile.GetTarget(placementType).AddSamplingTime(milliseconds);

        /// <summary>Records projection.</summary>
        public void RecordProjection(PlacementType placementType, bool hit, float milliseconds) =>
            Profile.GetTarget(placementType).AddProjection(hit, milliseconds);

        /// <summary>Records raycast.</summary>
        public void RecordRaycast(PlacementType placementType, int hitCount, float milliseconds) =>
            Profile.GetTarget(placementType).AddRaycast(hitCount, milliseconds);

        /// <summary>Records asset attempt.</summary>
        public void RecordAssetAttempt(PlacementType placementType, bool accepted, RejectionReason rejectionReason, float validationMilliseconds)
        {
            Profile.GetTarget(placementType).AddAssetAttempt(accepted, rejectionReason, validationMilliseconds);
            Profile.AddPlanningStep(PlanningProfileStep.CandidateValidation, validationMilliseconds);
        }

        /// <summary>Records validation step.</summary>
        public void RecordValidationStep(PlacementType placementType, ValidationProfileStep step, float milliseconds) =>
            Profile.GetTarget(placementType).AddValidationStep(step, milliseconds);
    }

    /// <summary>Provides a no-op profiler for generation runs that do not collect performance data.</summary>
    public sealed class NullGenerationProfiler : IGenerationProfiler
    {
        /// <summary>Gets instance.</summary>
        public static NullGenerationProfiler Instance { get; } = new();

        /// <summary>Indicates whether enabled.</summary>
        public bool IsEnabled => false;
        /// <summary>Gets profile.</summary>
        public GenerationProfile Profile { get; } = new();

        private NullGenerationProfiler()
        {
        }

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(GenerationContext context, string styleName, bool dryRun) { }
        /// <summary>Adds phase time.</summary>
        public void AddPhaseTime(GenerationProfilePhase phase, float milliseconds) { }
        /// <summary>Records area build step.</summary>
        public void RecordAreaBuildStep(AreaBuildProfileStep step, float milliseconds, int calls = 1) { }
        /// <summary>Records planning step.</summary>
        public void RecordPlanningStep(PlanningProfileStep step, float milliseconds, int calls = 1) { }
        /// <summary>Records candidate cache hit.</summary>
        public void RecordCandidateCacheHit(bool cacheHit) { }
        /// <summary>Records raw samples.</summary>
        public void RecordRawSamples(PlacementType placementType, int count) { }
        /// <summary>Records candidate seeds.</summary>
        public void RecordCandidateSeeds(PlacementType placementType, int count) { }
        /// <summary>Records tested seed.</summary>
        public void RecordTestedSeed(PlacementType placementType) { }
        /// <summary>Adds seed generation time.</summary>
        public void AddSeedGenerationTime(PlacementType placementType, float milliseconds) { }
        /// <summary>Adds sampling time.</summary>
        public void AddSamplingTime(PlacementType placementType, float milliseconds) { }
        /// <summary>Records projection.</summary>
        public void RecordProjection(PlacementType placementType, bool hit, float milliseconds) { }
        /// <summary>Records raycast.</summary>
        public void RecordRaycast(PlacementType placementType, int hitCount, float milliseconds) { }
        /// <summary>Records asset attempt.</summary>
        public void RecordAssetAttempt(PlacementType placementType, bool accepted, RejectionReason rejectionReason, float validationMilliseconds) { }
        /// <summary>Records validation step.</summary>
        public void RecordValidationStep(PlacementType placementType, ValidationProfileStep step, float milliseconds) { }
    }
}
