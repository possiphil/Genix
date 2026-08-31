using System.Collections.Generic;
using Genix.Assets;
using Genix.Authoring;
using Genix.Core;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>Geometry source used by a placement exclusion region.</summary>
    public enum ExclusionRegionShape
    {
        /// <summary>Uses an oriented box.</summary>
        [InspectorName("Box")] Box,
        /// <summary>Uses a world-space sphere.</summary>
        [InspectorName("Sphere")] Sphere,
        /// <summary>Uses the enabled colliders below this object as exact exclusion geometry.</summary>
        [InspectorName("Child Colliders")] ChildColliders
    }

    /// <summary>
    /// Reserves primitive volume or existing child-collider geometry for procedural placement.
    /// Primitive regions do not add colliders; child-collider regions reuse authored geometry without changing it.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PlacementExclusionRegion : MonoBehaviour
    {
        private static readonly HashSet<PlacementExclusionRegion> ActiveRegions = new();
        private static readonly Collider[] OverlapBuffer = new Collider[256];

        [SerializeField] private ExclusionRegionShape shape = ExclusionRegionShape.Box;
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size = Vector3.one;
        [SerializeField, Min(0f)] private float radius = 0.5f;
        [SerializeField] private PlacementTarget affectedTargets = PlacementTarget.All;
        [SerializeField] private List<SemanticTag> exemptAssetTags = new();

        /// <summary>Gets the configured geometry source.</summary>
        public ExclusionRegionShape Shape => shape;
        /// <summary>Gets the local center offset, interpreted without transform scale.</summary>
        public Vector3 Center => center;
        /// <summary>Gets the box dimensions, interpreted as world units.</summary>
        public Vector3 Size => PositiveSize(size);
        /// <summary>Gets the sphere radius in world units.</summary>
        public float Radius => Mathf.Max(0f, radius);
        /// <summary>Gets the placement targets rejected by this region.</summary>
        public PlacementTarget AffectedTargets => affectedTargets & PlacementTarget.All;
        /// <summary>Gets asset-compatible tags that may overlap this region.</summary>
        public IReadOnlyList<SemanticTag> ExemptAssetTags => exemptAssetTags;
        /// <summary>Gets the world-space center.</summary>
        public Vector3 WorldCenter => transform.position + transform.rotation * center;

        /// <summary>Configures a box-shaped region.</summary>
        public void ConfigureBox(Vector3 localCenter, Vector3 dimensions, PlacementTarget targets = PlacementTarget.All)
        {
            shape = ExclusionRegionShape.Box;
            center = localCenter;
            size = PositiveSize(dimensions);
            affectedTargets = targets & PlacementTarget.All;
        }

        /// <summary>Configures a sphere-shaped region.</summary>
        public void ConfigureSphere(Vector3 localCenter, float worldRadius, PlacementTarget targets = PlacementTarget.All)
        {
            shape = ExclusionRegionShape.Sphere;
            center = localCenter;
            radius = Mathf.Max(0f, worldRadius);
            affectedTargets = targets & PlacementTarget.All;
        }

        /// <summary>Uses enabled colliders below this object as exclusion geometry.</summary>
        public void ConfigureChildColliders(PlacementTarget targets = PlacementTarget.All)
        {
            shape = ExclusionRegionShape.ChildColliders;
            affectedTargets = targets & PlacementTarget.All;
        }

        /// <summary>Replaces the tags whose assets may overlap this region.</summary>
        public void SetExemptAssetTags(IEnumerable<SemanticTag> tags)
        {
            exemptAssetTags = NormalizeTags(tags);
        }

        /// <summary>Determines whether the supplied candidate bounds intersect this region for the requested target.</summary>
        public bool Intersects(
            OrientedBounds candidateBounds,
            PlacementType placementType,
            AssetDefinition asset = null)
        {
            if (!isActiveAndEnabled ||
                (AffectedTargets & ToTarget(placementType)) == 0 ||
                IsExempt(asset))
            {
                return false;
            }

            return shape switch
            {
                ExclusionRegionShape.Sphere => IntersectsSphere(candidateBounds),
                ExclusionRegionShape.ChildColliders => IntersectsChildColliders(candidateBounds),
                _ => candidateBounds.Intersects(new OrientedBounds(WorldCenter, Size, transform.rotation))
            };
        }

        /// <summary>Copies active regions whose broad bounds overlap the selected target bounds.</summary>
        internal static IReadOnlyList<PlacementExclusionRegion> Collect(Bounds targetBounds)
        {
            List<PlacementExclusionRegion> regions = new();
            ActiveRegions.RemoveWhere(region => !region);

            foreach (PlacementExclusionRegion region in ActiveRegions)
            {
                if (!region || !region.isActiveAndEnabled || region.AffectedTargets == PlacementTarget.None)
                    continue;

                if (region.GetAxisAlignedBounds().Intersects(targetBounds))
                    regions.Add(region);
            }

            return regions;
        }

        private void OnEnable() => ActiveRegions.Add(this);
        private void OnDisable() => ActiveRegions.Remove(this);
        private void OnDestroy() => ActiveRegions.Remove(this);

        private void OnValidate()
        {
            size = PositiveSize(size);
            radius = Mathf.Max(0f, radius);
            affectedTargets &= PlacementTarget.All;
            exemptAssetTags = NormalizeTags(exemptAssetTags);
        }

        private void OnDrawGizmos()
        {
            if (AuthoringVisualization.ShowSceneGuides)
                DrawRegion(selected: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawRegion(selected: true);
        }

        private void DrawRegion(bool selected)
        {
            Color fill = selected
                ? new Color(0.95f, 0.2f, 0.16f, 0.13f)
                : new Color(0.95f, 0.2f, 0.16f, 0.035f);
            Color wire = selected
                ? new Color(0.95f, 0.2f, 0.16f, 0.9f)
                : new Color(0.95f, 0.2f, 0.16f, 0.5f);
            Color previousColor = Gizmos.color;

            if (shape == ExclusionRegionShape.ChildColliders)
            {
                Gizmos.color = wire;
                foreach (Collider collider in GetComponentsInChildren<Collider>())
                {
                    if (collider && collider.enabled)
                        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
                }
                Gizmos.color = previousColor;
                return;
            }

            if (shape == ExclusionRegionShape.Sphere)
            {
                Gizmos.color = fill;
                Gizmos.DrawSphere(WorldCenter, Radius);
                Gizmos.color = wire;
                Gizmos.DrawWireSphere(WorldCenter, Radius);
                Gizmos.color = previousColor;
                return;
            }

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(WorldCenter, transform.rotation, Vector3.one);
            Gizmos.color = fill;
            Gizmos.DrawCube(Vector3.zero, Size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(Vector3.zero, Size);
            Gizmos.matrix = previous;
            Gizmos.color = previousColor;
        }

        private bool IntersectsSphere(OrientedBounds candidateBounds)
        {
            Vector3 localCenter = Quaternion.Inverse(candidateBounds.Rotation) * (WorldCenter - candidateBounds.Center);
            Vector3 extents = candidateBounds.Extents;
            Vector3 closest = new(
                Mathf.Clamp(localCenter.x, -extents.x, extents.x),
                Mathf.Clamp(localCenter.y, -extents.y, extents.y),
                Mathf.Clamp(localCenter.z, -extents.z, extents.z));
            return (localCenter - closest).sqrMagnitude <= Radius * Radius;
        }

        private bool IntersectsChildColliders(OrientedBounds candidateBounds)
        {
            int count = Physics.OverlapBoxNonAlloc(
                candidateBounds.Center,
                candidateBounds.Extents,
                OverlapBuffer,
                candidateBounds.Rotation,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider collider = OverlapBuffer[i];
                OverlapBuffer[i] = null;
                if (collider && collider.transform.IsChildOf(transform))
                    return true;
            }

            return false;
        }

        private bool IsExempt(AssetDefinition asset)
        {
            if (!asset || exemptAssetTags == null)
                return false;

            foreach (SemanticTag tag in exemptAssetTags)
            {
                if (tag && asset.HasTag(tag))
                    return true;
            }

            return false;
        }

        private Bounds GetAxisAlignedBounds()
        {
            if (shape == ExclusionRegionShape.Sphere)
                return new Bounds(WorldCenter, Vector3.one * (Radius * 2f));

            if (shape == ExclusionRegionShape.ChildColliders)
            {
                Bounds combined = new(transform.position, Vector3.zero);
                bool hasBounds = false;
                foreach (Collider collider in GetComponentsInChildren<Collider>())
                {
                    if (!collider || !collider.enabled)
                        continue;

                    if (!hasBounds)
                    {
                        combined = collider.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combined.Encapsulate(collider.bounds);
                    }
                }

                return combined;
            }

            return new OrientedBounds(WorldCenter, Size, transform.rotation).ToAxisAlignedBounds();
        }

        private static List<SemanticTag> NormalizeTags(IEnumerable<SemanticTag> tags)
        {
            List<SemanticTag> normalized = new();
            if (tags == null)
                return normalized;

            foreach (SemanticTag tag in tags)
            {
                if (tag && tag.SupportsAssets && !normalized.Contains(tag))
                    normalized.Add(tag);
            }

            return normalized;
        }

        private static PlacementTarget ToTarget(PlacementType placementType) =>
            placementType switch
            {
                PlacementType.Floor => PlacementTarget.Floor,
                PlacementType.Wall => PlacementTarget.Wall,
                PlacementType.Ceiling => PlacementTarget.Ceiling,
                PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                _ => PlacementTarget.None
            };

        private static Vector3 PositiveSize(Vector3 value) =>
            new(
                Mathf.Max(0.01f, value.x),
                Mathf.Max(0.01f, value.y),
                Mathf.Max(0.01f, value.z));
    }
}
