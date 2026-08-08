using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>Primitive shape used by a collider-free placement exclusion region.</summary>
    public enum ExclusionRegionShape
    {
        /// <summary>Uses an oriented box.</summary>
        [InspectorName("Box")] Box,
        /// <summary>Uses a world-space sphere.</summary>
        [InspectorName("Sphere")] Sphere
    }

    /// <summary>
    /// Reserves a box- or sphere-shaped scene volume without adding a collider or affecting gameplay physics.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PlacementExclusionRegion : MonoBehaviour
    {
        private static readonly HashSet<PlacementExclusionRegion> ActiveRegions = new();

        [SerializeField] private ExclusionRegionShape shape = ExclusionRegionShape.Box;
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size = Vector3.one;
        [SerializeField, Min(0f)] private float radius = 0.5f;
        [SerializeField] private PlacementTarget affectedTargets = PlacementTarget.All;

        /// <summary>Gets the configured primitive shape.</summary>
        public ExclusionRegionShape Shape => shape;
        /// <summary>Gets the local center offset, interpreted without transform scale.</summary>
        public Vector3 Center => center;
        /// <summary>Gets the box dimensions, interpreted as world units.</summary>
        public Vector3 Size => PositiveSize(size);
        /// <summary>Gets the sphere radius in world units.</summary>
        public float Radius => Mathf.Max(0f, radius);
        /// <summary>Gets the placement targets rejected by this region.</summary>
        public PlacementTarget AffectedTargets => affectedTargets & PlacementTarget.All;
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

        /// <summary>Determines whether the supplied candidate bounds intersect this region for the requested target.</summary>
        public bool Intersects(OrientedBounds candidateBounds, PlacementType placementType)
        {
            if (!isActiveAndEnabled || (AffectedTargets & ToTarget(placementType)) == 0)
                return false;

            return shape == ExclusionRegionShape.Sphere
                ? IntersectsSphere(candidateBounds)
                : candidateBounds.Intersects(new OrientedBounds(WorldCenter, Size, transform.rotation));
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
        }

        private void OnDrawGizmosSelected()
        {
            Color fill = new(0.95f, 0.2f, 0.16f, 0.13f);
            Color wire = new(0.95f, 0.2f, 0.16f, 0.9f);

            if (shape == ExclusionRegionShape.Sphere)
            {
                Gizmos.color = fill;
                Gizmos.DrawSphere(WorldCenter, Radius);
                Gizmos.color = wire;
                Gizmos.DrawWireSphere(WorldCenter, Radius);
                return;
            }

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(WorldCenter, transform.rotation, Vector3.one);
            Gizmos.color = fill;
            Gizmos.DrawCube(Vector3.zero, Size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(Vector3.zero, Size);
            Gizmos.matrix = previous;
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

        private Bounds GetAxisAlignedBounds()
        {
            if (shape == ExclusionRegionShape.Sphere)
                return new Bounds(WorldCenter, Vector3.one * (Radius * 2f));

            return new OrientedBounds(WorldCenter, Size, transform.rotation).ToAxisAlignedBounds();
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
