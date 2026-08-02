using System;
using Genix.Areas;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SpatialArea)]
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

        [Test]
        public void VoxelOccupancyUsesPlacementSpecificColumnsAndOptionalLayers()
        {
            VoxelOccupancy occupancy = new(
                new[] { new Vector3Int(1, 2, 3) },
                new[] { new Vector3Int(4, 5, 6) },
                Array.Empty<Vector3Int>(),
                1f);

            Assert.That(occupancy.HasSurfaceCells, Is.True);
            Assert.That(occupancy.HasGrid(Genix.Assets.PlacementType.Floor), Is.True);
            Assert.That(occupancy.HasGrid(Genix.Assets.PlacementType.Ceiling), Is.True);
            Assert.That(occupancy.ContainsPoint(new Vector3(1.5f, 100f, 3.5f), Genix.Assets.PlacementType.Floor, null), Is.True);
            Assert.That(occupancy.ContainsPoint(new Vector3(1.5f, 0f, 3.5f), Genix.Assets.PlacementType.Floor, 2), Is.True);
            Assert.That(occupancy.ContainsPoint(new Vector3(1.5f, 0f, 3.5f), Genix.Assets.PlacementType.Floor, 3), Is.False);
            Assert.That(occupancy.ContainsPoint(new Vector3(4.5f, 0f, 6.5f), Genix.Assets.PlacementType.Ceiling, 5), Is.True);
        }

        [Test]
        public void VoxelOccupancyFloorFootprintRequiresEveryCoveredColumn()
        {
            VoxelOccupancy occupancy = new(
                new[] { Vector3Int.zero, Vector3Int.right },
                Array.Empty<Vector3Int>(),
                Array.Empty<Vector3Int>(),
                1f);

            Assert.That(occupancy.ContainsFloorFootprint(
                new Bounds(new Vector3(1f, 0f, 0.5f), new Vector3(2f, 1f, 1f))), Is.True);
            Assert.That(occupancy.ContainsFloorFootprint(
                new Bounds(new Vector3(1.5f, 0f, 0.5f), new Vector3(3f, 1f, 1f))), Is.False);
        }

        [Test]
        public void VoxelOccupancyVolumePointFallsBackWithoutMaskAndRejectsMissingCells()
        {
            VoxelOccupancy unbounded = new(null, null, null, 1f);
            VoxelOccupancy bounded = new(null, null, new[] { new Vector3Int(2, 3, 4) }, 1f);

            Assert.That(unbounded.ContainsVolumePoint(new Vector3(100f, 100f, 100f)), Is.True);
            Assert.That(bounded.ContainsVolumePoint(new Vector3(2.5f, 3.5f, 4.5f)), Is.True);
            Assert.That(bounded.ContainsVolumePoint(new Vector3(3.5f, 3.5f, 4.5f)), Is.False);
        }

        [Test]
        public void VoxelOccupancyVolumeRequiresAllIntersectedCells()
        {
            VoxelOccupancy occupancy = new(
                null,
                null,
                new[] { Vector3Int.zero },
                1f);

            Assert.That(occupancy.ContainsVolume(
                new OrientedBounds(Vector3.one * 0.5f, Vector3.one * 0.5f, Quaternion.identity)), Is.True);
            Assert.That(occupancy.ContainsVolume(
                new OrientedBounds(new Vector3(1f, 0.5f, 0.5f), new Vector3(2f, 0.5f, 0.5f), Quaternion.identity)), Is.False);
        }

        [Test]
        public void VoxelOccupancyRandomPointHonorsBoundsAndInvalidInputs()
        {
            VoxelOccupancy occupancy = new(
                null,
                null,
                new[] { new Vector3Int(2, 3, 4) },
                1f);

            Assert.That(occupancy.TryGetRandomVolumePoint(
                new GenerationRandom(12),
                new Bounds(new Vector3(2.5f, 3.5f, 4.5f), Vector3.one),
                out Vector3 position), Is.True);
            Assert.That(position.x, Is.InRange(2f, 3f));
            Assert.That(occupancy.TryGetRandomVolumePoint(
                null,
                new Bounds(Vector3.zero, Vector3.one),
                out _), Is.False);
            Assert.That(occupancy.TryGetRandomVolumePoint(
                new GenerationRandom(12),
                new Bounds(Vector3.zero, Vector3.one),
                out _), Is.False);
        }

        [TestCase(0f, 2)]
        [TestCase(1f, 2)]
        [TestCase(3f, 3)]
        [TestCase(20f, 4)]
        public void VoxelOccupancyFootprintSegmentCountIsBounded(float length, int expected)
        {
            VoxelOccupancy occupancy = new(null, null, null, 1f);

            Assert.That(occupancy.GetFootprintSegmentCount(length), Is.EqualTo(expected));
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
