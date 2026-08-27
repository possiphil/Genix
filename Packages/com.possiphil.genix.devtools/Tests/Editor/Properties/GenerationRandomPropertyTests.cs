using System.Collections.Generic;
using FsCheck;
using FsCheck.Fluent;
using Genix.Core;
using Genix.Tests.Framework;
using NUnit.Framework;

namespace Genix.Tests.Properties
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Property)]
    [Category(GenixTestCategories.RandomnessArea)]
    public sealed class GenerationRandomPropertyTests
    {
        [Test]
        public void EqualSeedsAlwaysProduceEqualMixedSequences()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-1_000_000, 1_000_000));

            GenixProperty.Check(
                nameof(EqualSeedsAlwaysProduceEqualMixedSequences),
                Prop.ForAll(seeds, seed =>
                {
                    GenerationRandom first = new(seed);
                    GenerationRandom second = new(seed);

                    for (int i = 0; i < 128; i++)
                    {
                        if (first.Range(-10_000, 10_000) != second.Range(-10_000, 10_000))
                            return false;

                        if (first.Value != second.Value)
                            return false;
                    }

                    return true;
                }));
        }

        [Test]
        public void IntegerRangesRemainHalfOpenForAllValidBounds()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-1_000_000, 1_000_000));

            GenixProperty.Check(
                nameof(IntegerRangesRemainHalfOpenForAllValidBounds),
                Prop.ForAll(seeds, seed =>
                {
                    GenerationRandom source = new(seed);
                    int minimum = source.Range(-10_000, 10_000);
                    int width = source.Range(1, 2048);
                    int maximum = minimum + width;

                    for (int i = 0; i < 256; i++)
                    {
                        int value = source.Range(minimum, maximum);

                        if (value < minimum || value >= maximum)
                            return false;
                    }

                    return true;
                }));
        }

        [Test]
        public void ShufflePreservesEveryElementExactlyOnce()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-1_000_000, 1_000_000));

            GenixProperty.Check(
                nameof(ShufflePreservesEveryElementExactlyOnce),
                Prop.ForAll(seeds, seed =>
                {
                    List<int> values = new();

                    for (int i = 0; i < 128; i++)
                        values.Add(i);

                    new GenerationRandom(seed).Shuffle(values);

                    if (values.Count != 128)
                        return false;

                    values.Sort();

                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i] != i)
                            return false;
                    }

                    return true;
                }));
        }
    }
}
