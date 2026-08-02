using System;
using System.Collections.Generic;

namespace Genix.Core
{
    /// <summary>Deterministic per-run pseudo-random stream independent of Unity's global random state.</summary>
    public sealed class GenerationRandom
    {
        private ulong _state;

        /// <summary>Gets the seed from which this stream was initialized.</summary>
        public int Seed { get; }

        /// <summary>Gets or sets the current stream state for deterministic continuation.</summary>
        public ulong State
        {
            get => _state;
            set => _state = value == 0 ? 0x9E3779B97F4A7C15UL : value;
        }

        /// <summary>Gets the next uniformly distributed value in the range [0, 1).</summary>
        public float Value => (NextUInt64() >> 40) / (float)(1UL << 24);

        /// <summary>Initializes a new instance of generation random.</summary>
        public GenerationRandom(int seed)
        {
            Seed = seed;
            State = Mix(unchecked((ulong)(uint)seed) + 0x9E3779B97F4A7C15UL);
        }

        /// <summary>Creates a fixed or newly seeded stream according to request settings.</summary>
        public static GenerationRandom Create(bool useFixedSeed, int seed)
        {
            int effectiveSeed = useFixedSeed
                ? seed
                : unchecked(Environment.TickCount * 397 ^ Guid.NewGuid().GetHashCode());
            return new GenerationRandom(effectiveSeed);
        }

        /// <summary>Returns an integer in the half-open interval [minimumInclusive, maximumExclusive).</summary>
        public int Range(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
                return minimumInclusive;

            uint range = (uint)(maximumExclusive - minimumInclusive);
            return minimumInclusive + (int)(NextUInt64() % range);
        }

        /// <summary>Returns a floating-point value between the supplied bounds.</summary>
        public float Range(float minimumInclusive, float maximumInclusive)
        {
            return minimumInclusive + (maximumInclusive - minimumInclusive) * Value;
        }

        /// <summary>Shuffles a list in place using the Fisher-Yates algorithm and this stream.</summary>
        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int randomIndex = Range(0, i + 1);
                (items[i], items[randomIndex]) = (items[randomIndex], items[i]);
            }
        }

        private ulong NextUInt64()
        {
            ulong value = _state;
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            _state = value;
            return value * 2685821657736338717UL;
        }

        private static ulong Mix(ulong value)
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
