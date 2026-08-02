using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Placement.Providers;
using Genix.Profiling;
using Genix.Sampling;
using Genix.Tests.Framework;
using NUnit.Framework;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.PlacementArea)]
    public sealed class CandidateProviderTests
    {
        private GenerationTestScene _scene;

        [SetUp]
        public void SetUp()
        {
            CandidateSeedCache.Clear();
            _scene = new GenerationTestScene();
        }

        [TearDown]
        public void TearDown()
        {
            CandidateSeedCache.Clear();
            _scene.Dispose();
        }

        [Test]
        public void FloorProviderSamplesExtractedFloorRegion()
        {
            GenerationContext context = CreateContext(PlacementTarget.Floor);

            var seeds = new HorizontalSurfaceCandidateProvider(4, 4, 4)
                .CreateCandidateSeeds(context);

            Assert.That(seeds, Has.Count.EqualTo(4));
            Assert.That(seeds.All(seed => seed.PlacementType == PlacementType.Floor), Is.True);
            Assert.That(seeds.All(seed => seed.Position.y == _scene.Area.WorldBounds.min.y), Is.True);
        }

        [Test]
        public void CeilingProviderSamplesExtractedCeilingRegion()
        {
            GenerationContext context = CreateContext(PlacementTarget.Ceiling);

            var seeds = new CeilingCandidateProvider(4, 4, 4)
                .CreateCandidateSeeds(context);

            Assert.That(seeds, Has.Count.EqualTo(4));
            Assert.That(seeds.All(seed => seed.PlacementType == PlacementType.Ceiling), Is.True);
            Assert.That(seeds.All(seed => seed.Position.y == _scene.Area.WorldBounds.max.y), Is.True);
        }

        [Test]
        public void WallProviderSamplesExplicitWallSpan()
        {
            GenerationContext context = CreateContext(PlacementTarget.Wall);

            var seeds = new WallCandidateProvider(5, 5, 5)
                .CreateCandidateSeeds(context);

            Assert.That(seeds, Has.Count.EqualTo(5));
            Assert.That(seeds.All(seed => seed.PlacementType == PlacementType.Wall), Is.True);
            Assert.That(seeds.All(seed => seed.SurfaceNormal == UnityEngine.Vector3.forward), Is.True);
        }

        [TestCase(SamplingAlgorithm.Random)]
        [TestCase(SamplingAlgorithm.Grid)]
        [TestCase(SamplingAlgorithm.JitteredGrid)]
        [TestCase(SamplingAlgorithm.Cluster)]
        [TestCase(SamplingAlgorithm.BridsonPoissonDisk)]
        public void InsideSpaceProviderKeepsSeedsWithinArea(SamplingAlgorithm algorithm)
        {
            GenerationContext context = CreateContext(PlacementTarget.InsideSpace, algorithm);

            var seeds = new InsideSpaceCandidateProvider(3, 3, 3)
                .CreateCandidateSeeds(context);

            Assert.That(seeds, Is.Not.Empty);
            Assert.That(seeds.All(seed => seed.PlacementType == PlacementType.InsideSpace), Is.True);
            Assert.That(seeds.All(seed => _scene.Area.ContainsVolumePoint(seed.Position)), Is.True);
        }

        [Test]
        public void CombinedProviderEmitsEveryRequestedPlacementType()
        {
            GenerationContext context = CreateContext(PlacementTarget.All);

            var seeds = new PlacementTargetCandidateProvider(
                    PlacementTarget.All,
                    requestedCount: 8,
                    minimumCandidateCount: 8,
                    candidateCount: 8)
                .CreateCandidateSeeds(context);

            Assert.That(seeds.Select(seed => seed.PlacementType).Distinct(), Is.EquivalentTo(new[]
            {
                PlacementType.Floor,
                PlacementType.Wall,
                PlacementType.Ceiling,
                PlacementType.InsideSpace
            }));
        }

        [Test]
        public void CandidateSeedCacheEntrySnapshotsSeedsAndRandomState()
        {
            List<CandidateSeed> source = new()
            {
                new CandidateSeed(UnityEngine.Vector3.one, UnityEngine.Quaternion.identity)
            };

            CandidateSeedCacheEntry entry = new(source, 123UL);
            source.Clear();

            Assert.That(entry.Seeds, Has.Count.EqualTo(1));
            Assert.That(entry.Seeds[0].Position, Is.EqualTo(UnityEngine.Vector3.one));
            Assert.That(entry.RandomStateAfterGeneration, Is.EqualTo(123UL));
        }

        [Test]
        public void CandidateSeedCacheStoresOverwritesAndRejectsInvalidKeys()
        {
            CandidateSeed first = new(UnityEngine.Vector3.one, UnityEngine.Quaternion.identity);
            CandidateSeed second = new(UnityEngine.Vector3.right, UnityEngine.Quaternion.identity);

            CandidateSeedCache.Store(string.Empty, new[] { first }, 1UL);
            CandidateSeedCache.Store("valid", null, 2UL);
            Assert.That(CandidateSeedCache.TryGet(string.Empty, out _), Is.False);
            Assert.That(CandidateSeedCache.TryGet("valid", out _), Is.False);

            CandidateSeedCache.Store("valid", new[] { first }, 3UL);
            CandidateSeedCache.Store("valid", new[] { second }, 4UL);

            Assert.That(CandidateSeedCache.TryGet("valid", out CandidateSeedCacheEntry entry), Is.True);
            Assert.That(entry.Seeds.Single().Position, Is.EqualTo(UnityEngine.Vector3.right));
            Assert.That(entry.RandomStateAfterGeneration, Is.EqualTo(4UL));
        }

        [Test]
        public void CandidateSeedCacheClearsOldEntriesWhenCapacityIsExceeded()
        {
            CandidateSeed seed = new(UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);
            for (int i = 0; i < 32; i++)
                CandidateSeedCache.Store($"key-{i}", new[] { seed }, (ulong)i);

            CandidateSeedCache.Store("overflow", new[] { seed }, 99UL);

            Assert.That(CandidateSeedCache.TryGet("key-0", out _), Is.False);
            Assert.That(CandidateSeedCache.TryGet("overflow", out CandidateSeedCacheEntry entry), Is.True);
            Assert.That(entry.RandomStateAfterGeneration, Is.EqualTo(99UL));
        }

        [Test]
        public void CandidateSeedFactoryReusesDeterministicEagerResults()
        {
            GenerationContext firstContext = CreateContext(PlacementTarget.InsideSpace, SamplingAlgorithm.Grid);
            GenerationProfilerRecorder firstProfiler = new();
            List<CandidateSeed> first = CandidateSeedFactory.Create(
                firstContext,
                NullDiagnosticsSink.Instance,
                PlacementTarget.InsideSpace,
                firstProfiler);
            ulong randomStateAfterFirst = firstContext.Random.State;

            GenerationContext secondContext = CreateContext(PlacementTarget.InsideSpace, SamplingAlgorithm.Grid);
            GenerationProfilerRecorder secondProfiler = new();
            List<CandidateSeed> second = CandidateSeedFactory.Create(
                secondContext,
                NullDiagnosticsSink.Instance,
                PlacementTarget.InsideSpace,
                secondProfiler);

            Assert.That(first.Select(seed => seed.Position), Is.EqualTo(second.Select(seed => seed.Position)));
            Assert.That(firstProfiler.Profile.CandidateCacheHit, Is.False);
            Assert.That(secondProfiler.Profile.CandidateCacheHit, Is.True);
            Assert.That(secondContext.Random.State, Is.EqualTo(randomStateAfterFirst));
            Assert.That(secondProfiler.Profile.GetTarget(PlacementType.InsideSpace).CandidateSeeds, Is.EqualTo(second.Count));
        }

        [Test]
        public void CandidateSeedFactoryDoesNotCacheUnfixedSeedRuns()
        {
            GenerationContext firstContext = CreateContext(
                PlacementTarget.InsideSpace,
                SamplingAlgorithm.Grid,
                useFixedSeed: false);
            GenerationContext secondContext = CreateContext(
                PlacementTarget.InsideSpace,
                SamplingAlgorithm.Grid,
                useFixedSeed: false);
            GenerationProfilerRecorder firstProfiler = new();
            GenerationProfilerRecorder secondProfiler = new();

            CandidateSeedFactory.Create(firstContext, NullDiagnosticsSink.Instance, null, firstProfiler);
            CandidateSeedFactory.Create(secondContext, NullDiagnosticsSink.Instance, null, secondProfiler);

            Assert.That(firstProfiler.Profile.CandidateCacheHit, Is.False);
            Assert.That(secondProfiler.Profile.CandidateCacheHit, Is.False);
        }

        [Test]
        public void CandidateSeedFactoryCreatesEagerPoolPerGridPlacementType()
        {
            GenerationContext context = CreateContext(PlacementTarget.All, SamplingAlgorithm.Grid);

            Dictionary<PlacementType, CandidatePool> pools = CandidateSeedFactory.CreatePoolsByPlacementType(
                context,
                NullDiagnosticsSink.Instance,
                PlacementTarget.All);

            Assert.That(pools.Keys, Is.EquivalentTo(new[]
            {
                PlacementType.Floor,
                PlacementType.Wall,
                PlacementType.Ceiling,
                PlacementType.InsideSpace
            }));
            foreach (KeyValuePair<PlacementType, CandidatePool> entry in pools)
            {
                Assert.That(entry.Value.TryTakeNext(out CandidateSeed seed), Is.True);
                Assert.That(seed.PlacementType, Is.EqualTo(entry.Key));
            }
        }

        [Test]
        public void CandidateSeedFactoryCreatesLazyPoolPerRandomPlacementType()
        {
            GenerationContext context = CreateContext(PlacementTarget.All, SamplingAlgorithm.Random);

            Dictionary<PlacementType, CandidatePool> pools = CandidateSeedFactory.CreatePoolsByPlacementType(
                context,
                NullDiagnosticsSink.Instance,
                null);

            Assert.That(pools, Has.Count.EqualTo(4));
            foreach (KeyValuePair<PlacementType, CandidatePool> entry in pools)
            {
                Assert.That(entry.Value.TryTakeNext(out CandidateSeed seed), Is.True);
                Assert.That(seed.PlacementType, Is.EqualTo(entry.Key));
            }
        }

        [Test]
        public void CandidateSeedFactoryTargetOverrideRestrictsGeneratedTypes()
        {
            GenerationContext context = CreateContext(PlacementTarget.All, SamplingAlgorithm.Grid);

            List<CandidateSeed> seeds = CandidateSeedFactory.Create(
                context,
                NullDiagnosticsSink.Instance,
                PlacementTarget.Floor);

            Assert.That(seeds, Is.Not.Empty);
            Assert.That(seeds.All(seed => seed.PlacementType == PlacementType.Floor), Is.True);
        }

        [Test]
        public void CandidateSeedFactoryUsesCachedDataForLazyAlgorithmPool()
        {
            GenerationContext warmup = CreateContext(PlacementTarget.InsideSpace, SamplingAlgorithm.Random);
            CandidateSeedFactory.Create(
                warmup,
                NullDiagnosticsSink.Instance,
                PlacementTarget.InsideSpace);
            GenerationContext cachedContext = CreateContext(PlacementTarget.InsideSpace, SamplingAlgorithm.Random);
            GenerationProfilerRecorder profiler = new();

            CandidatePool pool = CandidateSeedFactory.CreatePool(
                cachedContext,
                NullDiagnosticsSink.Instance,
                PlacementTarget.InsideSpace,
                profiler);

            Assert.That(pool.TryTakeNext(out CandidateSeed seed), Is.True);
            Assert.That(seed.PlacementType, Is.EqualTo(PlacementType.InsideSpace));
            Assert.That(profiler.Profile.CandidateCacheHit, Is.True);
        }

        private GenerationContext CreateContext(
            PlacementTarget targets,
            SamplingAlgorithm algorithm = SamplingAlgorithm.Random,
            bool useFixedSeed = true)
        {
            GenerationRequest request = _scene.CreateRequest(
                count: 8,
                targets: targets,
                algorithm: algorithm);

            if (!useFixedSeed)
            {
                request = new GenerationRequest(
                    request.AreaSource,
                    request.AssetPool,
                    request.ObjectCount,
                    request.PlacementTargets,
                    request.TargetDistributionMode,
                    request.TargetDistributionWeights,
                    request.StyleSettings,
                    request.AreaBuildSettings,
                    request.RelativePlacement,
                    request.StyleName,
                    useFixedSeed: false,
                    randomSeed: request.RandomSeed,
                    bestEffort: request.BestEffort,
                    detailedDiagnostics: request.DetailedDiagnostics);
            }

            return _scene.CreateContext(request);
        }
    }
}
