using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Controls how a wall asset chooses its vertical position within the target area.</summary>
    public enum WallVerticalPlacementMode
    {
        /// <summary>Uses wall samples across the complete target height.</summary>
        [InspectorName("Full Wall")] FullWall,
        /// <summary>Places the asset's lower bound at one height above the target area's lower bound.</summary>
        [InspectorName("Fixed Height")] FixedHeight,
        /// <summary>Distributes the asset's lower bound between two heights above the target area's lower bound.</summary>
        [InspectorName("Height Range")] HeightRange
    }

    /// <summary>Optional horizontal relationship between a floor or ceiling asset and detected walls.</summary>
    public enum WallProximityMode
    {
        /// <summary>Does not constrain wall distance.</summary>
        [InspectorName("Any Distance")] AnyDistance,
        /// <summary>Requires the asset bounds to lie within a maximum wall distance.</summary>
        [InspectorName("Near Wall")] NearWall,
        /// <summary>Requires at least a minimum clearance from every detected wall.</summary>
        [InspectorName("Away From Wall")] AwayFromWall
    }
}
