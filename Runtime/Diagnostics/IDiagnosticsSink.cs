using System.Collections.Generic;
using Genix.Assets;
using Genix.Placement;
using UnityEngine;

namespace Genix.Diagnostics
{
    /// <summary>Receives aggregate and optional per-candidate diagnostics from generation.</summary>
    public interface IDiagnosticsSink
    {
        /// <summary>Determines whether per-candidate diagnostic details should be retained.</summary>
        bool ShouldRecordCandidateDetails(bool accepted);
        /// <summary>Records candidate pool.</summary>
        void RecordCandidatePool(int requestedCandidates, IReadOnlyList<CandidateSeed> seeds);
        /// <summary>Records candidate.</summary>
        void RecordCandidate(
            string assetId,
            string objectName,
            PlacementCandidate candidate,
            Bounds bounds,
            bool accepted,
            RejectionReason rejectionReason,
            string relatedObjectName = "");
        /// <summary>Records tested candidate seed.</summary>
        void RecordTestedCandidateSeed(Vector3 position);
        /// <summary>Records placement.</summary>
        void RecordPlacement(AssetDefinition asset, string objectName, PlacementCandidate candidate);
        /// <summary>Records target budgets.</summary>
        void RecordTargetBudgets(
            IReadOnlyDictionary<PlacementType, int> targetCounts,
            IReadOnlyDictionary<PlacementType, int> placedCounts);
        /// <summary>Records stop reason.</summary>
        void RecordStopReason(string stopReason);
        /// <summary>Records cluster center.</summary>
        void RecordClusterCenter(Vector3 position);
        /// <summary>Records cluster centers.</summary>
        void RecordClusterCenters(IReadOnlyList<Vector3> clusterCenters);
        /// <summary>Records raw sample position.</summary>
        void RecordRawSamplePosition(Vector3 position);
    }

    /// <summary>Discards diagnostics with minimal overhead when capture is disabled.</summary>
    public sealed class NullDiagnosticsSink : IDiagnosticsSink
    {
        /// <summary>Gets the shared stateless sink.</summary>
        public static NullDiagnosticsSink Instance { get; } = new();

        private NullDiagnosticsSink()
        {
        }

        /// <summary>Determines whether per-candidate diagnostic details should be retained.</summary>
        public bool ShouldRecordCandidateDetails(bool accepted) => false;
        /// <summary>Records candidate pool.</summary>
        public void RecordCandidatePool(int requestedCandidates, IReadOnlyList<CandidateSeed> seeds) { }
        /// <summary>Records candidate.</summary>
        public void RecordCandidate(string assetId, string objectName, PlacementCandidate candidate, Bounds bounds, bool accepted, RejectionReason rejectionReason, string relatedObjectName = "") { }
        /// <summary>Records tested candidate seed.</summary>
        public void RecordTestedCandidateSeed(Vector3 position) { }
        /// <summary>Records placement.</summary>
        public void RecordPlacement(AssetDefinition asset, string objectName, PlacementCandidate candidate) { }
        /// <summary>Records target budgets.</summary>
        public void RecordTargetBudgets(IReadOnlyDictionary<PlacementType, int> targetCounts, IReadOnlyDictionary<PlacementType, int> placedCounts) { }
        /// <summary>Records stop reason.</summary>
        public void RecordStopReason(string stopReason) { }
        /// <summary>Records cluster center.</summary>
        public void RecordClusterCenter(Vector3 position) { }
        /// <summary>Records cluster centers.</summary>
        public void RecordClusterCenters(IReadOnlyList<Vector3> clusterCenters) { }
        /// <summary>Records raw sample position.</summary>
        public void RecordRawSamplePosition(Vector3 position) { }
    }
}
