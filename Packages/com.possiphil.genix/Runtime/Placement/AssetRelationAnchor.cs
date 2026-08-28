using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Geometry;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>
    /// Gives a fixed scene object an asset identity, semantic tags, bounds, and local forward direction for asset-relative placement.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssetRelationAnchor : MonoBehaviour
    {
        [SerializeField] private AssetDefinition representedAsset;
        [SerializeField] private List<SemanticTag> assetTags = new();
        [SerializeField] private PlacementSurfaceDescriptor supportSurface;
        [SerializeField, Range(-180f, 180f)] private float forwardYawOffset;
        [SerializeField] private bool useCustomBounds;
        [SerializeField] private Vector3 boundsCenter;
        [SerializeField] private Vector3 boundsSize = Vector3.one;
        [SerializeField] private bool alwaysShowAnchor = false;

        /// <summary>Gets the concrete asset identity represented by this scene object, when available.</summary>
        public AssetDefinition RepresentedAsset => representedAsset;
        /// <summary>Gets additional asset-compatible tags exposed by this anchor.</summary>
        public IReadOnlyList<SemanticTag> AssetTags => assetTags;
        /// <summary>Gets the surface supporting this fixed anchor for same-support relation rules.</summary>
        public PlacementSurfaceDescriptor SupportSurface =>
            supportSurface ? supportSurface : GetComponent<PlacementSurfaceDescriptor>();
        /// <summary>Gets the anchor's world-space forward direction.</summary>
        public Vector3 Forward => RelationRotation * Vector3.forward;
        /// <summary>Gets the anchor's world-space right direction.</summary>
        public Vector3 Right => RelationRotation * Vector3.right;
        /// <summary>Gets the semantic yaw offset from the scene object's local +Z axis.</summary>
        public float ForwardYawOffset => forwardYawOffset;

        private Quaternion RelationRotation =>
            transform.rotation * Quaternion.Euler(0f, forwardYawOffset, 0f);

        /// <summary>Replaces the represented concrete asset.</summary>
        public void SetRepresentedAsset(AssetDefinition asset) => representedAsset = asset;

        /// <summary>Assigns the semantic support surface shared with dependent placements.</summary>
        public void SetSupportSurface(PlacementSurfaceDescriptor surface) => supportSurface = surface;

        /// <summary>Offsets semantic Front around local Y without rotating the represented scene object.</summary>
        public void SetForwardYawOffset(float degrees) =>
            forwardYawOffset = Mathf.Clamp(Mathf.DeltaAngle(0f, degrees), -180f, 180f);

        /// <summary>Replaces additional semantic tags with asset-compatible, duplicate-free values.</summary>
        public void SetAssetTags(IEnumerable<SemanticTag> tags)
        {
            assetTags = NormalizeTags(tags);
        }

        /// <summary>Configures optional local bounds instead of deriving them from child renderers and colliders.</summary>
        public void SetCustomBounds(bool enabled, Vector3 center, Vector3 size)
        {
            useCustomBounds = enabled;
            boundsCenter = center;
            boundsSize = ClampSize(size);
        }

        /// <summary>Returns the world-space anchor bounds used for distance and side evaluation.</summary>
        public bool TryGetBounds(out Bounds bounds)
        {
            if (useCustomBounds)
            {
                OrientedBounds oriented = new(
                    transform.TransformPoint(boundsCenter),
                    Vector3.Scale(boundsSize, Abs(transform.lossyScale)),
                    transform.rotation);
                bounds = oriented.ToAxisAlignedBounds();
                return true;
            }

            if (BoundsUtility.TryGetCombinedBounds(transform, out bounds))
                return true;

            bounds = new Bounds(transform.position, Vector3.zero);
            return true;
        }

        /// <summary>Determines whether all corners of an oriented candidate fit inside this anchor's bounds.</summary>
        public bool Contains(OrientedBounds candidateBounds)
        {
            if (!useCustomBounds)
            {
                if (!TryGetBounds(out Bounds bounds))
                    return false;

                return Contains(bounds, candidateBounds);
            }

            Vector3 halfSize = boundsSize * 0.5f;
            Vector3 candidateExtents = candidateBounds.Extents;
            Quaternion candidateRotation = candidateBounds.Rotation;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = candidateBounds.Center + candidateRotation * Vector3.Scale(
                    candidateExtents,
                    new Vector3(x, y, z));
                Vector3 local = transform.InverseTransformPoint(corner) - boundsCenter;
                if (Mathf.Abs(local.x) > halfSize.x + 0.0001f ||
                    Mathf.Abs(local.y) > halfSize.y + 0.0001f ||
                    Mathf.Abs(local.z) > halfSize.z + 0.0001f)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Determines whether this anchor carries the semantic target requested by a rule.</summary>
        public bool Matches(AssetRelativePlacementRule rule) =>
            rule != null && rule.Matches(representedAsset, assetTags);

        private void OnValidate()
        {
            assetTags = NormalizeTags(assetTags);
            boundsSize = ClampSize(boundsSize);
        }

        private void OnDrawGizmos()
        {
            if (alwaysShowAnchor)
                DrawAnchorGizmo(0.45f);
        }

        private void OnDrawGizmosSelected() => DrawAnchorGizmo(0.9f);

        private void DrawAnchorGizmo(float alpha)
        {
            if (!TryGetBounds(out Bounds bounds))
                return;

            Color previous = Gizmos.color;
            Gizmos.color = new Color(0.12f, 0.82f, 0.92f, alpha);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            float arrowLength = Mathf.Max(0.5f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            Vector3 start = bounds.center;
            Vector3 end = start + Forward.normalized * arrowLength;
            Gizmos.DrawLine(start, end);
            Vector3 right = Right.normalized * arrowLength * 0.2f;
            Vector3 back = Forward.normalized * arrowLength * 0.25f;
            Gizmos.DrawLine(end, end - back + right);
            Gizmos.DrawLine(end, end - back - right);
            Gizmos.color = previous;
        }

        private static List<SemanticTag> NormalizeTags(IEnumerable<SemanticTag> tags) =>
            tags?
                .Where(tag => tag && tag.SupportsAssets)
                .Distinct()
                .ToList() ?? new List<SemanticTag>();

        private static Vector3 ClampSize(Vector3 size) => new(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y),
            Mathf.Max(0.01f, size.z));

        private static Vector3 Abs(Vector3 value) => new(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));

        private static bool Contains(Bounds bounds, OrientedBounds candidateBounds)
        {
            Vector3 extents = candidateBounds.Extents;
            Quaternion rotation = candidateBounds.Rotation;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = candidateBounds.Center + rotation * Vector3.Scale(
                    extents,
                    new Vector3(x, y, z));
                if (!bounds.Contains(corner))
                    return false;
            }

            return true;
        }
    }
}
