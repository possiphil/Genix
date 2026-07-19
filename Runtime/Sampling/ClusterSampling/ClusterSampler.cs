using System.Collections.Generic;
using Genix.Diagnostics;
using UnityEngine;

namespace Genix.Sampling.ClusterSampling
{
    internal sealed class ClusterSampler : ISampler
    {
        private const int MaxAttemptsPerCandidate = 32;
        private const int MaxAttemptsPerClusterCenter = 64;

        public List<Vector3> SamplePositions(SamplingContext context)
        {
            List<Vector3> positions = new();

            int clusterCount = Mathf.Max(1, context.Cluster.count);
            float clusterRadius = Mathf.Max(0f, context.Cluster.radius);

            if (context.CandidateCount <= 0 || clusterRadius <= 0f)
                return positions;

            List<Vector3> clusterCenters = CreateClusterCenters(context, clusterCount);

            if (clusterCenters.Count == 0)
                return positions;

            AddBalancedClusterPositions(positions, clusterCenters, context, clusterRadius);

            return positions;
        }

        private static List<Vector3> CreateClusterCenters(SamplingContext context, int clusterCount)
        {
            List<Vector3> centers = new();

            for (int i = 0; i < clusterCount; i++)
            {
                if (TryCreateClusterCenter(context, centers, out Vector3 center))
                {
                    centers.Add(center);
                    context.Diagnostics.RecordClusterCenter(center);
                }
                else
                    break;
            }

            return centers;
        }

        private static bool TryCreateClusterCenter(
            SamplingContext context,
            IReadOnlyList<Vector3> existingCenters,
            out Vector3 center)
        {
            for (int i = 0; i < MaxAttemptsPerClusterCenter; i++)
            {
                center = GetRandomPosition(context);

                if (!context.Cluster.useMinCenterDistance ||
                    HasMinimumDistance(center, existingCenters, context.Cluster.minCenterDistance))
                    return true;
            }

            center = default;
            return false;
        }

        private static bool HasMinimumDistance(Vector3 position, IReadOnlyList<Vector3> existingPositions, float minDistance)
        {
            if (minDistance <= 0f)
                return true;

            float minDistanceSqr = minDistance * minDistance;

            foreach (Vector3 existingPosition in existingPositions)
            {
                float dx = position.x - existingPosition.x;
                float dz = position.z - existingPosition.z;

                if (dx * dx + dz * dz < minDistanceSqr)
                    return false;
            }

            return true;
        }

        private static void AddBalancedClusterPositions(
            List<Vector3> positions,
            IReadOnlyList<Vector3> clusterCenters,
            SamplingContext context,
            float clusterRadius)
        {
            int baseCountPerCluster = context.CandidateCount / clusterCenters.Count;
            int remainingCandidates = context.CandidateCount % clusterCenters.Count;

            for (int i = 0; i < clusterCenters.Count; i++)
            {
                int candidatesForCluster = baseCountPerCluster + (i < remainingCandidates ? 1 : 0);
                AddClusterPositions(positions, clusterCenters[i], context, clusterRadius, candidatesForCluster);
            }
        }

        private static void AddClusterPositions(
            List<Vector3> positions,
            Vector3 center,
            SamplingContext context,
            float clusterRadius,
            int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (TryGetRandomPositionInCluster(center, context, clusterRadius, out Vector3 position))
                    positions.Add(position);
            }
        }

        private static bool TryGetRandomPositionInCluster(
            Vector3 center,
            SamplingContext context,
            float radius,
            out Vector3 position)
        {
            for (int i = 0; i < MaxAttemptsPerCandidate; i++)
            {
                position = GetRandomPositionInDisk(center, context.Bounds.min.y, radius, context);

                if (context.Bounds.Contains(position))
                    return true;
            }

            position = default;
            return false;
        }

        private static Vector3 GetRandomPositionInDisk(
            Vector3 center,
            float y,
            float radius,
            SamplingContext context)
        {
            float angle = context.Random.Range(0f, Mathf.PI * 2f);
            float distance = radius * Mathf.Sqrt(context.Random.Value);

            return new Vector3(center.x + Mathf.Cos(angle) * distance, y, center.z + Mathf.Sin(angle) * distance);
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
