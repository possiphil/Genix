using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using UnityEngine;

namespace Genix.Semantics
{
    [Serializable]
    public sealed class TagCategoryFilter
    {
        [SerializeField] private TagCategory category;
        [SerializeField] private List<SemanticTag> tags = new();

        public TagCategory Category => category;
        public IReadOnlyList<SemanticTag> Tags => tags;

        public bool IsActive => category && tags.Any(tag => tag);

        public void Initialize(
            TagCategory category,
            IEnumerable<SemanticTag> tags)
        {
            this.category = category;
            SetTags(tags);
        }

        public void SetTags(IEnumerable<SemanticTag> tags)
        {
            this.tags = tags?
                .Where(tag => tag && tag.Category == category)
                .Distinct()
                .ToList() ?? new List<SemanticTag>();
        }

        public bool Matches(AssetDefinition asset)
        {
            if (!asset)
                return false;

            if (!IsActive)
                return true;

            return SemanticTagMatcher.MatchesFilterTags(asset, category, tags);
        }

        public void RemoveTag(SemanticTag tag)
        {
            tags.RemoveAll(existingTag => !existingTag || existingTag == tag);
        }

        public void RemoveMissingTags()
        {
            if (!category)
            {
                tags.Clear();
                return;
            }

            tags.RemoveAll(tag => !tag || tag.Category != category);
        }
    }
}
