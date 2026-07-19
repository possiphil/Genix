using System;
using Genix.Sampling;

namespace Genix.Extensions
{
    public static class SamplingAlgorithmExtensions
    {
        public static string ToAlgorithmName(this SamplingAlgorithm algorithm)
        {
            return algorithm switch
            {
                SamplingAlgorithm.Random => "Random Sampling",
                SamplingAlgorithm.Grid => "Grid Sampling",
                SamplingAlgorithm.JitteredGrid => "Jittered Grid Sampling",
                SamplingAlgorithm.Cluster => "Cluster Sampling",
                SamplingAlgorithm.BridsonPoissonDisk => "Bridson Poisson Disk Sampling",
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, $"Unsupported sampling algorithm: {algorithm}.")
            };
        }
    }
}