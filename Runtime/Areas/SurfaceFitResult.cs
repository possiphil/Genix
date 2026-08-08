using UnityEngine;

namespace Genix.Areas
{
    /// <summary>Contains the adjusted pose and support variation produced by a surface-fit query.</summary>
    public readonly struct SurfaceFitResult
    {
        /// <summary>Gets position.</summary>
        public Vector3 Position { get; }
        /// <summary>Gets normal.</summary>
        public Vector3 Normal { get; }
        /// <summary>Gets the supported height or wall-depth variation.</summary>
        public float HeightDifference { get; }
        /// <summary>Gets support ratio.</summary>
        public float SupportRatio { get; }

        /// <summary>Initializes a new instance of surface fit result.</summary>
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
