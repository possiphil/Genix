using Genix.Core;
using Genix.Tests.Framework;
using NUnit.Framework;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.RandomnessArea)]
    public sealed class GenerationRandomTests
    {
        [Test]
        public void SameSeedProducesSameSequence()
        {
            GenerationRandom first = new(12345);
            GenerationRandom second = new(12345);

            for (int i = 0; i < 100; i++)
                Assert.That(first.Range(-1000, 1000), Is.EqualTo(second.Range(-1000, 1000)));
        }

        [Test]
        public void ExposesEffectiveSeed()
        {
            GenerationRandom random = GenerationRandom.Create(true, 12345);

            Assert.That(random.Seed, Is.EqualTo(12345));
        }

        [Test]
        public void RestoredStateContinuesSameSequence()
        {
            GenerationRandom random = new(42);
            random.Range(0, 100);
            ulong state = random.State;
            float expected = random.Value;

            random.State = state;

            Assert.That(random.Value, Is.EqualTo(expected));
        }
    }
}
