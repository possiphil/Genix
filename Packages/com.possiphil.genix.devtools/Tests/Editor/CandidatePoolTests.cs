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

        [Test]
        public void LazyPoolNeverRetainsMoreThanCandidateBudget()
        {
            int calls = 0;
            CandidatePool pool = new(
                () =>
                {
                    calls++;
                    return new List<CandidateSeed>
                    {
                        new(Vector3.left, Quaternion.identity),
                        new(Vector3.up, Quaternion.identity),
                        new(Vector3.right, Quaternion.identity)
                    };
                },
                maxLoadCount: 10,
                maxSeedCount: 5);
            int consumed = 0;

            while (pool.TryTakeNext(out _))
                consumed++;

            Assert.That(consumed, Is.EqualTo(5));
            Assert.That(pool.GeneratedCount, Is.EqualTo(5));
            Assert.That(pool.CandidateBudget, Is.EqualTo(5));
            Assert.That(pool.BudgetExhausted, Is.True);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void FilteredReadsPreserveUnmatchedSeedsForLaterGroups()
        {
            CandidatePool pool = new(new List<CandidateSeed>
            {
                new(Vector3.left, Quaternion.identity),
                new(Vector3.up, Quaternion.identity),
                new(Vector3.right, Quaternion.identity)
            });

            Assert.That(pool.TryTakeNext(seed => seed.Position.y > 0f, out CandidateSeed matched), Is.True);
            Assert.That(matched.Position, Is.EqualTo(Vector3.up));
            Assert.That(pool.TryTakeNext(out CandidateSeed firstRemaining), Is.True);
            Assert.That(firstRemaining.Position, Is.EqualTo(Vector3.left));
            Assert.That(pool.TryTakeNext(out CandidateSeed secondRemaining), Is.True);
            Assert.That(secondRemaining.Position, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void FilteredLazyReadLoadsUntilMatchAndKeepsEarlierBatches()
        {
            int calls = 0;
            CandidatePool pool = new(
                () => ++calls == 1
                    ? new List<CandidateSeed> { new(Vector3.left, Quaternion.identity) }
                    : new List<CandidateSeed> { new(Vector3.right, Quaternion.identity) },
                2);

            Assert.That(pool.TryTakeNext(seed => seed.Position.x > 0f, out CandidateSeed matched), Is.True);
            Assert.That(matched.Position, Is.EqualTo(Vector3.right));
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(pool.TryTakeNext(out CandidateSeed preserved), Is.True);
            Assert.That(preserved.Position, Is.EqualTo(Vector3.left));
        }
    }
}
