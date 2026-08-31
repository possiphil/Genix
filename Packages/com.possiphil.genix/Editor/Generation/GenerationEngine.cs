using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Profiling;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Editor.Generation
{
    /// <summary>Builds a placement plan from a resolved context without modifying the Unity scene.</summary>
    internal static partial class GenerationEngine
    {
        public static GenerationOutcome BuildPlan(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler = null)
        {
            profiler ??= NullGenerationProfiler.Instance;
            long targetStart = StartPlanningStep(profiler);
            PlacementTarget usableTargets = TargetDistributionPolicy.GetUsableTargets(context, assets);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.UsableTargetSelection, targetStart);

            if (usableTargets == PlacementTarget.None)
            {
                return GenerationOutcome.Failed(
                    "No selected placement target has usable assets and matching area surfaces after prefab, semantic tag, and area filtering.");
            }

            if (AreAllUsableAssetsWaitingForAnchors(context, assets, usableTargets))
            {
                return GenerationOutcome.Failed(
                    "All eligible assets are waiting for missing or circular asset-relative anchors.");
            }

            long namingStart = StartPlanningStep(profiler);
            GeneratedObjectNamer namer = new(context.GeneratedParent);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.ObjectNaming, namingStart);

            long catalogStart = StartPlanningStep(profiler);
            AssetAttemptPlanner.Catalog attemptCatalog = AssetAttemptPlanner.CreateCatalog(assets);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.AssetCatalog, catalogStart);

            return TargetDistributionPolicy.IsActive(context)
                ? BuildDistributedPlan(context, assets, usableTargets, attemptCatalog, namer, diagnostics, profiler)
                : BuildRandomPlan(context, assets, usableTargets, attemptCatalog, namer, diagnostics, profiler);
        }

        private static PlacementTarget ToPlacementTargets(IEnumerable<PlacementType> placementTypes)
        {
            PlacementTarget targets = PlacementTarget.None;

            foreach (PlacementType type in placementTypes)
            {
                targets |= type switch
                {
                    PlacementType.Floor => PlacementTarget.Floor,
                    PlacementType.Wall => PlacementTarget.Wall,
                    PlacementType.Ceiling => PlacementTarget.Ceiling,
                    PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                    _ => PlacementTarget.None
                };
            }

            return targets;
        }

        private static bool AreAllUsableAssetsAtPlacementLimit(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementTarget usableTargets)
        {
            if (context == null || assets == null)
                return false;

            List<AssetDefinition> usableAssets = assets
                .Where(asset => asset && asset.Prefab &&
                                (usableTargets & ToPlacementTarget(asset.PlacementType)) != 0)
                .ToList();
            return usableAssets.Count > 0 && usableAssets.All(asset =>
                context.AssetPool.HasReachedPlacementLimit(asset, context));
        }

        private static bool AreAllUsableAssetsWaitingForAnchors(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementTarget usableTargets)
        {
            if (context == null || assets == null)
                return false;

            List<AssetDefinition> availableByLimit = assets
                .Where(asset => asset && asset.Prefab &&
                                (usableTargets & ToPlacementTarget(asset.PlacementType)) != 0 &&
                                !context.AssetPool.HasReachedPlacementLimit(asset, context))
                .ToList();
            return availableByLimit.Count > 0 && availableByLimit.All(asset =>
                !RelativeAnchorProvider.CanAttemptAsset(context, asset));
        }

        private static PlacementTarget ToPlacementTarget(PlacementType placementType) =>
            placementType switch
            {
                PlacementType.Floor => PlacementTarget.Floor,
                PlacementType.Wall => PlacementTarget.Wall,
                PlacementType.Ceiling => PlacementTarget.Ceiling,
                PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                _ => PlacementTarget.None
            };

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

        private static GenerationOutcome CreateRequiredRelationFailureOutcome(
            GenerationContext context,
            RequiredRelationPlanner relationPlanner)
        {
            string message = $"Required asset relations could not be completed.{relationPlanner.FailureSummary}";
            return context.BestEffort && context.Plan.Count > 0
                ? GenerationOutcome.Partial(context.Plan.Count, $"Partial result planned {context.Plan.Count} objects. {message}")
                : GenerationOutcome.Failed(context.Plan.Count, message);
        }

        private static GenerationOutcome CreateFinalOutcome(
            GenerationContext context,
            RequiredRelationPlanner relationPlanner)
        {
            if (relationPlanner.HasFailures)
                return CreateRequiredRelationFailureOutcome(context, relationPlanner);

            string unmetMinimums = context.AssetPool.FormatUnmetTagMinimums(context);
            if (string.IsNullOrEmpty(unmetMinimums))
                return GenerationOutcome.Completed(context.Plan.Count);

            string message = $"Required shared tag counts could not be completed.{unmetMinimums}";
            return context.BestEffort && context.Plan.Count > 0
                ? GenerationOutcome.Partial(context.Plan.Count, $"Partial result planned {context.Plan.Count} objects. {message}")
                : GenerationOutcome.Failed(context.Plan.Count, message);
        }
    }
}
