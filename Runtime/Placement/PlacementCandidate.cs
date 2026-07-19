using Genix.Assets;
using UnityEngine;

namespace Genix.Placement
{
    public readonly struct PlacementCandidate
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Collider SurfaceCollider { get; }
        public Vector3 SurfaceNormal { get; }
        public int? VoxelLayer { get; }
        public PlacementType PlacementType { get; }

        public PlacementCandidate(
            Vector3 position,
            Quaternion rotation,
            Collider surfaceCollider = null,
            Vector3 surfaceNormal = default,
            int? voxelLayer = null,
            PlacementType placementType = PlacementType.Floor)
        {
            Position = position;
            Rotation = rotation;
            SurfaceCollider = surfaceCollider;
            SurfaceNormal = surfaceNormal;
            VoxelLayer = voxelLayer;
            PlacementType = placementType;
        }
    }
}
