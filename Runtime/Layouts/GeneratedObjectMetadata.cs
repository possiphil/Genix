using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Layouts
{
    /// <summary>Records the placement target used to create a generated scene object.</summary>
    public sealed class GeneratedObjectMetadata : MonoBehaviour
    {
        [SerializeField] private PlacementTarget placementTarget;

        /// <summary>Gets placement target.</summary>
        public PlacementTarget PlacementTarget => placementTarget;

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(PlacementType placementType)
        {
            placementTarget = placementType switch
            {
                PlacementType.Floor => PlacementTarget.Floor,
                PlacementType.Wall => PlacementTarget.Wall,
                PlacementType.Ceiling => PlacementTarget.Ceiling,
                PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                _ => PlacementTarget.None
            };
            hideFlags = HideFlags.HideInInspector;
        }
    }
}
