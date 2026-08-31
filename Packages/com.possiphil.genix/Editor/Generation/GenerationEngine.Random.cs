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
        private static GenerationOutcome BuildRandomPlan(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementTarget usableTargets,
            AssetAttemptPlanner.Catalog attemptCatalog,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler)
        {
            CandidatePool candidates = PlacementSolver.CreateCandidatePool(
                context,
                diagnostics,
                usableTargets,
                profiler,
                assets);
            List<AssetDefinition> assetAttemptBuffer = new(assets?.Count ?? 0);
            SupportDistributionState supportDistribution = SupportDistributionState.Create(context);
            HashSet<int> attemptedSupportGroups = supportDistribution != null ? new HashSet<int>() : null;
            RequiredRelationPlanner relationPlanner = new(context, assets);
            relationPlanner.CompleteExistingAnchors(
                _ => candidates,
                namer,
                diagnostics,
                profiler,
                candidate => supportDistribution?.RecordPlacement(candidate));

            while (context.Plan.Count < context.Count)
            {
                int planCheckpoint = context.Plan.Count;
                int[] supportCheckpoint = supportDistribution?.CreateCheckpoint();
                int remainingSlots = context.Count - context.Plan.Count;
                if (TryPlanAsset(
                        context,
                        assets,
                        attemptCatalog,
                        assetAttemptBuffer,
                        candidates,
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
                            _ => candidates,
                            namer,
                            diagnostics,
                            profiler,
                            candidate => supportDistribution?.RecordPlacement(candidate)))
                    {
                        continue;
                    }

                    context.Plan.RollbackTo(planCheckpoint);
                    diagnostics.RollbackPlacements(planCheckpoint);
                    supportDistribution?.Restore(supportCheckpoint);
                    continue;
                }

                string reason = AreAllUsableAssetsAtPlacementLimit(context, assets, usableTargets)
                    ? "All eligible assets reached their Max Placements or a shared tag placement limit."
                    : AreAllUsableAssetsWaitingForAnchors(context, assets, usableTargets)
                        ? "All eligible assets are waiting for missing or circular asset-relative anchors."
                        : "No remaining sampled position fits any valid asset.";
                RecordSupportBudgets(diagnostics, supportDistribution);
                reason += FormatSupportBudgetSuffix(supportDistribution) +
                          FormatCandidateBudgetSuffix(candidates) +
                          context.AssetPool.FormatUnmetTagMinimums(context) +
                          relationPlanner.FailureSummary +
                          relationPlanner.LastRollbackSummary;
                return context.BestEffort && context.Plan.Count > 0
                    ? GenerationOutcome.Partial(
                        context.Plan.Count,
                        $"Partial result planned {context.Plan.Count} of {context.Count} requested objects. {reason}")
                    : GenerationOutcome.Failed(context.Plan.Count, reason);
            }

            RecordSupportBudgets(diagnostics, supportDistribution);
            return CreateFinalOutcome(context, relationPlanner);
        }
    }
}
