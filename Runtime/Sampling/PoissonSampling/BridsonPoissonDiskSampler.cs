using System.Collections.Generic;
using UnityEngine;

namespace Genix.Sampling.PoissonSampling
{
    internal sealed class BridsonPoissonDiskSampler : ISampler
    {
        private const float OuterRadiusFactor = 2f;

        public List<Vector3> SamplePositions(SamplingContext context)
        {
            List<Vector3> points = new();

            if (context.CandidateCount <= 0)
                return points;

            if (context.Poisson.minDistance <= 0f)
                return points;

            if (context.Poisson.attempts <= 0)
                return points;

            PoissonGrid grid = new(context.Bounds, context.Poisson.minDistance);
            List<Vector3> activePoints = new();

            AddPoint(GetRandomPosition(context), points, activePoints, grid);

            while (activePoints.Count > 0 && points.Count < context.CandidateCount)
            {
                int activeIndex = context.Random.Range(0, activePoints.Count);
                Vector3 activePoint = activePoints[activeIndex];

                if (TryCreatePointAround(activePoint, context, grid, out Vector3 newPoint))
                    AddPoint(newPoint, points, activePoints, grid);
                else
                    activePoints.RemoveAt(activeIndex);
            }

            return points;
        }

        private static bool TryCreatePointAround(Vector3 center, SamplingContext context, PoissonGrid grid, out Vector3 point)
        {
            for (int i = 0; i < context.Poisson.attempts; i++)
            {
                Vector3 candidate = CreateCandidateAround(center, context);

                if (IsValidCandidate(candidate, context.Bounds, context.Poisson.minDistance, grid))
                {
                    point = candidate;
                    return true;
                }
            }

            point = default;
            return false;
        }

        private static Vector3 CreateCandidateAround(Vector3 center, SamplingContext context)
        {
            float angle = context.Random.Range(0f, Mathf.PI * 2f);
            float distance = context.Random.Range(context.Poisson.minDistance, context.Poisson.minDistance * OuterRadiusFactor);

            return new Vector3(center.x + Mathf.Cos(angle) * distance, context.Bounds.min.y, center.z + Mathf.Sin(angle) * distance);
        }

        private static bool IsValidCandidate(Vector3 candidate, Bounds bounds, float minDistance, PoissonGrid grid)
        {
            return bounds.Contains(candidate) && grid.IsFarEnough(candidate, minDistance);
        }

        private static void AddPoint(Vector3 point, List<Vector3> points, List<Vector3> activePoints, PoissonGrid grid)
        {
            points.Add(point);
            activePoints.Add(point);
            grid.Add(point);
        }

        private static Vector3 GetRandomPosition(SamplingContext context)
        {
            Bounds bounds = context.Bounds;
            return new Vector3(
                context.Random.Range(bounds.min.x, bounds.max.x),
                bounds.min.y,
                context.Random.Range(bounds.min.z, bounds.max.z));
        }
    }
}
