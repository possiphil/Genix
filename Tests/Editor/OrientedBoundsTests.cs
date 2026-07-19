using Genix.Placement;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    public sealed class OrientedBoundsTests
    {
        [Test]
        public void RotatedOverlappingBoundsIntersect()
        {
            OrientedBounds first = new(Vector3.zero, new Vector3(2f, 1f, 1f), Quaternion.Euler(0f, 45f, 0f));
            OrientedBounds second = new(new Vector3(0.75f, 0f, 0f), Vector3.one, Quaternion.identity);

            Assert.That(first.Intersects(second), Is.True);
            Assert.That(second.Intersects(first), Is.True);
        }

        [Test]
        public void SeparatedBoundsDoNotIntersect()
        {
            OrientedBounds first = new(Vector3.zero, new Vector3(4f, 1f, 0.5f), Quaternion.Euler(0f, 45f, 0f));
            OrientedBounds second = new(new Vector3(5f, 0f, 0f), Vector3.one, Quaternion.identity);

            Assert.That(first.Intersects(second), Is.False);
        }
    }
}
