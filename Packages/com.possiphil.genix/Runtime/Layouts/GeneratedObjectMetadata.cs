using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Layouts
{
    /// <summary>Records the placement target used to create a generated scene object.</summary>
    public sealed class GeneratedObjectMetadata : MonoBehaviour
    {
        [SerializeField] private PlacementTarget placementTarget;
        [SerializeField] private PlacementSurfaceDescriptor supportSurface;
        [SerializeField] private AssetDefinition assetDefinition;
        [SerializeField] private string relationAnchorKey = string.Empty;

        /// <summary>Gets placement target.</summary>
        public PlacementTarget PlacementTarget => placementTarget;
        /// <summary>Gets the semantic surface that supported this object, if one was used.</summary>
        public PlacementSurfaceDescriptor SupportSurface => supportSurface;
        /// <summary>Gets the asset definition used to create this object, when available.</summary>
        public AssetDefinition AssetDefinition => assetDefinition;
        /// <summary>Gets the stable identity of the relation anchor selected by the planner.</summary>
        public string RelationAnchorKey => relationAnchorKey ?? string.Empty;

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(
            PlacementType placementType,
            PlacementSurfaceDescriptor placementSupport = null,
            AssetDefinition sourceAsset = null,
            string selectedRelationAnchorKey = "")
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
            assetDefinition = sourceAsset;
            relationAnchorKey = selectedRelationAnchorKey ?? string.Empty;
            hideFlags = HideFlags.HideInInspector;
        }

        private void OnDrawGizmosSelected()
        {
            if (!assetDefinition || !assetDefinition.ReserveClearance)
                return;

            OrientedBounds clearance = assetDefinition.CreateClearanceBounds(
                transform.position,
                transform.rotation);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.TRS(clearance.Center, clearance.Rotation, Vector3.one);
            Gizmos.color = new Color(1f, 0.72f, 0.08f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, clearance.Size);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
