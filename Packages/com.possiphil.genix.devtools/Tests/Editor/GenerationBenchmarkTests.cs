using System.Linq;
using Genix.Core;
using Genix.Editor.Benchmarking;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.PerformanceArea)]
    public sealed class GenerationBenchmarkTests
    {
        [Test]
        public void StatisticsUseInterpolatedPercentiles()
        {
            double[] samples = { 40d, 10d, 30d, 20d };

            Assert.That(GenerationBenchmarkStatistics.LowerQuartile(samples), Is.EqualTo(17.5d));
            Assert.That(GenerationBenchmarkStatistics.Median(samples), Is.EqualTo(25d));
            Assert.That(GenerationBenchmarkStatistics.UpperQuartile(samples), Is.EqualTo(32.5d));
            Assert.That(GenerationBenchmarkStatistics.P95(samples), Is.EqualTo(38.5d));
        }

        [Test]
        public void StandardDeviationUsesSampleEstimator()
        {
            double result = GenerationBenchmarkStatistics.StandardDeviation(new[] { 2d, 4d, 4d, 4d, 5d, 5d, 7d, 9d });

            Assert.That(result, Is.EqualTo(2.138089935d).Within(0.000000001d));
        }

        [Test]
        public void SuiteCreatesEnoughStableDistinctSeeds()
        {
            GenerationBenchmarkSuite first = ScriptableObject.CreateInstance<GenerationBenchmarkSuite>();
            GenerationBenchmarkSuite second = ScriptableObject.CreateInstance<GenerationBenchmarkSuite>();

            try
            {
                Assert.That(first.Seeds.Count, Is.GreaterThanOrEqualTo(first.WarmSeedCount));
                Assert.That(first.Seeds.Distinct().Count(), Is.EqualTo(first.Seeds.Count));
                Assert.That(second.Seeds, Is.EqualTo(first.Seeds));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void UninstrumentedContextDoesNotCreateAreaTimingSinks()
        {
            using GenerationTestScene scene = new();
            scene.CreateAsset("Floor Asset");
            GenerationRequest request = scene.CreateRequest();

            GenerationContext context = GenerationContextFactory.CreateUninstrumented(
                request,
                scene.GeneratedRoot.transform,
                scene.Pool.StaticAssets);

            Assert.That(context.AreaBuildProfile, Is.Null);
            Assert.That(context.AreaBuildMilliseconds, Is.Zero);
            Assert.That(scene.AreaSource.LastSettings.profile, Is.Null);
        }

        [Test]
        public void RegularContextStillCollectsAreaTimingAfterBenchmarking()
        {
            using GenerationTestScene scene = new();
            scene.CreateAsset("Floor Asset");
            GenerationRequest request = scene.CreateRequest();

            GenerationContext context = GenerationContextFactory.Create(
                request,
                scene.GeneratedRoot.transform,
                scene.Pool.StaticAssets);

            Assert.That(context.AreaBuildProfile, Is.Not.Null);
            Assert.That(context.AreaBuildMilliseconds, Is.GreaterThanOrEqualTo(0f));
            Assert.That(scene.AreaSource.LastSettings.profile, Is.SameAs(context.AreaBuildProfile));
        }
    }
}
