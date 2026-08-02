using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Fluent;
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
using UnityEngine;

namespace Genix.Tests.Properties
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Property)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class TargetDistributionPropertyTests
    {
        private GameObject _areaRoot;
        private GameObject _generatedRoot;
        private AssetPool _pool;
        private PlacementArea _area;

        [SetUp]
        public void SetUp()
        {
            _areaRoot = new GameObject("Area");
            _generatedRoot = new GameObject("Generated");
            _pool = ScriptableObject.CreateInstance<AssetPool>();
            _pool.Initialize("Pool", AssetPoolMode.Static);
            _area = new PlacementArea(
                new SpatialSourceInfo("Test", "Area", "distribution-property"),
                new Bounds(Vector3.zero, Vector3.one * 10f),
                Array.Empty<SurfaceRegion>(),
                Array.Empty<SurfaceRegion>());
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_pool);
            UnityEngine.Object.DestroyImmediate(_generatedRoot);
            UnityEngine.Object.DestroyImmediate(_areaRoot);
        }

        [Test]
        public void AllocatedTargetsAlwaysPreserveRequestedTotal()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-1_000_000, 1_000_000));

            GenixProperty.Check(
                nameof(AllocatedTargetsAlwaysPreserveRequestedTotal),
                Prop.ForAll(seeds, seed =>
                {
                    GenerationRandom random = new(seed);
                    int count = random.Range(1, 101);
                    int mask = random.Range(1, 16);
                    TargetDistributionMode mode = random.Range(0, 2) == 0
                        ? TargetDistributionMode.Balanced
                        : TargetDistributionMode.Weighted;
                    TargetDistributionWeights weights = new(
                        random.Range(1, 9),
                        random.Range(1, 9),
                        random.Range(1, 9),
                        random.Range(1, 9));
                    List<PlacementType> placementTypes = GetPlacementTypes(mask);
                    GenerationContext context = CreateContext(count, mode, weights, seed);

                    Dictionary<PlacementType, int> targets = TargetDistributionPolicy.CreateTargets(
                        context,
                        placementTypes);

                    bool totalPreserved = targets.Values.Sum() == count;
                    bool keysPreserved = targets.Keys.OrderBy(value => value)
                        .SequenceEqual(placementTypes.OrderBy(value => value));
                    bool nonNegative = targets.Values.All(value => value >= 0);
                    bool balanced = mode != TargetDistributionMode.Balanced ||
                                    targets.Values.Max() - targets.Values.Min() <= 1;
                    return totalPreserved && keysPreserved && nonNegative && balanced;
                }));
        }

        private GenerationContext CreateContext(
            int count,
            TargetDistributionMode mode,
            TargetDistributionWeights weights,
            int seed)
        {
            StyleSettings style = new(
                string.Empty,
                SamplingAlgorithm.Random,
                default,
                new CandidateSettings(2, 1, false),
                new GridSettings(1f, 0f),
                new ClusterSettings(2, 1f),
                new PoissonSettings(1f, 30));
            GenerationRequest request = new(
                new StubAreaSource(_areaRoot.transform),
                _pool,
                count,
                PlacementTarget.All,
                mode,
                weights,
                style,
                default,
                useFixedSeed: true,
                randomSeed: seed);
            return new GenerationContext(
                request,
                _generatedRoot.transform,
                _area,
                0f,
                null,
                SceneObjectIndex.Empty,
                SceneObjectIndex.Empty);
        }

        private static List<PlacementType> GetPlacementTypes(int mask)
        {
            List<PlacementType> result = new();

            if ((mask & 1) != 0)
                result.Add(PlacementType.Floor);
            if ((mask & 2) != 0)
                result.Add(PlacementType.Wall);
            if ((mask & 4) != 0)
                result.Add(PlacementType.Ceiling);
            if ((mask & 8) != 0)
                result.Add(PlacementType.InsideSpace);

            return result;
        }

        private sealed class StubAreaSource : IAreaSource
        {
            public SpatialSourceInfo SourceInfo { get; } = new("Test", "Area", "distribution-property");
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
