using UnityEngine;

namespace Genix.Sampling
{
    public enum SamplingAlgorithm
    {
        [InspectorName("Random Sampling")] Random,
        [InspectorName("Grid Sampling")] Grid,
        [InspectorName("Jittered Grid Sampling")] JitteredGrid,
        [InspectorName("Cluster Sampling")] Cluster,
        [InspectorName("Bridson Poisson Disk Sampling")] BridsonPoissonDisk
    }
}
