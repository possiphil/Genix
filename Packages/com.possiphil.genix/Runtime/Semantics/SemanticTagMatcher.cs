using System.Collections.Generic;
using System.Linq;
using Genix.Assets;

namespace Genix.Semantics
{
    /// <summary>Evaluates semantic tag rules.</summary>
    public static class SemanticTagMatcher
    {
        /// <summary>Determines whether an asset satisfies the required and excluded semantic tags.</summary>
        public static bool MatchesAssetRequirements(
            AssetDefinition asset,
            IEnumerable<SemanticTag> requiredTags,
            IEnumerable<TagCategory> anyRequirementCategories)
        {
            if (!asset)
                return false;

            HashSet<TagCategory> anyCategories = CreateCategorySet(anyRequirementCategories);

            if (requiredTags == null)
                return true;

            foreach (IGrouping<TagCategory, SemanticTag> group in requiredTags
                         .Where(tag => tag && tag.SupportsAssets)
                         .GroupBy(tag => tag.Category))
            {
                if (anyCategories.Contains(group.Key))
                    continue;

                if (asset.HasAnyTagCategory(group.Key))
                    continue;

                if (!group.Any(asset.HasTag))
                    return false;
            }

            return true;
        }

        /// <summary>Determines whether an asset satisfies the configured category and tag filters.</summary>
        public static bool MatchesFilterTags(
            AssetDefinition asset,
            TagCategory category,
            IEnumerable<SemanticTag> filterTags)
        {
            if (!asset || !category || !category.SupportsAssets)
                return false;

            if (filterTags == null)
                return true;

            List<SemanticTag> validTags = filterTags
                .Where(tag => tag && tag.Category == category)
                .Distinct()
                .ToList();

            if (validTags.Count == 0)
                return true;

            return asset.HasAnyTagCategory(category) || validTags.Any(asset.HasTag);
        }

        private static HashSet<TagCategory> CreateCategorySet(IEnumerable<TagCategory> categories)
        {
            HashSet<TagCategory> result = new();

            if (categories == null)
                return result;

            foreach (TagCategory category in categories)
            {
                if (category && category.SupportsAssets)
                    result.Add(category);
            }

            return result;
        }
    }
}
