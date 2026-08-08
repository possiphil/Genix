using Genix.Assets;
using Genix.Core;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Layouts
{
    /// <summary>Records the placement target used to create a generated scene object.</summary>
    public sealed class GeneratedObjectMetadata : MonoBehaviour
    {
        [SerializeField] private PlacementTarget placementTarget;
        [SerializeField] private PlacementSurfaceDescriptor supportSurface;

        /// <summary>Gets placement target.</summary>
        public PlacementTarget PlacementTarget => placementTarget;
        /// <summary>Gets the semantic surface that supported this object, if one was used.</summary>
        public PlacementSurfaceDescriptor SupportSurface => supportSurface;

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(PlacementType placementType, PlacementSurfaceDescriptor placementSupport = null)
        {
            placementTarget = placementType switch
            {
                PlacementType.Floor => PlacementTarget.Floor,
                PlacementType.Wall => PlacementTarget.Wall,
                PlacementType.Ceiling => PlacementTarget.Ceiling,
                PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                _ => PlacementTarget.None
            };
            supportSurface = placementSupport;
            hideFlags = HideFlags.HideInInspector;
        }
    }
}
