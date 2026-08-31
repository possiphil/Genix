using System;
using Genix.Assets;
using Genix.Core;
using Genix.Layouts;
using Genix.Semantics;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.LayoutsArea)]
    public sealed class SavedLayoutMetadataTests
    {
        private SavedLayout _layout;
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_layout)
                UnityEngine.Object.DestroyImmediate(_layout);

            if (_root)
                UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void InitializeStoresSceneAndTargetAreaMetadata()
        {
            _layout = ScriptableObject.CreateInstance<SavedLayout>();

            _layout.Initialize(
                "Test Layout",
                null,
                "Bridge Scene",
                "Assets/Scenes/Bridge.unity",
                "North Room",
                "area-42",
                "Test Source",
                PlacementTarget.Floor,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                null,
                "Natural",
                3,
                new Bounds(Vector3.zero, Vector3.one),
                "2026-07-31 16:00",
                Array.Empty<LayoutAssetSummary>());

            Assert.That(_layout.SceneName, Is.EqualTo("Bridge Scene"));
            Assert.That(_layout.ScenePath, Is.EqualTo("Assets/Scenes/Bridge.unity"));
            Assert.That(_layout.TargetAreaName, Is.EqualTo("North Room"));
            Assert.That(_layout.TargetAreaId, Is.EqualTo("area-42"));
        }

        [Test]
        public void AssetSummaryClampsCountAndUsesFallbackName()
        {
            GameObject prefab = new("Rock");

            try
            {
                LayoutAssetSummary summary = new(" ", -4, prefab);

                Assert.That(summary.AssetName, Is.EqualTo("Generated Object"));
                Assert.That(summary.Count, Is.Zero);
                Assert.That(summary.SourcePrefab, Is.SameAs(prefab));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void SavedLayoutRootStoresProvenanceAndHidesMetadataComponent()
        {
            _root = new GameObject("Layout Root");
            SavedLayoutRoot metadata = _root.AddComponent<SavedLayoutRoot>();

            metadata.Initialize(
                "Forest Layout",
                "Forest Scene",
                "Assets/Scenes/Forest.unity",
                "Clearing",
                "area-17",
                "2026-08-02 20:00",
                -5);

            Assert.That(metadata.DisplayName, Is.EqualTo("Forest Layout"));
            Assert.That(metadata.SceneName, Is.EqualTo("Forest Scene"));
            Assert.That(metadata.ScenePath, Is.EqualTo("Assets/Scenes/Forest.unity"));
            Assert.That(metadata.TargetAreaName, Is.EqualTo("Clearing"));
            Assert.That(metadata.TargetAreaId, Is.EqualTo("area-17"));
            Assert.That(metadata.CreatedAt, Is.EqualTo("2026-08-02 20:00"));
            Assert.That(metadata.ObjectCount, Is.Zero);
            Assert.That(metadata.hideFlags, Is.EqualTo(HideFlags.HideInInspector));
        }

        [TestCase(PlacementType.Floor, PlacementTarget.Floor)]
        [TestCase(PlacementType.Wall, PlacementTarget.Wall)]
        [TestCase(PlacementType.Ceiling, PlacementTarget.Ceiling)]
        [TestCase(PlacementType.InsideSpace, PlacementTarget.InsideSpace)]
        [TestCase((PlacementType)999, PlacementTarget.None)]
        public void GeneratedObjectMetadataMapsPlacementTypeToTarget(
            PlacementType placementType,
            PlacementTarget expectedTarget)
        {
            _root = new GameObject("Generated Object");
            GeneratedObjectMetadata metadata = _root.AddComponent<GeneratedObjectMetadata>();

            metadata.Initialize(placementType);

            Assert.That(metadata.PlacementTarget, Is.EqualTo(expectedTarget));
            Assert.That(metadata.hideFlags, Is.EqualTo(HideFlags.HideInInspector));
        }

        [Test]
        public void GeneratedObjectMetadataRetainsConcreteSupportSurface()
        {
            _root = new GameObject("Generated Object With Support");
            GameObject supportObject = new("Support Surface");

            try
            {
                PlacementSurfaceDescriptor support = supportObject.AddComponent<PlacementSurfaceDescriptor>();
                GeneratedObjectMetadata metadata = _root.AddComponent<GeneratedObjectMetadata>();

                metadata.Initialize(PlacementType.Floor, support);

                Assert.That(metadata.SupportSurface, Is.SameAs(support));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(supportObject);
            }
        }

        [Test]
        public void ObjectMetadataKeepsSourceAssetForSupportCapacityRules()
        {
            _root = new GameObject("Generated Object With Asset");
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.name = "Monitor";

            try
            {
                GeneratedObjectMetadata metadata = _root.AddComponent<GeneratedObjectMetadata>();
                metadata.Initialize(PlacementType.Floor, sourceAsset: asset);

                Assert.That(metadata.AssetDefinition, Is.SameAs(asset));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void GeneratedObjectMetadataRetainsSelectedRelationAnchorIdentity()
        {
            _root = new GameObject("Generated Relative Object");
            GeneratedObjectMetadata metadata = _root.AddComponent<GeneratedObjectMetadata>();

            metadata.Initialize(
                PlacementType.Floor,
                selectedRelationAnchorKey: "scene:Office|Desk[2]");

            Assert.That(metadata.RelationAnchorKey, Is.EqualTo("scene:Office|Desk[2]"));
        }
    }
}
