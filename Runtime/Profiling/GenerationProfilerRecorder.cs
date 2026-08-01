using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;

namespace Genix.Profiling
{
    public sealed class GenerationProfilerRecorder : IGenerationProfiler
    {
        public bool IsEnabled => true;
        public GenerationProfile Profile { get; } = new();

        public void Initialize(GenerationContext context, string styleName, bool dryRun) =>
            Profile.Initialize(context, styleName, dryRun);

        public void AddPhaseTime(GenerationProfilePhase phase, float milliseconds) =>
            Profile.AddPhaseTime(phase, milliseconds);

        public void RecordAreaBuildStep(AreaBuildProfileStep step, float milliseconds, int calls = 1) =>
            Profile.AddAreaBuildStep(step, milliseconds, calls);

        public void RecordPlanningStep(PlanningProfileStep step, float milliseconds, int calls = 1) =>
            Profile.AddPlanningStep(step, milliseconds, calls);

        public void RecordCandidateCacheHit(bool cacheHit) =>
            Profile.CandidateCacheHit = cacheHit;

        public void RecordRawSamples(PlacementType placementType, int count) =>
            Profile.GetTarget(placementType).AddRawSamples(count);

        public void RecordCandidateSeeds(PlacementType placementType, int count) =>
            Profile.GetTarget(placementType).AddCandidateSeeds(count);

        public void RecordTestedSeed(PlacementType placementType) =>
            Profile.GetTarget(placementType).AddTestedSeed();

        public void AddSeedGenerationTime(PlacementType placementType, float milliseconds) =>
            Profile.GetTarget(placementType).AddSeedGenerationTime(milliseconds);

        public void AddSamplingTime(PlacementType placementType, float milliseconds) =>
            Profile.GetTarget(placementType).AddSamplingTime(milliseconds);

        public void RecordProjection(PlacementType placementType, bool hit, float milliseconds) =>
            Profile.GetTarget(placementType).AddProjection(hit, milliseconds);

        public void RecordRaycast(PlacementType placementType, int hitCount, float milliseconds) =>
            Profile.GetTarget(placementType).AddRaycast(hitCount, milliseconds);

        public void RecordAssetAttempt(PlacementType placementType, bool accepted, RejectionReason rejectionReason, float validationMilliseconds)
        {
            Profile.GetTarget(placementType).AddAssetAttempt(accepted, rejectionReason, validationMilliseconds);
            Profile.AddPlanningStep(PlanningProfileStep.CandidateValidation, validationMilliseconds);
        }

        public void RecordValidationStep(PlacementType placementType, ValidationProfileStep step, float milliseconds) =>
            Profile.GetTarget(placementType).AddValidationStep(step, milliseconds);
    }

    public sealed class NullGenerationProfiler : IGenerationProfiler
    {
        public static NullGenerationProfiler Instance { get; } = new();

        public bool IsEnabled => false;
        public GenerationProfile Profile { get; } = new();

        private NullGenerationProfiler()
        {
        }

        public void Initialize(GenerationContext context, string styleName, bool dryRun) { }
        public void AddPhaseTime(GenerationProfilePhase phase, float milliseconds) { }
        public void RecordAreaBuildStep(AreaBuildProfileStep step, float milliseconds, int calls = 1) { }
        public void RecordPlanningStep(PlanningProfileStep step, float milliseconds, int calls = 1) { }
        public void RecordCandidateCacheHit(bool cacheHit) { }
        public void RecordRawSamples(PlacementType placementType, int count) { }
        public void RecordCandidateSeeds(PlacementType placementType, int count) { }
        public void RecordTestedSeed(PlacementType placementType) { }
        public void AddSeedGenerationTime(PlacementType placementType, float milliseconds) { }
        public void AddSamplingTime(PlacementType placementType, float milliseconds) { }
        public void RecordProjection(PlacementType placementType, bool hit, float milliseconds) { }
        public void RecordRaycast(PlacementType placementType, int hitCount, float milliseconds) { }
        public void RecordAssetAttempt(PlacementType placementType, bool accepted, RejectionReason rejectionReason, float validationMilliseconds) { }
        public void RecordValidationStep(PlacementType placementType, ValidationProfileStep step, float milliseconds) { }
    }
}
