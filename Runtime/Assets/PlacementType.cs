using UnityEngine;

namespace Genix.Assets
{
    public enum PlacementType
    {
        [InspectorName("Floor")] Floor,
        [InspectorName("Wall")] Wall,
        [InspectorName("Ceiling")] Ceiling,
        [InspectorName("Inside Space")] InsideSpace
    }
}
