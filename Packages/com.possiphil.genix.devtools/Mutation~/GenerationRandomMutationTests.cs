using System.Collections.Generic;
using System.Linq;
using Genix.Core;
using Genix.Extensions;
using NUnit.Framework;

namespace Genix.Mutation.Tests
{
    public sealed class GenerationRandomMutationTests
    {
        private enum DisplayValue
        {
            A,
            _LEADING_VALUE,
            TRAILING_,
            A__B,
            XMLParser,
            FIRST_VALUE,
            secondValue
        }

        [TestCase(-1000)]
        [TestCase(0)]
        [TestCase(42)]
        [TestCase(int.MaxValue)]
        public void EqualSeedsProduceEqualSequences(int seed)
        {
            GenerationRandom first = new(seed);
            GenerationRandom second = new(seed);

            for (int i = 0; i < 256; i++)
            {
                Assert.That(first.Value, Is.EqualTo(second.Value));
                Assert.That(first.Range(-512, 2048), Is.EqualTo(second.Range(-512, 2048)));
            }
        }

        [TestCase(0, 0xE220A8397B1DCDAFUL)]
        [TestCase(1, 0x910A2DEC89025CC1UL)]
        [TestCase(42, 0xBDD732262FEB6E95UL)]
        [TestCase(-1, 0x73B13BA2AFF181C0UL)]
        public void SeedInitializationMatchesApprovedSequence(int seed, ulong expectedState)
        {
            GenerationRandom random = new(seed);

            Assert.That(random.Seed, Is.EqualTo(seed));
            Assert.That(random.State, Is.EqualTo(expectedState));
        }

        [Test]
        public void GeneratedValuesMatchApprovedSequence()
        {
            GenerationRandom random = new(42);
            float[] expectedValues =
            {
                0.194105863571167f,
                0.5626317858695984f,
                0.48610609769821167f,
                0.2711055278778076f,
                0.8036677837371826f,
                0.5820214748382568f
            };
            ulong[] expectedStates =
            {
                0x17C7FC77B3761E8AUL,
                0x072E9A5B47DE629FUL,
                0x62EA3C804D2D8EFBUL,
                0x25E6409F7FE14F5BUL,
                0xD30833838910C272UL,
                0xD26753BC01C227B7UL
            };

            for (int i = 0; i < expectedValues.Length; i++)
            {
                Assert.That(random.Value, Is.EqualTo(expectedValues[i]));
                Assert.That(random.State, Is.EqualTo(expectedStates[i]));
            }
        }

        [Test]
        public void IntegerRangesMatchApprovedSequence()
        {
            GenerationRandom random = new(42);
            int[] expected = { -1, 10, -2, -2, 21, 18, -16, 4 };

            int[] actual = Enumerable.Range(0, expected.Length)
                .Select(_ => random.Range(-17, 29))
                .ToArray();

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void FloatingPointRangeUsesTheRequestedSpan()
        {
            GenerationRandom random = new(12345);

            Assert.That(random.Range(-2.5f, 9.25f), Is.EqualTo(0.8014582f).Within(0.000001f));
        }

        [Test]
        public void FactoryHonorsFixedAndGeneratedSeedModes()
        {
            const int requestedSeed = 123456789;

            Assert.That(GenerationRandom.Create(true, requestedSeed).Seed, Is.EqualTo(requestedSeed));
            Assert.That(GenerationRandom.Create(false, requestedSeed).Seed, Is.Not.EqualTo(requestedSeed));
        }

        [Test]
        public void ValuesAndRangesRespectTheirContracts()
        {
            GenerationRandom random = new(12345);

            for (int i = 0; i < 10_000; i++)
            {
                Assert.That(random.Value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(random.Range(-17, 29), Is.GreaterThanOrEqualTo(-17).And.LessThan(29));
                Assert.That(random.Range(-2.5f, 9.25f), Is.GreaterThanOrEqualTo(-2.5f).And.LessThanOrEqualTo(9.25f));
            }
        }

        [Test]
        public void InvalidIntegerRangeReturnsMinimum()
        {
            GenerationRandom random = new(7);

            Assert.That(random.Range(10, 10), Is.EqualTo(10));
            Assert.That(random.Range(10, -5), Is.EqualTo(10));
        }

        [Test]
        public void RestoredStateReplaysTheNextValue()
        {
            GenerationRandom random = new(91);
            random.Range(0, 100);
            ulong state = random.State;
            float expected = random.Value;
            random.State = state;

            Assert.That(random.Value, Is.EqualTo(expected));
            random.State = 0;
            Assert.That(random.State, Is.Not.Zero);
        }

        [Test]
        public void ShufflePreservesElements()
        {
            List<int> values = new();

            for (int i = 0; i < 100; i++)
                values.Add(i);

            new GenerationRandom(123).Shuffle(values);
            values.Sort();

            Assert.That(values, Is.EqualTo(CreateOrderedValues()));
        }

        [Test]
        public void ShuffleMatchesApprovedFisherYatesSequence()
        {
            List<int> values = Enumerable.Range(0, 10).ToList();

            new GenerationRandom(123).Shuffle(values);

            Assert.That(values, Is.EqualTo(new[] { 4, 9, 0, 7, 5, 8, 1, 2, 6, 3 }));
        }

        [Test]
        public void EnumDisplayNamesRemainHumanReadable()
        {
            Assert.That(DisplayValue.A.ToDisplayName(), Is.EqualTo("A"));
            Assert.That(DisplayValue._LEADING_VALUE.ToDisplayName(), Is.EqualTo("Leading Value"));
            Assert.That(DisplayValue.TRAILING_.ToDisplayName(), Is.EqualTo("Trailing"));
            Assert.That(DisplayValue.A__B.ToDisplayName(), Is.EqualTo("A B"));
            Assert.That(DisplayValue.XMLParser.ToDisplayName(), Is.EqualTo("Xml Parser"));
            Assert.That(DisplayValue.FIRST_VALUE.ToDisplayName(), Is.EqualTo("First Value"));
            Assert.That(DisplayValue.secondValue.ToDisplayName(), Is.EqualTo("Second Value"));
        }

        private static IEnumerable<int> CreateOrderedValues()
        {
            for (int i = 0; i < 100; i++)
                yield return i;
        }
    }
}
