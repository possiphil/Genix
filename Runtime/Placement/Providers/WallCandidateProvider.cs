using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Profiling;
using Genix.Sampling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Placement.Providers
{
    internal sealed class WallCandidateProvider : CandidateProviderBase
    {
        private const float MinValue = 0.001f;

        public WallCandidateProvider(
            int requestedCount = -1,
            int minimumCandidateCount = -1,
            int candidateCount = -1)
            : base(requestedCount, minimumCandidateCount, candidateCount)
        {
        }

        public override List<CandidateSeed> CreateCandidateSeeds(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null,
            IGenerationProfiler profiler = null)
        {
            profiler ??= NullGenerationProfiler.Instance;
            Stopwatch providerStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            List<CandidateSeed> seeds = new();

            WallSegment[] walls = CreateWallLines(context);
            WallSegmentLookup wallLookup = new(walls);
            float perimeterLength = wallLookup.PerimeterLength;

            if (perimeterLength <= 0f)
            {
                profiler.AddSeedGenerationTime(PlacementType.Wall, StopAndReadMilliseconds(providerStopwatch));
                return seeds;
            }

            List<Vector3> debugClusterCenters = new();
            Stopwatch samplingStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            int targetSeedCount = GetCandidateCount(context);
            List<float> distances = CreatePerimeterDistances(
                context,
                wallLookup,
                perimeterLength,
                debugClusterCenters,
                targetSeedCount);
            profiler.AddSamplingTime(PlacementType.Wall, StopAndReadMilliseconds(samplingStopwatch));
            profiler.RecordRawSamples(PlacementType.Wall, distances.Count);

            foreach (float distance in distances)
            {
                if (!wallLookup.TryGetAtDistance(distance, out WallSegment wall, out float wallDistance))
                    continue;

                Vector3 worldPosition = wall.Start + wall.Direction * wallDistance;

                Stopwatch projectionStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                bool projected = context.Area.TryProjectToWall(
                    worldPosition,
                    wall.InwardNormal,
                    wall.VoxelLayer,
                    out SurfacePoint surfacePoint,
                    profiler);
                projectionStopwatch?.Stop();
                profiler.RecordProjection(
                    PlacementType.Wall,
                    projected,
                    projectionStopwatch != null ? (float)projectionStopwatch.Elapsed.TotalMilliseconds : 0f);

                if (!projected)
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
                profiler.RecordCandidateSeeds(PlacementType.Wall, 1);
            }

            diagnostics.RecordClusterCenters(debugClusterCenters);

            ShuffleIfNeeded(seeds, context);
            profiler.AddSeedGenerationTime(PlacementType.Wall, StopAndReadMilliseconds(providerStopwatch));
            return seeds;
        }

        private static float StopAndReadMilliseconds(Stopwatch stopwatch)
        {
            if (stopwatch == null)
                return 0f;

            stopwatch.Stop();
            return (float)stopwatch.Elapsed.TotalMilliseconds;
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

        private static List<float> CreatePerimeterDistances(
            GenerationContext context,
            WallSegmentLookup wallLookup,
            float perimeterLength,
            List<Vector3> debugClusterCenters,
            int targetCount)
        {
            return context.StyleSettings.algorithm switch
            {
                SamplingAlgorithm.Random => CreateRandomDistances(context, perimeterLength, targetCount),
                SamplingAlgorithm.Grid => CreateGridDistances(context, perimeterLength, false, targetCount),
                SamplingAlgorithm.JitteredGrid => CreateGridDistances(context, perimeterLength, true, targetCount),
                SamplingAlgorithm.Cluster => CreateClusterDistances(context, wallLookup, perimeterLength, debugClusterCenters, targetCount),
                SamplingAlgorithm.BridsonPoissonDisk => CreatePoissonDistances(context, perimeterLength, targetCount),
                _ => CreateRandomDistances(context, perimeterLength, targetCount)
            };
        }

        private static List<float> CreateRandomDistances(
            GenerationContext context,
            float perimeterLength,
            int targetCount)
        {
            List<float> distances = new();

            for (int i = 0; i < targetCount; i++)
                distances.Add(context.Random.Range(0f, perimeterLength));

            return distances;
        }

        private static List<float> CreateGridDistances(
            GenerationContext context,
            float perimeterLength,
            bool jittered,
            int targetCount)
        {
            float cellSize = Mathf.Max(MinValue, context.StyleSettings.grid.cellSize);
            float jitterAmount = jittered ? Mathf.Clamp01(context.StyleSettings.grid.jitterAmount) : 0f;

            List<float> distances = new();

            for (float distance = cellSize / 2f; distance < perimeterLength; distance += cellSize)
            {
                float jitter = context.Random.Range(-cellSize * jitterAmount / 2f, cellSize * jitterAmount / 2f);
                distances.Add(WrapDistance(distance + jitter, perimeterLength));
            }

            EnsureMinimumEvenlySpacedDistances(distances, targetCount, perimeterLength);
            return distances;
        }

        private static List<float> CreateClusterDistances(
            GenerationContext context,
            WallSegmentLookup wallLookup,
            float perimeterLength,
            List<Vector3> debugClusterCenters,
            int targetCount)
        {
            float radius = Mathf.Max(MinValue, context.StyleSettings.cluster.radius);

            List<float> clusterCenters = CreateClusterCenterDistances(context, perimeterLength);
            List<float> distances = new();

            foreach (float centerDistance in clusterCenters)
            {
                if (wallLookup.TryGetAtDistance(centerDistance, out WallSegment wall, out float wallDistance))
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

        private static List<float> CreatePoissonDistances(
            GenerationContext context,
            float perimeterLength,
            int targetCount)
        {
            float minDistance = Mathf.Max(MinValue, context.StyleSettings.poisson.minDistance);
            int maxUsefulCount = Mathf.Max(1, Mathf.FloorToInt(perimeterLength / minDistance));

            targetCount = Mathf.Min(targetCount, maxUsefulCount);

            List<float> distances = new(targetCount);

            if (targetCount <= 0)
                return distances;

            float step = perimeterLength / targetCount;
            float jitterRadius = Mathf.Max(0f, (step - minDistance) * 0.5f);
            float offset = context.Random.Range(0f, step);

            for (int i = 0; i < targetCount; i++)
            {
                float jitter = jitterRadius > 0f
                    ? context.Random.Range(-jitterRadius, jitterRadius)
                    : 0f;
                distances.Add(WrapDistance(offset + step * i + jitter, perimeterLength));
            }

            context.Random.Shuffle(distances);
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

        private static float WrapDistance(float distance, float length)
        {
            if (length <= 0f)
                return 0f;

            return Mathf.Repeat(distance, length);
        }

        private readonly struct WallSegmentLookup
        {
            private readonly WallSegment[] _walls;
            private readonly float[] _cumulativeLengths;

            public float PerimeterLength { get; }

            public WallSegmentLookup(WallSegment[] walls)
            {
                _walls = walls ?? Array.Empty<WallSegment>();
                _cumulativeLengths = new float[_walls.Length];
                float length = 0f;

                for (int i = 0; i < _walls.Length; i++)
                {
                    length += Mathf.Max(0f, _walls[i].Length);
                    _cumulativeLengths[i] = length;
                }

                PerimeterLength = length;
            }

            public bool TryGetAtDistance(
                float perimeterDistance,
                out WallSegment wall,
                out float wallDistance)
            {
                if (_walls == null || _walls.Length == 0 || PerimeterLength <= 0f)
                {
                    wall = default;
                    wallDistance = 0f;
                    return false;
                }

                float distance = WrapDistance(perimeterDistance, PerimeterLength);
                int index = Array.BinarySearch(_cumulativeLengths, distance);

                if (index < 0)
                    index = ~index;

                if (index >= _walls.Length)
                    index = _walls.Length - 1;

                float previousLength = index > 0 ? _cumulativeLengths[index - 1] : 0f;
                wall = _walls[index];
                wallDistance = Mathf.Clamp(distance - previousLength, 0f, wall.Length);
                return wall.Length > 0f;
            }
        }
    }
}
