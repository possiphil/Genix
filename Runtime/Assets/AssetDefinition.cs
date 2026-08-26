using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Orientation;
using Genix.Placement;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Identifies whether an asset spacing rule targets one definition or an asset-tag group.</summary>
    public enum AssetSpacingRuleScope
    {
        /// <summary>The rule applies to one concrete neighboring asset definition.</summary>
        Asset,
        /// <summary>The rule applies to neighboring assets carrying the selected semantic tag.</summary>
        AssetTag
    }

    /// <summary>Defines a minimum center distance from matching generated assets.</summary>
    [Serializable]
    public sealed class AssetSpacingRule
    {
        [SerializeField] private AssetSpacingRuleScope scope = AssetSpacingRuleScope.AssetTag;
        [SerializeField] private AssetDefinition asset;
        [SerializeField] private SemanticTag assetTag;
        [SerializeField, Min(0f)] private float minimumDistance = 1f;

        /// <summary>Gets whether this rule matches one asset or an asset tag.</summary>
        public AssetSpacingRuleScope Scope => scope;
        /// <summary>Gets the concrete neighboring asset selected by this rule.</summary>
        public AssetDefinition Asset => asset;
        /// <summary>Gets the neighboring asset tag selected by this rule.</summary>
        public SemanticTag AssetTag => assetTag;
        /// <summary>Gets the required center-to-center distance in world units.</summary>
        public float MinimumDistance => Mathf.Max(0f, minimumDistance);
        /// <summary>Indicates whether the rule has a valid target and positive distance.</summary>
        public bool IsConfigured => MinimumDistance > 0f && (scope switch
        {
            AssetSpacingRuleScope.Asset => asset,
            AssetSpacingRuleScope.AssetTag => IsAssetTag(assetTag),
            _ => false
        });
        /// <summary>Gets a concise target name suitable for diagnostics.</summary>
        public string DisplayName => scope switch
        {
            AssetSpacingRuleScope.Asset when asset => asset.AssetName,
            AssetSpacingRuleScope.AssetTag when assetTag => assetTag.DisplayName,
            _ => "Unconfigured Rule"
        };

        /// <summary>Configures spacing from one concrete asset definition.</summary>
        public void ConfigureAsset(AssetDefinition targetAsset, float distance)
        {
            scope = AssetSpacingRuleScope.Asset;
            asset = targetAsset;
            assetTag = null;
            minimumDistance = Mathf.Max(0f, distance);
        }

        /// <summary>Configures spacing from every asset carrying an asset-compatible tag.</summary>
        public void ConfigureTag(SemanticTag targetTag, float distance)
        {
            scope = AssetSpacingRuleScope.AssetTag;
            asset = null;
            assetTag = IsAssetTag(targetTag) ? targetTag : null;
            minimumDistance = Mathf.Max(0f, distance);
        }

        /// <summary>Determines whether the supplied neighboring asset is constrained by this rule.</summary>
        public bool Matches(AssetDefinition other) => other && IsConfigured && (scope switch
        {
            AssetSpacingRuleScope.Asset => other == asset,
            AssetSpacingRuleScope.AssetTag => other.HasTag(assetTag),
            _ => false
        });

        internal void Normalize()
        {
            minimumDistance = Mathf.Max(0f, minimumDistance);

            if (scope == AssetSpacingRuleScope.Asset)
                assetTag = null;
            else
            {
                asset = null;

                if (!IsAssetTag(assetTag))
                    assetTag = null;
            }
        }

        private static bool IsAssetTag(SemanticTag tag) =>
            tag && tag.Category && tag.Category.SupportsAssets;
    }

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
            tag && tag.Category && tag.Category.SupportsAssets;
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
            tag && tag.Category && tag.Category.SupportsAssets;

        private void SyncLegacyCardinality()
        {
            limitPerAnchor = HasMaximumPerAnchor;
            maxPerAnchor = CardinalityMaximumCount;
        }
    }

    /// <summary>Controls how a wall asset chooses its vertical position within the target area.</summary>
    public enum WallVerticalPlacementMode
    {
        /// <summary>Uses wall samples across the complete target height.</summary>
        [InspectorName("Full Wall")] FullWall,
        /// <summary>Places the asset's lower bound at one height above the target area's lower bound.</summary>
        [InspectorName("Fixed Height")] FixedHeight,
        /// <summary>Distributes the asset's lower bound between two heights above the target area's lower bound.</summary>
        [InspectorName("Height Range")] HeightRange
    }

    /// <summary>Optional horizontal relationship between a floor or ceiling asset and detected walls.</summary>
    public enum WallProximityMode
    {
        /// <summary>Does not constrain wall distance.</summary>
        [InspectorName("Any Distance")] AnyDistance,
        /// <summary>Requires the asset bounds to lie within a maximum wall distance.</summary>
        [InspectorName("Near Wall")] NearWall,
        /// <summary>Requires at least a minimum clearance from every detected wall.</summary>
        [InspectorName("Away From Wall")] AwayFromWall
    }

    /// <summary>
    /// Defines the prefab, semantic identity, placement target, bounds, rotation, and surface-fit policy of one placeable asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Genix/Assets/Asset Definition")]
    public sealed class AssetDefinition : ScriptableObject
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private List<SemanticTag> semanticTags = new();
        [SerializeField] private List<TagCategory> anyTagCategories = new();
        [SerializeField] private List<SemanticTag> requiredSupportTags = new();
        [SerializeField] private List<SemanticTag> forbiddenSupportTags = new();
        [SerializeField] private List<TagCategory> requiredSupportNoneCategories = new();
        [SerializeField] private List<TagCategory> forbiddenSupportAnyCategories = new();
        [SerializeField] private bool limitPlacements;
        [SerializeField, Min(1)] private int maxPlacements = 1;
        [SerializeField] private List<AssetSpacingRule> spacingRules = new();
        [SerializeField] private AssetRelativePlacementRule assetRelativePlacement = new();
        [SerializeField] private PathPlacementRule pathPlacement = new();
        [SerializeField] private PlacementType placementType = PlacementType.Floor;
        [SerializeField] private WallVerticalPlacementMode wallVerticalPlacementMode = WallVerticalPlacementMode.FullWall;
        [SerializeField] private float placementHeight;
        [SerializeField, Min(0f)] private float wallMinHeight;
        [SerializeField, Min(0f)] private float wallMaxHeight = 2f;
        [SerializeField] private Vector3 prefabRotationOffset;
        [SerializeField] private Vector3 boundsSize = new(1f, 1f, 1f);
        [SerializeField] private Vector3 boundsCenterOffset;
        [SerializeField] private bool reserveClearance;
        [SerializeField] private Vector3 clearanceSize = Vector3.one;
        [SerializeField] private Vector3 clearanceCenterOffset;
        [SerializeField] private OrientationMode orientationMode = OrientationMode.None;
        [SerializeField] private SurfaceFitMode surfaceFitMode = SurfaceFitMode.Strict;
        [SerializeField] private SurfaceAlignmentMode surfaceAlignmentMode = SurfaceAlignmentMode.AlignToSurface;
        [SerializeField] private SurfaceHeightMode surfaceHeightMode = SurfaceHeightMode.Average;
        [SerializeField, Min(0f)] private float maxSurfaceHeightDifference = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float minSurfaceSupport = 0.75f;
        [SerializeField, Min(0f)] private float surfaceSinkOffset;
        [SerializeField] private bool randomYawRotation = true;
        [SerializeField] private bool randomPitchRotation;
        [SerializeField] private bool randomRollRotation;
        [SerializeField] private WallProximityMode wallProximityMode = WallProximityMode.AnyDistance;
        [SerializeField, Min(0f)] private float wallDistance = 1f;

        [NonSerialized] private bool placementGeometryCacheValid;
        [NonSerialized] private Quaternion cachedPrefabRotationOffset = Quaternion.identity;
        [NonSerialized] private Vector3 cachedBoundsSize = Vector3.one;
        [NonSerialized] private Vector3 cachedBoundsCenterOffset;
        [NonSerialized] private Vector3 cachedClearanceSize = Vector3.one;
        [NonSerialized] private Vector3 cachedClearanceCenterOffset;

        /// <summary>Gets asset name.</summary>
        public string AssetName => name;
        /// <summary>Gets prefab.</summary>
        public GameObject Prefab => prefab;
        /// <summary>Gets semantic tags.</summary>
        public IReadOnlyList<SemanticTag> SemanticTags => semanticTags;
        /// <summary>Gets any tag categories.</summary>
        public IReadOnlyList<TagCategory> AnyTagCategories => anyTagCategories;
        /// <summary>Gets support-tag alternatives; one tag from every represented category must match.</summary>
        public IReadOnlyList<SemanticTag> RequiredSupportTags => requiredSupportTags;
        /// <summary>Gets support tags that always reject the asset; forbidden tags take precedence over required tags.</summary>
        public IReadOnlyList<SemanticTag> ForbiddenSupportTags => forbiddenSupportTags;
        /// <summary>Gets support categories explicitly configured to accept no surface in Required Tags.</summary>
        public IReadOnlyList<TagCategory> RequiredSupportNoneCategories => requiredSupportNoneCategories;
        /// <summary>Gets support categories for which every surface is forbidden.</summary>
        public IReadOnlyList<TagCategory> ForbiddenSupportAnyCategories => forbiddenSupportAnyCategories;
        /// <summary>Indicates whether this asset has a per-generation-run placement limit.</summary>
        public bool LimitPlacements => limitPlacements;
        /// <summary>Gets the maximum accepted instances across existing generated output and the current plan.</summary>
        public int MaxPlacements => Mathf.Max(1, maxPlacements);
        /// <summary>Gets optional minimum-distance rules for neighboring generated assets.</summary>
        public IReadOnlyList<AssetSpacingRule> SpacingRules => spacingRules;
        /// <summary>Gets the largest active asset-specific spacing distance.</summary>
        public float MaxSpacingDistance
        {
            get
            {
                float maximum = 0f;

                foreach (AssetSpacingRule rule in spacingRules)
                {
                    if (rule?.IsConfigured == true)
                        maximum = Mathf.Max(maximum, rule.MinimumDistance);
                }

                return maximum;
            }
        }
        /// <summary>Gets the optional semantic relationship this asset requires from another asset or scene anchor.</summary>
        public AssetRelativePlacementRule AssetRelativePlacement =>
            assetRelativePlacement ??= new AssetRelativePlacementRule();
        /// <summary>Gets the optional distance, side, and facing constraint relative to a semantic scene path.</summary>
        public PathPlacementRule PathPlacement => pathPlacement ??= new PathPlacementRule();

        /// <summary>Gets placement type.</summary>
        public PlacementType PlacementType => placementType;
        /// <summary>Gets the policy used to choose a wall asset's vertical position.</summary>
        public WallVerticalPlacementMode WallVerticalPlacementMode => wallVerticalPlacementMode;
        /// <summary>Gets the sampled-baseline offset or fixed asset-bottom height, depending on the wall placement mode.</summary>
        public float PlacementHeight => placementHeight;
        /// <summary>Gets the lower wall-height limit measured from the target area's lower bound.</summary>
        public float WallMinHeight => Mathf.Min(Mathf.Max(0f, wallMinHeight), Mathf.Max(0f, wallMaxHeight));
        /// <summary>Gets the upper wall-height limit measured from the target area's lower bound.</summary>
        public float WallMaxHeight => Mathf.Max(Mathf.Max(0f, wallMinHeight), Mathf.Max(0f, wallMaxHeight));

        /// <summary>Gets the Euler correction applied to the prefab after Genix determines its placement orientation.</summary>
        public Vector3 PrefabRotationOffset => prefabRotationOffset;
        /// <summary>Gets placement-bound dimensions after applying the prefab rotation correction.</summary>
        public Vector3 BoundsSize
        {
            get
            {
                EnsurePlacementGeometryCache();
                return cachedBoundsSize;
            }
        }
        /// <summary>Gets the corrected offset from the prefab origin to the placement-bound center.</summary>
        public Vector3 BoundsCenterOffset
        {
            get
            {
                EnsurePlacementGeometryCache();
                return cachedBoundsCenterOffset;
            }
        }
        /// <summary>Gets footprint.</summary>
        public Vector2 Footprint => new(BoundsSize.x, BoundsSize.z);
        /// <summary>Gets width.</summary>
        public float Width => BoundsSize.x;
        /// <summary>Gets height.</summary>
        public float Height => BoundsSize.y;
        /// <summary>Gets depth.</summary>
        public float Depth => BoundsSize.z;
        /// <summary>Indicates whether this asset reserves an additional collider-free volume.</summary>
        public bool ReserveClearance => reserveClearance;
        /// <summary>Gets the full local-axis dimensions of the reserved clearance volume.</summary>
        public Vector3 ClearanceSize
        {
            get
            {
                EnsurePlacementGeometryCache();
                return cachedClearanceSize;
            }
        }
        /// <summary>Gets the clearance center relative to the prefab origin.</summary>
        public Vector3 ClearanceCenterOffset
        {
            get
            {
                EnsurePlacementGeometryCache();
                return cachedClearanceCenterOffset;
            }
        }

        /// <summary>Gets orientation mode.</summary>
        public OrientationMode OrientationMode => orientationMode;
        /// <summary>Gets surface fit mode.</summary>
        public SurfaceFitMode SurfaceFitMode => surfaceFitMode;
        /// <summary>Gets surface alignment mode.</summary>
        public SurfaceAlignmentMode SurfaceAlignmentMode => surfaceAlignmentMode;
        /// <summary>Gets surface height mode.</summary>
        public SurfaceHeightMode SurfaceHeightMode => surfaceHeightMode;
        /// <summary>Gets the maximum supported height or wall-depth variation.</summary>
        public float MaxSurfaceHeightDifference => Mathf.Max(0f, maxSurfaceHeightDifference);
        /// <summary>Gets min surface support.</summary>
        public float MinSurfaceSupport => Mathf.Clamp01(minSurfaceSupport);
        /// <summary>Gets surface sink offset.</summary>
        public float SurfaceSinkOffset => Mathf.Max(0f, surfaceSinkOffset);
        /// <summary>Indicates whether random yaw rotation.</summary>
        public bool RandomYawRotation => randomYawRotation;
        /// <summary>Indicates whether random pitch rotation.</summary>
        public bool RandomPitchRotation => randomPitchRotation;
        /// <summary>Indicates whether random roll is enabled around the forward axis or wall normal.</summary>
        public bool RandomRollRotation => randomRollRotation;
        /// <summary>Gets the optional relationship to detected walls.</summary>
        public WallProximityMode WallProximityMode => wallProximityMode;
        /// <summary>Gets the maximum near-wall distance or minimum away-from-wall clearance.</summary>
        public float WallDistance => Mathf.Max(0f, wallDistance);

        /// <summary>Initializes the prefab and placement bounds of a newly created definition.</summary>
        public void Initialize(GameObject sourcePrefab, Vector3 generatedBoundsSize, Vector3 generatedBoundsCenterOffset = default)
        {
            prefab = sourcePrefab;
            boundsSize = generatedBoundsSize;
            boundsCenterOffset = generatedBoundsCenterOffset;
            InvalidatePlacementGeometryCache();
        }

        /// <summary>Sets placement-bound dimensions with a positive minimum on every axis.</summary>
        public void SetBoundsSize(Vector3 value)
        {
            boundsSize = new Vector3(
                Mathf.Max(0.01f, value.x),
                Mathf.Max(0.01f, value.y),
                Mathf.Max(0.01f, value.z));
            InvalidatePlacementGeometryCache();
        }

        /// <summary>Sets the offset from the prefab origin to the placement-bound center.</summary>
        public void SetBoundsCenterOffset(Vector3 value)
        {
            boundsCenterOffset = value;
            InvalidatePlacementGeometryCache();
        }

        /// <summary>Sets the prefab-local Euler correction applied after Genix computes placement orientation.</summary>
        public void SetPrefabRotationOffset(Vector3 value)
        {
            prefabRotationOffset = value;
            InvalidatePlacementGeometryCache();
        }

        /// <summary>Combines a logical Genix placement orientation with this prefab's import-axis correction.</summary>
        public Quaternion ApplyPrefabRotationOffset(Quaternion placementRotation)
        {
            EnsurePlacementGeometryCache();
            return placementRotation * cachedPrefabRotationOffset;
        }

        /// <summary>Recovers the logical Genix placement orientation from an instantiated prefab root.</summary>
        public Quaternion RemovePrefabRotationOffset(Quaternion prefabRotation)
        {
            EnsurePlacementGeometryCache();
            return prefabRotation * Quaternion.Inverse(cachedPrefabRotationOffset);
        }

        /// <summary>Configures an optional local-space volume that other generated and fixed geometry must leave empty.</summary>
        public void SetClearance(bool enabled, Vector3 size, Vector3 centerOffset)
        {
            reserveClearance = enabled;
            clearanceSize = ClampSize(size);
            clearanceCenterOffset = centerOffset;
            InvalidatePlacementGeometryCache();
        }

        /// <summary>Creates this asset's world-space clearance volume for a planned placement.</summary>
        public OrientedBounds CreateClearanceBounds(PlacementCandidate candidate)
        {
            Vector3 objectOrigin = candidate.Position - candidate.Rotation * BoundsCenterOffset;
            return CreateCorrectedClearanceBounds(objectOrigin, candidate.Rotation);
        }

        /// <summary>Creates this asset's world-space clearance volume for an instantiated prefab root.</summary>
        public OrientedBounds CreateClearanceBounds(Vector3 objectOrigin, Quaternion prefabRotation) =>
            CreateCorrectedClearanceBounds(objectOrigin, RemovePrefabRotationOffset(prefabRotation));

        /// <summary>Determines whether tag.</summary>
        public bool HasTag(SemanticTag tag)
        {
            return tag && tag.Category && tag.Category.SupportsAssets && semanticTags.Contains(tag);
        }

        /// <summary>Determines whether any tag.</summary>
        public bool HasAnyTag(IReadOnlyList<SemanticTag> tags)
        {
            if (tags == null || tags.Count == 0)
                return true;

            foreach (SemanticTag tag in tags)
            {
                if (HasTag(tag))
                    return true;
            }

            return false;
        }

        /// <summary>Determines whether any tag category.</summary>
        public bool HasAnyTagCategory(TagCategory category)
        {
            return category && category.SupportsAssets && anyTagCategories.Contains(category);
        }

        /// <summary>Determines whether tag in category.</summary>
        public bool HasTagInCategory(TagCategory category)
        {
            if (!category || !category.SupportsAssets)
                return false;

            foreach (SemanticTag tag in semanticTags)
            {
                if (tag && tag.Category == category)
                    return true;
            }

            return false;
        }

        /// <summary>Adds tag.</summary>
        public void AddTag(SemanticTag tag)
        {
            if (!tag || !tag.Category || !tag.Category.SupportsAssets || semanticTags.Contains(tag))
                return;

            semanticTags.Add(tag);
        }

        /// <summary>Removes tag.</summary>
        public void RemoveTag(SemanticTag tag)
        {
            semanticTags.Remove(tag);
        }

        /// <summary>
        /// Replaces required support tags. Tags in one category are alternatives; represented categories combine conjunctively.
        /// </summary>
        public void SetRequiredSupportTags(IEnumerable<SemanticTag> tags)
        {
            requiredSupportTags = NormalizeTags(tags, requireSurfaceUsage: true);
        }

        /// <summary>Replaces the forbidden support tags, which take precedence over required tags.</summary>
        public void SetForbiddenSupportTags(IEnumerable<SemanticTag> tags)
        {
            forbiddenSupportTags = NormalizeTags(tags, requireSurfaceUsage: true);
        }

        /// <summary>Replaces categories whose Required selection is explicitly None.</summary>
        public void SetRequiredSupportNoneCategories(IEnumerable<TagCategory> categories)
        {
            requiredSupportNoneCategories = NormalizeSurfaceCategories(categories);
        }

        /// <summary>Replaces categories whose Forbidden selection is explicitly Any.</summary>
        public void SetForbiddenSupportAnyCategories(IEnumerable<TagCategory> categories)
        {
            forbiddenSupportAnyCategories = NormalizeSurfaceCategories(categories);
        }

        /// <summary>Configures the maximum number of this asset accepted in generated output.</summary>
        public void SetPlacementLimit(bool limited, int maximum)
        {
            limitPlacements = limited;
            maxPlacements = Mathf.Max(1, maximum);
        }

        /// <summary>Determines whether the supplied generated count has reached this asset's limit.</summary>
        public bool HasReachedPlacementLimit(int generatedCount) =>
            limitPlacements && Mathf.Max(0, generatedCount) >= MaxPlacements;

        /// <summary>Returns the greatest configured minimum distance matching another asset.</summary>
        public float GetMinimumSpacingTo(AssetDefinition other)
        {
            float minimum = 0f;

            foreach (AssetSpacingRule rule in spacingRules)
            {
                if (rule?.Matches(other) == true)
                    minimum = Mathf.Max(minimum, rule.MinimumDistance);
            }

            return minimum;
        }

        /// <summary>Replaces asset-specific spacing rules with normalized entries.</summary>
        public void SetSpacingRules(IEnumerable<AssetSpacingRule> rules)
        {
            spacingRules = rules?.Where(rule => rule != null).ToList() ?? new List<AssetSpacingRule>();
            NormalizeSpacingRules();
        }

        /// <summary>Configures the optional distance relationship to detected walls.</summary>
        public void SetWallProximity(WallProximityMode mode, float distance)
        {
            wallProximityMode = placementType is PlacementType.Floor or PlacementType.Ceiling
                ? mode
                : WallProximityMode.AnyDistance;
            wallDistance = Mathf.Max(0f, distance);
        }

        /// <summary>Removes missing tags.</summary>
        public void RemoveMissingTags()
        {
            semanticTags.RemoveAll(tag => !tag || !tag.Category || !tag.Category.SupportsAssets);
            anyTagCategories.RemoveAll(category => !category || !category.SupportsAssets);
            requiredSupportTags.RemoveAll(tag => !tag || !tag.Category || !tag.Category.SupportsSurfaces);
            forbiddenSupportTags.RemoveAll(tag => !tag || !tag.Category || !tag.Category.SupportsSurfaces);
            requiredSupportNoneCategories.RemoveAll(category => !category || !category.SupportsSurfaces);
            forbiddenSupportAnyCategories.RemoveAll(category => !category || !category.SupportsSurfaces);
            NormalizeSpacingRules();
            assetRelativePlacement ??= new AssetRelativePlacementRule();
            assetRelativePlacement.Normalize();
            pathPlacement ??= new PathPlacementRule();
            pathPlacement.Normalize();
        }

        private void OnValidate()
        {
            InvalidatePlacementGeometryCache();
            maxPlacements = Mathf.Max(1, maxPlacements);
            wallDistance = Mathf.Max(0f, wallDistance);
            clearanceSize = ClampSize(clearanceSize);

            if (placementType is PlacementType.Wall or PlacementType.InsideSpace)
                wallProximityMode = WallProximityMode.AnyDistance;

            RemoveMissingTags();
        }

        private static Vector3 ClampSize(Vector3 size) => new(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y),
            Mathf.Max(0.01f, size.z));

        private OrientedBounds CreateCorrectedClearanceBounds(
            Vector3 objectOrigin,
            Quaternion placementRotation)
        {
            EnsurePlacementGeometryCache();
            return new OrientedBounds(
                objectOrigin + placementRotation * cachedClearanceCenterOffset,
                cachedClearanceSize,
                placementRotation);
        }

        private void EnsurePlacementGeometryCache()
        {
            if (placementGeometryCacheValid)
                return;

            cachedPrefabRotationOffset = Quaternion.Euler(prefabRotationOffset);
            Vector3 prefabScale = prefab ? prefab.transform.localScale : Vector3.one;
            Vector3 scaledBoundsCenterOffset = Vector3.Scale(boundsCenterOffset, prefabScale);
            Vector3 scaledClearanceCenterOffset = Vector3.Scale(clearanceCenterOffset, prefabScale);
            cachedBoundsSize = RotateAxisAlignedSize(ClampSize(boundsSize), cachedPrefabRotationOffset);
            cachedBoundsCenterOffset = cachedPrefabRotationOffset * scaledBoundsCenterOffset;
            cachedClearanceSize = RotateAxisAlignedSize(ClampSize(clearanceSize), cachedPrefabRotationOffset);
            cachedClearanceCenterOffset = cachedPrefabRotationOffset * scaledClearanceCenterOffset;
            placementGeometryCacheValid = true;
        }

        private void InvalidatePlacementGeometryCache() => placementGeometryCacheValid = false;

        private static Vector3 RotateAxisAlignedSize(Vector3 size, Quaternion rotation)
        {
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            return new Vector3(
                Mathf.Abs(right.x) * size.x + Mathf.Abs(up.x) * size.y + Mathf.Abs(forward.x) * size.z,
                Mathf.Abs(right.y) * size.x + Mathf.Abs(up.y) * size.y + Mathf.Abs(forward.y) * size.z,
                Mathf.Abs(right.z) * size.x + Mathf.Abs(up.z) * size.y + Mathf.Abs(forward.z) * size.z);
        }

        private void NormalizeSpacingRules()
        {
            spacingRules ??= new List<AssetSpacingRule>();
            spacingRules.RemoveAll(rule => rule == null);

            foreach (AssetSpacingRule rule in spacingRules)
                rule.Normalize();
        }

        private static List<SemanticTag> NormalizeTags(
            IEnumerable<SemanticTag> tags,
            bool requireSurfaceUsage = false) =>
            tags?
                .Where(tag => tag && tag.Category && (!requireSurfaceUsage || tag.Category.SupportsSurfaces))
                .Distinct()
                .ToList() ?? new List<SemanticTag>();

        private static List<TagCategory> NormalizeSurfaceCategories(
            IEnumerable<TagCategory> categories) =>
            categories?
                .Where(category => category && category.SupportsSurfaces)
                .Distinct()
                .ToList() ?? new List<TagCategory>();
    }
}
