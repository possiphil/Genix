using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Selects which supported probe position anchors an adaptively fitted asset.</summary>
    public enum SurfaceHeightMode
    {
        /// <summary>Uses the mean height, or mean wall depth, of all supporting footprint probes.</summary>
        [InspectorName("Average")] Average,
        /// <summary>Uses the lowest floor height or deepest wall probe to avoid floating edges.</summary>
        [InspectorName("Lowest")] Lowest,
        /// <summary>Uses the highest floor height or outermost wall probe to reduce surface penetration.</summary>
        [InspectorName("Highest")] Highest
    }
}
