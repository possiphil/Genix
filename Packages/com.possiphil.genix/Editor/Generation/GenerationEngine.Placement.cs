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
            IGenerationProfiler profiler,
            SupportDistributionState supportDistribution,
            HashSet<int> attemptedSupportGroups,
            System.Func<AssetDefinition, bool> assetFilter)
        {
            long assetCheckStart = StartPlanningStep(profiler);
            bool hasAssets = TargetDistributionPolicy.HasAssets(assets, placementType);
            StopAndRecordPlanningStep(profiler, PlanningProfileStep.UsableTargetSelection, assetCheckStart);

            if (!hasAssets ||
                !pools.TryGetValue(placementType, out CandidatePool candidates) ||
                candidates.Count <= 0 ||
                !TryPlanAsset(
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
                    assetFilter))
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
            IGenerationProfiler profiler,
            SupportDistributionState supportDistribution,
            HashSet<int> attemptedSupportGroups,
            System.Func<AssetDefinition, bool> assetFilter)
        {
            if (supportDistribution is not { IsActive: true })
            {
                return TryPlanAssetWithFilter(
                    context,
                    assets,
                    attemptCatalog,
                    assetAttemptBuffer,
                    candidates,
                    namer,
                    diagnostics,
                    profiler,
                    null,
                    assetFilter);
            }

            attemptedSupportGroups.Clear();

            while (supportDistribution.TrySelectUnderfilled(attemptedSupportGroups, out int group))
            {
                if (TryPlanAssetOnSupportGroup(
                        context,
                        assets,
                        attemptCatalog,
                        assetAttemptBuffer,
                        candidates,
                        namer,
                        diagnostics,
                        profiler,
                        supportDistribution,
                        group,
                        assetFilter))
                {
                    return true;
                }

                attemptedSupportGroups.Add(group);
            }

            for (int group = 0; group < supportDistribution.GroupCount; group++)
            {
                if (attemptedSupportGroups.Contains(group))
                    continue;

                if (TryPlanAssetOnSupportGroup(
                        context,
                        assets,
                        attemptCatalog,
                        assetAttemptBuffer,
                        candidates,
                        namer,
                        diagnostics,
                        profiler,
                        supportDistribution,
                        group,
                        assetFilter))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryPlanAssetOnSupportGroup(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            AssetAttemptPlanner.Catalog attemptCatalog,
            List<AssetDefinition> assetAttemptBuffer,
            CandidatePool candidates,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            SupportDistributionState supportDistribution,
            int group,
            System.Func<AssetDefinition, bool> assetFilter)
        {
            supportDistribution.SelectGroup(group);
            bool found = TryPlanAssetWithFilter(
                context,
                assets,
                attemptCatalog,
                assetAttemptBuffer,
                candidates,
                namer,
                diagnostics,
                profiler,
                supportDistribution.ActiveSeedFilter,
                assetFilter);
            if (found)
                supportDistribution.RecordPlacement(group);
            return found;
        }

        private static void RecordSupportBudgets(
            IDiagnosticsSink diagnostics,
            SupportDistributionState supportDistribution)
        {
            if (supportDistribution is not { IsActive: true })
                return;

            List<SupportBudgetDiagnostic> budgets = new(supportDistribution.GroupCount);
            for (int group = 0; group < supportDistribution.GroupCount; group++)
            {
                budgets.Add(new SupportBudgetDiagnostic(
                    supportDistribution.GetLabel(group),
                    supportDistribution.GetTarget(group),
                    supportDistribution.GetPlaced(group)));
            }

            diagnostics.RecordSupportBudgets(budgets);
        }

        private static string FormatSupportBudgetSuffix(SupportDistributionState supportDistribution) =>
            supportDistribution is { IsActive: true }
                ? $" Support budgets: {supportDistribution.FormatBudgets()}."
                : string.Empty;

        private static string FormatCandidateBudgetSuffix(CandidatePool pool)
        {
            if (pool is not { BudgetExhausted: true })
                return string.Empty;

            return $" Candidate search budget exhausted after generating the configured maximum of {pool.CandidateBudget:N0} candidates.";
        }

        private static string FormatCandidateBudgetSuffix(IEnumerable<CandidatePool> pools)
        {
            if (pools == null)
                return string.Empty;

            List<CandidatePool> exhausted = pools
                .Where(pool => pool is { BudgetExhausted: true })
                .ToList();
            if (exhausted.Count == 0)
                return string.Empty;

            long budget = exhausted.Sum(pool => (long)pool.CandidateBudget);
            return $" Candidate search budget exhausted for {exhausted.Count:N0} target pool(s) after generating their configured maximum of {budget:N0} candidates.";
        }

        private static bool TryPlanAssetWithFilter(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            AssetAttemptPlanner.Catalog attemptCatalog,
            List<AssetDefinition> assetAttemptBuffer,
            CandidatePool candidates,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            System.Predicate<CandidateSeed> seedFilter,
            System.Func<AssetDefinition, bool> assetFilter)
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
                assetAttemptBuffer,
                seedFilter,
                assetFilter);

            if (found)
            {
                long planStart = StartPlanningStep(profiler);
                object relationAnchorIdentity = candidate.RelationAnchorIdentity;
                if (relationAnchorIdentity == null &&
                    asset.AssetRelativePlacement?.IsConfigured == true &&
                    RelativeAnchorProvider.TryFindAssetAnchor(
                        context,
                        asset,
                        candidate.Position,
                        CandidateFactory.GetBounds(candidate, asset).ToAxisAlignedBounds(),
                        PlacementSupportRules.GetDescriptor(candidate.SurfaceCollider),
                        out RelativeAnchor relationAnchor))
                {
                    relationAnchorIdentity = relationAnchor.Identity;
                }

                context.Plan.Add(asset, candidate, objectName, relationAnchorIdentity);
                StopAndRecordPlanningStep(profiler, PlanningProfileStep.PlanRecording, planStart);
            }

            return found;
        }

        private static void RecordPlacement(
            PlacementCandidate candidate,
            IDictionary<PlacementType, int> placed,
            SupportDistributionState supportDistribution)
        {
            if (placed != null)
            {
                placed[candidate.PlacementType] = placed.TryGetValue(candidate.PlacementType, out int count)
                    ? count + 1
                    : 1;
            }

            supportDistribution?.RecordPlacement(candidate);
        }
    }
}

