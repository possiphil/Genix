using System;

namespace Genix.Sampling
{
    /// <summary>Controls how many inexpensive sample positions are generated before validation.</summary>
    [Serializable]
    public struct CandidateSettings
    {
        /// <summary>Candidate count requested per desired object.</summary>
        public int multiplier;
        /// <summary>Lower bound for the generated candidate count.</summary>
        public int minimumCount;
        /// <summary>Whether candidates are randomized before placement planning.</summary>
        public bool shuffle;

        /// <summary>Creates candidate-generation settings.</summary>
        /// <param name="multiplier">Candidates requested per desired object.</param>
        /// <param name="minimumCount">Minimum candidate count.</param>
        /// <param name="shuffle">Whether to randomize candidate order.</param>
        public CandidateSettings(int multiplier, int minimumCount, bool shuffle)
        {
            this.multiplier = multiplier;
            this.minimumCount = minimumCount;
            this.shuffle = shuffle;
        }

        /// <summary>Returns the hard candidate-position budget for a requested object count.</summary>
        /// <param name="requestedCount">Number of objects requested by the caller.</param>
        /// <param name="minimumCandidateCount">
        /// Optional scaled minimum. A negative value uses <see cref="minimumCount"/>.
        /// </param>
        public readonly int GetBudget(int requestedCount, int minimumCandidateCount = -1)
        {
            int count = Math.Max(1, requestedCount);
            int minimum = Math.Max(1, minimumCandidateCount >= 0 ? minimumCandidateCount : minimumCount);
            long scaled = (long)count * Math.Max(1, multiplier);
            long budget = Math.Max(count, Math.Max(minimum, scaled));
            return budget >= int.MaxValue ? int.MaxValue : (int)budget;
        }
    }
}
