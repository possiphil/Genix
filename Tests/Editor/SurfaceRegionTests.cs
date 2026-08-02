using Genix.Areas;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SpatialArea)]
    public sealed class SurfaceRegionTests
    {
        [Test]
        public void FloorNormalAndSurfaceHeightPointUpward()
        {
            SurfaceRegion region = SurfaceRegion.CreateFloor("Floor", -2f, 4f, -3f, 5f, 7f, 3);

            Assert.That(region.Kind, Is.EqualTo(SurfaceKind.Floor));
            Assert.That(region.Normal, Is.EqualTo(Vector3.up));
            Assert.That(region.SurfaceY, Is.EqualTo(7f));
            Assert.That(region.VoxelLayer, Is.EqualTo(3));
        }

        [Test]
        public void CeilingNormalPointsDownward()
        {
            SurfaceRegion region = SurfaceRegion.CreateCeiling("Ceiling", -1f, 1f, -1f, 1f, 8f);

            Assert.That(region.Kind, Is.EqualTo(SurfaceKind.Ceiling));
            Assert.That(region.Normal, Is.EqualTo(Vector3.down));
            Assert.That(region.SurfaceY, Is.EqualTo(8f));
        }

        [Test]
        public void WallNormalIsNormalizedAndBoundsCoverEndpoints()
        {
            Vector3 start = new(-2f, 1f, 3f);
            Vector3 end = new(4f, 1f, 3f);
            SurfaceRegion region = SurfaceRegion.CreateWall("Wall", start, end, 6f, new Vector3(0f, 0f, 5f));

            Assert.That(region.Kind, Is.EqualTo(SurfaceKind.Wall));
            Assert.That(region.Normal, Is.EqualTo(Vector3.forward));
            Assert.That(region.Bounds.Contains(start), Is.True);
            Assert.That(region.Bounds.Contains(end), Is.True);
            Assert.That(region.Bounds.max.y, Is.GreaterThan(6f));
        }

        [TestCase(0f, 0f, true)]
        [TestCase(2f, 2f, true)]
        [TestCase(2.01f, 0f, false)]
        [TestCase(0f, -2.01f, false)]
        public void ContainsXZHonorsRegionEdges(float x, float z, bool expected)
        {
            SurfaceRegion region = SurfaceRegion.CreateFloor("Floor", -2f, 2f, -2f, 2f, 0f);

            Assert.That(region.ContainsXZ(new Vector3(x, 100f, z), 0f), Is.EqualTo(expected));
        }

        [Test]
        public void ContainsBoundsXZRequiresEveryCornerInside()
        {
            SurfaceRegion region = SurfaceRegion.CreateFloor("Floor", -2f, 2f, -2f, 2f, 0f);

            Assert.That(region.ContainsBoundsXZ(new Bounds(Vector3.zero, new Vector3(3f, 100f, 3f))), Is.True);
            Assert.That(region.ContainsBoundsXZ(new Bounds(new Vector3(1.5f, 0f, 0f), new Vector3(2f, 1f, 1f))), Is.False);
        }
    }
}
