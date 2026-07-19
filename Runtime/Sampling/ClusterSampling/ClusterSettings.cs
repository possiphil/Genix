using System;

namespace Genix.Sampling.ClusterSampling
{
    [Serializable] public struct ClusterSettings
    {
        public int count;
        public float radius;

        public bool useMinCenterDistance;
        public float minCenterDistance;

        public ClusterSettings(int count, float radius, bool useMinCenterDistance = false, float minCenterDistance = 0f)
        {
            this.count = count;
            this.radius = radius;

            this.useMinCenterDistance = useMinCenterDistance;
            this.minCenterDistance = minCenterDistance;
        }
    }
}