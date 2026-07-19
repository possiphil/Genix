using System.Collections.Generic;
using Genix.Assets;
using Genix.Placement;
using UnityEngine;

namespace Genix.Diagnostics
{
    public interface IDiagnosticsSink
    {
        void RecordCandidatePool(int requestedCandidates, IReadOnlyList<CandidateSeed> seeds);
        void RecordCandidate(
            string assetId,
            string objectName,
            PlacementCandidate candidate,
            Bounds bounds,
            bool accepted,
            RejectionReason rejectionReason,
            string relatedObjectName = "");
        void RecordTestedCandidateSeed(Vector3 position);
        void RecordPlacement(AssetDefinition asset, string objectName, PlacementCandidate candidate);
        void RecordTargetBudgets(
            IReadOnlyDictionary<PlacementType, int> targetCounts,
            IReadOnlyDictionary<PlacementType, int> placedCounts);
        void RecordStopReason(string stopReason);
        void RecordClusterCenter(Vector3 position);
        void RecordClusterCenters(IReadOnlyList<Vector3> clusterCenters);
        void RecordRawSamplePosition(Vector3 position);
    }

    public sealed class NullDiagnosticsSink : IDiagnosticsSink
    {
        public static NullDiagnosticsSink Instance { get; } = new();

        private NullDiagnosticsSink()
        {
        }

        public void RecordCandidatePool(int requestedCandidates, IReadOnlyList<CandidateSeed> seeds) { }
        public void RecordCandidate(string assetId, string objectName, PlacementCandidate candidate, Bounds bounds, bool accepted, RejectionReason rejectionReason, string relatedObjectName = "") { }
        public void RecordTestedCandidateSeed(Vector3 position) { }
        public void RecordPlacement(AssetDefinition asset, string objectName, PlacementCandidate candidate) { }
        public void RecordTargetBudgets(IReadOnlyDictionary<PlacementType, int> targetCounts, IReadOnlyDictionary<PlacementType, int> placedCounts) { }
        public void RecordStopReason(string stopReason) { }
        public void RecordClusterCenter(Vector3 position) { }
        public void RecordClusterCenters(IReadOnlyList<Vector3> clusterCenters) { }
        public void RecordRawSamplePosition(Vector3 position) { }
    }
}
