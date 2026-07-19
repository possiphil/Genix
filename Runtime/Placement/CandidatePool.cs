using System.Collections.Generic;

namespace Genix.Placement
{
    public sealed class CandidatePool
    {
        private readonly List<CandidateSeed> _seeds;
        private int _nextIndex;

        public int Count => _seeds.Count - _nextIndex;

        internal CandidatePool(List<CandidateSeed> seeds)
        {
            _seeds = seeds ?? new List<CandidateSeed>();
            _nextIndex = 0;
        }

        internal bool TryTakeNext(out CandidateSeed seed)
        {
            if (_nextIndex >= _seeds.Count)
            {
                seed = default;
                return false;
            }

            seed = _seeds[_nextIndex];
            _nextIndex++;

            return true;
        }
    }
}
