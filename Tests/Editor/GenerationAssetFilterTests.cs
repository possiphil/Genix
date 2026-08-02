using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Editor.Generation;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Semantics;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class GenerationAssetFilterTests
    {
        private readonly List<UnityEngine.Object> _objects = new();
        private AssetCatalog _catalog;
        private AssetPool _pool;
        private GameObject _areaRoot;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<AssetCatalog>();
            _pool = ScriptableObject.CreateInstance<AssetPool>();
            _pool.Initialize("Test Pool", AssetPoolMode.Static);
            _areaRoot = CreateGameObject("Area");
            _objects.Add(_catalog);
            _objects.Add(_pool);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object value in _objects)
            {
                if (value)
                    UnityEngine.Object.DestroyImmediate(value);
            }

            _objects.Clear();
        }

        [Test]
        public void EmptyStaticPoolReturnsActionableError()
        {
            bool resolved = GenerationAssetFilter.TryResolve(
                Request(new StubAreaSource(_areaRoot.transform), PlacementTarget.Floor),
                _catalog,
                out List<AssetDefinition> assets,
                out string error);

            Assert.That(resolved, Is.False);
            Assert.That(assets, Is.Empty);
            Assert.That(error, Does.Contain("is empty"));
        }

        [Test]
        public void MatchingTargetWithPrefabResolvesSuccessfully()
        {
            AssetDefinition floor = CreateAsset("Floor", PlacementType.Floor, true);

            bool resolved = GenerationAssetFilter.TryResolve(
                Request(new StubAreaSource(_areaRoot.transform), PlacementTarget.Floor),
                _catalog,
                out List<AssetDefinition> assets,
                out string error);

            Assert.That(resolved, Is.True);
            Assert.That(assets, Is.EqualTo(new[] { floor }));
            Assert.That(error, Is.Empty);
        }

        [Test]
        public void WrongPlacementTargetIsRejectedWithReason()
        {
            CreateAsset("Wall", PlacementType.Wall, true);

            bool resolved = GenerationAssetFilter.TryResolve(
                Request(new StubAreaSource(_areaRoot.transform), PlacementTarget.Floor),
                _catalog,
                out _,
                out string error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("different placement target"));
        }

        [Test]
        public void MissingPrefabIsRejectedWithReason()
        {
            CreateAsset("Missing", PlacementType.Floor, false);

            bool resolved = GenerationAssetFilter.TryResolve(
                Request(new StubAreaSource(_areaRoot.transform), PlacementTarget.Floor),
                _catalog,
                out _,
                out string error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("no prefab reference"));
        }

        [Test]
        public void SemanticMismatchNamesLocationTags()
        {
            TagCategory biome = CreateCategory("Biome");
            SemanticTag forest = CreateTag("Forest", biome);
            AssetDefinition floor = CreateAsset("Floor", PlacementType.Floor, true);
            StubAreaSource source = new(_areaRoot.transform, new[] { forest });

            bool resolved = GenerationAssetFilter.TryResolve(
                Request(source, PlacementTarget.Floor),
                _catalog,
                out _,
                out string error);

            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("semantic tags"));
            Assert.That(error, Does.Contain("Biome: Forest"));
            Assert.That(floor.SemanticTags, Is.Empty);
        }

        [Test]
        public void UnavailableTargetWarningsSkipZeroWeightedTargets()
        {
            AssetDefinition floor = CreateAsset("Floor", PlacementType.Floor, true);
            GenerationRequest request = Request(
                new StubAreaSource(_areaRoot.transform),
                PlacementTarget.Floor | PlacementTarget.Wall,
                TargetDistributionMode.Weighted,
                new TargetDistributionWeights(1, 0, 0, 0));

            List<string> warnings = GenerationAssetFilter
                .GetUnavailableTargetWarnings(request, new[] { floor })
                .ToList();

            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void UnavailableTargetWarningNamesMissingSelectedTarget()
        {
            AssetDefinition floor = CreateAsset("Floor", PlacementType.Floor, true);
            GenerationRequest request = Request(
                new StubAreaSource(_areaRoot.transform),
                PlacementTarget.Floor | PlacementTarget.Wall);

            List<string> warnings = GenerationAssetFilter
                .GetUnavailableTargetWarnings(request, new[] { floor })
                .ToList();

            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("Wall is selected"));
        }

        private GenerationRequest Request(
            IAreaSource source,
            PlacementTarget targets,
            TargetDistributionMode mode = TargetDistributionMode.Random,
            TargetDistributionWeights? weights = null) => new(
                source,
                _pool,
                10,
                targets,
                mode,
                weights ?? TargetDistributionWeights.Default,
                new StyleSettings(
                    string.Empty,
                    SamplingAlgorithm.Random,
                    new PlacementSettings(),
                    new CandidateSettings(2, 1, false),
                    new GridSettings(1f, 0f),
                    new ClusterSettings(2, 1f),
                    new PoissonSettings(1f, 30)),
                default,
                useFixedSeed: true,
                randomSeed: 1);

        private AssetDefinition CreateAsset(string name, PlacementType placementType, bool withPrefab)
        {
            GameObject prefab = withPrefab ? CreateGameObject(name + " Prefab") : null;
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.name = name;
            asset.Initialize(prefab, Vector3.one);
            SerializedObject serialized = new(asset);
            serialized.FindProperty("placementType").enumValueIndex = (int)placementType;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _pool.AddStaticAsset(asset);
            _catalog.AddAsset(asset);
            _objects.Add(asset);
            return asset;
        }

        private TagCategory CreateCategory(string name)
        {
            TagCategory category = ScriptableObject.CreateInstance<TagCategory>();
            category.name = name;
            category.Initialize();
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

        private GameObject CreateGameObject(string name)
        {
            GameObject value = new(name);
            _objects.Add(value);
            return value;
        }

        private sealed class StubAreaSource : IAreaSource
        {
            public SpatialSourceInfo SourceInfo { get; } = new("Test", "Area", "asset-filter-tests");
            public Transform ParentTransform { get; }
            public IReadOnlyList<SemanticTag> SemanticTags { get; }
            public IReadOnlyList<TagCategory> AnyTagCategories => Array.Empty<TagCategory>();

            public StubAreaSource(Transform parentTransform, IReadOnlyList<SemanticTag> tags = null)
            {
                ParentTransform = parentTransform;
                SemanticTags = tags ?? Array.Empty<SemanticTag>();
            }

            public bool IsSourceCollider(Collider collider) => false;

            public bool TryBuildArea(AreaBuildSettings settings, out PlacementArea area, out string error)
            {
                area = null;
                error = "Not used.";
                return false;
            }
        }
    }
}
