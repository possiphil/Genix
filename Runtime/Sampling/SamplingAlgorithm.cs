using UnityEngine;

namespace Genix.Sampling
{
    public enum SamplingAlgorithm
    {
        [InspectorName("Random Sampling")] Random = 0,
        [InspectorName("Grid Sampling")] Grid = 1,
        [InspectorName("Jittered Grid Sampling")] JitteredGrid = 2,
        [InspectorName("Cluster Sampling")] Cluster = 3,
        [InspectorName("Bridson Poisson Disk Sampling")] BridsonPoissonDisk = 4
    }
}
