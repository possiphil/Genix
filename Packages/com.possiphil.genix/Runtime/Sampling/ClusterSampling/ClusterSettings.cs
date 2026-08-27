using System;

namespace Genix.Sampling.ClusterSampling
{
    /// <summary>Parameters for grouping candidates around randomly selected centers.</summary>
    [Serializable]
    public struct ClusterSettings
    {
        /// <summary>Number of cluster centers.</summary>
        public int count;
        /// <summary>Maximum candidate distance from its center in world units.</summary>
        public float radius;

        /// <summary>Whether cluster centers must maintain a minimum separation.</summary>
        public bool useMinCenterDistance;
        /// <summary>Required center separation in world units when enabled.</summary>
        public float minCenterDistance;

        /// <summary>Creates cluster-sampling settings.</summary>
        /// <param name="count">Number of centers.</param>
        /// <param name="radius">Cluster radius in world units.</param>
        /// <param name="useMinCenterDistance">Whether center separation is enforced.</param>
        /// <param name="minCenterDistance">Required center separation in world units.</param>
        public ClusterSettings(int count, float radius, bool useMinCenterDistance = false, float minCenterDistance = 0f)
        {
            this.count = count;
            this.radius = radius;

            this.useMinCenterDistance = useMinCenterDistance;
            this.minCenterDistance = minCenterDistance;
        }
    }
}
