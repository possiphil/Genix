using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Surface or volume class on which an asset may be placed.</summary>
    public enum PlacementType
    {
        /// <summary>Places the asset on an upward-facing surface.</summary>
        [InspectorName("Floor")] Floor,
        /// <summary>Places the asset against a near-vertical surface.</summary>
        [InspectorName("Wall")] Wall,
        /// <summary>Places the asset beneath a downward-facing surface.</summary>
        [InspectorName("Ceiling")] Ceiling,
        /// <summary>Places the asset freely within valid volume cells.</summary>
        [InspectorName("Inside Space")] InsideSpace
    }
}
