using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using UnityEngine;

namespace Genix.Semantics
{
    /// <summary>Identifies whether a surface capacity rule targets one asset or every asset carrying a tag.</summary>
    public enum PlacementSurfaceCapacityRuleScope
    {
        /// <summary>The rule applies to one concrete asset definition.</summary>
        Asset,
        /// <summary>The rule applies to every asset carrying the selected semantic tag.</summary>
        AssetTag
    }

    /// <summary>Limits how many matching assets may use one placement surface.</summary>
    [Serializable]
    public sealed class PlacementSurfaceCapacityRule
    {
        [SerializeField] private PlacementSurfaceCapacityRuleScope scope = PlacementSurfaceCapacityRuleScope.AssetTag;
        [SerializeField] private AssetDefinition asset;
        [SerializeField] private SemanticTag assetTag;
        [SerializeField, Min(0)] private int maxCapacity = 1;

        /// <summary>Gets whether this rule targets a concrete asset or an asset tag.</summary>
        public PlacementSurfaceCapacityRuleScope Scope => scope;
        /// <summary>Gets the concrete asset selected by an asset-scoped rule.</summary>
        public AssetDefinition Asset => asset;
        /// <summary>Gets the semantic tag selected by a tag-scoped rule.</summary>
        public SemanticTag AssetTag => assetTag;
        /// <summary>Gets the maximum number of matching placements supported by the surface.</summary>
        public int MaxCapacity => Mathf.Max(0, maxCapacity);
        /// <summary>Indicates whether the rule has a valid target and can affect placement.</summary>
        public bool IsConfigured => scope switch
        {
            PlacementSurfaceCapacityRuleScope.Asset => asset,
            PlacementSurfaceCapacityRuleScope.AssetTag => IsAssetTag(assetTag),
            _ => false
        };
        /// <summary>Gets a concise name suitable for diagnostics.</summary>
        public string DisplayName => scope switch
        {
            PlacementSurfaceCapacityRuleScope.Asset when asset => asset.AssetName,
            PlacementSurfaceCapacityRuleScope.AssetTag when assetTag => assetTag.DisplayName,
            _ => "Unconfigured Rule"
        };

        /// <summary>Configures the rule to limit one concrete asset.</summary>
        public void ConfigureAsset(AssetDefinition targetAsset, int capacity)
        {
            scope = PlacementSurfaceCapacityRuleScope.Asset;
            asset = targetAsset;
            assetTag = null;
            maxCapacity = Mathf.Max(0, capacity);
        }

        /// <summary>Configures the rule to limit every asset carrying an asset-compatible semantic tag.</summary>
        public void ConfigureTag(SemanticTag targetTag, int capacity)
        {
            scope = PlacementSurfaceCapacityRuleScope.AssetTag;
            asset = null;
            assetTag = IsAssetTag(targetTag) ? targetTag : null;
            maxCapacity = Mathf.Max(0, capacity);
        }

        /// <summary>Determines whether the supplied asset consumes this rule's capacity.</summary>
        public bool Matches(AssetDefinition candidate) =>
            candidate && IsConfigured && (scope switch
            {
                PlacementSurfaceCapacityRuleScope.Asset => candidate == asset,
                PlacementSurfaceCapacityRuleScope.AssetTag => candidate.HasTag(assetTag),
                _ => false
            });

        internal void Normalize()
        {
            maxCapacity = Mathf.Max(0, maxCapacity);

            if (scope == PlacementSurfaceCapacityRuleScope.Asset)
                assetTag = null;
            else
            {
                asset = null;

                if (!IsAssetTag(assetTag))
                    assetTag = null;
            }
        }

        internal bool HasSameTarget(PlacementSurfaceCapacityRule other) =>
            other != null && scope == other.scope && (scope switch
            {
                PlacementSurfaceCapacityRuleScope.Asset => asset && asset == other.asset,
                PlacementSurfaceCapacityRuleScope.AssetTag => assetTag && assetTag == other.assetTag,
                _ => false
            });

        private static bool IsAssetTag(SemanticTag tag) =>
            tag && tag.SupportsAssets;
    }

    /// <summary>
    /// Describes the semantic role, preferred facing direction, and optional capacity of one placement surface.
    /// </summary>
    /// <remarks>
    /// Add the component to the collider itself or one of its parents. All descendant colliders share the same
    /// descriptor and capacity. The component does not change Unity physics behavior.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlacementSurfaceDescriptor : MonoBehaviour
    {
        [SerializeField] private List<SemanticTag> surfaceTags = new();
        [SerializeField] private List<TagCategory> noneTagCategories = new();
        [SerializeField] private List<SemanticTag> allowedAssetTags = new();
        [SerializeField] private List<SemanticTag> forbiddenAssetTags = new();
        [SerializeField] private bool limitCapacity;
        [SerializeField, Min(0)] private int maxCapacity = 1;
        [SerializeField] private List<PlacementSurfaceCapacityRule> assetCapacityRules = new();

        /// <summary>Gets the semantic tags exposed by this surface.</summary>
        public IReadOnlyList<SemanticTag> SurfaceTags => surfaceTags;
        /// <summary>Gets categories explicitly configured as None instead of their default Any state.</summary>
        public IReadOnlyList<TagCategory> NoneTagCategories => noneTagCategories;
        /// <summary>Gets asset tags of which at least one must match when the collection is not empty.</summary>
        public IReadOnlyList<SemanticTag> AllowedAssetTags => allowedAssetTags;
        /// <summary>Gets asset tags that reject matching assets before allowed tags are considered.</summary>
        public IReadOnlyList<SemanticTag> ForbiddenAssetTags => forbiddenAssetTags;
        /// <summary>Gets the world-space direction used by assets that match their support orientation.</summary>
        public Vector3 SupportForward => transform.forward;
        /// <summary>Indicates whether the number of supported generated objects is limited.</summary>
        public bool LimitCapacity => limitCapacity;
        /// <summary>Gets the maximum supported object count when <see cref="LimitCapacity"/> is enabled.</summary>
        public int MaxCapacity => Mathf.Max(0, maxCapacity);
        /// <summary>Gets optional limits for concrete assets or groups of assets sharing a semantic tag.</summary>
        public IReadOnlyList<PlacementSurfaceCapacityRule> AssetCapacityRules => assetCapacityRules;

        /// <summary>Determines whether the descriptor contains the supplied semantic tag.</summary>
        public bool HasTag(SemanticTag tag)
        {
            if (!tag || !tag.Category || !tag.Category.SupportsSurfaces || noneTagCategories.Contains(tag.Category))
                return false;

            bool hasSpecificSelection = surfaceTags.Any(existing =>
                existing && existing.Category == tag.Category);
            return !hasSpecificSelection || surfaceTags.Contains(tag);
        }

        /// <summary>Indicates whether the category uses its default Any state.</summary>
        public bool AcceptsAnyTag(TagCategory category) =>
            category &&
            category.SupportsSurfaces &&
            !noneTagCategories.Contains(category) &&
            surfaceTags.All(tag => !tag || tag.Category != category);

        /// <summary>Indicates whether the category explicitly accepts no tags.</summary>
        public bool AcceptsNoTag(TagCategory category) =>
            category && category.SupportsSurfaces && noneTagCategories.Contains(category);

        /// <summary>Determines whether an asset satisfies this surface's allow and deny rules.</summary>
        public bool AcceptsAsset(AssetDefinition asset)
        {
            if (!asset)
                return false;

            foreach (SemanticTag tag in forbiddenAssetTags)
            {
                if (IsAssetTag(tag) && asset.HasTag(tag))
                    return false;
            }

            if (allowedAssetTags.Count == 0)
                return true;

            foreach (SemanticTag tag in allowedAssetTags)
            {
                if (IsAssetTag(tag) && asset.HasTag(tag))
                    return true;
            }

            return false;
        }

        /// <summary>Replaces the semantic tags with a normalized, duplicate-free collection.</summary>
        public void SetSurfaceTags(IEnumerable<SemanticTag> tags)
        {
            surfaceTags = tags?
                .Where(tag => tag && tag.SupportsSurfaces)
                .Distinct()
                .ToList() ?? new List<SemanticTag>();
            noneTagCategories.RemoveAll(category =>
                surfaceTags.Any(tag => tag && tag.Category == category));
        }

        /// <summary>Sets one category to Any, None, or a concrete tag selection.</summary>
        public void SetCategorySelection(
            TagCategory category,
            IEnumerable<SemanticTag> selectedTags,
            bool selectNone)
        {
            if (!category || !category.SupportsSurfaces)
                return;

            surfaceTags.RemoveAll(tag => !tag || tag.Category == category);
            noneTagCategories.RemoveAll(existing => !existing || existing == category);

            if (selectNone)
            {
                noneTagCategories.Add(category);
                return;
            }

            surfaceTags.AddRange(selectedTags?
                .Where(tag => tag && tag.Category == category)
                .Distinct() ?? Enumerable.Empty<SemanticTag>());
        }

        /// <summary>Resets every surface-tag category to its default Any state.</summary>
        public void ResetTagSelections()
        {
            surfaceTags.Clear();
            noneTagCategories.Clear();
        }

        /// <summary>Replaces the tags accepted by this surface. An empty collection accepts every asset.</summary>
        public void SetAllowedAssetTags(IEnumerable<SemanticTag> tags) =>
            allowedAssetTags = NormalizeAssetTags(tags);

        /// <summary>Replaces tags rejected by this surface. Forbidden matches take precedence.</summary>
        public void SetForbiddenAssetTags(IEnumerable<SemanticTag> tags) =>
            forbiddenAssetTags = NormalizeAssetTags(tags);

        /// <summary>Configures the optional placement capacity. A limited capacity of zero rejects all placements.</summary>
        public void SetCapacity(bool limited, int capacity)
        {
            limitCapacity = limited;
            maxCapacity = Mathf.Max(0, capacity);
        }

        /// <summary>Replaces the asset-specific capacity rules with normalized, non-duplicate entries.</summary>
        public void SetAssetCapacityRules(IEnumerable<PlacementSurfaceCapacityRule> rules)
        {
            assetCapacityRules = rules?.Where(rule => rule != null).ToList() ??
                                 new List<PlacementSurfaceCapacityRule>();
            NormalizeCapacityRules();
        }

        private void OnValidate()
        {
            maxCapacity = Mathf.Max(0, maxCapacity);
            NormalizeCapacityRules();
            surfaceTags.RemoveAll(tag => !tag || !tag.Category || !tag.Category.SupportsSurfaces);
            noneTagCategories.RemoveAll(category => !category || !category.SupportsSurfaces);
            allowedAssetTags = NormalizeAssetTags(allowedAssetTags);
            forbiddenAssetTags = NormalizeAssetTags(forbiddenAssetTags);

            for (int i = surfaceTags.Count - 1; i >= 0; i--)
            {
                if (surfaceTags.IndexOf(surfaceTags[i]) != i)
                    surfaceTags.RemoveAt(i);
            }

            for (int i = noneTagCategories.Count - 1; i >= 0; i--)
            {
                TagCategory category = noneTagCategories[i];

                if (noneTagCategories.IndexOf(category) != i ||
                    surfaceTags.Any(tag => tag && tag.Category == category))
                {
                    noneTagCategories.RemoveAt(i);
                }
            }
        }

        private static List<SemanticTag> NormalizeAssetTags(IEnumerable<SemanticTag> tags) =>
            tags?.Where(IsAssetTag).Distinct().ToList() ?? new List<SemanticTag>();

        private static bool IsAssetTag(SemanticTag tag) =>
            tag && tag.SupportsAssets;

        private void NormalizeCapacityRules()
        {
            assetCapacityRules ??= new List<PlacementSurfaceCapacityRule>();
            assetCapacityRules.RemoveAll(rule => rule == null);

            for (int i = 0; i < assetCapacityRules.Count; i++)
            {
                PlacementSurfaceCapacityRule rule = assetCapacityRules[i];
                rule.Normalize();

                if (!rule.IsConfigured)
                    continue;

                for (int duplicateIndex = assetCapacityRules.Count - 1; duplicateIndex > i; duplicateIndex--)
                {
                    if (rule.HasSameTarget(assetCapacityRules[duplicateIndex]))
                        assetCapacityRules.RemoveAt(duplicateIndex);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;
            Vector3 direction = SupportForward.normalized;
            float length = 1.25f;
            Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.95f);
            Gizmos.DrawLine(origin, origin + direction * length);
            Gizmos.DrawSphere(origin + direction * length, 0.06f);
        }
    }
}
