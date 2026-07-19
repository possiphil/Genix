using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Layouts
{
    public sealed class GeneratedObjectMetadata : MonoBehaviour
    {
        [SerializeField] private PlacementTarget placementTarget;

        public PlacementTarget PlacementTarget => placementTarget;

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
