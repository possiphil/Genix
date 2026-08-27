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
                        $"Best Effort planned {context.Plan.Count} of {context.Count} requested objects. {reason}")
                    : GenerationOutcome.Failed(context.Plan.Count, reason);
            }

            RecordSupportBudgets(diagnostics, supportDistribution);
            return CreateFinalOutcome(context, relationPlanner);
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
                        $"Best Effort planned {context.Plan.Count} of {context.Count} requested objects. {reason}")
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

        private static void RestoreCounts<TKey>(
            IDictionary<TKey, int> destination,
            IReadOnlyDictionary<TKey, int> checkpoint)
        {
            destination.Clear();
            foreach (KeyValuePair<TKey, int> entry in checkpoint)
                destination[entry.Key] = entry.Value;
        }

        private static GenerationOutcome CreateRequiredRelationFailureOutcome(
            GenerationContext context,
            RequiredRelationPlanner relationPlanner)
        {
            string message = $"Required asset relations could not be completed.{relationPlanner.FailureSummary}";
            return context.BestEffort && context.Plan.Count > 0
                ? GenerationOutcome.Partial(context.Plan.Count, $"Best Effort planned {context.Plan.Count} objects. {message}")
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
                ? GenerationOutcome.Partial(context.Plan.Count, $"Best Effort planned {context.Plan.Count} objects. {message}")
                : GenerationOutcome.Failed(context.Plan.Count, message);
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
