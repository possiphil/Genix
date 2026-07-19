using Genix.Assets;
using UnityEngine;

namespace Genix.Diagnostics
{
    public readonly struct PlacementDiagnostic
    {
        public string AssetId { get; }
        public string ObjectName { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public PlacementType PlacementType { get; }

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
