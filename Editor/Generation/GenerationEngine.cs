using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;

namespace Genix.Editor.Generation
{
    internal static class GenerationEngine
    {
        public static GenerationOutcome BuildPlan(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            IDiagnosticsSink diagnostics)
        {
            PlacementTarget usableTargets = TargetDistributionPolicy.GetUsableTargets(context, assets);

            if (usableTargets == PlacementTarget.None)
            {
                return GenerationOutcome.Failed(
                    "No selected placement target has usable assets after prefab and semantic tag filtering.");
            }

            GeneratedObjectNamer namer = new(context.GeneratedParent);

            return TargetDistributionPolicy.IsActive(context)
                ? BuildDistributedPlan(context, assets, usableTargets, namer, diagnostics)
                : BuildRandomPlan(context, assets, usableTargets, namer, diagnostics);
        }

        private static GenerationOutcome BuildRandomPlan(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementTarget usableTargets,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics)
        {
            CandidatePool candidates = PlacementSolver.CreateCandidatePool(context, diagnostics, usableTargets);

            for (int i = 0; i < context.Count; i++)
            {
                if (TryPlanAsset(context, assets, candidates, namer, diagnostics))
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
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics)
        {
            List<PlacementType> placementTypes = TargetDistributionPolicy.GetPlacementTypes(context, usableTargets);
            Dictionary<PlacementType, int> targets = TargetDistributionPolicy.CreateTargets(context, placementTypes);
            Dictionary<PlacementType, int> placed = placementTypes.ToDictionary(type => type, _ => 0);
            Dictionary<PlacementType, CandidatePool> pools = PlacementSolver.CreateCandidatePoolsByPlacementType(
                context,
                diagnostics,
                usableTargets);
            HashSet<PlacementType> exhausted = new();

            for (int i = 0; i < context.Count; i++)
            {
                if (TryPlanDistributedAsset(
                        context,
                        assets,
                        targets,
                        placed,
                        pools,
                        exhausted,
                        namer,
                        diagnostics))
                {
                    continue;
                }

                diagnostics.RecordTargetBudgets(targets, placed);
                string summary = TargetDistributionPolicy.FormatTargets(targets, placed);
                string reason = $"The remaining target distribution has no valid placement. Target budgets: {summary}.";

                return context.BestEffort && context.Plan.Count > 0
                    ? GenerationOutcome.Partial(
                        context.Plan.Count,
                        $"Best Effort planned {context.Plan.Count} of {context.Count} requested objects. {reason}")
                    : GenerationOutcome.Failed(context.Plan.Count, reason);
            }

            diagnostics.RecordTargetBudgets(targets, placed);
            return GenerationOutcome.Completed(context.Plan.Count);
        }

        private static bool TryPlanDistributedAsset(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            IReadOnlyDictionary<PlacementType, int> targets,
            Dictionary<PlacementType, int> placed,
            IReadOnlyDictionary<PlacementType, CandidatePool> pools,
            ISet<PlacementType> exhausted,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics)
        {
            while (TargetDistributionPolicy.TrySelectTarget(
                       context,
                       targets,
                       placed,
                       pools,
                       exhausted,
                       out PlacementType targetType))
            {
                if (TryPlanAssetOnTarget(
                        context,
                        assets,
                        targetType,
                        pools,
                        placed,
                        namer,
                        diagnostics))
                {
                    return true;
                }

                exhausted.Add(targetType);
            }

            foreach (PlacementType overflowType in TargetDistributionPolicy.GetOverflowTypes(
                         targets.Keys,
                         pools,
                         exhausted,
                         context))
            {
                if (TryPlanAssetOnTarget(
                        context,
                        assets,
                        overflowType,
                        pools,
                        placed,
                        namer,
                        diagnostics))
                {
                    return true;
                }

                exhausted.Add(overflowType);
            }

            return false;
        }

        private static bool TryPlanAssetOnTarget(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementType placementType,
            IReadOnlyDictionary<PlacementType, CandidatePool> pools,
            IDictionary<PlacementType, int> placed,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics)
        {
            if (!TargetDistributionPolicy.HasAssets(assets, placementType) ||
                !pools.TryGetValue(placementType, out CandidatePool candidates) ||
                candidates.Count <= 0 ||
                !TryPlanAsset(context, assets, candidates, namer, diagnostics))
            {
                return false;
            }

            placed[placementType] = placed.TryGetValue(placementType, out int count) ? count + 1 : 1;
            return true;
        }

        private static bool TryPlanAsset(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            CandidatePool candidates,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics)
        {
            bool found = PlacementSolver.TryGetValidCandidateForAnyAsset(
                context,
                assets,
                candidates,
                namer.Next,
                out AssetDefinition asset,
                out PlacementCandidate candidate,
                out string objectName,
                diagnostics);

            if (found)
                context.Plan.Add(asset, candidate, objectName);

            return found;
        }
    }

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
