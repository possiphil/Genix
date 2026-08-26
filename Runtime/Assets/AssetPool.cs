using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Core;
using Genix.Orientation;
using Genix.Placement;
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
            tag && tag.Category && tag.Category.SupportsAssets;
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
            tag && tag.Category && tag.Category.SupportsAssets;
    }

    /// <summary>
    /// Supplies asset definitions either from an explicit static list or from dynamic catalog filters.
    /// </summary>
    public sealed class AssetPool : ScriptableObject
    {
        [SerializeField] private AssetPoolMode mode = AssetPoolMode.Static;

        [SerializeField] private List<AssetDefinition> staticAssets = new();

        [SerializeField] private bool filterByPlacementType;
        [SerializeField] private PlacementType placementType = PlacementType.Floor;

        [SerializeField] private bool filterByOrientationMode;
        [SerializeField] private OrientationMode orientationMode = OrientationMode.None;

        [SerializeField] private List<TagCategoryFilter> categoryFilters = new();
        [SerializeField] private List<AssetPoolTagLimit> tagPlacementLimits = new();
        [SerializeField] private List<AssetPoolAnchorGroupLimit> anchorGroupLimits = new();

        /// <summary>Gets mode.</summary>
        public AssetPoolMode Mode => mode;
        /// <summary>Gets static assets.</summary>
        public IReadOnlyList<AssetDefinition> StaticAssets => staticAssets;

        /// <summary>Indicates whether filter by placement type.</summary>
        public bool FilterByPlacementType => filterByPlacementType;
        /// <summary>Gets placement type.</summary>
        public PlacementType PlacementType => placementType;

        /// <summary>Indicates whether filter by orientation mode.</summary>
        public bool FilterByOrientationMode => filterByOrientationMode;
        /// <summary>Gets orientation mode.</summary>
        public OrientationMode OrientationMode => orientationMode;

        /// <summary>Gets category filters.</summary>
        public IReadOnlyList<TagCategoryFilter> CategoryFilters => categoryFilters;
        /// <summary>Gets shared placement limits for asset-tag groups.</summary>
        public IReadOnlyList<AssetPoolTagLimit> TagPlacementLimits => tagPlacementLimits;
        /// <summary>Gets grouped dependent-count rules evaluated independently for every matching anchor.</summary>
        public IReadOnlyList<AssetPoolAnchorGroupLimit> AnchorGroupLimits => anchorGroupLimits;

        /// <summary>Indicates whether static.</summary>
        public bool IsStatic => mode == AssetPoolMode.Static;
        /// <summary>Indicates whether dynamic.</summary>
        public bool IsDynamic => mode == AssetPoolMode.Dynamic;

        /// <summary>Indicates whether valid static assets.</summary>
        public bool HasValidStaticAssets => staticAssets.Any(asset => asset);

        /// <summary>Determines whether an asset or any matching tag group has reached its current-plan limit.</summary>
        public bool HasReachedPlacementLimit(AssetDefinition asset, GenerationPlan plan)
        {
            return HasReachedPlacementLimit(asset, plan, null);
        }

        /// <summary>Includes existing generated output and the current plan when evaluating placement limits.</summary>
        public bool HasReachedPlacementLimit(AssetDefinition asset, GenerationContext context)
        {
            return HasReachedPlacementLimit(
                asset,
                context?.Plan,
                context?.GeneratedSceneObjects);
        }

        /// <summary>Includes existing generated output when evaluating placement limits.</summary>
        internal bool HasReachedPlacementLimit(
            AssetDefinition asset,
            GenerationPlan plan,
            SceneObjectIndex existingGeneratedObjects)
        {
            if (!asset)
                return true;

            int assetCount = (plan?.GetAssetCount(asset) ?? 0) +
                             (existingGeneratedObjects?.GetAssetCount(asset) ?? 0);

            if (asset.HasReachedPlacementLimit(assetCount))
                return true;

            return TryGetReachedTagLimit(asset, plan, existingGeneratedObjects, out _);
        }

        /// <summary>Finds the first matching tag quota that has reached its configured maximum.</summary>
        public bool TryGetReachedTagLimit(
            AssetDefinition asset,
            GenerationPlan plan,
            out AssetPoolTagLimit reachedLimit)
        {
            return TryGetReachedTagLimit(asset, plan, null, out reachedLimit);
        }

        internal bool TryGetReachedTagLimit(
            AssetDefinition asset,
            GenerationPlan plan,
            SceneObjectIndex existingGeneratedObjects,
            out AssetPoolTagLimit reachedLimit)
        {
            reachedLimit = null;

            if (!asset)
                return false;

            foreach (AssetPoolTagLimit limit in tagPlacementLimits)
            {
                if (limit == null || !limit.Matches(asset))
                {
                    continue;
                }

                int tagCount = (plan?.GetAssetTagCount(limit.AssetTag) ?? 0) +
                               (existingGeneratedObjects?.GetAssetTagCount(limit.AssetTag) ?? 0);

                if (tagCount < limit.MaxPlacements)
                    continue;

                reachedLimit = limit;
                return true;
            }

            return false;
        }

        /// <summary>Finds the first grouped per-anchor quota reached by this dependent asset.</summary>
        internal bool TryGetReachedAnchorGroupLimit(
            AssetDefinition asset,
            GenerationContext context,
            RelativeAnchor anchor,
            out AssetPoolAnchorGroupLimit reachedLimit)
        {
            reachedLimit = null;
            if (!asset || context == null)
                return false;

            foreach (AssetPoolAnchorGroupLimit limit in anchorGroupLimits)
            {
                if (limit is not { IsConfigured: true, HasMaximumPerAnchor: true } ||
                    !limit.MatchesMember(asset) ||
                    !limit.MatchesAnchor(anchor.Asset, anchor.AssetTags) ||
                    limit.Source != AssetRelativeAnchorSource.Any && limit.Source != anchor.Source)
                {
                    continue;
                }

                if (RelativeAnchorProvider.GetAssignedAssetTagCount(context, limit, anchor) <
                    limit.MaximumPerAnchor)
                {
                    continue;
                }

                reachedLimit = limit;
                return true;
            }

            return false;
        }

        /// <summary>Determines whether this asset belongs to a shared tag count that has not reached its minimum.</summary>
        public bool ShouldPrioritizeForMinimum(AssetDefinition asset, GenerationContext context)
        {
            if (!asset || context == null)
                return false;

            foreach (AssetPoolTagLimit limit in tagPlacementLimits)
            {
                if (limit is { MinPlacements: > 0 } &&
                    limit.Matches(asset) &&
                    GetTagPlacementCount(limit.AssetTag, context.Plan, context.GeneratedSceneObjects) <
                    limit.MinPlacements)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reserves the final plan slots for still-missing shared tag minimums while leaving earlier
        /// placements unconstrained.
        /// </summary>
        public bool CanPlaceBeforeRequiredMinimum(AssetDefinition asset, GenerationContext context)
        {
            if (!asset || context == null)
                return false;

            int totalDeficit = 0;
            bool matchesUnmetMinimum = false;

            foreach (AssetPoolTagLimit limit in tagPlacementLimits)
            {
                if (limit is not { IsConfigured: true, MinPlacements: > 0 })
                    continue;

                int count = GetTagPlacementCount(
                    limit.AssetTag,
                    context.Plan,
                    context.GeneratedSceneObjects);
                int deficit = Mathf.Max(0, limit.MinPlacements - count);
                if (deficit <= 0)
                    continue;

                totalDeficit += deficit;
                matchesUnmetMinimum |= limit.Matches(asset);
            }

            int remainingSlots = Mathf.Max(0, context.Count - context.Plan.Count);
            return totalDeficit == 0 || remainingSlots > totalDeficit || matchesUnmetMinimum;
        }

        /// <summary>Formats every shared tag minimum that the current output and plan do not satisfy.</summary>
        public string FormatUnmetTagMinimums(GenerationContext context)
        {
            if (context == null)
                return string.Empty;

            List<string> missing = new();
            foreach (AssetPoolTagLimit limit in tagPlacementLimits)
            {
                if (limit is not { IsConfigured: true, MinPlacements: > 0 })
                    continue;

                int count = GetTagPlacementCount(
                    limit.AssetTag,
                    context.Plan,
                    context.GeneratedSceneObjects);
                if (count < limit.MinPlacements)
                    missing.Add($"{limit.AssetTag.DisplayName} {count}/{limit.MinPlacements}");
            }

            return missing.Count > 0
                ? $" Required shared tag counts not completed: {string.Join(", ", missing)}."
                : string.Empty;
        }

        /// <summary>Resolves the effective, distinct asset set against a catalog.</summary>
        public IReadOnlyList<AssetDefinition> ResolveAssets(AssetCatalog catalog)
        {
            if (!catalog)
                return new List<AssetDefinition>();

            return ResolveAssets(catalog.Assets);
        }

        /// <summary>Resolves the effective, distinct asset set against an arbitrary catalog sequence.</summary>
        public IReadOnlyList<AssetDefinition> ResolveAssets(IEnumerable<AssetDefinition> catalogAssets)
        {
            if (IsStatic)
                return GetValidStaticAssets();

            if (catalogAssets == null)
                return new List<AssetDefinition>();

            return catalogAssets
                .Where(MatchesAsset)
                .Distinct()
                .ToList();
        }

        /// <summary>Determines whether an asset satisfies every active dynamic-pool filter.</summary>
        public bool MatchesAsset(AssetDefinition asset)
        {
            if (!asset)
                return false;

            if (filterByPlacementType && asset.PlacementType != placementType)
                return false;

            if (filterByOrientationMode && asset.OrientationMode != orientationMode)
                return false;

            foreach (TagCategoryFilter categoryFilter in categoryFilters)
            {
                if (categoryFilter == null || !categoryFilter.IsActive)
                    continue;

                if (!categoryFilter.Matches(asset))
                    return false;
            }

            return true;
        }

        /// <summary>Adds static asset.</summary>
        public void AddStaticAsset(AssetDefinition asset)
        {
            if (!asset || staticAssets.Contains(asset))
                return;

            staticAssets.Add(asset);
        }

        /// <summary>Adds static assets.</summary>
        public void AddStaticAssets(IEnumerable<AssetDefinition> assets)
        {
            if (assets == null)
                return;

            foreach (AssetDefinition asset in assets)
                AddStaticAsset(asset);
        }

        /// <summary>Removes static asset.</summary>
        public void RemoveStaticAsset(AssetDefinition asset)
        {
            staticAssets.Remove(asset);
        }

        /// <summary>Removes missing references.</summary>
        public void RemoveMissingReferences()
        {
            staticAssets.RemoveAll(asset => !asset);

            foreach (TagCategoryFilter filter in categoryFilters)
                filter?.RemoveMissingTags();

            categoryFilters.RemoveAll(filter => filter == null || !filter.IsActive);
            NormalizeTagPlacementLimits();
            NormalizeAnchorGroupLimits();
        }

        private IReadOnlyList<AssetDefinition> GetValidStaticAssets()
        {
            return staticAssets
                .Where(asset => asset)
                .Distinct()
                .ToList();
        }

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(string displayName, AssetPoolMode mode)
        {
            name = displayName;
            this.mode = mode;
        }

        /// <summary>Replaces shared asset-tag placement limits with normalized, unique entries.</summary>
        public void SetTagPlacementLimits(IEnumerable<AssetPoolTagLimit> limits)
        {
            tagPlacementLimits = limits?.Where(limit => limit != null).ToList() ??
                                 new List<AssetPoolTagLimit>();
            NormalizeTagPlacementLimits();
        }

        /// <summary>Replaces per-anchor grouped cardinality rules with normalized entries.</summary>
        public void SetAnchorGroupLimits(IEnumerable<AssetPoolAnchorGroupLimit> limits)
        {
            anchorGroupLimits = limits?.Where(limit => limit != null).ToList() ??
                                new List<AssetPoolAnchorGroupLimit>();
            NormalizeAnchorGroupLimits();
        }

        /// <summary>Removes tag.</summary>
        public void RemoveTag(SemanticTag tag)
        {
            foreach (TagCategoryFilter filter in categoryFilters)
                filter?.RemoveTag(tag);

            tagPlacementLimits.RemoveAll(limit => limit == null || limit.AssetTag == tag);
            anchorGroupLimits.RemoveAll(limit =>
                limit == null || limit.AnchorTag == tag || limit.MemberTag == tag);
        }

        /// <summary>Removes category.</summary>
        public void RemoveCategory(TagCategory category)
        {
            categoryFilters.RemoveAll(filter => filter != null && filter.Category == category);
            tagPlacementLimits.RemoveAll(limit =>
                limit == null || (limit.AssetTag && limit.AssetTag.Category == category));
            anchorGroupLimits.RemoveAll(limit =>
                limit == null ||
                limit.AnchorTag && limit.AnchorTag.Category == category ||
                limit.MemberTag && limit.MemberTag.Category == category);
        }

        private void OnValidate()
        {
            NormalizeTagPlacementLimits();
            NormalizeAnchorGroupLimits();
        }

        private void NormalizeTagPlacementLimits()
        {
            tagPlacementLimits ??= new List<AssetPoolTagLimit>();
            tagPlacementLimits.RemoveAll(limit => limit == null);

            for (int i = 0; i < tagPlacementLimits.Count; i++)
            {
                AssetPoolTagLimit limit = tagPlacementLimits[i];
                limit.Normalize();

                if (!limit.IsConfigured)
                    continue;

                for (int duplicateIndex = tagPlacementLimits.Count - 1; duplicateIndex > i; duplicateIndex--)
                {
                    if (tagPlacementLimits[duplicateIndex]?.AssetTag == limit.AssetTag)
                        tagPlacementLimits.RemoveAt(duplicateIndex);
                }
            }
        }

        private void NormalizeAnchorGroupLimits()
        {
            anchorGroupLimits ??= new List<AssetPoolAnchorGroupLimit>();
            anchorGroupLimits.RemoveAll(limit => limit == null);
            foreach (AssetPoolAnchorGroupLimit limit in anchorGroupLimits)
                limit.Normalize();
        }

        private static int GetTagPlacementCount(
            SemanticTag tag,
            GenerationPlan plan,
            SceneObjectIndex existingGeneratedObjects) =>
            (plan?.GetAssetTagCount(tag) ?? 0) +
            (existingGeneratedObjects?.GetAssetTagCount(tag) ?? 0);
    }
}
