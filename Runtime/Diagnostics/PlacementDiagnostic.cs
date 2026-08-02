using Genix.Assets;
using UnityEngine;

namespace Genix.Diagnostics
{
    /// <summary>Stores placement data.</summary>
    public readonly struct PlacementDiagnostic
    {
        /// <summary>Gets asset id.</summary>
        public string AssetId { get; }
        /// <summary>Gets object name.</summary>
        public string ObjectName { get; }
        /// <summary>Gets position.</summary>
        public Vector3 Position { get; }
        /// <summary>Gets rotation.</summary>
        public Quaternion Rotation { get; }
        /// <summary>Gets placement type.</summary>
        public PlacementType PlacementType { get; }

        /// <summary>Initializes a new instance of placement diagnostic.</summary>
        public PlacementDiagnostic(string assetId, string objectName, Vector3 position, Quaternion rotation, PlacementType placementType)
        {
            AssetId = assetId;
            ObjectName = objectName;
            Position = position;
            Rotation = rotation;
            PlacementType = placementType;
        }
    }
}
