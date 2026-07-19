using Genix.Areas;
using Genix.Assets;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    public sealed class SurfaceClassifierTests
    {
        private static readonly AreaBuildSettings Settings = new(
            AreaDecompositionMode.Fast,
            false,
            0,
            floorNormalYThreshold: 0.5f,
            ceilingNormalYThreshold: -0.5f);

        [TestCase(0f, 1f, 0f, PlacementType.Floor)]
        [TestCase(0f, -1f, 0f, PlacementType.Ceiling)]
        [TestCase(1f, 0f, 0f, PlacementType.Wall)]
        [TestCase(0f, 0.5f, 0.8660254f, PlacementType.Floor)]
        [TestCase(0f, -0.5f, 0.8660254f, PlacementType.Ceiling)]
        public void ClassifiesEntireNormalRange(float x, float y, float z, PlacementType expected)
        {
            Assert.That(SurfaceClassifier.Classify(new Vector3(x, y, z), Settings), Is.EqualTo(expected));
        }

        [Test]
        public void ZeroNormalFallsBackToWall()
        {
            Assert.That(SurfaceClassifier.Classify(Vector3.zero, Settings), Is.EqualTo(PlacementType.Wall));
        }
    }
}
