using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Controls how a floor or ceiling asset footprint is validated against its support surface.</summary>
    public enum SurfaceFitMode
    {
        /// <summary>Requires the nominal footprint to fit the discovered surface region.</summary>
        [InspectorName("Strict")] Strict,
        /// <summary>Probes the physical footprint to derive support, height, and surface normal.</summary>
        [InspectorName("Adaptive")] Adaptive
    }
}
