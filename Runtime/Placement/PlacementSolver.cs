using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Placement
{
    /// <summary>Coordinates candidate generation, asset attempts, validation, diagnostics, and plan construction.</summary>
    public static class PlacementSolver
    {
        /// <summary>Clears candidate cache.</summary>
        public static void ClearCandidateCache()
        {
            CandidateSeedCache.Clear();
        }

        /// <summary>Clears scene object cache.</summary>
        public static void ClearSceneObjectCache()
        {
            SceneObjectIndex.ClearCache();
        }

        /// <summary>Creates candidate pool.</summary>
        public static CandidatePool CreateCandidatePool(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null,
            PlacementTarget? targets = null,
            IGenerationProfiler profiler = null)
        {
            diagnostics ??= NullDiagnosticsSink.Instance;
            profiler ??= NullGenerationProfiler.Instance;
            return CandidateSeedFactory.CreatePool(context, diagnostics, targets, profiler);
        }

        /// <summary>Creates candidate pools by placement type.</summary>
        public static Dictionary<PlacementType, CandidatePool> CreateCandidatePoolsByPlacementType(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null,
            PlacementTarget? targets = null,
            IGenerationProfiler profiler = null)
        {
            diagnostics ??= NullDiagnosticsSink.Instance;
            profiler ??= NullGenerationProfiler.Instance;
            return CandidateSeedFactory.CreatePoolsByPlacementType(context, diagnostics, targets, profiler);
        }

        /// <summary>Attempts to get valid candidate.</summary>
        public static bool TryGetValidCandidate(
            GenerationContext context,
            AssetDefinition asset,
            CandidatePool candidates,
            out PlacementCandidate candidate,
            IDiagnosticsSink diagnostics = null,
            string generatedObjectName = "",
            IGenerationProfiler profiler = null)
        {
            diagnostics ??= NullDiagnosticsSink.Instance;
            profiler ??= NullGenerationProfiler.Instance;

            if (!asset || asset.HasReachedPlacementLimit(context?.Plan?.GetAssetCount(asset) ?? 0))
            {
                candidate = default;
                return false;
            }

            while (true)
            {
                long iterationStart = StartPlanningStep(profiler);
                bool hasSeed = candidates.TryTakeNext(out CandidateSeed seed);

                if (hasSeed)
                {
                    diagnostics.RecordTestedCandidateSeed(seed.Position);
                    profiler.RecordTestedSeed(seed.PlacementType);
                }

                StopAndRecordPlanningStep(profiler, PlanningProfileStep.CandidateIteration, iterationStart);

                if (!hasSeed)
                    break;

                if (asset && asset.PlacementType != seed.PlacementType)
                    continue;

                if (TryEvaluateAsset(
                        context,
                        asset,
                        seed,
                        null,
                        generatedObjectName,
                        diagnostics,
                        profiler,
                        out candidate,
                        out _,
                        out _))
                {
                    return true;
                }
            }

            candidate = default;
            return false;
        }

        /// <summary>Finds the first valid asset and candidate combination from the remaining candidate pool.</summary>
        public static bool TryGetValidCandidateForAnyAsset(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            CandidatePool candidates,
            Func<AssetDefinition, string> createObjectName,
            out AssetDefinition selectedAsset,
            out PlacementCandidate candidate,
            out string generatedObjectName,
            IDiagnosticsSink diagnostics = null,
            IGenerationProfiler profiler = null,
            AssetAttemptPlanner.Catalog attemptCatalog = null,
            List<AssetDefinition> remainingBuffer = null)
        {
            diagnostics ??= NullDiagnosticsSink.Instance;
            profiler ??= NullGenerationProfiler.Instance;
            selectedAsset = null;
            candidate = default;
            generatedObjectName = string.Empty;

            if (context == null || assets == null || assets.Count == 0 || candidates == null)
                return false;

            if (attemptCatalog == null)
            {
                long catalogStart = StartPlanningStep(profiler);
                attemptCatalog = AssetAttemptPlanner.CreateCatalog(assets);
                StopAndRecordPlanningStep(profiler, PlanningProfileStep.AssetCatalog, catalogStart);
            }

            List<AssetDefinition> remaining = remainingBuffer ?? new List<AssetDefinition>();
            remaining.Clear();

            while (true)
            {
                long iterationStart = StartPlanningStep(profiler);
                bool hasSeed = candidates.TryTakeNext(out CandidateSeed seed);

                if (hasSeed)
                {
                    diagnostics.RecordTestedCandidateSeed(seed.Position);
                    profiler.RecordTestedSeed(seed.PlacementType);
                }

                StopAndRecordPlanningStep(profiler, PlanningProfileStep.CandidateIteration, iterationStart);

                if (!hasSeed)
                    break;

                long orderStart = StartPlanningStep(profiler);
                attemptCatalog.CreateOrder(
                    seed.PlacementType,
                    context.Random,
                    remaining,
                    asset => !asset.HasReachedPlacementLimit(context.Plan.GetAssetCount(asset)));
                StopAndRecordPlanningStep(profiler, PlanningProfileStep.AssetOrder, orderStart);

                if (remaining.Count == 0)
                {
                    if (AreAllAssetsAtPlacementLimit(assets, context))
                        break;

                    continue;
                }
                int remainingIndex = 0;

                while (remainingIndex < remaining.Count)
                {
                    AssetDefinition asset = remaining[remainingIndex];
                    remainingIndex++;

                    if (TryEvaluateAsset(
                            context,
                            asset,
                            seed,
                            createObjectName,
                            string.Empty,
                            diagnostics,
                            profiler,
                            out PlacementCandidate attempt,
                            out RejectionReason rejection,
                            out string objectName))
                    {
                        selectedAsset = asset;
                        candidate = attempt;
                        generatedObjectName = objectName;
                        return true;
                    }

                    long pruningStart = StartPlanningStep(profiler);
                    AssetAttemptPlanner.PruneRemaining(
                        remaining,
                        remainingIndex,
                        rejection);
                    StopAndRecordPlanningStep(profiler, PlanningProfileStep.AssetPruning, pruningStart);
                }
            }

            return false;
        }

        private static bool AreAllAssetsAtPlacementLimit(
            IReadOnlyList<AssetDefinition> assets,
            GenerationContext context)
        {
            bool foundUsableAsset = false;

            for (int i = 0; i < assets.Count; i++)
            {
                AssetDefinition asset = assets[i];

                if (!asset || !asset.Prefab)
                    continue;

                foundUsableAsset = true;

                if (!asset.HasReachedPlacementLimit(context.Plan.GetAssetCount(asset)))
                    return false;
            }

            return foundUsableAsset;
        }

        private static bool TryEvaluateAsset(
            GenerationContext context,
            AssetDefinition asset,
            CandidateSeed seed,
            Func<AssetDefinition, string> createObjectName,
            string generatedObjectName,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            out PlacementCandidate candidate,
            out RejectionReason pruningReason,
            out string resolvedObjectName)
        {
            candidate = default;
            pruningReason = RejectionReason.None;
            resolvedObjectName = string.Empty;

            if (!asset || !asset.Prefab)
                return false;

            string objectNameValue = string.IsNullOrWhiteSpace(generatedObjectName)
                ? createObjectName == null ? asset.AssetName : string.Empty
                : generatedObjectName;

            string GetObjectName()
            {
                if (string.IsNullOrWhiteSpace(objectNameValue))
                {
                    long objectNameStart = StartPlanningStep(profiler);
                    objectNameValue = createObjectName?.Invoke(asset) ?? asset.AssetName;
                    StopAndRecordPlanningStep(profiler, PlanningProfileStep.ObjectNaming, objectNameStart);
                }

                return objectNameValue;
            }

            Stopwatch supportRulesStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            bool supportRulesValid = PlacementSupportRules.TryValidate(
                seed,
                asset,
                context,
                out RejectionReason supportRejection,
                out string supportObjectName);
            supportRulesStopwatch?.Stop();

            if (!supportRulesValid)
            {
                float validationMilliseconds = supportRulesStopwatch != null
                    ? (float)supportRulesStopwatch.Elapsed.TotalMilliseconds
                    : 0f;
                PlacementCandidate supportAttempt = new(
                    seed.Position,
                    seed.Rotation,
                    seed.SurfaceCollider,
                    seed.SurfaceNormal,
                    seed.VoxelLayer,
                    seed.PlacementType);
                OrientedBounds supportBounds = new(
                    seed.Position,
                    AssetAttemptPlanner.Dimensions(asset),
                    seed.Rotation);
                profiler.RecordAssetAttempt(
                    seed.PlacementType,
                    false,
                    supportRejection,
                    validationMilliseconds);
                RecordRejectedCandidate(
                    diagnostics,
                    asset,
                    supportAttempt,
                    supportBounds,
                    supportRejection,
                    supportObjectName,
                    profiler);
                pruningReason = supportRejection;
                return false;
            }

            Stopwatch earlyValidationStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;

            bool earlyRejected = PlacementValidator.TryRejectByPlannedSpacing(
                seed,
                asset,
                context,
                out string earlyRelatedObjectName);
            earlyValidationStopwatch?.Stop();

            if (earlyRejected)
            {
                float earlyValidationMilliseconds = earlyValidationStopwatch != null
                    ? (float)earlyValidationStopwatch.Elapsed.TotalMilliseconds
                    : 0f;
                PlacementCandidate earlyAttempt = new(
                    seed.Position,
                    seed.Rotation,
                    seed.SurfaceCollider,
                    seed.SurfaceNormal,
                    seed.VoxelLayer,
                    seed.PlacementType);
                OrientedBounds earlyBounds = new(
                    seed.Position,
                    AssetAttemptPlanner.Dimensions(asset),
                    seed.Rotation);
                profiler.RecordAssetAttempt(
                    seed.PlacementType,
                    false,
                    RejectionReason.TooCloseToGenerated,
                    earlyValidationMilliseconds);
                profiler.RecordValidationStep(
                    seed.PlacementType,
                    ValidationProfileStep.PlannedSpacing,
                    earlyValidationMilliseconds);
                RecordRejectedCandidate(
                    diagnostics,
                    asset,
                    earlyAttempt,
                    earlyBounds,
                    RejectionReason.TooCloseToGenerated,
                    earlyRelatedObjectName,
                    profiler);
                pruningReason = RejectionReason.TooCloseToGenerated;
                return false;
            }

            earlyValidationStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            bool earlySceneSpacingRejected = PlacementValidator.TryRejectByGeneratedSceneSpacing(
                seed,
                asset,
                context,
                out earlyRelatedObjectName);
            earlyValidationStopwatch?.Stop();

            if (earlySceneSpacingRejected)
            {
                float earlyValidationMilliseconds = earlyValidationStopwatch != null
                    ? (float)earlyValidationStopwatch.Elapsed.TotalMilliseconds
                    : 0f;
                PlacementCandidate earlyAttempt = new(
                    seed.Position,
                    seed.Rotation,
                    seed.SurfaceCollider,
                    seed.SurfaceNormal,
                    seed.VoxelLayer,
                    seed.PlacementType);
                OrientedBounds earlyBounds = new(
                    seed.Position,
                    AssetAttemptPlanner.Dimensions(asset),
                    seed.Rotation);
                profiler.RecordAssetAttempt(
                    seed.PlacementType,
                    false,
                    RejectionReason.TooCloseToGenerated,
                    earlyValidationMilliseconds);
                profiler.RecordValidationStep(
                    seed.PlacementType,
                    ValidationProfileStep.GeneratedSceneSpacing,
                    earlyValidationMilliseconds);
                RecordRejectedCandidate(
                    diagnostics,
                    asset,
                    earlyAttempt,
                    earlyBounds,
                    RejectionReason.TooCloseToGenerated,
                    earlyRelatedObjectName,
                    profiler);
                pruningReason = RejectionReason.TooCloseToGenerated;
                return false;
            }

            if (TryRejectInsideSpaceBeforeRotation(
                    context,
                    asset,
                    seed,
                    out RejectionReason earlyRejection,
                    out OrientedBounds earlyRejectedBounds,
                    out float earlyRejectionMilliseconds,
                    profiler))
            {
                PlacementCandidate earlyAttempt = new(
                    seed.Position,
                    seed.Rotation,
                    seed.SurfaceCollider,
                    seed.SurfaceNormal,
                    seed.VoxelLayer,
                    seed.PlacementType);
                profiler.RecordAssetAttempt(
                    seed.PlacementType,
                    false,
                    earlyRejection,
                    earlyRejectionMilliseconds);
                RecordRejectedCandidate(
                    diagnostics,
                    asset,
                    earlyAttempt,
                    earlyRejectedBounds,
                    earlyRejection,
                    string.Empty,
                    profiler);
                pruningReason = earlyRejection;
                return false;
            }

            int rotationCount = CandidateFactory.GetRotationAttemptCount(context, asset, seed.PlacementType);
            float yawBase = CandidateFactory.UsesRandomPlanarRotation(context, asset, seed.PlacementType)
                ? context.Random.Range(0f, 360f)
                : 0f;

            for (int rotationIndex = 0; rotationIndex < rotationCount; rotationIndex++)
            {
                long candidateBuildStart = StartPlanningStep(profiler);
                PlacementCandidate attempt = CandidateFactory.Create(
                    seed,
                    context,
                    asset,
                    rotationIndex,
                    rotationCount,
                    yawBase,
                    profiler);
                OrientedBounds bounds = CandidateFactory.GetBounds(attempt, asset);
                StopAndRecordPlanningStep(profiler, PlanningProfileStep.CandidateBuild, candidateBuildStart);

                Stopwatch validationStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                bool isValid = PlacementValidator.TryValidateCandidate(
                        attempt,
                        bounds,
                        context,
                        asset,
                        out RejectionReason rejection,
                        out string relatedObjectName,
                        profiler);
                validationStopwatch?.Stop();
                profiler.RecordAssetAttempt(
                    seed.PlacementType,
                    isValid,
                    isValid ? RejectionReason.None : rejection,
                    validationStopwatch != null ? (float)validationStopwatch.Elapsed.TotalMilliseconds : 0f);

                if (isValid)
                {
                    string objectName = GetObjectName();
                    long diagnosticsStart = StartPlanningStep(profiler);
                    diagnostics.RecordCandidate(
                        asset.AssetName,
                        objectName,
                        attempt,
                        bounds.ToLocalBounds(),
                        true,
                        RejectionReason.None);
                    diagnostics.RecordPlacement(asset, objectName, attempt);
                    StopAndRecordPlanningStep(profiler, PlanningProfileStep.DiagnosticsRecording, diagnosticsStart);
                    candidate = attempt;
                    resolvedObjectName = objectName;
                    return true;
                }

                RecordRejectedCandidate(
                    diagnostics,
                    asset,
                    attempt,
                    bounds,
                    rejection,
                    relatedObjectName,
                    profiler);

                if (pruningReason == RejectionReason.None)
                    pruningReason = rejection;
            }

            return false;
        }

        private static void RecordRejectedCandidate(
            IDiagnosticsSink diagnostics,
            AssetDefinition asset,
            PlacementCandidate attempt,
            OrientedBounds bounds,
            RejectionReason rejection,
            string relatedObjectName,
            IGenerationProfiler profiler)
        {
            long diagnosticsStart = StartPlanningStep(profiler);
            bool recordDetails = diagnostics.ShouldRecordCandidateDetails(false);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.DiagnosticsRecording, diagnosticsStart);

            if (recordDetails)
            {
                diagnosticsStart = StartPlanningStep(profiler);
                diagnostics.RecordCandidate(
                    asset.AssetName,
                    asset.AssetName,
                    attempt,
                    bounds.ToLocalBounds(),
                    false,
                    rejection,
                    relatedObjectName);
                StopAndRecordPlanningStep(profiler, PlanningProfileStep.DiagnosticsRecording, diagnosticsStart);
                return;
            }

            diagnosticsStart = StartPlanningStep(profiler);
            diagnostics.RecordCandidate(
                asset.AssetName,
                string.Empty,
                default,
                default,
                false,
                rejection,
                relatedObjectName);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.DiagnosticsRecording, diagnosticsStart);
        }

        private static bool TryRejectInsideSpaceBeforeRotation(
            GenerationContext context,
            AssetDefinition asset,
            CandidateSeed seed,
            out RejectionReason rejection,
            out OrientedBounds bounds,
            out float validationMilliseconds,
            IGenerationProfiler profiler)
        {
            rejection = RejectionReason.None;
            validationMilliseconds = 0f;
            Vector3 dimensions = AssetAttemptPlanner.Dimensions(asset);
            bounds = new OrientedBounds(seed.Position, dimensions, seed.Rotation);

            if (seed.PlacementType != PlacementType.InsideSpace ||
                asset.RandomPitchRotation ||
                asset.RandomRollRotation)
            {
                return false;
            }

            long stepStart = StartValidationStep(profiler);
            Bounds axisAlignedBounds = new(seed.Position, dimensions);

            if (!PlacementValidator.FitsTargetHeight(axisAlignedBounds, context.TargetBounds))
            {
                validationMilliseconds = StopAndRecordValidationStep(
                    profiler,
                    seed.PlacementType,
                    ValidationProfileStep.Height,
                    stepStart);
                rejection = RejectionReason.ExceedsTargetHeight;
                return true;
            }

            if (asset.RandomYawRotation)
                return false;

            stepStart = StartValidationStep(profiler);

            if (!context.Area.ContainsPlacementVolume(bounds))
            {
                validationMilliseconds = StopAndRecordValidationStep(
                    profiler,
                    seed.PlacementType,
                    ValidationProfileStep.Volume,
                    stepStart);
                rejection = RejectionReason.OutsideTargetVolume;
                return true;
            }

            return false;
        }

        private static long StartValidationStep(IGenerationProfiler profiler) =>
            profiler is { IsEnabled: true } ? Stopwatch.GetTimestamp() : 0L;

        private static float StopAndRecordValidationStep(
            IGenerationProfiler profiler,
            PlacementType placementType,
            ValidationProfileStep step,
            long startTimestamp)
        {
            if (profiler is not { IsEnabled: true } || startTimestamp <= 0L)
                return 0f;

            float milliseconds = (float)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency);
            profiler.RecordValidationStep(placementType, step, milliseconds);
            return milliseconds;
        }

        private static long StartPlanningStep(IGenerationProfiler profiler) =>
            profiler is { IsEnabled: true } ? Stopwatch.GetTimestamp() : 0L;

        private static void StopAndRecordPlanningStep(
            IGenerationProfiler profiler,
            PlanningProfileStep step,
            long startTimestamp)
        {
            if (profiler is not { IsEnabled: true } || startTimestamp <= 0L)
                return;

            float milliseconds = (float)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency);
            profiler.RecordPlanningStep(step, milliseconds);
        }

    }
}
