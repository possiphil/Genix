using System.Collections.Generic;
using Genix.Assets;
using Genix.Semantics;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    public sealed class SemanticTagMatcherTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in _createdObjects)
                Object.DestroyImmediate(createdObject);

            _createdObjects.Clear();
        }

        [Test]
        public void AssetAnyMatchesEveryRequiredTagInCategory()
        {
            TagCategory category = CreateCategory();
            SemanticTag required = CreateTag(category, "Primary");
            AssetDefinition asset = CreateAsset();
            SetAny(asset, category);

            Assert.That(
                SemanticTagMatcher.MatchesAssetRequirements(asset, new[] { required }, null),
                Is.True);
        }

        [Test]
        public void LocationAnyAcceptsAssetWithoutTags()
        {
            TagCategory category = CreateCategory();
            AssetDefinition asset = CreateAsset();

            Assert.That(
                SemanticTagMatcher.MatchesAssetRequirements(asset, null, new[] { category }),
                Is.True);
        }

        [Test]
        public void SpecificLocationTagRejectsUntaggedAsset()
        {
            TagCategory category = CreateCategory();
            SemanticTag required = CreateTag(category, "Primary");
            AssetDefinition asset = CreateAsset();

            Assert.That(
                SemanticTagMatcher.MatchesAssetRequirements(asset, new[] { required }, null),
                Is.False);
        }

        private TagCategory CreateCategory()
        {
            TagCategory category = ScriptableObject.CreateInstance<TagCategory>();
            category.Initialize(true);
            _createdObjects.Add(category);
            return category;
        }

        private SemanticTag CreateTag(TagCategory category, string name)
        {
            SemanticTag tag = ScriptableObject.CreateInstance<SemanticTag>();
            tag.name = name;
            tag.Initialize(category);
            _createdObjects.Add(tag);
            return tag;
        }

        private AssetDefinition CreateAsset()
        {
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            _createdObjects.Add(asset);
            return asset;
        }

        private static void SetAny(AssetDefinition asset, TagCategory category)
        {
            UnityEditor.SerializedObject serializedAsset = new(asset);
            UnityEditor.SerializedProperty categories = serializedAsset.FindProperty("anyTagCategories");
            categories.arraySize = 1;
            categories.GetArrayElementAtIndex(0).objectReferenceValue = category;
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
