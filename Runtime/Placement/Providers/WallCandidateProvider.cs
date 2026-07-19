using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Sampling;
using UnityEngine;

namespace Genix.Placement.Providers
{
    internal sealed class WallCandidateProvider : CandidateProviderBase
    {
        private const float MinValue = 0.001f;

        public override List<CandidateSeed> CreateCandidateSeeds(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null)
        {
            List<CandidateSeed> seeds = new();

            WallSegment[] walls = CreateWallLines(context);
            float perimeterLength = GetPerimeterLength(walls);

            if (perimeterLength <= 0f)
                return seeds;

            List<Vector3> debugClusterCenters = new();
            List<float> distances = CreatePerimeterDistances(
                context,
                walls,
                perimeterLength,
                debugClusterCenters);

            foreach (float distance in distances)
            {
                if (!TryGetWallAtDistance(walls, distance, perimeterLength, out WallSegment wall, out float wallDistance))
                    continue;

                Vector3 worldPosition = wall.Start + wall.Direction * wallDistance;

                if (!context.Area.TryProjectToWall(worldPosition, wall.InwardNormal, wall.VoxelLayer, out SurfacePoint surfacePoint))
                    continue;

                Quaternion rotation = Quaternion.LookRotation(surfacePoint.Normal, Vector3.up);
                AddSeed(
                    seeds,
                    surfacePoint.Position,
                    rotation,
                    surfacePoint.SurfaceCollider,
                    surfacePoint.Normal,
                    surfacePoint.VoxelLayer,
                    PlacementType.Wall);
            }

            diagnostics.RecordClusterCenters(debugClusterCenters);

            ShuffleIfNeeded(seeds, context);
            return seeds;
        }

        private static WallSegment[] CreateWallLines(GenerationContext context)
        {
            if (context.Area.WallRegions.Count > 0)
            {
                WallSegment[] areaWalls = new WallSegment[context.Area.WallRegions.Count];

                for (int i = 0; i < context.Area.WallRegions.Count; i++)
                {
                    Genix.Areas.SurfaceRegion region = context.Area.WallRegions[i];
                    areaWalls[i] = new WallSegment(region.WallStart, region.WallEnd, region.Normal, region.VoxelLayer);
                }

                return areaWalls;
            }

            Bounds bounds = context.TargetBounds;
            float y = bounds.min.y;

            return new[]
            {
                new WallSegment(new Vector3(bounds.min.x, y, bounds.min.z), new Vector3(bounds.max.x, y, bounds.min.z), Vector3.forward),
                new WallSegment(new Vector3(bounds.max.x, y, bounds.min.z), new Vector3(bounds.max.x, y, bounds.max.z), Vector3.left),
                new WallSegment(new Vector3(bounds.max.x, y, bounds.max.z), new Vector3(bounds.min.x, y, bounds.max.z), Vector3.back),
                new WallSegment(new Vector3(bounds.min.x, y, bounds.max.z), new Vector3(bounds.min.x, y, bounds.min.z), Vector3.right)
            };
        }

        private static List<float> CreatePerimeterDistances(GenerationContext context, WallSegment[] walls, float perimeterLength, List<Vector3> debugClusterCenters)
        {
            return context.StyleSettings.algorithm switch
            {
                SamplingAlgorithm.Random => CreateRandomDistances(context, perimeterLength),
                SamplingAlgorithm.Grid => CreateGridDistances(context, perimeterLength, false),
                SamplingAlgorithm.JitteredGrid => CreateGridDistances(context, perimeterLength, true),
                SamplingAlgorithm.Cluster => CreateClusterDistances(context, walls, perimeterLength, debugClusterCenters),
                SamplingAlgorithm.BridsonPoissonDisk => CreatePoissonDistances(context, perimeterLength),
                _ => CreateRandomDistances(context, perimeterLength)
            };
        }

        private static List<float> CreateRandomDistances(GenerationContext context, float perimeterLength)
        {
            int targetCount = GetTargetSeedCount(context);
            List<float> distances = new();

            for (int i = 0; i < targetCount; i++)
                distances.Add(context.Random.Range(0f, perimeterLength));

            return distances;
        }

        private static List<float> CreateGridDistances(
            GenerationContext context,
            float perimeterLength,
            bool jittered)
        {
            float cellSize = Mathf.Max(MinValue, context.StyleSettings.grid.cellSize);
            float jitterAmount = jittered ? Mathf.Clamp01(context.StyleSettings.grid.jitterAmount) : 0f;

            List<float> distances = new();

            for (float distance = cellSize / 2f; distance < perimeterLength; distance += cellSize)
            {
                float jitter = context.Random.Range(-cellSize * jitterAmount / 2f, cellSize * jitterAmount / 2f);
                distances.Add(WrapDistance(distance + jitter, perimeterLength));
            }

            EnsureMinimumEvenlySpacedDistances(distances, GetTargetSeedCount(context), perimeterLength);
            return distances;
        }

        private static List<float> CreateClusterDistances(
            GenerationContext context,
            WallSegment[] walls,
            float perimeterLength,
            List<Vector3> debugClusterCenters)
        {
            int targetCount = GetTargetSeedCount(context);
            float radius = Mathf.Max(MinValue, context.StyleSettings.cluster.radius);

            List<float> clusterCenters = CreateClusterCenterDistances(context, perimeterLength);
            List<float> distances = new();

            foreach (float centerDistance in clusterCenters)
            {
                if (TryGetWallAtDistance(walls, centerDistance, perimeterLength, out WallSegment wall, out float wallDistance))
                    debugClusterCenters.Add(wall.Start + wall.Direction * wallDistance);
            }

            for (int i = 0; i < targetCount; i++)
            {
                float center = clusterCenters[i % clusterCenters.Count];
                float offset = context.Random.Range(-radius, radius);

                distances.Add(WrapDistance(center + offset, perimeterLength));
            }

            return distances;
        }

        private static List<float> CreateClusterCenterDistances(
            GenerationContext context,
            float perimeterLength)
        {
            int clusterCount = Mathf.Max(1, context.StyleSettings.cluster.count);
            List<float> centers = new();

            if (!context.StyleSettings.cluster.useMinCenterDistance)
            {
                for (int i = 0; i < clusterCount; i++)
                    centers.Add(context.Random.Range(0f, perimeterLength));

                return centers;
            }

            float minDistance = Mathf.Max(
                MinValue,
                context.StyleSettings.cluster.minCenterDistance);

            int maxUsefulCenters = Mathf.Max(
                1,
                Mathf.FloorToInt(perimeterLength / minDistance));

            clusterCount = Mathf.Min(clusterCount, maxUsefulCenters);

            int maxAttempts = Mathf.Max(clusterCount * 32, 128);

            for (int i = 0; i < maxAttempts && centers.Count < clusterCount; i++)
            {
                float center = context.Random.Range(0f, perimeterLength);

                if (IsFarEnoughFromClusterCenters(center, centers, minDistance, perimeterLength))
                    centers.Add(center);
            }

            AddFallbackClusterCenters(centers, clusterCount, perimeterLength, minDistance);

            return centers;
        }

        private static void AddFallbackClusterCenters(
            List<float> centers,
            int clusterCount,
            float perimeterLength,
            float minDistance)
        {
            if (centers.Count >= clusterCount)
                return;

            float step = perimeterLength / clusterCount;

            for (int i = 0; i < clusterCount && centers.Count < clusterCount; i++)
            {
                float center = WrapDistance(step * i + step / 2f, perimeterLength);

                if (IsFarEnoughFromClusterCenters(center, centers, minDistance, perimeterLength))
                    centers.Add(center);
            }
        }

        private static bool IsFarEnoughFromClusterCenters(
            float center,
            List<float> existingCenters,
            float minDistance,
            float perimeterLength)
        {
            foreach (float existingCenter in existingCenters)
            {
                float delta = Mathf.Abs(center - existingCenter);
                float circularDelta = Mathf.Min(delta, perimeterLength - delta);

                if (circularDelta < minDistance)
                    return false;
            }

            return true;
        }

        private static List<float> CreatePoissonDistances(GenerationContext context, float perimeterLength)
        {
            float minDistance = Mathf.Max(MinValue, context.StyleSettings.poisson.minDistance);
            int attempts = Mathf.Max(1, context.StyleSettings.poisson.attempts);

            int targetCount = GetTargetSeedCount(context);
            int maxUsefulCount = Mathf.Max(1, Mathf.FloorToInt(perimeterLength / minDistance));

            targetCount = Mathf.Min(targetCount, maxUsefulCount);

            List<float> distances = new();
            int maxAttempts = Mathf.Max(targetCount * attempts * 16, 128);

            for (int i = 0; i < maxAttempts && distances.Count < targetCount; i++)
            {
                float distance = context.Random.Range(0f, perimeterLength);
                TryAddPoissonDistance(distances, distance, minDistance, perimeterLength);
            }

            AddPoissonFallbackDistances(distances, targetCount, minDistance, perimeterLength);

            return distances;
        }

        private static void EnsureMinimumEvenlySpacedDistances(
            List<float> distances,
            int targetCount,
            float perimeterLength)
        {
            if (distances.Count >= targetCount)
                return;

            float step = perimeterLength / targetCount;

            for (int i = 0; i < targetCount && distances.Count < targetCount; i++)
                distances.Add(WrapDistance(step * i + step / 2f, perimeterLength));
        }

        private static void AddPoissonFallbackDistances(
            List<float> distances,
            int targetCount,
            float minDistance,
            float perimeterLength)
        {
            if (distances.Count >= targetCount)
                return;

            float step = perimeterLength / targetCount;

            for (int i = 0; i < targetCount && distances.Count < targetCount; i++)
            {
                float distance = WrapDistance(step * i, perimeterLength);
                TryAddPoissonDistance(distances, distance, minDistance, perimeterLength);
            }
        }

        private static bool TryAddPoissonDistance(
            List<float> distances,
            float distance,
            float minDistance,
            float perimeterLength)
        {
            foreach (float existingDistance in distances)
            {
                float delta = Mathf.Abs(distance - existingDistance);
                float circularDelta = Mathf.Min(delta, perimeterLength - delta);

                if (circularDelta < minDistance)
                    return false;
            }

            distances.Add(WrapDistance(distance, perimeterLength));
            return true;
        }

        private static bool TryGetWallAtDistance(
            WallSegment[] walls,
            float perimeterDistance,
            float perimeterLength,
            out WallSegment wall,
            out float wallDistance)
        {
            float remainingDistance = WrapDistance(perimeterDistance, perimeterLength);

            foreach (WallSegment currentWall in walls)
            {
                if (remainingDistance <= currentWall.Length)
                {
                    wall = currentWall;
                    wallDistance = remainingDistance;
                    return true;
                }

                remainingDistance -= currentWall.Length;
            }

            wall = default;
            wallDistance = 0f;
            return false;
        }

        private static float GetPerimeterLength(WallSegment[] walls)
        {
            float length = 0f;

            foreach (WallSegment wall in walls)
                length += wall.Length;

            return length;
        }

        private static int GetTargetSeedCount(GenerationContext context)
        {
            int multipliedCount = Mathf.CeilToInt(
                context.Count * context.StyleSettings.candidates.multiplier);

            return Mathf.Max(
                context.Count,
                Mathf.Max(multipliedCount, context.StyleSettings.candidates.minimumCount));
        }

        private static float WrapDistance(float distance, float length)
        {
            if (length <= 0f)
                return 0f;

            return Mathf.Repeat(distance, length);
        }
    }
}
