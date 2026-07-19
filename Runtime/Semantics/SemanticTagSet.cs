using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using UnityEngine;

namespace Genix.Semantics
{
    public sealed class SemanticTagSet : MonoBehaviour
    {
        [SerializeField] private List<SemanticTag> semanticTags = new();
        [SerializeField] private List<TagCategory> anyTagCategories = new();

        public IReadOnlyList<SemanticTag> SemanticTags => semanticTags;
        public IReadOnlyList<TagCategory> AnyTagCategories => anyTagCategories;

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

        public void Clear()
        {
            semanticTags.Clear();
            anyTagCategories.Clear();
        }

        public bool MatchesAsset(AssetDefinition asset)
        {
            return SemanticTagMatcher.MatchesAssetRequirements(
                asset,
                semanticTags,
                anyTagCategories);
        }
    }
}
