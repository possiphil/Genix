using System;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.JitteredGridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Sampling.RandomSampling;

namespace Genix.Sampling
{
    internal static class SamplerFactory
    {
        public static ISampler Create(SamplingAlgorithm algorithm)
        {
            return algorithm switch
            {
                SamplingAlgorithm.Random => new RandomSampler(),
                SamplingAlgorithm.Grid => new GridSampler(),
                SamplingAlgorithm.JitteredGrid => new JitteredGridSampler(),
                SamplingAlgorithm.Cluster => new ClusterSampler(),
                SamplingAlgorithm.BridsonPoissonDisk => new BridsonPoissonDiskSampler(),
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, $"Unsupported sampling algorithm: {algorithm}.")
            };
        }
    }
}