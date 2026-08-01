using System.Collections.Generic;
using UnityEngine;

namespace Genix.Sampling.PoissonSampling
{
    internal sealed class ProgressiveBridsonPoissonDiskSampler
    {
        private const float OuterRadiusFactor = 2f;
        private const int MinimumGlobalAttempts = 8;
        private const int MaximumGlobalAttempts = 96;

        private readonly SamplingContext _context;
        private readonly PoissonGrid _grid;
        private readonly List<Vector3> _activePoints = new();
        private bool _exhausted;

        public ProgressiveBridsonPoissonDiskSampler(SamplingContext context)
        {
            _context = context;
            _grid = IsUsable(context)
                ? new PoissonGrid(context.Bounds, context.Poisson.minDistance)
                : null;
            _exhausted = _grid == null;
        }

        public List<Vector3> SamplePositions(int count)
        {
            List<Vector3> points = new();

            if (count <= 0 || _exhausted)
                return points;

            while (!_exhausted && points.Count < count)
            {
                if (TryAddGlobalPoint(points))
                    continue;

                if (TryAddLocalPoint(points))
                    continue;

                _exhausted = true;
            }

            return points;
        }

        private bool TryAddGlobalPoint(ICollection<Vector3> points)
        {
            int attempts = GetGlobalAttemptCount();

            for (int i = 0; i < attempts; i++)
            {
                Vector3 candidate = GetRandomPosition();

                if (!IsValidCandidate(candidate))
                    continue;

                AddPoint(candidate, points);
                return true;
            }

            return false;
        }

        private bool TryAddLocalPoint(ICollection<Vector3> points)
        {
            while (_activePoints.Count > 0)
            {
                int activeIndex = _context.Random.Range(0, _activePoints.Count);
                Vector3 activePoint = _activePoints[activeIndex];

                if (TryCreatePointAround(activePoint, out Vector3 newPoint))
                {
                    AddPoint(newPoint, points);
                    return true;
                }

                _activePoints.RemoveAt(activeIndex);
            }

            return false;
        }

        private bool TryCreatePointAround(Vector3 center, out Vector3 point)
        {
            for (int i = 0; i < _context.Poisson.attempts; i++)
            {
                Vector3 candidate = CreateCandidateAround(center);

                if (IsValidCandidate(candidate))
                {
                    point = candidate;
                    return true;
                }
            }

            point = default;
            return false;
        }

        private Vector3 CreateCandidateAround(Vector3 center)
        {
            float angle = _context.Random.Range(0f, Mathf.PI * 2f);
            float distance = _context.Random.Range(
                _context.Poisson.minDistance,
                _context.Poisson.minDistance * OuterRadiusFactor);

            return new Vector3(
                center.x + Mathf.Cos(angle) * distance,
                _context.Bounds.min.y,
                center.z + Mathf.Sin(angle) * distance);
        }

        private bool IsValidCandidate(Vector3 candidate) =>
            _context.Bounds.Contains(candidate) &&
            _grid.IsFarEnough(candidate, _context.Poisson.minDistance);

        private void AddPoint(Vector3 point, ICollection<Vector3> points)
        {
            points.Add(point);
            _activePoints.Add(point);
            _grid.Add(point);
        }

        private Vector3 GetRandomPosition()
        {
            Bounds bounds = _context.Bounds;
            return new Vector3(
                _context.Random.Range(bounds.min.x, bounds.max.x),
                bounds.min.y,
                _context.Random.Range(bounds.min.z, bounds.max.z));
        }

        private int GetGlobalAttemptCount()
        {
            return Mathf.Clamp(_context.Poisson.attempts, MinimumGlobalAttempts, MaximumGlobalAttempts);
        }

        private static bool IsUsable(SamplingContext context) =>
            context.Poisson.minDistance > 0f &&
            context.Poisson.attempts > 0 &&
            context.Bounds.size.x > 0f &&
            context.Bounds.size.z > 0f;
    }
}
