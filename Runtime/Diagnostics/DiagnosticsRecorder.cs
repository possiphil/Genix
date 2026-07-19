using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using UnityEngine;

namespace Genix.Diagnostics
{
    public sealed class DiagnosticsRecorder : IDiagnosticsSink
    {
        private readonly GenerationDiagnostics _diagnostics;
        private readonly DiagnosticsMode _mode;

        public GenerationDiagnostics Diagnostics => _diagnostics;

        public DiagnosticsRecorder(GenerationContext context, DiagnosticsMode mode, string styleName = "")
        {
            _mode = mode;

            _diagnostics = new GenerationDiagnostics(
                context.Area.SourceInfo.SourceName,
                styleName,
                context.StyleSettings,
                context.GenerationMode,
                context.PlacementTargets,
                context.TargetDistributionMode,
                context.TargetDistributionWeights,
                context.StyleSettings.algorithm,
                context.TargetBounds,
                context.Count,
                context.UseRandomSeed,
                context.RandomSeed,
                context.BestEffort,
                context.RelativePlacement);
        }

        public void RecordCandidatePool(int requestedCandidates, IReadOnlyList<CandidateSeed> seeds)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.Sampler.RequestedCandidates = requestedCandidates;
            _diagnostics.Sampler.GeneratedCandidates = seeds.Count;

            if (_mode != DiagnosticsMode.Detailed)
                return;

            foreach (CandidateSeed seed in seeds)
                _diagnostics.Sampler.CandidateSeeds.Add(seed.Position);
        }

        public void RecordCandidate(string assetId, string objectName, PlacementCandidate candidate, Bounds bounds, bool accepted, RejectionReason rejectionReason, string relatedObjectName = "")
        {
            if (_mode != DiagnosticsMode.Detailed)
                return;

            _diagnostics.Candidates.Add(new CandidateDiagnostic(assetId, objectName, candidate.Position, candidate.Rotation, bounds, candidate.PlacementType, accepted, rejectionReason, relatedObjectName));
        }

        public void RecordTestedCandidateSeed(Vector3 position)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.Sampler.TestedCandidateSeeds++;

            if (_mode != DiagnosticsMode.Detailed)
                return;

            _diagnostics.Sampler.TestedCandidateSeedPositions.Add(position);
        }

        public void RecordPlacement(AssetDefinition asset, string objectName, PlacementCandidate candidate)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.Placements.Add(new PlacementDiagnostic(asset.AssetName, objectName, candidate.Position, candidate.Rotation, candidate.PlacementType));
        }

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

        public void RecordStopReason(string stopReason)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            _diagnostics.StopReason = stopReason;
        }

        public void RecordClusterCenter(Vector3 position)
        {
            if (_mode != DiagnosticsMode.Detailed)
                return;

            _diagnostics.Sampler.ClusterCenters.Add(position);
        }

        public void RecordClusterCenters(IReadOnlyList<Vector3> clusterCenters)
        {
            if (_mode == DiagnosticsMode.None)
                return;

            if (clusterCenters == null)
                return;

            _diagnostics.Sampler.ClusterCenters.AddRange(clusterCenters);
        }

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
    }
}
