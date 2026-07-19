using Genix.Placement;
using Genix.Assets;
using UnityEngine;

namespace Genix.Diagnostics
{
    public readonly struct CandidateDiagnostic
    {
        public string AssetId { get; }
        public string ObjectName { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Bounds Bounds { get; }
        public PlacementType PlacementType { get; }
        public bool Accepted { get; }
        public RejectionReason RejectionReason { get; }
        public string RelatedObjectName { get; }

        public CandidateDiagnostic(string assetId, string objectName, Vector3 position, Quaternion rotation, Bounds bounds, PlacementType placementType, bool accepted,
            RejectionReason rejectionReason, string relatedObjectName)
        {
            AssetId = assetId;
            ObjectName = objectName;
            Position = position;
            Rotation = rotation;
            Bounds = bounds;
            PlacementType = placementType;
            Accepted = accepted;
            RejectionReason = rejectionReason;
            RelatedObjectName = relatedObjectName;
        }
    }
}
