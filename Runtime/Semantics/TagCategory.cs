using UnityEngine;

namespace Genix.Semantics
{
    /// <summary>Controls where tags from a semantic category may be assigned.</summary>
    public enum TagCategoryUsage
    {
        /// <summary>Tags describe assets and may be used by asset, pool, and spatial-context filters.</summary>
        [InspectorName("Asset")] Asset,
        /// <summary>Tags describe placement supports and may be used by surface compatibility rules.</summary>
        [InspectorName("Surface")] Surface,
        /// <summary>Tags may be used for both assets and placement supports.</summary>
        [InspectorName("Asset and Surface")] AssetAndSurface
    }

    /// <summary>Defines a semantic-tag category, its assignment scope, and whether it allows multiple values.</summary>
    public sealed class TagCategory : ScriptableObject
    {
        [SerializeField] private TagCategoryUsage usage = TagCategoryUsage.Asset;
        [SerializeField] private bool allowMultipleTags = true;

        /// <summary>Gets display name.</summary>
        public string DisplayName => name;
        /// <summary>Gets where tags from this category may be assigned.</summary>
        public TagCategoryUsage Usage => usage;
        /// <summary>Indicates whether tags may describe assets and spatial asset requirements.</summary>
        public bool SupportsAssets => usage is TagCategoryUsage.Asset or TagCategoryUsage.AssetAndSurface;
        /// <summary>Indicates whether tags may describe placement support surfaces.</summary>
        public bool SupportsSurfaces => usage is TagCategoryUsage.Surface or TagCategoryUsage.AssetAndSurface;
        /// <summary>Indicates whether allow multiple tags.</summary>
        public bool AllowMultipleTags => allowMultipleTags;

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(
            bool allowMultipleTags = true,
            TagCategoryUsage categoryUsage = TagCategoryUsage.Asset)
        {
            this.allowMultipleTags = allowMultipleTags;
            usage = categoryUsage;
        }
    }
}
