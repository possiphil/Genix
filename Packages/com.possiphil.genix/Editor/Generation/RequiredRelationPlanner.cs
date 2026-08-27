using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Extensions;
using Genix.Placement;
using Genix.Profiling;

namespace Genix.Editor.Generation
{
    /// <summary>Plans mandatory per-anchor dependents as complete, budgeted local compositions.</summary>
    internal sealed class RequiredRelationPlanner
    {
        private const int MaximumRequiredBranchAttempts = 32;

        private readonly GenerationContext _context;
        private readonly IReadOnlyList<AssetDefinition> _assets;
        private readonly Dictionary<AssetDefinition, List<AssetDefinition>> _dependentsByAnchor = new();
        private readonly Dictionary<AssetDefinition, int> _closureCosts = new();
        private readonly HashSet<AssetDefinition> _cyclicAssets = new();
        private readonly Dictionary<LocalPoolKey, CandidatePool> _localCandidatePools = new();
        private readonly List<string> _failures = new();
        private string _lastRollbackFailure = string.Empty;
        private string _lastRequiredFailureDetail = string.Empty;

        public bool HasFailures => _failures.Count > 0;
        public string FailureSummary => _failures.Count == 0
            ? string.Empty
            : $" Required relations not completed: {string.Join("; ", _failures.Distinct())}.";
        public string LastRollbackSummary => string.IsNullOrEmpty(_lastRollbackFailure)
            ? string.Empty
            : $" Required composition rolled back: {_lastRollbackFailure}.";

        public RequiredRelationPlanner(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets)
        {
            _context = context;
            _assets = assets != null
                ? assets.Where(asset => asset && asset.Prefab).Distinct().ToList()
                : Array.Empty<AssetDefinition>();
            BuildGraph();
        }

        public bool CanStart(AssetDefinition asset, int remainingSlots) =>
            asset && !_cyclicAssets.Contains(asset) && GetClosureCost(asset) <= remainingSlots;

        public void CompleteExistingAnchors(
            Func<PlacementType, CandidatePool> getPool,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            Action<PlacementCandidate> onPlaced)
        {
            foreach (AssetDefinition dependent in _assets)
            {
                AssetRelativePlacementRule rule = dependent.AssetRelativePlacement;
                if (rule?.IsConfigured != true || !rule.HasMinimumPerAnchor)
                    continue;

                IReadOnlyList<RelativeAnchor> anchors =
                    RelativeAnchorProvider.CollectMatchingAssetAnchors(
                        _context,
                        rule,
                        includePlannedObjects: false,
                        dependentAsset: dependent);
                foreach (RelativeAnchor anchor in anchors)
                {
                    int missing = rule.MinimumPerAnchor -
                                  RelativeAnchorProvider.GetAssignedAssetCount(_context, dependent, rule, anchor);
                    for (int i = 0; i < missing; i++)
                    {
                        if (!TryPlanComposition(
                                dependent,
                                anchor,
                                getPool,
                                namer,
                                diagnostics,
                                profiler,
                                onPlaced))
                        {
                            AddFailure(dependent, anchor, missing - i);
                            break;
                        }
                    }
                }
            }


            foreach (AssetPoolAnchorGroupLimit group in _context.AssetPool.AnchorGroupLimits)
            {
                if (group is not { IsConfigured: true, HasMinimumPerAnchor: true })
                    continue;

                IReadOnlyList<RelativeAnchor> anchors =
                    RelativeAnchorProvider.CollectMatchingAssetAnchors(
                        _context,
                        group,
                        includePlannedObjects: false);
                foreach (RelativeAnchor anchor in anchors)
                {
                    CompleteAnchorGroup(
                        group,
                        anchor,
                        getPool,
                        namer,
                        diagnostics,
                        profiler,
                        onPlaced,
                        null,
                        new HashSet<AssetDefinition>());
                }
            }
        }

        public bool CompleteNewAnchor(
            PlannedObject root,
            Func<PlacementType, CandidatePool> getPool,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            Action<PlacementCandidate> onPlaced)
        {
            if (!root.Asset || _cyclicAssets.Contains(root.Asset))
                return false;

            int failureCheckpoint = _failures.Count;
            List<PlacementCandidate> placedCandidates = new();
            bool completed = CompleteDependents(
                RelativeAnchorProvider.CreatePlannedAnchor(root),
                getPool,
                namer,
                diagnostics,
                profiler,
                placedCandidates,
                new HashSet<AssetDefinition> { root.Asset });
            if (completed)
            {
                foreach (PlacementCandidate candidate in placedCandidates)
                    onPlaced?.Invoke(candidate);
            }
            else if (_failures.Count > failureCheckpoint)
            {
                _lastRollbackFailure = string.Join(
                    "; ",
                    _failures.Skip(failureCheckpoint).Distinct());
                _failures.RemoveRange(failureCheckpoint, _failures.Count - failureCheckpoint);
            }

            return completed;
        }

        private bool TryPlanComposition(
            AssetDefinition rootAsset,
            RelativeAnchor requiredAnchor,
            Func<PlacementType, CandidatePool> getPool,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            Action<PlacementCandidate> onPlaced)
        {
            List<PlacementCandidate> placedCandidates = new();
            if (!CanStart(rootAsset, _context.Count - _context.Plan.Count) ||
                !TryPlanRequiredBranch(
                    rootAsset,
                    requiredAnchor,
                    getPool,
                    namer,
                    diagnostics,
                    profiler,
                    placedCandidates,
                    new HashSet<AssetDefinition>(),
                    out _))
            {
                return false;
            }

            foreach (PlacementCandidate candidate in placedCandidates)
                onPlaced?.Invoke(candidate);
            return true;
        }

        private bool CompleteDependents(
            RelativeAnchor anchor,
            Func<PlacementType, CandidatePool> getPool,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            List<PlacementCandidate> placedCandidates,
            HashSet<AssetDefinition> ancestry)
        {
            if (anchor.Asset && _dependentsByAnchor.TryGetValue(anchor.Asset, out List<AssetDefinition> dependents))
            {
                foreach (AssetDefinition dependent in dependents)
                {
                    AssetRelativePlacementRule rule = dependent.AssetRelativePlacement;
                    int missing = rule.MinimumPerAnchor -
                                  RelativeAnchorProvider.GetAssignedAssetCount(_context, dependent, rule, anchor);

                    for (int i = 0; i < missing; i++)
                    {
                        if (ancestry.Contains(dependent) ||
                            !CanStart(dependent, _context.Count - _context.Plan.Count) ||
                            !TryPlanRequiredBranch(
                                dependent,
                                anchor,
                                getPool,
                                namer,
                                diagnostics,
                                profiler,
                                placedCandidates,
                                ancestry,
                                out _))
                        {
                            AddFailure(dependent, anchor, missing - i);
                            return false;
                        }
                    }
                }
            }

            foreach (AssetPoolAnchorGroupLimit group in _context.AssetPool.AnchorGroupLimits)
            {
                if (group is not { IsConfigured: true, HasMinimumPerAnchor: true } ||
                    group.Source is not (AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.GeneratedObjects) ||
                    !group.MatchesAnchor(anchor.Asset, anchor.AssetTags))
                {
                    continue;
                }

                if (!CompleteAnchorGroup(
                        group,
                        anchor,
                        getPool,
                        namer,
                        diagnostics,
                        profiler,
                        null,
                        placedCandidates,
                        ancestry))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CompleteAnchorGroup(
            AssetPoolAnchorGroupLimit group,
            RelativeAnchor anchor,
            Func<PlacementType, CandidatePool> getPool,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            Action<PlacementCandidate> onPlaced,
            List<PlacementCandidate> placedCandidates,
            HashSet<AssetDefinition> ancestry)
        {
            int assigned = RelativeAnchorProvider.GetAssignedAssetTagCount(_context, group, anchor);
            int missing = Math.Max(0, group.MinimumPerAnchor - assigned);
            if (missing == 0)
                return true;

            List<AssetDefinition> choices = _assets
                .Where(asset => IsEligibleGroupMember(asset, group, anchor))
                .OrderBy(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _context.Random.Shuffle(choices);

            for (int requiredIndex = 0; requiredIndex < missing; requiredIndex++)
            {
                bool placed = false;
                foreach (AssetDefinition choice in choices)
                {
                    if (ancestry.Contains(choice) ||
                        !CanStart(choice, _context.Count - _context.Plan.Count))
                    {
                        continue;
                    }

                    int candidateCheckpoint = placedCandidates?.Count ?? 0;
                    List<PlacementCandidate> branchCandidates = placedCandidates ?? new List<PlacementCandidate>();
                    if (TryPlanRequiredBranch(
                            choice,
                            anchor,
                            getPool,
                            namer,
                            diagnostics,
                            profiler,
                            branchCandidates,
                            ancestry,
                            out _))
                    {
                        if (placedCandidates == null)
                        {
                            foreach (PlacementCandidate candidate in branchCandidates)
                                onPlaced?.Invoke(candidate);
                        }

                        placed = true;
                        break;
                    }

                    if (placedCandidates != null && placedCandidates.Count > candidateCheckpoint)
                    {
                        placedCandidates.RemoveRange(
                            candidateCheckpoint,
                            placedCandidates.Count - candidateCheckpoint);
                    }
                }

                if (placed)
                    continue;

                AddGroupFailure(group, anchor, missing - requiredIndex);
                return false;
            }

            return true;
        }

        private bool TryPlanRequiredBranch(
            AssetDefinition asset,
            RelativeAnchor anchor,
            Func<PlacementType, CandidatePool> getPool,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            List<PlacementCandidate> placedCandidates,
            HashSet<AssetDefinition> ancestry,
            out PlannedObject plannedObject)
        {
            plannedObject = default;

            for (int attempt = 0; attempt < MaximumRequiredBranchAttempts; attempt++)
            {
                int planCheckpoint = _context.Plan.Count;
                int candidateCheckpoint = placedCandidates.Count;
                int failureCheckpoint = _failures.Count;

                if (!TryPlanRequiredAsset(
                        asset,
                        anchor,
                        getPool,
                        namer,
                        diagnostics,
                        profiler,
                        out PlannedObject attemptObject))
                {
                    return false;
                }

                placedCandidates.Add(attemptObject.Candidate);
                ancestry.Add(asset);
                bool completed = CompleteDependents(
                    RelativeAnchorProvider.CreatePlannedAnchor(attemptObject),
                    getPool,
                    namer,
                    diagnostics,
                    profiler,
                    placedCandidates,
                    ancestry);
                ancestry.Remove(asset);

                if (completed)
                {
                    plannedObject = attemptObject;
                    return true;
                }

                _context.Plan.RollbackTo(planCheckpoint);
                diagnostics.RollbackPlacements(planCheckpoint);
                if (placedCandidates.Count > candidateCheckpoint)
                {
                    placedCandidates.RemoveRange(
                        candidateCheckpoint,
                        placedCandidates.Count - candidateCheckpoint);
                }

                if (_failures.Count > failureCheckpoint)
                    _failures.RemoveRange(failureCheckpoint, _failures.Count - failureCheckpoint);
            }

            return false;
        }

        private bool TryPlanRequiredAsset(
            AssetDefinition asset,
            RelativeAnchor anchor,
            Func<PlacementType, CandidatePool> getPool,
            GeneratedObjectNamer namer,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            out PlannedObject plannedObject)
        {
            plannedObject = default;
            CandidatePool pool = getPool?.Invoke(asset.PlacementType);
            Dictionary<RejectionReason, int> rejectionCheckpoint = CaptureRejectionCounts(diagnostics);

            object previousRequiredAnchor = _context.RequiredAssetRelationAnchorIdentity;
            _context.RequiredAssetRelationAnchorIdentity = anchor.Identity;
            try
            {
                string objectName = namer.Next(asset);
                PlacementCandidate candidate = default;
                CandidatePool localPool = GetLocalPool(asset, anchor, diagnostics, profiler);
                bool found = localPool.Count > 0 && PlacementSolver.TryGetValidCandidate(
                    _context,
                    asset,
                    localPool,
                    out candidate,
                    diagnostics,
                    objectName,
                    profiler);
                if (!found)
                {
                    found = pool != null && pool.Count > 0 && PlacementSolver.TryGetValidCandidate(
                        _context,
                        asset,
                        pool,
                        out candidate,
                        diagnostics,
                        objectName,
                        profiler,
                        seed => RelativeAnchorProvider.IsPotentialSeedForAnchor(_context, seed, asset, anchor));
                }

                if (!found)
                {
                    _lastRequiredFailureDetail = FormatRejectionDelta(diagnostics, rejectionCheckpoint);
                    return false;
                }

                _lastRequiredFailureDetail = string.Empty;
                _context.Plan.Add(asset, candidate, objectName, anchor.Identity);
                plannedObject = _context.Plan.Objects[_context.Plan.Count - 1];
                return true;
            }
            finally
            {
                _context.RequiredAssetRelationAnchorIdentity = previousRequiredAnchor;
            }
        }

        private CandidatePool GetLocalPool(
            AssetDefinition asset,
            RelativeAnchor anchor,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler)
        {
            LocalPoolKey key = new(asset, anchor.Identity);
            if (_localCandidatePools.TryGetValue(key, out CandidatePool pool))
                return pool;

            List<CandidateSeed> seeds = RequiredRelationCandidateFactory.Create(
                _context,
                asset,
                anchor,
                profiler);
            diagnostics.RecordCandidatePool(seeds.Count, seeds);
            pool = new CandidatePool(seeds);
            _localCandidatePools[key] = pool;
            return pool;
        }

        private void BuildGraph()
        {
            foreach (AssetDefinition anchor in _assets)
            {
                List<AssetDefinition> dependents = _assets
                    .Where(dependent => IsMandatoryGeneratedDependent(dependent, anchor))
                    .OrderBy(dependent => dependent.AssetName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (dependents.Count > 0)
                    _dependentsByAnchor[anchor] = dependents;
            }

            foreach (AssetDefinition asset in _assets)
                ComputeClosureCost(asset, new HashSet<AssetDefinition>());
        }

        private bool IsMandatoryGeneratedDependent(AssetDefinition dependent, AssetDefinition anchor)
        {
            AssetRelativePlacementRule rule = dependent.AssetRelativePlacement;
            return rule?.IsConfigured == true &&
                   rule.HasMinimumPerAnchor &&
                   rule.Source is AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.GeneratedObjects &&
                   rule.Matches(anchor, anchor.SemanticTags);
        }

        private int GetClosureCost(AssetDefinition asset) =>
            _closureCosts.TryGetValue(asset, out int cost) ? cost : int.MaxValue;

        private int ComputeClosureCost(AssetDefinition asset, HashSet<AssetDefinition> ancestry)
        {
            if (_closureCosts.TryGetValue(asset, out int cached))
                return cached;
            if (!ancestry.Add(asset))
            {
                foreach (AssetDefinition cyclic in ancestry)
                    _cyclicAssets.Add(cyclic);
                return int.MaxValue;
            }

            long cost = 1;
            if (_dependentsByAnchor.TryGetValue(asset, out List<AssetDefinition> dependents))
            {
                foreach (AssetDefinition dependent in dependents)
                {
                    int dependentCost = ComputeClosureCost(dependent, ancestry);
                    if (dependentCost == int.MaxValue)
                    {
                        cost = int.MaxValue;
                        break;
                    }

                    cost += (long)dependent.AssetRelativePlacement.MinimumPerAnchor * dependentCost;
                    if (cost >= int.MaxValue)
                    {
                        cost = int.MaxValue;
                        break;
                    }
                }
            }

            ancestry.Remove(asset);
            int resolved = (int)cost;
            _closureCosts[asset] = resolved;
            return resolved;
        }

        private static bool IsEligibleGroupMember(
            AssetDefinition asset,
            AssetPoolAnchorGroupLimit group,
            RelativeAnchor anchor)
        {
            if (!group.MatchesMember(asset))
                return false;

            AssetRelativePlacementRule relation = asset.AssetRelativePlacement;
            return relation?.IsConfigured == true && anchor.Matches(relation);
        }

        private void AddFailure(AssetDefinition dependent, RelativeAnchor anchor, int missing)
        {
            string detail = string.IsNullOrEmpty(_lastRequiredFailureDetail)
                ? string.Empty
                : $" ({_lastRequiredFailureDetail})";
            _failures.Add($"{dependent.AssetName} {Math.Max(1, missing)} missing at {anchor.Name}{detail}");
        }

        private static Dictionary<RejectionReason, int> CaptureRejectionCounts(IDiagnosticsSink diagnostics) =>
            diagnostics is DiagnosticsRecorder recorder
                ? recorder.Diagnostics.CandidateRejectionCounts.ToDictionary(entry => entry.Key, entry => entry.Value)
                : null;

        private static string FormatRejectionDelta(
            IDiagnosticsSink diagnostics,
            IReadOnlyDictionary<RejectionReason, int> checkpoint)
        {
            if (checkpoint == null || diagnostics is not DiagnosticsRecorder recorder)
                return string.Empty;

            KeyValuePair<RejectionReason, int> top = recorder.Diagnostics.CandidateRejectionCounts
                .Select(entry => new KeyValuePair<RejectionReason, int>(
                    entry.Key,
                    entry.Value - (checkpoint.TryGetValue(entry.Key, out int previous) ? previous : 0)))
                .Where(entry => entry.Value > 0)
                .OrderByDescending(entry => entry.Value)
                .FirstOrDefault();
            return top.Value > 0 ? $"{top.Key.ToDisplayName()}: {top.Value}" : string.Empty;
        }

        private void AddGroupFailure(
            AssetPoolAnchorGroupLimit group,
            RelativeAnchor anchor,
            int missing)
        {
            string tagName = group.MemberTag ? group.MemberTag.DisplayName : "asset group";
            _failures.Add($"{tagName} group {Math.Max(1, missing)} missing at {anchor.Name}");
        }

        private readonly struct LocalPoolKey : IEquatable<LocalPoolKey>
        {
            private readonly AssetDefinition _asset;
            private readonly object _anchorIdentity;

            public LocalPoolKey(AssetDefinition asset, object anchorIdentity)
            {
                _asset = asset;
                _anchorIdentity = anchorIdentity;
            }

            public bool Equals(LocalPoolKey other) =>
                _asset == other._asset && Equals(_anchorIdentity, other._anchorIdentity);

            public override bool Equals(object obj) => obj is LocalPoolKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_asset ? _asset.GetHashCode() : 0) * 397) ^
                           (_anchorIdentity?.GetHashCode() ?? 0);
                }
            }
        }
    }
}
