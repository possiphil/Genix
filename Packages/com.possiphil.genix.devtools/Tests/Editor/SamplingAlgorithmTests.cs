using System;
using System.Collections.Generic;
using Genix.Core;
using Genix.Extensions;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.JitteredGridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Sampling.RandomSampling;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SamplingArea)]
    public sealed class SamplingAlgorithmTests
    {
        [TestCase(SamplingAlgorithm.Random, typeof(RandomSampler))]
        [TestCase(SamplingAlgorithm.Grid, typeof(GridSampler))]
        [TestCase(SamplingAlgorithm.JitteredGrid, typeof(JitteredGridSampler))]
        [TestCase(SamplingAlgorithm.Cluster, typeof(ClusterSampler))]
        [TestCase(SamplingAlgorithm.BridsonPoissonDisk, typeof(BridsonPoissonDiskSampler))]
        public void SamplerFactoryMapsEverySupportedAlgorithm(SamplingAlgorithm algorithm, Type expectedType)
        {
            Assert.That(SamplerFactory.Create(algorithm), Is.TypeOf(expectedType));
        }

        [Test]
        public void SamplerFactoryRejectsUnknownAlgorithm()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SamplerFactory.Create((SamplingAlgorithm)999));
        }

        [TestCase(SamplingAlgorithm.Random, "Random Sampling")]
        [TestCase(SamplingAlgorithm.Grid, "Grid Sampling")]
        [TestCase(SamplingAlgorithm.JitteredGrid, "Jittered Grid Sampling")]
        [TestCase(SamplingAlgorithm.Cluster, "Cluster Sampling")]
        [TestCase(SamplingAlgorithm.BridsonPoissonDisk, "Bridson Poisson Disk Sampling")]
        public void AlgorithmNameMatchesDesignerFacingLabel(SamplingAlgorithm algorithm, string expected)
        {
            Assert.That(algorithm.ToAlgorithmName(), Is.EqualTo(expected));
        }

        [Test]
        public void AlgorithmNameRejectsUnknownAlgorithm()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ((SamplingAlgorithm)999).ToAlgorithmName());
        }

        [Test]
        public void GridIncludesBothBoundsAtExactCellIntervals()
        {
            SamplingContext context = CreateContext(SamplingAlgorithm.Grid, 7, grid: new GridSettings(2f, 0f));

            List<Vector3> samples = new GridSampler().SamplePositions(context);

            Assert.That(samples, Has.Count.EqualTo(9));
            Assert.That(samples, Does.Contain(new Vector3(-2f, -1f, -2f)));
            Assert.That(samples, Does.Contain(new Vector3(2f, -1f, 2f)));
        }

        [Test]
        public void JitteredGridIsDeterministicAndRemainsInsideBounds()
        {
            SamplingContext first = CreateContext(SamplingAlgorithm.JitteredGrid, 41, grid: new GridSettings(1f, 0.45f));
            SamplingContext second = CreateContext(SamplingAlgorithm.JitteredGrid, 41, grid: new GridSettings(1f, 0.45f));

            List<Vector3> firstSamples = new JitteredGridSampler().SamplePositions(first);
            List<Vector3> secondSamples = new JitteredGridSampler().SamplePositions(second);

            Assert.That(firstSamples, Is.EqualTo(secondSamples));
            Assert.That(firstSamples, Has.Some.Not.EqualTo(new Vector3(-2f, -1f, -2f)));
            Assert.That(firstSamples, Has.All.Matches<Vector3>(sample => first.Bounds.Contains(sample)));
        }

        [Test]
        public void ClusterSamplingProducesRequestedCountInsideBounds()
        {
            SamplingContext context = CreateContext(
                SamplingAlgorithm.Cluster,
                123,
                candidateCount: 75,
                cluster: new ClusterSettings(4, 1.25f, true, 0.5f));

            List<Vector3> samples = new ClusterSampler().SamplePositions(context);

            Assert.That(samples, Has.Count.EqualTo(75));
            Assert.That(samples, Has.All.Matches<Vector3>(sample => context.Bounds.Contains(sample)));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void ClusterSamplingRejectsNonPositiveRadius(float radius)
        {
            SamplingContext context = CreateContext(
                SamplingAlgorithm.Cluster,
                1,
                cluster: new ClusterSettings(3, radius));

            Assert.That(new ClusterSampler().SamplePositions(context), Is.Empty);
        }

        private static SamplingContext CreateContext(
            SamplingAlgorithm algorithm,
            int seed,
            int candidateCount = 20,
            GridSettings? grid = null,
            ClusterSettings? cluster = null)
        {
            StyleSettings settings = new(
                string.Empty,
                algorithm,
                new PlacementSettings(),
                new CandidateSettings(2, 1, false),
                grid ?? new GridSettings(1f, 0f),
                cluster ?? new ClusterSettings(2, 1f),
                new PoissonSettings(0.5f, 30));
            Bounds bounds = new(Vector3.zero, new Vector3(4f, 2f, 4f));
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
