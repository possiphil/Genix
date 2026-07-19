using UnityEngine;

namespace Genix.Areas
{
    public readonly struct SurfacePoint
    {
        public Vector3 Position { get; }
        public Vector3 Normal { get; }
        public Collider SurfaceCollider { get; }
        public int? VoxelLayer { get; }

        public SurfacePoint(Vector3 position, Vector3 normal, Collider surfaceCollider = null, int? voxelLayer = null)
        {
            Position = position;
            Normal = normal;
            SurfaceCollider = surfaceCollider;
            VoxelLayer = voxelLayer;
        }
    }
}
