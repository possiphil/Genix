using UnityEngine;

namespace Genix.Sampling
{
    /// <summary>Spatial pattern used to create inexpensive candidate positions before asset validation.</summary>
    public enum SamplingAlgorithm
    {
        /// <summary>Produces independent uniformly distributed candidates.</summary>
        [InspectorName("Random Sampling")] Random = 0,
        /// <summary>Produces candidates on a regular grid.</summary>
        [InspectorName("Grid Sampling")] Grid = 1,
        /// <summary>Adds bounded random offsets to a regular grid.</summary>
        [InspectorName("Jittered Grid Sampling")] JitteredGrid = 2,
        /// <summary>Groups candidates around randomly chosen cluster centers.</summary>
        [InspectorName("Cluster Sampling")] Cluster = 3,
        /// <summary>Produces even organic spacing using Bridson's Poisson-disk algorithm.</summary>
        [InspectorName("Bridson Poisson Disk Sampling")] BridsonPoissonDisk = 4
    }
}
