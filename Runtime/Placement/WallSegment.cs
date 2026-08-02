using UnityEngine;

namespace Genix.Placement
{
    /// <summary>Immutable horizontal wall span with an inward-facing normal and optional voxel-layer identity.</summary>
    internal readonly struct WallSegment
    {
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public Vector3 InwardNormal { get; }
        public Vector3 Direction { get; }
        public float Length { get; }
        public int? VoxelLayer { get; }

        public WallSegment(Vector3 start, Vector3 end, Vector3 inwardNormal, int? voxelLayer = null)
        {
            Start = start;
            End = end;
            InwardNormal = inwardNormal;
            VoxelLayer = voxelLayer;

            Vector3 offset = end - start;
            Length = offset.magnitude;
            Direction = Length > 0f ? offset / Length : Vector3.zero;
        }
    }
}
