using System;

namespace Genix.Sampling.PoissonSampling
{
    /// <summary>Parameters for Bridson Poisson-disk sampling.</summary>
    [Serializable]
    public struct PoissonSettings
    {
        /// <summary>Minimum distance between generated samples in world units.</summary>
        public float minDistance;
        /// <summary>Candidate attempts made around each active sample before retiring it.</summary>
        public int attempts;

        /// <summary>Creates Poisson-disk settings.</summary>
        /// <param name="minDistance">Minimum sample separation in world units.</param>
        /// <param name="attempts">Attempts per active sample.</param>
        public PoissonSettings(float minDistance, int attempts)
        {
            this.minDistance = minDistance;
            this.attempts = attempts;
        }
    }
}
