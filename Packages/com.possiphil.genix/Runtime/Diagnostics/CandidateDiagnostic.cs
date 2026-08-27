using Genix.Placement;
using Genix.Assets;
using UnityEngine;

namespace Genix.Diagnostics
{
    /// <summary>Stores candidate data.</summary>
    public readonly struct CandidateDiagnostic
    {
        /// <summary>Gets asset id.</summary>
        public string AssetId { get; }
        /// <summary>Gets object name.</summary>
        public string ObjectName { get; }
        /// <summary>Gets position.</summary>
        public Vector3 Position { get; }
        /// <summary>Gets rotation.</summary>
        public Quaternion Rotation { get; }
        /// <summary>Gets bounds.</summary>
        public Bounds Bounds { get; }
        /// <summary>Gets placement type.</summary>
        public PlacementType PlacementType { get; }
        /// <summary>Indicates whether accepted.</summary>
        public bool Accepted { get; }
        /// <summary>Gets rejection reason.</summary>
        public RejectionReason RejectionReason { get; }
        /// <summary>Gets related object name.</summary>
        public string RelatedObjectName { get; }

        /// <summary>Initializes a new instance of candidate diagnostic.</summary>
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
