using FsCheck;
using FsCheck.Fluent;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests.Properties
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SpatialArea)]
    public sealed class SurfaceClassifierPropertyTests
    {
        [Test]
        [Category(GenixTestCategories.Property)]
        public void PositiveAndNegativeThresholdBandsCoverEveryFiniteNormal()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-1_000_000, 1_000_000));
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Fast,
                0,
                floorNormalYThreshold: 0.65f,
                ceilingNormalYThreshold: -0.65f);

            GenixProperty.Check(
                nameof(PositiveAndNegativeThresholdBandsCoverEveryFiniteNormal),
                Prop.ForAll(seeds, seed =>
                {
                    GenerationRandom random = new(seed);
                    Vector3 normal = new(
                        random.Range(-100f, 100f),
                        random.Range(-100f, 100f),
                        random.Range(-100f, 100f));

                    if (normal.sqrMagnitude <= 0.001f)
                        return SurfaceClassifier.Classify(normal, settings) == PlacementType.Wall;

                    float y = normal.normalized.y;
                    PlacementType expected = y >= 0.65f
                        ? PlacementType.Floor
                        : y <= -0.65f
                            ? PlacementType.Ceiling
                            : PlacementType.Wall;
                    return SurfaceClassifier.Classify(normal, settings) == expected;
                }));
        }

        [Test]
        public void ReversedThresholdsAreNormalizedBeforeClassification()
        {
            AreaBuildSettings normal = new(
                AreaDecompositionMode.Fast,
                0,
                floorNormalYThreshold: 0.4f,
                ceilingNormalYThreshold: -0.4f);
            AreaBuildSettings reversed = new(
                AreaDecompositionMode.Fast,
                0,
                floorNormalYThreshold: -0.4f,
                ceilingNormalYThreshold: 0.4f);

            for (int i = -100; i <= 100; i++)
            {
                Vector3 input = new(1f, i / 100f, 0.5f);
                Assert.That(SurfaceClassifier.Classify(input, reversed), Is.EqualTo(SurfaceClassifier.Classify(input, normal)));
            }
        }
    }
}
