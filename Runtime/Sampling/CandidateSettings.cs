using System;

namespace Genix.Sampling
{
    [Serializable] public struct CandidateSettings
    {
        public int multiplier;
        public int minimumCount;
        public bool shuffle;

        public CandidateSettings(int multiplier, int minimumCount, bool shuffle)
        {
            this.multiplier = multiplier;
            this.minimumCount = minimumCount;
            this.shuffle = shuffle;
        }
    }
}