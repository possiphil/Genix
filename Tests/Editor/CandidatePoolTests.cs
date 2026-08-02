using System.Collections.Generic;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.PlacementArea)]
    public sealed class CandidatePoolTests
    {
        [Test]
        public void EagerPoolReturnsSeedsInStoredOrder()
        {
            CandidatePool pool = new(new List<CandidateSeed>
            {
                new(Vector3.left, Quaternion.identity),
                new(Vector3.right, Quaternion.identity)
            });

            Assert.That(pool.Count, Is.EqualTo(2));
            Assert.That(pool.TryTakeNext(out CandidateSeed first), Is.True);
            Assert.That(first.Position, Is.EqualTo(Vector3.left));
            Assert.That(pool.TryTakeNext(out CandidateSeed second), Is.True);
            Assert.That(second.Position, Is.EqualTo(Vector3.right));
            Assert.That(pool.TryTakeNext(out _), Is.False);
            Assert.That(pool.Count, Is.Zero);
        }

        [Test]
        public void LazyPoolLoadsOnlyWhenDataIsRequested()
        {
            int calls = 0;
            CandidatePool pool = new(
                () =>
                {
                    calls++;
                    return new List<CandidateSeed> { new(Vector3.one, Quaternion.identity) };
                },
                2);

            Assert.That(calls, Is.Zero);
            Assert.That(pool.TryTakeNext(out CandidateSeed seed), Is.True);
            Assert.That(seed.Position, Is.EqualTo(Vector3.one));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void LazyPoolSkipsEmptyBatchesUntilDataArrives()
        {
            int calls = 0;
            CandidatePool pool = new(
                () => ++calls < 3
                    ? new List<CandidateSeed>()
                    : new List<CandidateSeed> { new(Vector3.up, Quaternion.identity) },
                3);

            Assert.That(pool.TryTakeNext(out CandidateSeed seed), Is.True);
            Assert.That(seed.Position, Is.EqualTo(Vector3.up));
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void LazyPoolStopsAtMaximumBatchCount()
        {
            int calls = 0;
            CandidatePool pool = new(
                () =>
                {
                    calls++;
                    return new List<CandidateSeed>();
                },
                2);

            Assert.That(pool.TryTakeNext(out _), Is.False);
            Assert.That(pool.TryTakeNext(out _), Is.False);
            Assert.That(calls, Is.EqualTo(2));
        }
    }
}
