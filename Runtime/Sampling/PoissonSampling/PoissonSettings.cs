using System;

namespace Genix.Sampling.PoissonSampling
{
    [Serializable]
    public struct PoissonSettings
    {
        public float minDistance;
        public int attempts;

        public PoissonSettings(float minDistance, int attempts)
        {
            this.minDistance = minDistance;
            this.attempts = attempts;
        }
    }
}