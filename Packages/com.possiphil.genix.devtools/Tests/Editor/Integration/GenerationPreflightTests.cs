using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Semantics;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests.Integration
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Integration)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class GenerationPreflightTests
    {
        private GameObject _areaObject;
        private GameObject _prefab;
        private AssetDefinition _asset;
        private AssetPool _pool;
        private StubAreaSource _areaSource;
        private TagCategory _surfaceCategory;
        private SemanticTag _surfaceTag;

        [SetUp]
        public void SetUp()
        {
            _areaObject = new GameObject("Area");
            _prefab = new GameObject("Prefab");
            _asset = ScriptableObject.CreateInstance<AssetDefinition>();
            _asset.Initialize(_prefab, Vector3.one);
            _pool = ScriptableObject.CreateInstance<AssetPool>();
            _pool.Initialize("Pool", AssetPoolMode.Static);
            _pool.AddStaticAsset(_asset);
            _areaSource = new StubAreaSource(_areaObject.transform);
            _surfaceCategory = ScriptableObject.CreateInstance<TagCategory>();
            _surfaceCategory.Initialize(true, TagCategoryUsage.Surface);
            _surfaceTag = ScriptableObject.CreateInstance<SemanticTag>();
            _surfaceTag.name = "Desktop";
            _surfaceTag.Initialize(_surfaceCategory);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_pool);
            UnityEngine.Object.DestroyImmediate(_asset);
            UnityEngine.Object.DestroyImmediate(_prefab);
            UnityEngine.Object.DestroyImmediate(_areaObject);
            UnityEngine.Object.DestroyImmediate(_surfaceTag);
            UnityEngine.Object.DestroyImmediate(_surfaceCategory);
        }

        [Test]
        public void CompleteMinimalRequestPassesPreflight()
        {
            GenerationRequest request = CreateRequest();

            Assert.That(GenerationPreflight.IsValid(request, out string error), Is.True);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void MissingRequestReturnsActionableFailure()
        {
            Assert.That(GenerationPreflight.IsValid(null, out string error), Is.False);
            Assert.That(error, Does.Contain("request"));
        }

        [Test]
        public void NoPlacementTargetFailsBeforeAreaConstruction()
        {
            GenerationRequest request = CreateRequest(placementTargets: PlacementTarget.None);

            Assert.That(GenerationPreflight.IsValid(request, out string error), Is.False);
            Assert.That(error, Does.Contain("placement targets"));
            Assert.That(_areaSource.BuildCalls, Is.Zero);
        }

        [Test]
        public void WeightedRequestRejectsZeroWeightsOnSelectedTargets()
        {
            GenerationRequest request = CreateRequest(
                targetDistributionMode: TargetDistributionMode.Weighted,
                weights: new TargetDistributionWeights(0, 0, 1, 1),
                placementTargets: PlacementTarget.Floor | PlacementTarget.Wall);

            Assert.That(GenerationPreflight.IsValid(request, out string error), Is.False);
            Assert.That(error, Does.Contain("weights"));
        }

        [Test]
        public void SupportDistributionRejectsExactCountsAboveObjectCount()
        {
            SupportDistributionSettings distribution = new(
                true,
                1,
                new[] { new SupportDistributionRule(_surfaceTag, SupportDistributionRuleMode.ExactCount, 11) });

            Assert.That(GenerationPreflight.IsValid(CreateRequest(supportDistribution: distribution), out string error), Is.False);
            Assert.That(error, Does.Contain("11 exact placements"));
        }

        [Test]
        public void RemainingObjectsRequireWeightAfterExactSupportRules()
        {
            SupportDistributionSettings distribution = new(
                true,
                0,
                new[] { new SupportDistributionRule(_surfaceTag, SupportDistributionRuleMode.ExactCount, 4) });

            Assert.That(GenerationPreflight.IsValid(CreateRequest(supportDistribution: distribution), out string error), Is.False);
            Assert.That(error, Does.Contain("every Weight rule"));
        }

        [Test]
        public void MissingAreaSourceReturnsActionableFailure()
        {
            GenerationRequest request = new(
                null,
                _pool,
                10,
                PlacementTarget.Floor,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                default,
                default);

            Assert.That(GenerationPreflight.IsValid(request, out string error), Is.False);
            Assert.That(error, Does.Contain("target area"));
        }

        [Test]
        public void MissingAssetPoolReturnsActionableFailure()
        {
            GenerationRequest request = new(
                _areaSource,
                null,
                10,
                PlacementTarget.Floor,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                default,
                default);

            Assert.That(GenerationPreflight.IsValid(request, out string error), Is.False);
            Assert.That(error, Does.Contain("asset pool"));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveObjectCountReturnsActionableFailure(int objectCount)
        {
            GenerationRequest request = new(
                _areaSource,
                _pool,
                objectCount,
                PlacementTarget.Floor,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                default,
                default);

            Assert.That(GenerationPreflight.IsValid(request, out string error), Is.False);
            Assert.That(error, Does.Contain("Object Count"));
        }

        [Test]
        public void SelectedObjectRelativePlacementRequiresASelection()
        {
            RelativePlacementSettings relativePlacement = new(
                RelativePlacementSource.SelectedObjects,
                2f,
                ~0,
                Array.Empty<Transform>());
            GenerationRequest request = new(
                _areaSource,
                _pool,
                10,
                PlacementTarget.Floor,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                default,
                default,
                relativePlacement);

            Assert.That(GenerationPreflight.IsValid(request, out string error), Is.False);
            Assert.That(error, Does.Contain("no scene objects are selected"));
        }

        private GenerationRequest CreateRequest(
            PlacementTarget placementTargets = PlacementTarget.Floor,
            TargetDistributionMode targetDistributionMode = TargetDistributionMode.Random,
            TargetDistributionWeights? weights = null,
            SupportDistributionSettings supportDistribution = null)
        {
            return new GenerationRequest(
                _areaSource,
                _pool,
                10,
                placementTargets,
                targetDistributionMode,
                weights ?? TargetDistributionWeights.Default,
                default,
                default,
                supportDistribution: supportDistribution);
        }

        private sealed class StubAreaSource : IAreaSource
        {
            public SpatialSourceInfo SourceInfo { get; } = new("Test", "Test Area", "test-area");
            public Transform ParentTransform { get; }
            public IReadOnlyList<SemanticTag> SemanticTags => Array.Empty<SemanticTag>();
            public IReadOnlyList<TagCategory> AnyTagCategories => Array.Empty<TagCategory>();
            public int BuildCalls { get; private set; }

            public StubAreaSource(Transform parentTransform)
            {
                ParentTransform = parentTransform;
            }

            public bool IsSourceCollider(Collider collider) => false;

            public bool TryBuildArea(AreaBuildSettings settings, out PlacementArea area, out string error)
            {
                BuildCalls++;
                area = null;
                error = "Not used by preflight tests.";
                return false;
            }
        }
    }
}
