using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using UnityEngine;

namespace Genix.Semantics
{
    /// <summary>Constrains semantic matching to selected tags within one category.</summary>
    [Serializable]
    public sealed class TagCategoryFilter
    {
        [SerializeField] private TagCategory category;
        [SerializeField] private List<SemanticTag> tags = new();

        /// <summary>Gets category.</summary>
        public TagCategory Category => category;
        /// <summary>Gets tags.</summary>
        public IReadOnlyList<SemanticTag> Tags => tags;

        /// <summary>Indicates whether active.</summary>
        public bool IsActive => category && category.SupportsAssets && tags.Any(tag => tag);

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(
            TagCategory category,
            IEnumerable<SemanticTag> tags)
        {
            this.category = category;
            SetTags(tags);
        }

        /// <summary>Sets tags.</summary>
        public void SetTags(IEnumerable<SemanticTag> tags)
        {
            this.tags = tags?
                .Where(tag => category && category.SupportsAssets && tag && tag.Category == category)
                .Distinct()
                .ToList() ?? new List<SemanticTag>();
        }

        /// <summary>Determines whether the value satisfies this filter.</summary>
        public bool Matches(AssetDefinition asset)
        {
            if (!asset)
                return false;

            if (!IsActive)
                return true;

            return SemanticTagMatcher.MatchesFilterTags(asset, category, tags);
        }

        /// <summary>Removes tag.</summary>
        public void RemoveTag(SemanticTag tag)
        {
            tags.RemoveAll(existingTag => !existingTag || existingTag == tag);
        }

        /// <summary>Removes missing tags.</summary>
        public void RemoveMissingTags()
        {
            if (!category)
            {
                tags.Clear();
                return;
            }

            tags.RemoveAll(tag => !category.SupportsAssets || !tag || tag.Category != category);
        }
    }
}
