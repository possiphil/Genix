using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Selects where an asset-specific relative-placement rule obtains anchors.</summary>
    public enum AssetRelativeAnchorSource
    {
        /// <summary>Uses generated objects and explicit scene relation anchors.</summary>
        Any,
        /// <summary>Uses objects generated earlier in this or a previous run.</summary>
        GeneratedObjects,
        /// <summary>Uses explicit <c>AssetRelationAnchor</c> scene components.</summary>
        SceneAnchors
    }

    /// <summary>Selects whether a relative-placement target is one asset or an asset-tag group.</summary>
    public enum AssetRelativeTargetScope
    {
        /// <summary>Matches one concrete asset definition.</summary>
        Asset,
        /// <summary>Matches every anchor carrying one asset-compatible tag.</summary>
        AssetTag
    }

    /// <summary>Constrains a candidate to one spatial sector around its relation anchor.</summary>
    public enum AssetRelativeSide
    {
        /// <summary>Accepts every direction around the anchor.</summary>
        Any,
        /// <summary>Requires the candidate in the anchor's local positive-Z sector.</summary>
        Front,
        /// <summary>Requires the candidate in the anchor's local negative-Z sector.</summary>
        Back,
        /// <summary>Requires the candidate in the anchor's local negative-X sector.</summary>
        Left,
        /// <summary>Requires the candidate in the anchor's local positive-X sector.</summary>
        Right,
        /// <summary>Requires the candidate in the world-positive-Y sector above the anchor.</summary>
        Above,
        /// <summary>Requires the candidate in the world-negative-Y sector below the anchor.</summary>
        Below
    }

    /// <summary>Controls the preferred position along the selected side of a relation anchor.</summary>
    public enum AssetRelativeAlignment
    {
        /// <summary>Uses deterministic seeded variation among relation-compatible positions.</summary>
        Random,
        /// <summary>Prefers the local center of the selected anchor side.</summary>
        Center,
        /// <summary>Prefers the negative end of the selected side's local tangent axis.</summary>
        Start,
        /// <summary>Prefers the positive end of the selected side's local tangent axis.</summary>
        End
    }

    /// <summary>Controls how an asset faces its matched relative-placement anchor.</summary>
    public enum AssetRelativeFacing
    {
        /// <summary>Keeps the asset's normal orientation policy.</summary>
        Any,
        /// <summary>Faces the anchor center.</summary>
        Toward,
        /// <summary>Faces away from the anchor center.</summary>
        Away,
        /// <summary>Matches the anchor's local forward direction.</summary>
        MatchForward
    }

    /// <summary>Controls how many dependent assets may or must be assigned to each matching anchor.</summary>
    public enum AssetRelativeCardinalityMode
    {
        /// <summary>Does not constrain the number of dependents per anchor.</summary>
        Unlimited,
        /// <summary>Allows up to the configured number of dependents per anchor.</summary>
        AtMost,
        /// <summary>Requires at least the configured number of dependents per anchor.</summary>
        AtLeast,
        /// <summary>Requires exactly the configured number of dependents per anchor.</summary>
        Exactly,
        /// <summary>Requires at least the lower and allows at most the upper dependent count per anchor.</summary>
        Between
    }

    /// <summary>Defines an optional semantic position and facing relationship to another asset.</summary>
    [Serializable]
    public sealed class AssetRelativePlacementRule
    {
        [SerializeField] private bool enabled;
        [SerializeField] private AssetRelativeAnchorSource source = AssetRelativeAnchorSource.Any;
        [SerializeField] private AssetRelativeTargetScope targetScope = AssetRelativeTargetScope.AssetTag;
        [SerializeField] private AssetDefinition targetAsset;
        [SerializeField] private SemanticTag targetTag;
        [SerializeField] private AssetRelativeSide side = AssetRelativeSide.Any;
        [SerializeField] private List<AssetRelativeSide> additionalSides = new();
        [SerializeField] private AssetRelativeAlignment alignment = AssetRelativeAlignment.Random;
        [SerializeField] private bool requireSameSupportSurface;
        [SerializeField] private bool requireInsideAnchorBounds;
        [SerializeField, Min(0f)] private float minimumDistance;
        [SerializeField, Min(0.01f)] private float maximumDistance = 2f;
        [SerializeField] private AssetRelativeFacing facing = AssetRelativeFacing.Any;
        [SerializeField, Range(0f, 180f)] private float facingVariationDegrees;
        [SerializeField] private AssetRelativeCardinalityMode cardinalityMode;
        [SerializeField, Min(1)] private int cardinalityCount = 1;
        [SerializeField, Min(1)] private int cardinalityMaximumCount = 1;
        [SerializeField, HideInInspector] private int cardinalityVersion;
        [SerializeField, HideInInspector] private bool limitPerAnchor;
        [SerializeField, HideInInspector, Min(1)] private int maxPerAnchor = 1;
        [SerializeField] private bool usePathStations;
        [SerializeField] private PathPlacementSide pathStationSides = PathPlacementSide.BothSides;
        [SerializeField, Min(0.1f)] private float pathStationSpacing = 5f;
        [SerializeField, Min(0f)] private float pathStationLateralOffset = 2f;
        [SerializeField, Min(0f)] private float pathStationEndpointMargin = 2.5f;
        [SerializeField, Min(1)] private int pathStationMaximumCount = 6;

        /// <summary>Indicates whether this relationship should constrain placement.</summary>
        public bool Enabled => enabled;
        /// <summary>Gets the anchor source.</summary>
        public AssetRelativeAnchorSource Source => source;
        /// <summary>Gets whether the rule matches a concrete asset or an asset tag.</summary>
        public AssetRelativeTargetScope TargetScope => targetScope;
        /// <summary>Gets the concrete target asset.</summary>
        public AssetDefinition TargetAsset => targetAsset;
        /// <summary>Gets the asset-compatible target tag.</summary>
        public SemanticTag TargetTag => targetTag;
        /// <summary>Gets the required local side of the anchor.</summary>
        public AssetRelativeSide Side => side;
        /// <summary>Gets additional accepted local sides beyond <see cref="Side"/>.</summary>
        public IReadOnlyList<AssetRelativeSide> AdditionalSides =>
            additionalSides is { } ? additionalSides : Array.Empty<AssetRelativeSide>();
        /// <summary>Gets the preferred position along the selected anchor side.</summary>
        public AssetRelativeAlignment Alignment => alignment;
        /// <summary>Indicates whether candidate and anchor must reference the same semantic support surface.</summary>
        public bool RequireSameSupportSurface => requireSameSupportSurface;
        /// <summary>Indicates whether the complete candidate bounds must remain inside the matched scene anchor.</summary>
        public bool RequireInsideAnchorBounds => requireInsideAnchorBounds;
        /// <summary>Gets the minimum 3D distance from the anchor bounds.</summary>
        public float MinimumDistance => Mathf.Max(0f, minimumDistance);
        /// <summary>Gets the maximum 3D distance from the anchor bounds.</summary>
        public float MaximumDistance => Mathf.Max(MinimumDistance, maximumDistance);
        /// <summary>Gets the facing relationship applied after an anchor has matched.</summary>
        public AssetRelativeFacing Facing => facing;
        /// <summary>Gets the maximum deterministic yaw deviation from the resolved facing direction.</summary>
        public float FacingVariationDegrees => Mathf.Clamp(facingVariationDegrees, 0f, 180f);
        /// <summary>Gets the authored per-anchor cardinality policy.</summary>
        public AssetRelativeCardinalityMode CardinalityMode => cardinalityMode;
        /// <summary>Gets the normalized cardinality value.</summary>
        public int CardinalityCount => Mathf.Max(1, cardinalityCount);
        /// <summary>Gets the normalized upper cardinality value.</summary>
        public int CardinalityMaximumCount => cardinalityMode == AssetRelativeCardinalityMode.Between
            ? Mathf.Max(CardinalityCount, cardinalityMaximumCount)
            : CardinalityCount;
        /// <summary>Indicates whether each matched anchor has an upper dependent limit.</summary>
        public bool HasMaximumPerAnchor =>
            cardinalityMode is AssetRelativeCardinalityMode.AtMost or
                AssetRelativeCardinalityMode.Exactly or
                AssetRelativeCardinalityMode.Between;
        /// <summary>Indicates whether generation must actively satisfy a lower dependent count.</summary>
        public bool HasMinimumPerAnchor =>
            cardinalityMode is AssetRelativeCardinalityMode.AtLeast or
                AssetRelativeCardinalityMode.Exactly or
                AssetRelativeCardinalityMode.Between;
        /// <summary>Gets the required minimum dependent count, or zero when no minimum is configured.</summary>
        public int MinimumPerAnchor => HasMinimumPerAnchor ? CardinalityCount : 0;
        /// <summary>Gets the allowed maximum dependent count, or <see cref="int.MaxValue"/> when unlimited.</summary>
        public int MaximumPerAnchor => HasMaximumPerAnchor ? CardinalityMaximumCount : int.MaxValue;
        /// <summary>Compatibility alias for the former optional per-anchor maximum.</summary>
        public bool LimitPerAnchor => HasMaximumPerAnchor;
        /// <summary>Compatibility alias for the former maximum value.</summary>
        public int MaxPerAnchor => CardinalityMaximumCount;
        /// <summary>Indicates whether the enabled rule has a usable semantic target.</summary>
        public bool IsConfigured => enabled && (targetScope switch
        {
            AssetRelativeTargetScope.Asset => targetAsset,
            AssetRelativeTargetScope.AssetTag => IsAssetTag(targetTag),
            _ => false
        });
        /// <summary>Indicates whether candidate rotation depends on the matched anchor.</summary>
        public bool UsesFacing => IsConfigured && facing != AssetRelativeFacing.Any;
        /// <summary>Indicates whether one matching path source should expose regularly spaced virtual anchors.</summary>
        public bool UsesPathStations => IsConfigured && usePathStations &&
                                        targetScope == AssetRelativeTargetScope.AssetTag &&
                                        source is AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.SceneAnchors;
        /// <summary>Gets the sides on which virtual station anchors are created.</summary>
        public PathPlacementSide PathStationSides => pathStationSides;
        /// <summary>Gets the along-path spacing between virtual station groups.</summary>
        public float PathStationSpacing => Mathf.Max(0.1f, pathStationSpacing);
        /// <summary>Gets the lateral centerline offset of virtual station anchors.</summary>
        public float PathStationLateralOffset => Mathf.Max(0f, pathStationLateralOffset);
        /// <summary>Gets the path distance omitted at each endpoint.</summary>
        public float PathStationEndpointMargin => Mathf.Max(0f, pathStationEndpointMargin);
        /// <summary>Gets the maximum number of station groups exposed across matching paths.</summary>
        public int PathStationMaximumCount => Mathf.Max(1, pathStationMaximumCount);

        /// <summary>Configures a relationship to one concrete asset definition.</summary>
        public void ConfigureAsset(
            AssetDefinition asset,
            AssetRelativeAnchorSource anchorSource,
            AssetRelativeSide requiredSide,
            float minDistance,
            float maxDistance,
            AssetRelativeFacing facingMode,
            bool sameSupportSurface = false)
        {
            enabled = true;
            source = anchorSource;
            targetScope = AssetRelativeTargetScope.Asset;
            targetAsset = asset;
            targetTag = null;
            side = requiredSide;
            additionalSides ??= new List<AssetRelativeSide>();
            additionalSides.Clear();
            requireSameSupportSurface = sameSupportSurface;
            minimumDistance = minDistance;
            maximumDistance = maxDistance;
            facing = facingMode;
            Normalize();
        }

        /// <summary>Configures a relationship to every anchor carrying an asset-compatible tag.</summary>
        public void ConfigureTag(
            SemanticTag tag,
            AssetRelativeAnchorSource anchorSource,
            AssetRelativeSide requiredSide,
            float minDistance,
            float maxDistance,
            AssetRelativeFacing facingMode,
            bool sameSupportSurface = false)
        {
            enabled = true;
            source = anchorSource;
            targetScope = AssetRelativeTargetScope.AssetTag;
            targetAsset = null;
            targetTag = tag;
            side = requiredSide;
            additionalSides ??= new List<AssetRelativeSide>();
            additionalSides.Clear();
            requireSameSupportSurface = sameSupportSurface;
            minimumDistance = minDistance;
            maximumDistance = maxDistance;
            facing = facingMode;
            Normalize();
        }

        /// <summary>Disables the relationship while retaining its authored settings.</summary>
        public void Disable() => enabled = false;

        /// <summary>Configures the per-anchor cardinality for this dependent asset.</summary>
        public void SetCardinality(AssetRelativeCardinalityMode mode, int count)
        {
            cardinalityMode = Enum.IsDefined(typeof(AssetRelativeCardinalityMode), mode)
                ? mode
                : AssetRelativeCardinalityMode.Unlimited;
            cardinalityCount = Mathf.Max(1, count);
            cardinalityMaximumCount = cardinalityCount;
            cardinalityVersion = 2;
            SyncLegacyCardinality();
        }

        /// <summary>Requires at least <paramref name="minimum"/> and allows at most <paramref name="maximum"/> dependents per anchor.</summary>
        public void SetCardinalityRange(int minimum, int maximum)
        {
            cardinalityMode = AssetRelativeCardinalityMode.Between;
            cardinalityCount = Mathf.Max(1, minimum);
            cardinalityMaximumCount = Mathf.Max(cardinalityCount, maximum);
            cardinalityVersion = 2;
            SyncLegacyCardinality();
        }

        /// <summary>Configures an optional per-anchor maximum for compatibility with existing callers.</summary>
        public void SetPerAnchorLimit(bool enabled, int maximum)
        {
            SetCardinality(
                enabled ? AssetRelativeCardinalityMode.AtMost : AssetRelativeCardinalityMode.Unlimited,
                maximum);
        }

        /// <summary>Sets the maximum deterministic yaw deviation from the resolved facing direction.</summary>
        public void SetFacingVariation(float degrees) =>
            facingVariationDegrees = Mathf.Clamp(degrees, 0f, 180f);

        /// <summary>Generates reusable virtual relation anchors at regular intervals along matching path sources.</summary>
        public void ConfigurePathStations(
            PathPlacementSide sides,
            float spacing,
            float lateralOffset,
            float endpointMargin,
            int maximumStationCount)
        {
            usePathStations = true;
            pathStationSides = sides == PathPlacementSide.Any ? PathPlacementSide.BothSides : sides;
            pathStationSpacing = Mathf.Max(0.1f, spacing);
            pathStationLateralOffset = Mathf.Max(0f, lateralOffset);
            pathStationEndpointMargin = Mathf.Max(0f, endpointMargin);
            pathStationMaximumCount = Mathf.Max(1, maximumStationCount);
            Normalize();
        }

        /// <summary>Stops deriving virtual anchors from path sources.</summary>
        public void DisablePathStations() => usePathStations = false;

        /// <summary>Sets the preferred position along the selected anchor side.</summary>
        public void SetAlignment(AssetRelativeAlignment value) =>
            alignment = Enum.IsDefined(typeof(AssetRelativeAlignment), value)
                ? value
                : AssetRelativeAlignment.Random;

        /// <summary>Requires the complete candidate bounds to remain inside the matched anchor bounds.</summary>
        public void SetRequireInsideAnchorBounds(bool required) =>
            requireInsideAnchorBounds = required;

        /// <summary>Replaces the accepted local sides. An empty set selects Any.</summary>
        public void SetSides(IEnumerable<AssetRelativeSide> requiredSides)
        {
            List<AssetRelativeSide> normalized = requiredSides?
                .Where(value => value != AssetRelativeSide.Any &&
                                Enum.IsDefined(typeof(AssetRelativeSide), value))
                .Distinct()
                .ToList() ?? new List<AssetRelativeSide>();
            side = normalized.Count > 0 ? normalized[0] : AssetRelativeSide.Any;
            additionalSides = normalized.Skip(1).ToList();
        }

        /// <summary>Determines whether one resolved local side satisfies this rule.</summary>
        public bool AllowsSide(AssetRelativeSide candidateSide) =>
            side == AssetRelativeSide.Any ||
            side == candidateSide ||
            additionalSides?.Contains(candidateSide) == true;

        /// <summary>Indicates whether vertical sectors participate in dominant-axis side classification.</summary>
        public bool UsesVerticalSides
        {
            get
            {
                if (side is AssetRelativeSide.Above or AssetRelativeSide.Below)
                    return true;

                if (additionalSides == null)
                    return false;

                foreach (AssetRelativeSide additionalSide in additionalSides)
                {
                    if (additionalSide is AssetRelativeSide.Above or AssetRelativeSide.Below)
                        return true;
                }

                return false;
            }
        }

        /// <summary>Determines whether supplied semantic anchor data matches this rule.</summary>
        public bool Matches(AssetDefinition asset, IReadOnlyList<SemanticTag> tags)
        {
            if (!IsConfigured)
                return false;

            if (targetScope == AssetRelativeTargetScope.Asset)
                return asset == targetAsset;

            if (asset && asset.HasTag(targetTag))
                return true;

            if (tags == null)
                return false;

            foreach (SemanticTag tag in tags)
            {
                if (tag == targetTag)
                    return true;
            }

            return false;
        }

        internal void Normalize()
        {
            if (cardinalityVersion < 1)
            {
                cardinalityMode = limitPerAnchor
                    ? AssetRelativeCardinalityMode.AtMost
                    : AssetRelativeCardinalityMode.Unlimited;
                cardinalityCount = Mathf.Max(1, maxPerAnchor);
                cardinalityVersion = 1;
            }

            if (cardinalityVersion < 2)
            {
                cardinalityMaximumCount = Mathf.Max(1, cardinalityCount);
                cardinalityVersion = 2;
            }

            if (!Enum.IsDefined(typeof(AssetRelativeAnchorSource), source))
                source = AssetRelativeAnchorSource.Any;
            if (!Enum.IsDefined(typeof(AssetRelativeTargetScope), targetScope))
                targetScope = AssetRelativeTargetScope.AssetTag;
            if (!Enum.IsDefined(typeof(AssetRelativeSide), side))
                side = AssetRelativeSide.Any;
            if (!Enum.IsDefined(typeof(AssetRelativeAlignment), alignment))
                alignment = AssetRelativeAlignment.Random;
            if (!Enum.IsDefined(typeof(AssetRelativeFacing), facing))
                facing = AssetRelativeFacing.Any;
            if (!Enum.IsDefined(typeof(AssetRelativeCardinalityMode), cardinalityMode))
                cardinalityMode = AssetRelativeCardinalityMode.Unlimited;
            if (!Enum.IsDefined(typeof(PathPlacementSide), pathStationSides) ||
                pathStationSides == PathPlacementSide.Any)
                pathStationSides = PathPlacementSide.BothSides;

            minimumDistance = Mathf.Max(0f, minimumDistance);
            maximumDistance = Mathf.Max(minimumDistance, maximumDistance);
            facingVariationDegrees = Mathf.Clamp(facingVariationDegrees, 0f, 180f);
            cardinalityCount = Mathf.Max(1, cardinalityCount);
            cardinalityMaximumCount = Mathf.Max(cardinalityCount, cardinalityMaximumCount);
            pathStationSpacing = Mathf.Max(0.1f, pathStationSpacing);
            pathStationLateralOffset = Mathf.Max(0f, pathStationLateralOffset);
            pathStationEndpointMargin = Mathf.Max(0f, pathStationEndpointMargin);
            pathStationMaximumCount = Mathf.Max(1, pathStationMaximumCount);
            SyncLegacyCardinality();
            additionalSides ??= new List<AssetRelativeSide>();
            additionalSides = additionalSides
                .Where(value => value != AssetRelativeSide.Any &&
                                Enum.IsDefined(typeof(AssetRelativeSide), value))
                .Distinct()
                .ToList();
            if (side == AssetRelativeSide.Any && additionalSides.Count > 0)
            {
                side = additionalSides[0];
                additionalSides.RemoveAt(0);
            }
            additionalSides.RemoveAll(value => value == side);
            if (side == AssetRelativeSide.Any)
                alignment = AssetRelativeAlignment.Random;
            if (alignment is AssetRelativeAlignment.Start or AssetRelativeAlignment.End &&
                (additionalSides.Count > 0 ||
                 side is not (AssetRelativeSide.Front or AssetRelativeSide.Back or
                     AssetRelativeSide.Left or AssetRelativeSide.Right)))
            {
                alignment = AssetRelativeAlignment.Random;
            }

            if (targetScope == AssetRelativeTargetScope.Asset)
                targetTag = null;
            else
            {
                targetAsset = null;
                if (!IsAssetTag(targetTag))
                    targetTag = null;
            }
        }

        private static bool IsAssetTag(SemanticTag tag) =>
            tag && tag.SupportsAssets;

        private void SyncLegacyCardinality()
        {
            limitPerAnchor = HasMaximumPerAnchor;
            maxPerAnchor = CardinalityMaximumCount;
        }
    }
}
