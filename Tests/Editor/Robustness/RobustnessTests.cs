using System;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests.Robustness
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.RobustnessArea)]
    public sealed class RobustnessTests
    {
        [Test]
        public void OrientedBoundsClampsNonPositiveDimensions()
        {
            OrientedBounds bounds = new(Vector3.zero, new Vector3(-10f, 0f, float.NegativeInfinity), Quaternion.identity);

            Assert.That(bounds.Size.x, Is.EqualTo(0.01f));
            Assert.That(bounds.Size.y, Is.EqualTo(0.01f));
            Assert.That(bounds.Size.z, Is.EqualTo(0.01f));
        }

        [Test]
        public void InvalidGridAndPoissonSettingsReturnNoSamples()
        {
            Bounds bounds = new(Vector3.zero, Vector3.one * 10f);
            StyleSettings settings = CreateStyleSettings(gridSize: 0f, poissonDistance: 0f, poissonAttempts: 0);
            SamplingContext context = new(
                bounds,
                bounds.center,
                settings,
                100,
                new GenerationRandom(1),
                candidateCountOverride: 100);

            Assert.That(new GridSampler().SamplePositions(context), Is.Empty);
            Assert.That(new BridsonPoissonDiskSampler().SamplePositions(context), Is.Empty);
        }

        [Test]
        public void EmptyPlacementAreaDoesNotThrowForSupportedQueries()
        {
            PlacementArea area = new(
                new SpatialSourceInfo("Test", "Empty", "empty"),
                new Bounds(Vector3.zero, Vector3.zero),
                Array.Empty<SurfaceRegion>(),
                Array.Empty<SurfaceRegion>(),
                subspaceCells: Array.Empty<Vector3Int>(),
                ceilingRegions: Array.Empty<SurfaceRegion>());

            Assert.That(area.SupportsPlacementType(PlacementType.Floor), Is.False);
            Assert.That(area.SupportsPlacementType(PlacementType.Wall), Is.False);
            Assert.That(area.SupportsPlacementType(PlacementType.Ceiling), Is.False);
            Assert.That(area.SupportsPlacementType(PlacementType.InsideSpace), Is.False);
            Assert.That(area.TryGetRandomVolumePoint(new GenerationRandom(1), out _), Is.False);
        }

        private static StyleSettings CreateStyleSettings(float gridSize, float poissonDistance, int poissonAttempts) => new(
            string.Empty,
            SamplingAlgorithm.BridsonPoissonDisk,
            new PlacementSettings(),
            new CandidateSettings(1, 0, false),
            new GridSettings(gridSize, 0f),
            new ClusterSettings(1, 1f),
            new PoissonSettings(poissonDistance, poissonAttempts));
    }
}
