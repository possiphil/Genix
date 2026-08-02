using UnityEngine;

namespace Genix.Areas
{
    /// <summary>Describes a projected point, normal, and optional collider on a placement surface.</summary>
    public readonly struct SurfacePoint
    {
        /// <summary>Gets position.</summary>
        public Vector3 Position { get; }
        /// <summary>Gets normal.</summary>
        public Vector3 Normal { get; }
        /// <summary>Gets surface collider.</summary>
        public Collider SurfaceCollider { get; }
        /// <summary>Gets voxel layer.</summary>
        public int? VoxelLayer { get; }

        /// <summary>Initializes a new instance of surface point.</summary>
        public SurfacePoint(Vector3 position, Vector3 normal, Collider surfaceCollider = null, int? voxelLayer = null)
        {
            Position = position;
            Normal = normal;
            SurfaceCollider = surfaceCollider;
            VoxelLayer = voxelLayer;
        }
    }
}
