using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using UnityEngine;

namespace Genix.Semantics
{
    /// <summary>Attaches semantic tags and category-level wildcard matches to a scene object.</summary>
    public sealed class SemanticTagSet : MonoBehaviour
    {
        [SerializeField] private List<SemanticTag> semanticTags = new();
        [SerializeField] private List<TagCategory> anyTagCategories = new();

        /// <summary>Gets semantic tags.</summary>
        public IReadOnlyList<SemanticTag> SemanticTags => semanticTags;
        /// <summary>Gets any tag categories.</summary>
        public IReadOnlyList<TagCategory> AnyTagCategories => anyTagCategories;

        /// <summary>Sets tags for category.</summary>
        public void SetTagsForCategory(
            TagCategory category,
            IEnumerable<SemanticTag> tags,
            bool forceAllowMultipleTags = false,
            bool selectAny = false)
        {
            if (!category)
                return;

            semanticTags.RemoveAll(tag => !tag || tag.Category == category);
            anyTagCategories.RemoveAll(existingCategory => !existingCategory || existingCategory == category);

            if (selectAny)
            {
                anyTagCategories.Add(category);
                return;
            }

            List<SemanticTag> validTags = tags
                .Where(tag => tag && tag.Category == category)
                .Distinct()
                .ToList();

            if (!forceAllowMultipleTags && !category.AllowMultipleTags)
                validTags = validTags.Take(1).ToList();

            semanticTags.AddRange(validTags);
        }

        /// <summary>Clears the stored state.</summary>
        public void Clear()
        {
            semanticTags.Clear();
            anyTagCategories.Clear();
        }

        /// <summary>Determines whether an asset satisfies this semantic tag set.</summary>
        public bool MatchesAsset(AssetDefinition asset)
        {
            return SemanticTagMatcher.MatchesAssetRequirements(
                asset,
                semanticTags,
                anyTagCategories);
        }
    }
}
