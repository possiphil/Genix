using System;
using System.Collections.Generic;

namespace Genix.Placement
{
    public sealed class CandidatePool
    {
        private readonly List<CandidateSeed> _seeds;
        private readonly Func<List<CandidateSeed>> _loadMore;
        private readonly int _maxLoadCount;
        private int _nextIndex;
        private int _loadCount;
        private bool _exhausted;

        public int Count
        {
            get
            {
                EnsureAvailable();
                return _seeds.Count - _nextIndex;
            }
        }

        internal CandidatePool(List<CandidateSeed> seeds)
        {
            _seeds = seeds ?? new List<CandidateSeed>();
            _nextIndex = 0;
            _exhausted = true;
        }

        internal CandidatePool(Func<List<CandidateSeed>> loadMore, int maxLoadCount)
        {
            _seeds = new List<CandidateSeed>();
            _loadMore = loadMore;
            _maxLoadCount = Math.Max(1, maxLoadCount);
            _nextIndex = 0;
        }

        internal bool TryTakeNext(out CandidateSeed seed)
        {
            EnsureAvailable();

            if (_nextIndex >= _seeds.Count)
            {
                seed = default;
                return false;
            }

            seed = _seeds[_nextIndex];
            _nextIndex++;

            return true;
        }

        private void EnsureAvailable()
        {
            while (_nextIndex >= _seeds.Count && !_exhausted)
                LoadNextBatch();
        }

        private void LoadNextBatch()
        {
            if (_loadMore == null || _loadCount >= _maxLoadCount)
            {
                _exhausted = true;
                return;
            }

            _loadCount++;
            List<CandidateSeed> seeds = _loadMore();

            if (seeds is { Count: > 0 })
                _seeds.AddRange(seeds);

            if (_loadCount >= _maxLoadCount)
                _exhausted = true;
        }
    }
}
