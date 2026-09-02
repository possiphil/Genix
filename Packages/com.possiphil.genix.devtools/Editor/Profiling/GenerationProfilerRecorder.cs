using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;

namespace Genix.Profiling
{
    /// <summary>Accumulates phase timings and counters for a profiled generation run.</summary>
    public sealed class GenerationProfilerRecorder : IGenerationProfiler
    {
        /// <summary>Indicates whether profiling is enabled.</summary>
        public bool IsEnabled => true;
        /// <summary>Gets the accumulated profile.</summary>
        public GenerationProfile Profile { get; } = new();

        /// <inheritdoc />
        public void Initialize(GenerationContext context, string styleName, bool dryRun) =>
            Profile.Initialize(context, styleName, dryRun);

        /// <inheritdoc />
        public void AddPhaseTime(GenerationProfilePhase phase, float milliseconds) =>
            Profile.AddPhaseTime(phase, milliseconds);

        /// <inheritdoc />
        public void RecordAreaBuildStep(AreaBuildProfileStep step, float milliseconds, int calls = 1) =>
            Profile.AddAreaBuildStep(step, milliseconds, calls);

        /// <inheritdoc />
        public void RecordPlanningStep(PlanningProfileStep step, float milliseconds, int calls = 1) =>
            Profile.AddPlanningStep(step, milliseconds, calls);

        /// <inheritdoc />
        public void RecordCandidateCacheHit(bool cacheHit) =>
            Profile.CandidateCacheHit = cacheHit;

        /// <inheritdoc />
        public void RecordRawSamples(PlacementType placementType, int count) =>
            Profile.GetTarget(placementType).AddRawSamples(count);

        /// <inheritdoc />
        public void RecordCandidateSeeds(PlacementType placementType, int count) =>
            Profile.GetTarget(placementType).AddCandidateSeeds(count);

        /// <inheritdoc />
        public void RecordTestedSeed(PlacementType placementType) =>
            Profile.GetTarget(placementType).AddTestedSeed();

        /// <inheritdoc />
        public void AddSeedGenerationTime(PlacementType placementType, float milliseconds) =>
            Profile.GetTarget(placementType).AddSeedGenerationTime(milliseconds);

        /// <inheritdoc />
        public void AddSamplingTime(PlacementType placementType, float milliseconds) =>
            Profile.GetTarget(placementType).AddSamplingTime(milliseconds);

        /// <inheritdoc />
        public void RecordProjection(PlacementType placementType, bool hit, float milliseconds) =>
            Profile.GetTarget(placementType).AddProjection(hit, milliseconds);

        /// <inheritdoc />
        public void RecordRaycast(PlacementType placementType, int hitCount, float milliseconds) =>
            Profile.GetTarget(placementType).AddRaycast(hitCount, milliseconds);

        /// <inheritdoc />
        public void RecordAssetAttempt(
            PlacementType placementType,
            bool accepted,
            RejectionReason rejectionReason,
            float validationMilliseconds)
        {
            Profile.GetTarget(placementType).AddAssetAttempt(accepted, rejectionReason, validationMilliseconds);
            Profile.AddPlanningStep(PlanningProfileStep.CandidateValidation, validationMilliseconds);
        }

        /// <inheritdoc />
        public void RecordValidationStep(
            PlacementType placementType,
            ValidationProfileStep step,
            float milliseconds) =>
            Profile.GetTarget(placementType).AddValidationStep(step, milliseconds);
    }
}
