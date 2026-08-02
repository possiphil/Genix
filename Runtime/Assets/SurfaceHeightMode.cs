using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Selects which supported probe height positions an adaptively fitted asset.</summary>
    public enum SurfaceHeightMode
    {
        /// <summary>Uses the mean height of all supporting footprint probes.</summary>
        [InspectorName("Average")] Average,
        /// <summary>Uses the lowest supporting probe to avoid floating edges.</summary>
        [InspectorName("Lowest")] Lowest,
        /// <summary>Uses the highest supporting probe to reduce surface penetration.</summary>
        [InspectorName("Highest")] Highest
    }
}
