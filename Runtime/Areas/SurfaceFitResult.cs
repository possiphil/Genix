using UnityEngine;

namespace Genix.Areas
{
    public readonly struct SurfaceFitResult
    {
        public Vector3 Position { get; }
        public Vector3 Normal { get; }
        public float HeightDifference { get; }
        public float SupportRatio { get; }

        public SurfaceFitResult(
            Vector3 position,
            Vector3 normal,
            float heightDifference,
            float supportRatio)
        {
            Position = position;
            Normal = normal;
            HeightDifference = heightDifference;
            SupportRatio = supportRatio;
        }
    }
}
