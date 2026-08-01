using System;
using Genix.Areas;
using Genix.Core;
using Genix.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    public sealed class PlacementAreaVolumeSamplingTests
    {
        [Test]
        public void TryGetRandomVolumePointSamplesInsideKnownSubspaceCell()
        {
            PlacementArea area = CreateArea(
                new Bounds(new Vector3(2.5f, 3.5f, 4.5f), Vector3.one),
                new[] { new Vector3Int(2, 3, 4) });

            Assert.That(area.HasVolumeCells, Is.True);
            Assert.That(area.TryGetRandomVolumePoint(new GenerationRandom(123), out Vector3 position), Is.True);
            Assert.That(position.x, Is.InRange(2f, 3f));
            Assert.That(position.y, Is.InRange(3f, 4f));
            Assert.That(position.z, Is.InRange(4f, 5f));
            Assert.That(area.ContainsVolumePoint(position), Is.True);
        }

        [Test]
        public void TryGetRandomVolumePointFailsWithoutVolumeGrid()
        {
            PlacementArea area = CreateArea(new Bounds(Vector3.zero, Vector3.one), Array.Empty<Vector3Int>());

            Assert.That(area.HasVolumeCells, Is.False);
            Assert.That(area.TryGetRandomVolumePoint(new GenerationRandom(123), out _), Is.False);
        }

        [Test]
        public void SupportsInsideSpaceForUsableBoundsWithoutVolumeGrid()
        {
            PlacementArea area = CreateArea(new Bounds(Vector3.zero, Vector3.one), Array.Empty<Vector3Int>());

            Assert.That(area.HasVolumeCells, Is.False);
            Assert.That(area.SupportsPlacementType(Genix.Assets.PlacementType.InsideSpace), Is.True);
        }

        private static PlacementArea CreateArea(Bounds bounds, Vector3Int[] subspaceCells)
        {
            return new PlacementArea(
                new SpatialSourceInfo("Test", "Test Area", "test-area"),
                bounds,
                Array.Empty<SurfaceRegion>(),
                Array.Empty<SurfaceRegion>(),
                cellSize: 1f,
                settings: default,
                subspaceCells: subspaceCells,
                ceilingRegions: Array.Empty<SurfaceRegion>());
        }
    }
}
