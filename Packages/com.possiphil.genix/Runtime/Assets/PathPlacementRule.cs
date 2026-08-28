using System;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Selects which horizontal side of a semantic path may contain an asset.</summary>
    public enum PathPlacementSide
    {
        /// <summary>Accepts either side and positions on the path centerline.</summary>
        Any,
        /// <summary>Accepts only the path's local left side.</summary>
        Left,
        /// <summary>Accepts only the path's local right side.</summary>
        Right,
        /// <summary>Creates matching station anchors on both sides of the path.</summary>
        BothSides
    }

    /// <summary>Controls how an asset is oriented relative to its nearest semantic path segment.</summary>
    public enum PathPlacementFacing
    {
        /// <summary>Keeps the asset's normal orientation policy.</summary>
        Any,
        /// <summary>Faces along the authored path direction.</summary>
        AlongPath,
        /// <summary>Faces opposite the authored path direction.</summary>
        AgainstPath,
        /// <summary>Faces from its position toward the path centerline.</summary>
        TowardPath,
        /// <summary>Faces away from the path centerline.</summary>
        AwayFromPath
    }

    /// <summary>Constrains an asset by distance, side, and facing relative to a reusable scene path.</summary>
    [Serializable]
    public sealed class PathPlacementRule
    {
        [SerializeField] private bool enabled;
        [SerializeField] private SemanticTag pathTag;
        [SerializeField, Min(0f)] private float minimumDistance;
        [SerializeField, Min(0.01f)] private float maximumDistance = 3f;
        [SerializeField, Min(0f)] private float endpointMargin;
        [SerializeField] private PathPlacementSide side;
        [SerializeField] private PathPlacementFacing facing;
        [SerializeField, Range(0f, 180f)] private float facingVariationDegrees;

        /// <summary>Indicates whether this rule participates in placement.</summary>
        public bool Enabled => enabled;
        /// <summary>Gets the asset-compatible semantic tag identifying eligible paths.</summary>
        public SemanticTag PathTag => pathTag;
        /// <summary>Gets the minimum horizontal center distance from the path.</summary>
        public float MinimumDistance => Mathf.Max(0f, minimumDistance);
        /// <summary>Gets the maximum horizontal center distance from the path.</summary>
        public float MaximumDistance => Mathf.Max(MinimumDistance, maximumDistance);
        /// <summary>Gets the path length excluded at both endpoints.</summary>
        public float EndpointMargin => Mathf.Max(0f, endpointMargin);
        /// <summary>Gets the accepted side of the nearest path segment.</summary>
        public PathPlacementSide Side => side;
        /// <summary>Gets the path-relative facing policy.</summary>
        public PathPlacementFacing Facing => facing;
        /// <summary>Gets the maximum deterministic yaw variation around the path-relative facing.</summary>
        public float FacingVariationDegrees => Mathf.Clamp(facingVariationDegrees, 0f, 180f);
        /// <summary>Indicates whether this rule has a usable path target.</summary>
        public bool IsConfigured => enabled && IsAssetTag(pathTag);
        /// <summary>Indicates whether candidate rotation depends on the nearest path.</summary>
        public bool UsesFacing => IsConfigured && facing != PathPlacementFacing.Any;

        /// <summary>Configures a path-relative placement constraint.</summary>
        public void Configure(
            SemanticTag tag,
            float minDistance,
            float maxDistance,
            PathPlacementSide requiredSide,
            PathPlacementFacing facingMode,
            float facingVariation = 0f,
            float pathEndpointMargin = 0f)
        {
            enabled = true;
            pathTag = IsAssetTag(tag) ? tag : null;
            minimumDistance = Mathf.Max(0f, minDistance);
            maximumDistance = Mathf.Max(minimumDistance, maxDistance);
            endpointMargin = Mathf.Max(0f, pathEndpointMargin);
            side = requiredSide == PathPlacementSide.BothSides
                ? PathPlacementSide.Any
                : requiredSide;
            facing = facingMode;
            facingVariationDegrees = facingVariation;
            Normalize();
        }

        /// <summary>Disables path-relative placement while retaining authored values.</summary>
        public void Disable() => enabled = false;

        internal void Normalize()
        {
            if (!IsAssetTag(pathTag))
                pathTag = null;
            if (!Enum.IsDefined(typeof(PathPlacementSide), side) || side == PathPlacementSide.BothSides)
                side = PathPlacementSide.Any;
            if (!Enum.IsDefined(typeof(PathPlacementFacing), facing))
                facing = PathPlacementFacing.Any;

            minimumDistance = Mathf.Max(0f, minimumDistance);
            maximumDistance = Mathf.Max(minimumDistance, maximumDistance);
            endpointMargin = Mathf.Max(0f, endpointMargin);
            facingVariationDegrees = Mathf.Clamp(facingVariationDegrees, 0f, 180f);
        }

        private static bool IsAssetTag(SemanticTag tag) =>
            tag && tag.SupportsAssets;
    }
}
