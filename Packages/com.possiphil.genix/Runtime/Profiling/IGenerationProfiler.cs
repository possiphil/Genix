using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;

namespace Genix.Profiling
{
    /// <summary>Receives aggregate timing and work counters from one generation run.</summary>
    /// <remarks>Implementations must keep disabled calls inexpensive because instrumentation is present in hot placement paths.</remarks>
    public interface IGenerationProfiler
    {
        /// <summary>Indicates whether timing and counter collection is active.</summary>
        bool IsEnabled { get; }
        /// <summary>Gets the mutable profile populated by this recorder.</summary>
        GenerationProfile Profile { get; }

        /// <summary>Initializes metadata and baselines for a new run.</summary>
        /// <param name="context">Resolved generation context.</param>
        /// <param name="styleName">Designer-facing style name.</param>
        /// <param name="dryRun">Whether the run plans a preview without applying scene objects.</param>
        void Initialize(GenerationContext context, string styleName, bool dryRun);
        /// <summary>Adds elapsed time to a top-level generation phase.</summary>
        /// <param name="phase">Phase receiving the measurement.</param>
        /// <param name="milliseconds">Elapsed time in milliseconds.</param>
        void AddPhaseTime(GenerationProfilePhase phase, float milliseconds);
        /// <summary>Adds elapsed time and invocation count to an area-build step.</summary>
        /// <param name="step">Area-build operation receiving the measurement.</param>
        /// <param name="milliseconds">Elapsed time in milliseconds.</param>
        /// <param name="calls">Number of operations represented by the measurement.</param>
        void RecordAreaBuildStep(AreaBuildProfileStep step, float milliseconds, int calls = 1);
        /// <summary>Adds elapsed time and invocation count to a planning step.</summary>
        /// <param name="step">Planning operation receiving the measurement.</param>
        /// <param name="milliseconds">Elapsed time in milliseconds.</param>
        /// <param name="calls">Number of operations represented by the measurement.</param>
        void RecordPlanningStep(PlanningProfileStep step, float milliseconds, int calls = 1);
        /// <summary>Records the result of a candidate-cache lookup.</summary>
        /// <param name="cacheHit">Whether cached seeds were reused.</param>
        void RecordCandidateCacheHit(bool cacheHit);
        /// <summary>Adds sampler outputs produced for a placement type.</summary>
        /// <param name="placementType">Target type receiving the samples.</param>
        /// <param name="count">Number of raw samples.</param>
        void RecordRawSamples(PlacementType placementType, int count);
        /// <summary>Adds projected candidate seeds produced for a placement type.</summary>
        /// <param name="placementType">Target type receiving the seeds.</param>
        /// <param name="count">Number of usable seeds.</param>
        void RecordCandidateSeeds(PlacementType placementType, int count);
        /// <summary>Records one seed consumed by the placement planner.</summary>
        /// <param name="placementType">Target type of the tested seed.</param>
        void RecordTestedSeed(PlacementType placementType);
        /// <summary>Adds total seed-generation time for a placement type.</summary>
        /// <param name="placementType">Target type receiving the measurement.</param>
        /// <param name="milliseconds">Elapsed time in milliseconds.</param>
        void AddSeedGenerationTime(PlacementType placementType, float milliseconds);
        /// <summary>Adds sampler-only time for a placement type.</summary>
        /// <param name="placementType">Target type receiving the measurement.</param>
        /// <param name="milliseconds">Elapsed time in milliseconds.</param>
        void AddSamplingTime(PlacementType placementType, float milliseconds);
        /// <summary>Records one sample-to-surface projection attempt.</summary>
        /// <param name="placementType">Target type being projected.</param>
        /// <param name="hit">Whether projection produced a usable surface point.</param>
        /// <param name="milliseconds">Elapsed projection time in milliseconds.</param>
        void RecordProjection(PlacementType placementType, bool hit, float milliseconds);
        /// <summary>Records a group of physics raycasts used by projection or surface validation.</summary>
        /// <param name="placementType">Target type responsible for the raycasts.</param>
        /// <param name="hitCount">Number of raycasts that hit a collider.</param>
        /// <param name="milliseconds">Combined elapsed time in milliseconds.</param>
        void RecordRaycast(PlacementType placementType, int hitCount, float milliseconds);
        /// <summary>Records one asset-to-seed validation attempt.</summary>
        /// <param name="placementType">Target type of the attempted placement.</param>
        /// <param name="accepted">Whether the attempt was accepted.</param>
        /// <param name="rejectionReason">Failure reason, or <see cref="RejectionReason.None"/> when accepted.</param>
        /// <param name="validationMilliseconds">Total validation time in milliseconds.</param>
        void RecordAssetAttempt(PlacementType placementType, bool accepted, RejectionReason rejectionReason, float validationMilliseconds);
        /// <summary>Adds elapsed time for one validation component.</summary>
        /// <param name="placementType">Target type receiving the measurement.</param>
        /// <param name="step">Validation component receiving the measurement.</param>
        /// <param name="milliseconds">Elapsed time in milliseconds.</param>
        void RecordValidationStep(PlacementType placementType, ValidationProfileStep step, float milliseconds);
    }
}
