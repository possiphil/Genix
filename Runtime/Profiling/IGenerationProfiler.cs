using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;

namespace Genix.Profiling
{
    public interface IGenerationProfiler
    {
        bool IsEnabled { get; }
        GenerationProfile Profile { get; }

        void Initialize(GenerationContext context, string styleName, bool dryRun);
        void AddPhaseTime(GenerationProfilePhase phase, float milliseconds);
        void RecordAreaBuildStep(AreaBuildProfileStep step, float milliseconds, int calls = 1);
        void RecordPlanningStep(PlanningProfileStep step, float milliseconds, int calls = 1);
        void RecordCandidateCacheHit(bool cacheHit);
        void RecordRawSamples(PlacementType placementType, int count);
        void RecordCandidateSeeds(PlacementType placementType, int count);
        void RecordTestedSeed(PlacementType placementType);
        void AddSeedGenerationTime(PlacementType placementType, float milliseconds);
        void AddSamplingTime(PlacementType placementType, float milliseconds);
        void RecordProjection(PlacementType placementType, bool hit, float milliseconds);
        void RecordRaycast(PlacementType placementType, int hitCount, float milliseconds);
        void RecordAssetAttempt(PlacementType placementType, bool accepted, RejectionReason rejectionReason, float validationMilliseconds);
        void RecordValidationStep(PlacementType placementType, ValidationProfileStep step, float milliseconds);
    }
}
