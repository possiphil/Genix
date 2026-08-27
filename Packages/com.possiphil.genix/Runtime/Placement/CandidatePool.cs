using System;
using System.Collections.Generic;

namespace Genix.Placement
{
    /// <summary>Provides sequential, optionally lazy access to candidate seeds.</summary>
    public sealed class CandidatePool
    {
        private readonly List<CandidateSeed> _seeds;
        private readonly Func<List<CandidateSeed>> _loadMore;
        private readonly int _maxLoadCount;
        private readonly int _maxSeedCount;
        private int _nextIndex;
        private int _loadCount;
        private bool _exhausted;
        private bool _budgetExhausted;

        /// <summary>Gets the number of currently available unconsumed seeds.</summary>
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
            _maxSeedCount = _seeds.Count;
            _nextIndex = 0;
            _exhausted = true;
        }

        internal CandidatePool(Func<List<CandidateSeed>> loadMore, int maxLoadCount, int maxSeedCount = int.MaxValue)
        {
            _seeds = new List<CandidateSeed>();
            _loadMore = loadMore;
            _maxLoadCount = Math.Max(1, maxLoadCount);
            _maxSeedCount = Math.Max(1, maxSeedCount);
            _nextIndex = 0;
        }

        /// <summary>Gets the maximum number of candidate seeds this pool may retain.</summary>
        internal int CandidateBudget => _maxSeedCount;

        /// <summary>Gets the number of candidate seeds generated into this pool.</summary>
        internal int GeneratedCount => _seeds.Count;

        /// <summary>Indicates that loading stopped because the configured candidate budget was reached.</summary>
        internal bool BudgetExhausted => _budgetExhausted;

        internal bool TryTakeNext(out CandidateSeed seed)
        {
            return TryTakeNext(null, out seed);
        }

        internal bool TryTakeNext(Predicate<CandidateSeed> predicate, out CandidateSeed seed)
        {
            if (predicate == null)
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

            while (true)
            {
                for (int i = _nextIndex; i < _seeds.Count; i++)
                {
                    if (!predicate(_seeds[i]))
                        continue;

                    seed = _seeds[i];
                    _seeds[i] = _seeds[_nextIndex];
                    _seeds[_nextIndex] = seed;
                    _nextIndex++;
                    return true;
                }

                if (_exhausted)
                {
                    seed = default;
                    return false;
                }

                LoadNextBatch();
            }
        }

        private void EnsureAvailable()
        {
            while (_nextIndex >= _seeds.Count && !_exhausted)
                LoadNextBatch();
        }

        private void LoadNextBatch()
        {
            if (_seeds.Count >= _maxSeedCount)
            {
                _budgetExhausted = true;
                _exhausted = true;
                return;
            }

            if (_loadMore == null || _loadCount >= _maxLoadCount)
            {
                _exhausted = true;
                return;
            }

            _loadCount++;
            List<CandidateSeed> seeds = _loadMore();

            if (seeds is { Count: > 0 })
            {
                int remaining = _maxSeedCount - _seeds.Count;
                int addCount = Math.Min(remaining, seeds.Count);

                for (int i = 0; i < addCount; i++)
                    _seeds.Add(seeds[i]);

                if (_seeds.Count >= _maxSeedCount)
                {
                    _budgetExhausted = true;
                    _exhausted = true;
                }
            }

            if (_loadCount >= _maxLoadCount)
                _exhausted = true;
        }
    }
}
