using System;
using System.Collections.Generic;
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
    public sealed class TargetDistributionPolicyTests
    {
        private readonly List<UnityEngine.Object> _objects = new();
        private GameObject _areaRoot;
        private GameObject _generatedRoot;
        private AssetPool _pool;

        [SetUp]
        public void SetUp()
        {
            _areaRoot = CreateGameObject("Area");
            _generatedRoot = CreateGameObject("Generated");
            _pool = ScriptableObject.CreateInstance<AssetPool>();
            _pool.Initialize("Pool", AssetPoolMode.Static);
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
        public void BalancedTargetsAllocateEveryRequestedObject()
        {
            GenerationContext context = CreateContext(10, TargetDistributionMode.Balanced, TargetDistributionWeights.Default);

            Dictionary<PlacementType, int> targets = TargetDistributionPolicy.CreateTargets(
                context,
                new[] { PlacementType.Floor, PlacementType.Wall, PlacementType.InsideSpace });

            Assert.That(targets[PlacementType.Floor] + targets[PlacementType.Wall] + targets[PlacementType.InsideSpace], Is.EqualTo(10));
            Assert.That(Mathf.Abs(targets[PlacementType.Floor] - targets[PlacementType.Wall]), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void WeightedTargetsFollowExactRatiosWhenDivisible()
        {
            GenerationContext context = CreateContext(
                8,
                TargetDistributionMode.Weighted,
                new TargetDistributionWeights(1, 3, 0, 0));

            Dictionary<PlacementType, int> targets = TargetDistributionPolicy.CreateTargets(
                context,
                new[] { PlacementType.Floor, PlacementType.Wall });

            Assert.That(targets[PlacementType.Floor], Is.EqualTo(2));
            Assert.That(targets[PlacementType.Wall], Is.EqualTo(6));
        }

        [Test]
        public void ZeroWeightTargetIsExcludedFromUsableTargets()
        {
            AssetDefinition floor = CreateAsset("Floor", PlacementType.Floor);
            AssetDefinition wall = CreateAsset("Wall", PlacementType.Wall);
            GenerationContext context = CreateContext(
                8,
                TargetDistributionMode.Weighted,
                new TargetDistributionWeights(1, 0, 0, 0));

            PlacementTarget result = TargetDistributionPolicy.GetUsableTargets(context, new[] { floor, wall });

            Assert.That(result, Is.EqualTo(PlacementTarget.Floor));
        }

        [Test]
        public void UsableTargetsRequireBothAreaSupportAndAsset()
        {
            AssetDefinition floor = CreateAsset("Floor", PlacementType.Floor);
            GenerationContext context = CreateContext(5, TargetDistributionMode.Balanced, TargetDistributionWeights.Default);

            PlacementTarget result = TargetDistributionPolicy.GetUsableTargets(context, new[] { floor });

            Assert.That(result, Is.EqualTo(PlacementTarget.Floor));
        }

        [Test]
        public void TrySelectTargetIgnoresSatisfiedAndExhaustedTargets()
        {
            GenerationContext context = CreateContext(4, TargetDistributionMode.Balanced, TargetDistributionWeights.Default);
            Dictionary<PlacementType, int> targets = new()
            {
                [PlacementType.Floor] = 2,
                [PlacementType.Wall] = 2,
                [PlacementType.InsideSpace] = 2
            };
            Dictionary<PlacementType, int> placed = new() { [PlacementType.Floor] = 2 };
            Dictionary<PlacementType, CandidatePool> pools = new()
            {
                [PlacementType.Floor] = Pool(PlacementType.Floor),
                [PlacementType.Wall] = Pool(PlacementType.Wall),
                [PlacementType.InsideSpace] = Pool(PlacementType.InsideSpace)
            };
            HashSet<PlacementType> exhausted = new() { PlacementType.Wall };

            bool selected = TargetDistributionPolicy.TrySelectTarget(
                context,
                targets,
                placed,
                pools,
                exhausted,
                out PlacementType placementType);

            Assert.That(selected, Is.True);
            Assert.That(placementType, Is.EqualTo(PlacementType.InsideSpace));
        }

        [Test]
        public void TrySelectTargetReturnsFalseWhenNoBudgetRemains()
        {
            GenerationContext context = CreateContext(1, TargetDistributionMode.Balanced, TargetDistributionWeights.Default);
            Dictionary<PlacementType, int> targets = new() { [PlacementType.Floor] = 1 };
            Dictionary<PlacementType, int> placed = new() { [PlacementType.Floor] = 1 };

            bool selected = TargetDistributionPolicy.TrySelectTarget(
                context,
                targets,
                placed,
                new Dictionary<PlacementType, CandidatePool> { [PlacementType.Floor] = Pool(PlacementType.Floor) },
                new HashSet<PlacementType>(),
                out _);

            Assert.That(selected, Is.False);
        }

        [Test]
        public void OverflowTypesExcludeEmptyAndExhaustedPools()
        {
            GenerationContext context = CreateContext(3, TargetDistributionMode.Balanced, TargetDistributionWeights.Default);
            Dictionary<PlacementType, CandidatePool> pools = new()
            {
                [PlacementType.Floor] = Pool(PlacementType.Floor),
                [PlacementType.Wall] = new CandidatePool(new List<CandidateSeed>()),
                [PlacementType.InsideSpace] = Pool(PlacementType.InsideSpace)
            };

            List<PlacementType> result = TargetDistributionPolicy.GetOverflowTypes(
                new[] { PlacementType.Floor, PlacementType.Wall, PlacementType.InsideSpace },
                pools,
                new HashSet<PlacementType> { PlacementType.InsideSpace },
                context);

            Assert.That(result, Is.EqualTo(new[] { PlacementType.Floor }));
        }

        private GenerationContext CreateContext(
            int count,
            TargetDistributionMode mode,
            TargetDistributionWeights weights)
        {
            PlacementArea area = new(
                new SpatialSourceInfo("Test", "Area", "distribution-tests"),
                new Bounds(Vector3.zero, Vector3.one * 10f),
                new[] { SurfaceRegion.CreateFloor("Floor", -5f, 5f, -5f, 5f, -5f) },
                new[] { SurfaceRegion.CreateWall("Wall", new Vector3(-5f, -5f, -5f), new Vector3(5f, -5f, -5f), 10f, Vector3.forward) });
            StyleSettings style = new(
                string.Empty,
                SamplingAlgorithm.Random,
                new PlacementSettings(),
                new CandidateSettings(2, 1, false),
                new GridSettings(1f, 0f),
                new ClusterSettings(2, 1f),
                new PoissonSettings(1f, 30));
            GenerationRequest request = new(
                new StubAreaSource(_areaRoot.transform),
                _pool,
                count,
                PlacementTarget.Floor | PlacementTarget.Wall | PlacementTarget.InsideSpace,
                mode,
                weights,
                style,
                default,
                useFixedSeed: true,
                randomSeed: 9);
            return new GenerationContext(request, _generatedRoot.transform, area);
        }

        private AssetDefinition CreateAsset(string name, PlacementType placementType)
        {
            GameObject prefab = CreateGameObject(name + " Prefab");
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.Initialize(prefab, Vector3.one);
            SerializedObject serialized = new(asset);
            serialized.FindProperty("placementType").enumValueIndex = (int)placementType;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _objects.Add(asset);
            return asset;
        }

        private static CandidatePool Pool(PlacementType placementType) => new(new List<CandidateSeed>
        {
            new(Vector3.zero, Quaternion.identity, placementType: placementType)
        });

        private GameObject CreateGameObject(string name)
        {
            GameObject value = new(name);
            _objects.Add(value);
            return value;
        }

        private sealed class StubAreaSource : IAreaSource
        {
            public SpatialSourceInfo SourceInfo { get; } = new("Test", "Area", "distribution-tests");
            public Transform ParentTransform { get; }
            public IReadOnlyList<SemanticTag> SemanticTags => Array.Empty<SemanticTag>();
            public IReadOnlyList<TagCategory> AnyTagCategories => Array.Empty<TagCategory>();

            public StubAreaSource(Transform parentTransform) => ParentTransform = parentTransform;
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
