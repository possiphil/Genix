using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Diagnostics
{
    /// <summary>Records generation events according to the selected diagnostics detail level.</summary>
    public sealed class DiagnosticsRecorder : IDiagnosticsSink
    {
        private readonly GenerationDiagnostics _diagnostics;
        private readonly DiagnosticsMode _mode;
        private readonly bool _recordAcceptedCandidates;

        /// <summary>Gets diagnostics.</summary>
        public GenerationDiagnostics Diagnostics => _diagnostics;

        /// <summary>Initializes a new instance of diagnostics recorder.</summary>
        public DiagnosticsRecorder(
            GenerationContext context,
            DiagnosticsMode mode,
            string styleName = "",
            bool recordAcceptedCandidates = false)
        {
            _mode = mode;
            _recordAcceptedCandidates = recordAcceptedCandidates;

            _diagnostics = new GenerationDiagnostics(
                context.Area.SourceInfo.SourceName,
                styleName,
                context.StyleSettings,
                context.PlacementTargets,
                context.TargetDistributionMode,
                context.TargetDistributionWeights,
                context.StyleSettings.algorithm,
                context.TargetBounds,
                context.Count,
                context.UseFixedSeed,
                context.RandomSeed,
                context.BestEffort,
                context.RelativePlacement,
                mode);
            _diagnostics.EnsureCapacity(
                GetCandidateDetailCapacity(context.Count, mode, recordAcceptedCandidates),
                context.Count,
                4);
        }

        /// <summary>Records candidate pool.</summary>
        public void RecordCandidatePool(int requestedCandidates, IReadOnlyList<CandidateSeed> seeds)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.Sampler.RequestedCandidates = Mathf.Max(_diagnostics.Sampler.RequestedCandidates, requestedCandidates);
            _diagnostics.Sampler.GeneratedCandidates += seeds.Count;
            RecordSupportCandidates(seeds);

            if (_mode != DiagnosticsMode.Detailed)
                return;

            foreach (CandidateSeed seed in seeds)
                _diagnostics.Sampler.CandidateSeeds.Add(seed.Position);
        }

        /// <summary>Records assets skipped by immutable support compatibility.</summary>
        public void RecordSupportPrefilterSkips(int count)
        {
            if (_mode != DiagnosticsMode.None && count > 0)
                _diagnostics.Sampler.SupportPrefilterSkips += count;
        }

        private void RecordSupportCandidates(IReadOnlyList<CandidateSeed> seeds)
        {
            foreach (CandidateSeed seed in seeds)
            {
                PlacementSurfaceDescriptor descriptor = PlacementSupportRules.GetDescriptor(seed.SurfaceCollider);

                if (!descriptor)
                    continue;

                string label = GetSupportLabel(descriptor);
                SupportCandidateDiagnostic aggregate = _diagnostics.Sampler.SupportCandidates
                    .FirstOrDefault(entry => entry.Label == label);

                if (aggregate == null)
                {
                    aggregate = new SupportCandidateDiagnostic(label);
                    _diagnostics.Sampler.SupportCandidates.Add(aggregate);
                }

                aggregate.Record(descriptor);
            }
        }

        private static string GetSupportLabel(PlacementSurfaceDescriptor descriptor)
        {
            string[] tags = descriptor.SurfaceTags
                .Where(tag => tag && tag.Category && tag.Category.SupportsSurfaces)
                .Select(tag => tag.DisplayName)
                .Distinct()
                .OrderBy(name => name)
                .ToArray();
            return tags.Length > 0 ? string.Join(", ", tags) : descriptor.name;
        }

        /// <summary>Determines whether per-candidate diagnostic details should be retained.</summary>
        public bool ShouldRecordCandidateDetails(bool accepted) =>
            _mode == DiagnosticsMode.Detailed ||
            (_recordAcceptedCandidates && accepted);

        /// <summary>Records candidate.</summary>
        public void RecordCandidate(string assetId, string objectName, PlacementCandidate candidate, Bounds bounds, bool accepted, RejectionReason rejectionReason, string relatedObjectName = "")
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.RecordCandidateOutcome(accepted, rejectionReason);

            if (!ShouldRecordCandidateDetails(accepted))
                return;

            _diagnostics.Candidates.Add(new CandidateDiagnostic(assetId, objectName, candidate.Position, candidate.Rotation, bounds, candidate.PlacementType, accepted, rejectionReason, relatedObjectName));
        }

        /// <summary>Records tested candidate seed.</summary>
        public void RecordTestedCandidateSeed(Vector3 position)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.Sampler.TestedCandidateSeeds++;

            if (_mode != DiagnosticsMode.Detailed)
                return;

            _diagnostics.Sampler.TestedCandidateSeedPositions.Add(position);
        }

        /// <summary>Records placement.</summary>
        public void RecordPlacement(AssetDefinition asset, string objectName, PlacementCandidate candidate)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.Placements.Add(new PlacementDiagnostic(asset.AssetName, objectName, candidate.Position, candidate.Rotation, candidate.PlacementType));
        }

        /// <summary>Removes placement records created by a rolled-back required composition.</summary>
        public void RollbackPlacements(int placementCount)
        {
            placementCount = Mathf.Clamp(placementCount, 0, _diagnostics.Placements.Count);
            if (placementCount < _diagnostics.Placements.Count)
            {
                int removed = _diagnostics.Placements.Count - placementCount;
                _diagnostics.Placements.RemoveRange(
                    placementCount,
                    removed);
                _diagnostics.RollbackAcceptedOutcomes(removed);
            }
        }

        /// <summary>Records target budgets.</summary>
        public void RecordTargetBudgets(IReadOnlyDictionary<PlacementType, int> targetCounts, IReadOnlyDictionary<PlacementType, int> placedCounts)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.TargetBudgets.Clear();

            foreach (PlacementType placementType in GetBudgetPlacementTypes(targetCounts, placedCounts))
            {
                int targetCount = targetCounts != null && targetCounts.TryGetValue(placementType, out int target)
                    ? target
                    : 0;
                int placedCount = placedCounts != null && placedCounts.TryGetValue(placementType, out int placed)
                    ? placed
                    : 0;

                _diagnostics.TargetBudgets.Add(new TargetBudgetDiagnostic(placementType, targetCount, placedCount));
            }
        }

        /// <summary>Records semantic support-surface budgets.</summary>
        public void RecordSupportBudgets(IReadOnlyList<SupportBudgetDiagnostic> budgets)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.SupportBudgets.Clear();
            if (budgets == null)
                return;

            foreach (SupportBudgetDiagnostic budget in budgets)
            {
                if (budget != null)
                    _diagnostics.SupportBudgets.Add(new SupportBudgetDiagnostic(
                        budget.Label,
                        budget.TargetCount,
                        budget.PlacedCount));
            }
        }

        /// <summary>Records stop reason.</summary>
        public void RecordStopReason(string stopReason)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.StopReason = stopReason;
        }

        /// <summary>Records cluster center.</summary>
        public void RecordClusterCenter(Vector3 position)
        {
            if (_mode != DiagnosticsMode.Detailed)
                return;

            _diagnostics.Sampler.ClusterCenters.Add(position);
        }

        /// <summary>Records cluster centers.</summary>
        public void RecordClusterCenters(IReadOnlyList<Vector3> clusterCenters)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            if (clusterCenters == null)
                return;

            _diagnostics.Sampler.ClusterCenters.AddRange(clusterCenters);
        }

        /// <summary>Records raw sample position.</summary>
        public void RecordRawSamplePosition(Vector3 position)
        {
            if (_mode != DiagnosticsMode.Detailed)
                return;

            _diagnostics.Sampler.RawSamplePositions.Add(position);
        }

        private static IEnumerable<PlacementType> GetBudgetPlacementTypes(
            IReadOnlyDictionary<PlacementType, int> targetCounts,
            IReadOnlyDictionary<PlacementType, int> placedCounts)
        {
            HashSet<PlacementType> placementTypes = new();

            if (targetCounts != null)
            {
                foreach (PlacementType placementType in targetCounts.Keys)
                    placementTypes.Add(placementType);
            }

            if (placedCounts != null)
            {
                foreach (PlacementType placementType in placedCounts.Keys)
                    placementTypes.Add(placementType);
            }

            return placementTypes.OrderBy(placementType => placementType);
        }

        private static int GetCandidateDetailCapacity(
            int requestedObjectCount,
            DiagnosticsMode mode,
            bool recordAcceptedCandidates)
        {
            if (mode == DiagnosticsMode.None)
                return 0;

            int safeCount = Mathf.Max(0, requestedObjectCount);

            if (mode == DiagnosticsMode.Detailed)
                return safeCount <= int.MaxValue / 2 ? safeCount * 2 : safeCount;

            return recordAcceptedCandidates ? safeCount : 0;
        }
    }
}
