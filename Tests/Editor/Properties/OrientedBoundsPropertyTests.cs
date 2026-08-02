using FsCheck;
using FsCheck.Fluent;
using Genix.Core;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests.Properties
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Property)]
    [Category(GenixTestCategories.GeometryArea)]
    public sealed class OrientedBoundsPropertyTests
    {
        [Test]
        public void IntersectionIsSymmetric()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-1_000_000, 1_000_000));

            GenixProperty.Check(
                nameof(IntersectionIsSymmetric),
                Prop.ForAll(seeds, seed =>
                {
                    GenerationRandom random = new(seed);
                    OrientedBounds first = CreateBounds(random);
                    OrientedBounds second = CreateBounds(random);
                    return first.Intersects(second) == second.Intersects(first);
                }));
        }

        [Test]
        public void TranslatingBothBoundsDoesNotChangeIntersection()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-1_000_000, 1_000_000));

            GenixProperty.Check(
                nameof(TranslatingBothBoundsDoesNotChangeIntersection),
                Prop.ForAll(seeds, seed =>
                {
                    GenerationRandom random = new(seed);
                    OrientedBounds first = CreateBounds(random);
                    OrientedBounds second = CreateBounds(random);
                    Vector3 translation = RandomVector(random, -500f, 500f);
                    bool before = first.Intersects(second);
                    bool after = new OrientedBounds(first.Center + translation, first.Size, first.Rotation)
                        .Intersects(new OrientedBounds(second.Center + translation, second.Size, second.Rotation));
                    return before == after;
                }));
        }

        [Test]
        public void AxisAlignedEnvelopeContainsEveryCorner()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-1_000_000, 1_000_000));

            GenixProperty.Check(
                nameof(AxisAlignedEnvelopeContainsEveryCorner),
                Prop.ForAll(seeds, seed =>
                {
                    OrientedBounds bounds = CreateBounds(new GenerationRandom(seed));
                    Bounds envelope = bounds.ToAxisAlignedBounds();
                    Vector3 extents = bounds.Extents;

                    for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bounds.Center + bounds.Rotation * Vector3.Scale(
                            extents,
                            new Vector3(x, y, z));

                        if (!ContainsWithTolerance(envelope, corner))
                            return false;
                    }

                    return true;
                }));
        }

        private static OrientedBounds CreateBounds(GenerationRandom random) => new(
            RandomVector(random, -50f, 50f),
            RandomVector(random, 0.01f, 20f),
            Quaternion.Euler(RandomVector(random, -180f, 180f)));

        private static Vector3 RandomVector(GenerationRandom random, float minimum, float maximum) => new(
            random.Range(minimum, maximum),
            random.Range(minimum, maximum),
            random.Range(minimum, maximum));

        private static bool ContainsWithTolerance(Bounds bounds, Vector3 point)
        {
            const float tolerance = 0.001f;
            return point.x >= bounds.min.x - tolerance && point.x <= bounds.max.x + tolerance &&
                   point.y >= bounds.min.y - tolerance && point.y <= bounds.max.y + tolerance &&
                   point.z >= bounds.min.z - tolerance && point.z <= bounds.max.z + tolerance;
        }
    }
}
