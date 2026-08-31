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
    internal static partial class GenerationEngine
    {
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
                profiler,
                assets);
            HashSet<PlacementType> exhausted = new();
            List<AssetDefinition> assetAttemptBuffer = new(assets?.Count ?? 0);
            SupportDistributionState supportDistribution = SupportDistributionState.Create(context);
            HashSet<int> attemptedSupportGroups = supportDistribution != null ? new HashSet<int>() : null;
            RequiredRelationPlanner relationPlanner = new(context, assets);
            relationPlanner.CompleteExistingAnchors(
                type => pools.TryGetValue(type, out CandidatePool pool) ? pool : null,
                namer,
                diagnostics,
                profiler,
                candidate => RecordDistributedPlacement(candidate, placed, supportDistribution));

            while (context.Plan.Count < context.Count)
            {
                int planCheckpoint = context.Plan.Count;
                Dictionary<PlacementType, int> placedCheckpoint = new(placed);
                int[] supportCheckpoint = supportDistribution?.CreateCheckpoint();
                int remainingSlots = context.Count - context.Plan.Count;
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
                        profiler,
                        supportDistribution,
                        attemptedSupportGroups,
                        asset => relationPlanner.CanStart(asset, remainingSlots)))
                {
                    PlannedObject root = context.Plan.Objects[planCheckpoint];
                    if (relationPlanner.CompleteNewAnchor(
                            root,
                            type => pools.TryGetValue(type, out CandidatePool pool) ? pool : null,
                            namer,
                            diagnostics,
                            profiler,
                            candidate => RecordDistributedPlacement(candidate, placed, supportDistribution)))
                    {
                        continue;
                    }

                    context.Plan.RollbackTo(planCheckpoint);
                    diagnostics.RollbackPlacements(planCheckpoint);
                    RestoreCounts(placed, placedCheckpoint);
                    supportDistribution?.Restore(supportCheckpoint);
                    continue;
                }

                long budgetStart = StartPlanningStep(profiler);
                diagnostics.RecordTargetBudgets(targets, placed);
                RecordSupportBudgets(diagnostics, supportDistribution);
                StopAndRecordPlanningStep(profiler, PlanningProfileStep.TargetBudgetRecording, budgetStart);
                string summary = TargetDistributionPolicy.FormatTargets(targets, placed);
                string reason = AreAllUsableAssetsAtPlacementLimit(context, assets, usableTargets)
                    ? $"All eligible assets reached their Max Placements or a shared tag placement limit. Target budgets: {summary}."
                    : AreAllUsableAssetsWaitingForAnchors(context, assets, usableTargets)
                        ? $"All eligible assets are waiting for missing or circular asset-relative anchors. Target budgets: {summary}."
                        : $"The remaining target distribution has no valid placement. Target budgets: {summary}.";
                reason += FormatSupportBudgetSuffix(supportDistribution) +
                          FormatCandidateBudgetSuffix(pools.Values) +
                          context.AssetPool.FormatUnmetTagMinimums(context) +
                          relationPlanner.FailureSummary +
                          relationPlanner.LastRollbackSummary;

                return context.BestEffort && context.Plan.Count > 0
                    ? GenerationOutcome.Partial(
                        context.Plan.Count,
                        $"Partial result planned {context.Plan.Count} of {context.Count} requested objects. {reason}")
                    : GenerationOutcome.Failed(context.Plan.Count, reason);
            }

            long finalBudgetStart = StartPlanningStep(profiler);
            diagnostics.RecordTargetBudgets(targets, placed);
            RecordSupportBudgets(diagnostics, supportDistribution);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.TargetBudgetRecording, finalBudgetStart);
            return CreateFinalOutcome(context, relationPlanner);
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
            IGenerationProfiler profiler,
            SupportDistributionState supportDistribution,
            HashSet<int> attemptedSupportGroups,
            System.Func<AssetDefinition, bool> assetFilter)
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
                        profiler,
                        supportDistribution,
                        attemptedSupportGroups,
                        assetFilter))
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
                        profiler,
                        supportDistribution,
                        attemptedSupportGroups,
                        assetFilter))
                {
                    return true;
                }

                exhausted.Add(overflowType);
            }

            return false;
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

        private static void RecordDistributedPlacement(
            PlacementCandidate candidate,
            IDictionary<PlacementType, int> placed,
            SupportDistributionState supportDistribution)
        {
            placed[candidate.PlacementType] = placed.TryGetValue(candidate.PlacementType, out int count)
                ? count + 1
                : 1;
            supportDistribution?.RecordPlacement(candidate);
        }

        private static void RestoreCounts<TKey>(
            IDictionary<TKey, int> destination,
            IReadOnlyDictionary<TKey, int> checkpoint)
        {
            destination.Clear();
            foreach (KeyValuePair<TKey, int> entry in checkpoint)
                destination[entry.Key] = entry.Value;
        }
    }
}
