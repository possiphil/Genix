using Genix.Assets;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>
    /// Inexpensive sampled placement position before asset-specific bounds, orientation, and surface fit are applied.
    /// </summary>
    public readonly struct CandidateSeed
    {
        /// <summary>Gets position.</summary>
        public Vector3 Position { get; }
        /// <summary>Gets rotation.</summary>
        public Quaternion Rotation { get; }
        /// <summary>Gets surface collider.</summary>
        public Collider SurfaceCollider { get; }
        /// <summary>Gets surface normal.</summary>
        public Vector3 SurfaceNormal { get; }
        /// <summary>Gets voxel layer.</summary>
        public int? VoxelLayer { get; }
        /// <summary>Gets placement type.</summary>
        public PlacementType PlacementType { get; }

        /// <summary>Initializes a new instance of candidate seed.</summary>
        public CandidateSeed(
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
