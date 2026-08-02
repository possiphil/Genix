using System.Collections.Generic;
using FsCheck;
using FsCheck.Fluent;
using Genix.Core;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Sampling.RandomSampling;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests.Properties
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Property)]
    [Category(GenixTestCategories.SamplingArea)]
    public sealed class SamplingPropertyTests
    {
        [Test]
        public void RandomSamplerReturnsRequestedPointsInsideBounds()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-1_000_000, 1_000_000));

            GenixProperty.Check(
                nameof(RandomSamplerReturnsRequestedPointsInsideBounds),
                Prop.ForAll(seeds, seed =>
                {
                    Bounds bounds = new(new Vector3(10f, 4f, -7f), new Vector3(80f, 2f, 60f));
                    SamplingContext context = CreateContext(bounds, seed, 128, 1.5f);
                    List<Vector3> points = new RandomSampler().SamplePositions(context);

                    if (points.Count != 128)
                        return false;

                    foreach (Vector3 point in points)
                    {
                        if (!bounds.Contains(point))
                            return false;
                    }

                    return true;
                }));
        }

        [Test]
        public void PoissonSamplerPreservesConfiguredMinimumDistance()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-100_000, 100_000));

            GenixProperty.Check(
                nameof(PoissonSamplerPreservesConfiguredMinimumDistance),
                Prop.ForAll(seeds, seed =>
                {
                    const float minimumDistance = 2.5f;
                    Bounds bounds = new(Vector3.zero, new Vector3(40f, 1f, 40f));
                    SamplingContext context = CreateContext(bounds, seed, 96, minimumDistance);
                    List<Vector3> points = new BridsonPoissonDiskSampler().SamplePositions(context);
                    float minimumDistanceSquared = minimumDistance * minimumDistance - 0.0001f;

                    for (int i = 0; i < points.Count; i++)
                    {
                        if (!bounds.Contains(points[i]))
                            return false;

                        for (int j = i + 1; j < points.Count; j++)
                        {
                            Vector2 delta = new(points[i].x - points[j].x, points[i].z - points[j].z);

                            if (delta.sqrMagnitude < minimumDistanceSquared)
                                return false;
                        }
                    }

                    return true;
                }));
        }

        private static SamplingContext CreateContext(
            Bounds bounds,
            int seed,
            int candidateCount,
            float minimumDistance)
        {
            StyleSettings settings = new(
                string.Empty,
                SamplingAlgorithm.BridsonPoissonDisk,
                new PlacementSettings(),
                new CandidateSettings(1, 0, false),
                new GridSettings(1f, 0.25f),
                new ClusterSettings(4, 5f),
                new PoissonSettings(minimumDistance, 24));

            return new SamplingContext(
                bounds,
                bounds.center,
                settings,
                candidateCount,
                new GenerationRandom(seed),
                candidateCountOverride: candidateCount);
        }
    }
}
