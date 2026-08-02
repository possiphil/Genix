using System.Collections.Generic;
using Genix.Assets;
using Genix.Semantics;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SemanticsArea)]
    public sealed class SemanticTagSetTests
    {
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _objects)
            {
                if (value)
                    Object.DestroyImmediate(value);
            }

            _objects.Clear();
        }

        [Test]
        public void SingleSelectCategoryKeepsOnlyFirstDistinctTag()
        {
            TagCategory category = CreateCategory("Biome", false);
            SemanticTag forest = CreateTag("Forest", category);
            SemanticTag desert = CreateTag("Desert", category);
            SemanticTagSet set = CreateSet();

            set.SetTagsForCategory(category, new[] { forest, forest, desert });

            Assert.That(set.SemanticTags, Is.EqualTo(new[] { forest }));
            Assert.That(set.AnyTagCategories, Is.Empty);
        }

        [Test]
        public void MultiSelectCategoryKeepsDistinctMatchingTags()
        {
            TagCategory category = CreateCategory("Biome", true);
            TagCategory other = CreateCategory("Mood", true);
            SemanticTag forest = CreateTag("Forest", category);
            SemanticTag desert = CreateTag("Desert", category);
            SemanticTag ignored = CreateTag("Calm", other);
            SemanticTagSet set = CreateSet();

            set.SetTagsForCategory(category, new[] { forest, desert, forest, ignored });

            Assert.That(set.SemanticTags, Is.EqualTo(new[] { forest, desert }));
        }

        [Test]
        public void SelectAnyReplacesSpecificTagsForCategory()
        {
            TagCategory category = CreateCategory("Biome", true);
            SemanticTag forest = CreateTag("Forest", category);
            SemanticTagSet set = CreateSet();
            set.SetTagsForCategory(category, new[] { forest });

            set.SetTagsForCategory(category, new[] { forest }, selectAny: true);

            Assert.That(set.SemanticTags, Is.Empty);
            Assert.That(set.AnyTagCategories, Is.EqualTo(new[] { category }));
        }

        [Test]
        public void ClearingSetRemovesTagsAndCategoryWildcards()
        {
            TagCategory category = CreateCategory("Biome", true);
            SemanticTagSet set = CreateSet();
            set.SetTagsForCategory(category, System.Array.Empty<SemanticTag>(), selectAny: true);

            set.Clear();

            Assert.That(set.SemanticTags, Is.Empty);
            Assert.That(set.AnyTagCategories, Is.Empty);
        }

        [Test]
        public void TagCategoryFilterDropsTagsFromOtherCategoriesAndDuplicates()
        {
            TagCategory category = CreateCategory("Biome", true);
            TagCategory other = CreateCategory("Mood", true);
            SemanticTag forest = CreateTag("Forest", category);
            SemanticTag calm = CreateTag("Calm", other);
            TagCategoryFilter filter = new();

            filter.Initialize(category, new[] { forest, forest, calm });

            Assert.That(filter.IsActive, Is.True);
            Assert.That(filter.Tags, Is.EqualTo(new[] { forest }));
        }

        [Test]
        public void InactiveFilterAcceptsAnyExistingAssetButRejectsNull()
        {
            TagCategoryFilter filter = new();
            AssetDefinition asset = CreateAsset();

            Assert.That(filter.Matches(asset), Is.True);
            Assert.That(filter.Matches(null), Is.False);
        }

        [Test]
        public void FilterMatchesAssetWithSelectedTag()
        {
            TagCategory category = CreateCategory("Biome", true);
            SemanticTag forest = CreateTag("Forest", category);
            TagCategoryFilter filter = new();
            filter.Initialize(category, new[] { forest });
            AssetDefinition asset = CreateAsset();
            asset.AddTag(forest);

            Assert.That(filter.Matches(asset), Is.True);
            filter.RemoveTag(forest);
            Assert.That(filter.IsActive, Is.False);
        }

        private SemanticTagSet CreateSet()
        {
            GameObject gameObject = new("Tag Set");
            _objects.Add(gameObject);
            return gameObject.AddComponent<SemanticTagSet>();
        }

        private TagCategory CreateCategory(string name, bool allowMultiple)
        {
            TagCategory category = ScriptableObject.CreateInstance<TagCategory>();
            category.name = name;
            category.Initialize(allowMultiple);
            _objects.Add(category);
            return category;
        }

        private SemanticTag CreateTag(string name, TagCategory category)
        {
            SemanticTag tag = ScriptableObject.CreateInstance<SemanticTag>();
            tag.name = name;
            tag.Initialize(category);
            _objects.Add(tag);
            return tag;
        }

        private AssetDefinition CreateAsset()
        {
            GameObject prefab = new("Prefab");
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.Initialize(prefab, Vector3.one);
            _objects.Add(prefab);
            _objects.Add(asset);
            return asset;
        }
    }
}
