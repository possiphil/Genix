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
    }
}
