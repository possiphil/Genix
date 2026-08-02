using Genix.Areas;
using Genix.Assets;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>Asset-specific placement attempt derived from a candidate seed.</summary>
    public readonly struct PlacementCandidate
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
        /// <summary>Indicates whether surface fit.</summary>
        public bool HasSurfaceFit { get; }
        /// <summary>Gets surface fit.</summary>
        public SurfaceFitResult SurfaceFit { get; }

        /// <summary>Initializes a new instance of placement candidate.</summary>
        public PlacementCandidate(
            Vector3 position,
            Quaternion rotation,
            Collider surfaceCollider = null,
            Vector3 surfaceNormal = default,
            int? voxelLayer = null,
            PlacementType placementType = PlacementType.Floor,
            bool hasSurfaceFit = false,
            SurfaceFitResult surfaceFit = default)
        {
            Position = position;
            Rotation = rotation;
            SurfaceCollider = surfaceCollider;
            SurfaceNormal = surfaceNormal;
            VoxelLayer = voxelLayer;
            PlacementType = placementType;
            HasSurfaceFit = hasSurfaceFit;
            SurfaceFit = surfaceFit;
        }
    }
}
