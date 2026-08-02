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
    internal static class GenerationEngine
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

        private static GenerationOutcome BuildRandomPlan(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementTarget usableTargets,
            AssetAttemptPlanner.Catalog attemptCatalog,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler)
        {
            CandidatePool candidates = PlacementSolver.CreateCandidatePool(context, diagnostics, usableTargets, profiler);
            List<AssetDefinition> assetAttemptBuffer = new(assets?.Count ?? 0);

            for (int i = 0; i < context.Count; i++)
            {
                if (TryPlanAsset(context, assets, attemptCatalog, assetAttemptBuffer, candidates, namer, diagnostics, profiler))
                    continue;

                string reason = "No remaining sampled position fits any valid asset.";
                return context.BestEffort && context.Plan.Count > 0
                    ? GenerationOutcome.Partial(
                        context.Plan.Count,
                        $"Best Effort planned {context.Plan.Count} of {context.Count} requested objects. {reason}")
                    : GenerationOutcome.Failed(context.Plan.Count, reason);
            }

            return GenerationOutcome.Completed(context.Plan.Count);
        }

        private static GenerationOutcome BuildDistributedPlan(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementTarget usableTargets,
            AssetAttemptPlanner.Catalog attemptCatalog,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler)
        {
            long targetStart = StartPlanningStep(profiler);
            List<PlacementType> placementTypes = TargetDistributionPolicy.GetPlacementTypes(context, usableTargets);
            Dictionary<PlacementType, int> targets = TargetDistributionPolicy.CreateTargets(context, placementTypes);
            placementTypes = placementTypes
                .Where(type => targets.TryGetValue(type, out int targetCount) && targetCount > 0)
                .ToList();
            targets = targets
                .Where(target => target.Value > 0)
                .ToDictionary(target => target.Key, target => target.Value);
            Dictionary<PlacementType, int> placed = placementTypes.ToDictionary(type => type, _ => 0);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.TargetSelection, targetStart);

            Dictionary<PlacementType, CandidatePool> pools = PlacementSolver.CreateCandidatePoolsByPlacementType(
                context,
                diagnostics,
                ToPlacementTargets(placementTypes),
                profiler);
            HashSet<PlacementType> exhausted = new();
            List<AssetDefinition> assetAttemptBuffer = new(assets?.Count ?? 0);

            for (int i = 0; i < context.Count; i++)
            {
                if (TryPlanDistributedAsset(
                        context,
                        assets,
                        targets,
                        placed,
                        pools,
                        exhausted,
                        attemptCatalog,
                        assetAttemptBuffer,
                        namer,
                        diagnostics,
                        profiler))
                {
                    continue;
                }

                long budgetStart = StartPlanningStep(profiler);
                diagnostics.RecordTargetBudgets(targets, placed);
                StopAndRecordPlanningStep(profiler, PlanningProfileStep.TargetBudgetRecording, budgetStart);
                string summary = TargetDistributionPolicy.FormatTargets(targets, placed);
                string reason = $"The remaining target distribution has no valid placement. Target budgets: {summary}.";

                return context.BestEffort && context.Plan.Count > 0
                    ? GenerationOutcome.Partial(
                        context.Plan.Count,
                        $"Best Effort planned {context.Plan.Count} of {context.Count} requested objects. {reason}")
                    : GenerationOutcome.Failed(context.Plan.Count, reason);
            }

            long finalBudgetStart = StartPlanningStep(profiler);
            diagnostics.RecordTargetBudgets(targets, placed);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.TargetBudgetRecording, finalBudgetStart);
            return GenerationOutcome.Completed(context.Plan.Count);
        }

        private static bool TryPlanDistributedAsset(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            IReadOnlyDictionary<PlacementType, int> targets,
            Dictionary<PlacementType, int> placed,
            IReadOnlyDictionary<PlacementType, CandidatePool> pools,
            ISet<PlacementType> exhausted,
            AssetAttemptPlanner.Catalog attemptCatalog,
            List<AssetDefinition> assetAttemptBuffer,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler)
        {
            while (TrySelectTarget(
                       context,
                       targets,
                       placed,
                       pools,
                       exhausted,
                       profiler,
                       out PlacementType targetType))
            {
                if (TryPlanAssetOnTarget(
                        context,
                        assets,
                        targetType,
                        pools,
                        placed,
                        attemptCatalog,
                        assetAttemptBuffer,
                        namer,
                        diagnostics,
                        profiler))
                {
                    return true;
                }

                exhausted.Add(targetType);
            }

            long overflowStart = StartPlanningStep(profiler);
            List<PlacementType> overflowTypes = TargetDistributionPolicy.GetOverflowTypes(
                targets.Keys,
                pools,
                exhausted,
                context);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.TargetSelection, overflowStart);

            foreach (PlacementType overflowType in overflowTypes)
            {
                if (TryPlanAssetOnTarget(
                        context,
                        assets,
                        overflowType,
                        pools,
                        placed,
                        attemptCatalog,
                        assetAttemptBuffer,
                        namer,
                        diagnostics,
                        profiler))
                {
                    return true;
                }

                exhausted.Add(overflowType);
            }

            return false;
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

        private static bool TryPlanAssetOnTarget(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementType placementType,
            IReadOnlyDictionary<PlacementType, CandidatePool> pools,
            IDictionary<PlacementType, int> placed,
            AssetAttemptPlanner.Catalog attemptCatalog,
            List<AssetDefinition> assetAttemptBuffer,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler)
        {
            long assetCheckStart = StartPlanningStep(profiler);
            bool hasAssets = TargetDistributionPolicy.HasAssets(assets, placementType);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.UsableTargetSelection, assetCheckStart);

            if (!hasAssets ||
                !pools.TryGetValue(placementType, out CandidatePool candidates) ||
                candidates.Count <= 0 ||
                !TryPlanAsset(context, assets, attemptCatalog, assetAttemptBuffer, candidates, namer, diagnostics, profiler))
            {
                return false;
            }

            placed[placementType] = placed.TryGetValue(placementType, out int count) ? count + 1 : 1;
            return true;
        }

        private static bool TryPlanAsset(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            AssetAttemptPlanner.Catalog attemptCatalog,
            List<AssetDefinition> assetAttemptBuffer,
            CandidatePool candidates,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler)
        {
            bool found = PlacementSolver.TryGetValidCandidateForAnyAsset(
                context,
                assets,
                candidates,
                namer.Next,
                out AssetDefinition asset,
                out PlacementCandidate candidate,
                out string objectName,
                diagnostics,
                profiler,
                attemptCatalog,
                assetAttemptBuffer);

            if (found)
            {
                long planStart = StartPlanningStep(profiler);
                context.Plan.Add(asset, candidate, objectName);
                StopAndRecordPlanningStep(profiler, PlanningProfileStep.PlanRecording, planStart);
            }

            return found;
        }

        private static bool TrySelectTarget(
            GenerationContext context,
            IReadOnlyDictionary<PlacementType, int> targets,
            IReadOnlyDictionary<PlacementType, int> placed,
            IReadOnlyDictionary<PlacementType, CandidatePool> pools,
            ISet<PlacementType> exhausted,
            IGenerationProfiler profiler,
            out PlacementType selected)
        {
            long start = StartPlanningStep(profiler);
            bool result = TargetDistributionPolicy.TrySelectTarget(
                context,
                targets,
                placed,
                pools,
                exhausted,
                out selected);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.TargetSelection, start);
            return result;
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

    /// <summary>Completion state, accepted count, and designer-facing message returned by planning.</summary>
    internal readonly struct GenerationOutcome
    {
        public bool ShouldApply { get; }
        public bool IsComplete { get; }
        public int PlacedCount { get; }
        public string Message { get; }

        private GenerationOutcome(bool shouldApply, bool isComplete, int placedCount, string message)
        {
            ShouldApply = shouldApply;
            IsComplete = isComplete;
            PlacedCount = placedCount;
            Message = message;
        }

        public static GenerationOutcome Completed(int count) =>
            new(true, true, count, string.Empty);

        public static GenerationOutcome Partial(int count, string message) =>
            new(true, false, count, message);

        public static GenerationOutcome Failed(string message) =>
            Failed(0, message);

        public static GenerationOutcome Failed(int count, string message) =>
            new(false, false, count, message);
    }
}
