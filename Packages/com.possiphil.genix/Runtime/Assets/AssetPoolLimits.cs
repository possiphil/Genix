using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Constrains the combined generated count of every asset carrying one semantic tag.</summary>
    [Serializable]
    public sealed class AssetPoolTagLimit
    {
        [SerializeField] private SemanticTag assetTag;
        [SerializeField, Min(0)] private int minPlacements;
        [SerializeField, Min(0)] private int maxPlacements = 1;

        /// <summary>Gets the asset-compatible tag whose instances share this quota.</summary>
        public SemanticTag AssetTag => assetTag;
        /// <summary>Gets the minimum combined count across existing output and the current plan.</summary>
        public int MinPlacements => Mathf.Clamp(minPlacements, 0, MaxPlacements);
        /// <summary>Gets the maximum combined count across existing output and the current plan.</summary>
        public int MaxPlacements => Mathf.Max(0, maxPlacements);
        /// <summary>Indicates whether this rule has a usable asset tag.</summary>
        public bool IsConfigured => IsAssetTag(assetTag);

        /// <summary>Configures the shared generated-output limit for an asset-compatible tag.</summary>
        public void Configure(SemanticTag tag, int maximum)
        {
            assetTag = IsAssetTag(tag) ? tag : null;
            minPlacements = 0;
            maxPlacements = Mathf.Max(0, maximum);
        }

        /// <summary>Configures a required count range shared by all assets carrying the tag.</summary>
        public void Configure(SemanticTag tag, int minimum, int maximum)
        {
            assetTag = IsAssetTag(tag) ? tag : null;
            minPlacements = Mathf.Max(0, minimum);
            maxPlacements = Mathf.Max(minPlacements, maximum);
        }

        /// <summary>Determines whether the supplied asset consumes this quota.</summary>
        public bool Matches(AssetDefinition asset) => asset && IsConfigured && asset.HasTag(assetTag);

        internal void Normalize()
        {
            if (!IsAssetTag(assetTag))
                assetTag = null;

            maxPlacements = Mathf.Max(0, maxPlacements);
            minPlacements = Mathf.Clamp(minPlacements, 0, maxPlacements);
        }

        private static bool IsAssetTag(SemanticTag tag) =>
            tag && tag.SupportsAssets;
    }

    /// <summary>
    /// Constrains the combined number of assets carrying one tag that may be assigned to each
    /// matching relation anchor.
    /// </summary>
    [Serializable]
    public sealed class AssetPoolAnchorGroupLimit
    {
        [SerializeField] private AssetRelativeAnchorSource source = AssetRelativeAnchorSource.Any;
        [SerializeField] private AssetRelativeTargetScope anchorScope = AssetRelativeTargetScope.AssetTag;
        [SerializeField] private AssetDefinition anchorAsset;
        [SerializeField] private SemanticTag anchorTag;
        [SerializeField] private SemanticTag memberTag;
        [SerializeField] private AssetRelativeCardinalityMode cardinalityMode = AssetRelativeCardinalityMode.Exactly;
        [SerializeField, Min(1)] private int cardinalityCount = 1;
        [SerializeField, Min(1)] private int cardinalityMaximumCount = 1;

        /// <summary>Gets which generated or explicit scene anchors participate.</summary>
        public AssetRelativeAnchorSource Source => source;
        /// <summary>Gets whether anchors are selected by concrete asset or semantic tag.</summary>
        public AssetRelativeTargetScope AnchorScope => anchorScope;
        /// <summary>Gets the concrete anchor asset when <see cref="AnchorScope"/> is Asset.</summary>
        public AssetDefinition AnchorAsset => anchorAsset;
        /// <summary>Gets the anchor tag when <see cref="AnchorScope"/> is Asset Tag.</summary>
        public SemanticTag AnchorTag => anchorTag;
        /// <summary>Gets the tag shared by all dependent assets counted by this rule.</summary>
        public SemanticTag MemberTag => memberTag;
        /// <summary>Gets the authored cardinality policy.</summary>
        public AssetRelativeCardinalityMode CardinalityMode => cardinalityMode;
        /// <summary>Gets the required lower count, or zero when no minimum applies.</summary>
        public int MinimumPerAnchor => HasMinimumPerAnchor ? Mathf.Max(1, cardinalityCount) : 0;
        /// <summary>Gets the allowed upper count, or an unlimited value when no maximum applies.</summary>
        public int MaximumPerAnchor => HasMaximumPerAnchor
            ? cardinalityMode == AssetRelativeCardinalityMode.Between
                ? Mathf.Max(Mathf.Max(1, cardinalityCount), cardinalityMaximumCount)
                : Mathf.Max(1, cardinalityCount)
            : int.MaxValue;
        /// <summary>Indicates whether generation must actively satisfy a lower count.</summary>
        public bool HasMinimumPerAnchor => cardinalityMode is
            AssetRelativeCardinalityMode.AtLeast or
            AssetRelativeCardinalityMode.Exactly or
            AssetRelativeCardinalityMode.Between;
        /// <summary>Indicates whether candidates must respect an upper count.</summary>
        public bool HasMaximumPerAnchor => cardinalityMode is
            AssetRelativeCardinalityMode.AtMost or
            AssetRelativeCardinalityMode.Exactly or
            AssetRelativeCardinalityMode.Between;
        /// <summary>Indicates whether the rule has usable anchor and member selectors.</summary>
        public bool IsConfigured =>
            cardinalityMode != AssetRelativeCardinalityMode.Unlimited &&
            IsAssetTag(memberTag) &&
            (anchorScope switch
            {
                AssetRelativeTargetScope.Asset => anchorAsset,
                AssetRelativeTargetScope.AssetTag => IsAssetTag(anchorTag),
                _ => false
            });

        /// <summary>Determines whether semantic anchor data belongs to this group rule.</summary>
        public bool MatchesAnchor(AssetDefinition asset, IReadOnlyList<SemanticTag> tags)
        {
            if (!IsConfigured)
                return false;

            if (anchorScope == AssetRelativeTargetScope.Asset)
                return asset == anchorAsset;

            if (asset && asset.HasTag(anchorTag))
                return true;

            return tags != null && tags.Contains(anchorTag);
        }

        /// <summary>Determines whether an asset contributes to the grouped dependent count.</summary>
        public bool MatchesMember(AssetDefinition asset) => asset && IsConfigured && asset.HasTag(memberTag);

        /// <summary>Configures a group around one concrete anchor asset.</summary>
        public void ConfigureAsset(
            AssetDefinition asset,
            SemanticTag groupedMemberTag,
            AssetRelativeAnchorSource anchorSource = AssetRelativeAnchorSource.Any)
        {
            source = anchorSource;
            anchorScope = AssetRelativeTargetScope.Asset;
            anchorAsset = asset;
            anchorTag = null;
            memberTag = groupedMemberTag;
            Normalize();
        }

        /// <summary>Configures a group around every anchor carrying one asset-compatible tag.</summary>
        public void ConfigureTag(
            SemanticTag tag,
            SemanticTag groupedMemberTag,
            AssetRelativeAnchorSource anchorSource = AssetRelativeAnchorSource.Any)
        {
            source = anchorSource;
            anchorScope = AssetRelativeTargetScope.AssetTag;
            anchorAsset = null;
            anchorTag = tag;
            memberTag = groupedMemberTag;
            Normalize();
        }

        /// <summary>Sets a one-value per-anchor cardinality.</summary>
        public void SetCardinality(AssetRelativeCardinalityMode mode, int count)
        {
            cardinalityMode = Enum.IsDefined(typeof(AssetRelativeCardinalityMode), mode)
                ? mode
                : AssetRelativeCardinalityMode.Unlimited;
            cardinalityCount = Mathf.Max(1, count);
            cardinalityMaximumCount = cardinalityCount;
        }

        /// <summary>Sets an inclusive per-anchor count range.</summary>
        public void SetCardinalityRange(int minimum, int maximum)
        {
            cardinalityMode = AssetRelativeCardinalityMode.Between;
            cardinalityCount = Mathf.Max(1, minimum);
            cardinalityMaximumCount = Mathf.Max(cardinalityCount, maximum);
        }

        internal void Normalize()
        {
            if (!Enum.IsDefined(typeof(AssetRelativeAnchorSource), source))
                source = AssetRelativeAnchorSource.Any;
            if (!Enum.IsDefined(typeof(AssetRelativeTargetScope), anchorScope))
                anchorScope = AssetRelativeTargetScope.AssetTag;
            if (!Enum.IsDefined(typeof(AssetRelativeCardinalityMode), cardinalityMode))
                cardinalityMode = AssetRelativeCardinalityMode.Unlimited;

            if (!IsAssetTag(anchorTag))
                anchorTag = null;
            if (!IsAssetTag(memberTag))
                memberTag = null;

            cardinalityCount = Mathf.Max(1, cardinalityCount);
            cardinalityMaximumCount = Mathf.Max(cardinalityCount, cardinalityMaximumCount);
        }

        private static bool IsAssetTag(SemanticTag tag) =>
            tag && tag.SupportsAssets;
    }
}
