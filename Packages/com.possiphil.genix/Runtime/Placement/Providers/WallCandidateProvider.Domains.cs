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
    internal sealed partial class WallCandidateProvider
    {
        private static void CreateBoundarySeeds(
            GenerationContext context,
            IGenerationProfiler profiler,
            List<CandidateSeed> seeds,
            WallSegmentLookup wallLookup,
            float perimeterLength,
            List<Vector3> debugClusterCenters,
            int targetSeedCount)
        {
            Stopwatch samplingStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
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
        }

        private void CreateAllMatchingSurfaceSeeds(
            GenerationContext context,
            IGenerationProfiler profiler,
            List<CandidateSeed> seeds,
            int targetSeedCount)
        {
            List<WallSamplingDomain> domains = CreateSamplingDomains(context);

            if (domains.Count == 0 || targetSeedCount <= 0)
                return;

            int[] sampleCounts = AllocateSampleCounts(domains, targetSeedCount);
            List<SurfacePoint> surfacePoints = new(2);

            for (int domainIndex = 0; domainIndex < domains.Count; domainIndex++)
            {
                int sampleCount = sampleCounts[domainIndex];

                if (sampleCount <= 0)
                    continue;

                WallSamplingDomain domain = domains[domainIndex];
                Stopwatch samplingStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                List<Vector3> positions = SampleDomainPositions(context, domain.Bounds, sampleCount);
                profiler.AddSamplingTime(PlacementType.Wall, StopAndReadMilliseconds(samplingStopwatch));
                profiler.RecordRawSamples(PlacementType.Wall, positions.Count);

                for (int positionIndex = 0; positionIndex < positions.Count; positionIndex++)
                {
                    surfacePoints.Clear();
                    Stopwatch projectionStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                    int projectedCount = context.Area.CollectWallSurfaces(
                        domain.Source,
                        domain.Axis,
                        positions[positionIndex],
                        surfacePoints,
                        profiler);
                    profiler.RecordProjection(
                        PlacementType.Wall,
                        projectedCount > 0,
                        StopAndReadMilliseconds(projectionStopwatch));

                    for (int surfaceIndex = 0; surfaceIndex < surfacePoints.Count; surfaceIndex++)
                    {
                        SurfacePoint surfacePoint = surfacePoints[surfaceIndex];
                        AddSeed(
                            seeds,
                            surfacePoint.Position,
                            Quaternion.LookRotation(surfacePoint.Normal, Vector3.up),
                            surfacePoint.SurfaceCollider,
                            surfacePoint.Normal,
                            surfacePoint.VoxelLayer,
                            PlacementType.Wall);
                    }
                }
            }

            if (seeds.Count > targetSeedCount)
            {
                context.Random.Shuffle(seeds);
                seeds.RemoveRange(targetSeedCount, seeds.Count - targetSeedCount);
            }

            profiler.RecordCandidateSeeds(PlacementType.Wall, seeds.Count);
        }

        private static List<WallSamplingDomain> CreateSamplingDomains(GenerationContext context)
        {
            IReadOnlyList<WallSurfaceSource> sources = context.Area.WallSurfaceSources;
            List<WallSamplingDomain> domains = new();

            for (int i = 0; i < sources.Count; i++)
            {
                WallSurfaceSource source = sources[i];
                Bounds clipped = IntersectBounds(source.Bounds, context.TargetBounds);

                if (source.IsTerrain)
                {
                    if (TryCreateSamplingBounds(
                            clipped.min.x,
                            clipped.max.x,
                            clipped.min.z,
                            clipped.max.z,
                            clipped.center.y,
                            out Bounds terrainBounds))
                    {
                        domains.Add(new WallSamplingDomain(
                            source,
                            WallSurfaceSampleAxis.Terrain,
                            terrainBounds,
                            source.Weight));
                    }

                    continue;
                }

                float xProjectionWeight = clipped.size.y * clipped.size.z;

                if (xProjectionWeight > MinValue &&
                    TryCreateEncodedSamplingBounds(
                        clipped.min.z,
                        clipped.max.z,
                        clipped.min.y,
                        clipped.max.y,
                        out Bounds xBounds))
                {
                    domains.Add(new WallSamplingDomain(
                        source,
                        WallSurfaceSampleAxis.X,
                        xBounds,
                        xProjectionWeight));
                }

                float zProjectionWeight = clipped.size.y * clipped.size.x;

                if (zProjectionWeight > MinValue &&
                    TryCreateEncodedSamplingBounds(
                        clipped.min.x,
                        clipped.max.x,
                        clipped.min.y,
                        clipped.max.y,
                        out Bounds zBounds))
                {
                    domains.Add(new WallSamplingDomain(
                        source,
                        WallSurfaceSampleAxis.Z,
                        zBounds,
                        zProjectionWeight));
                }
            }

            return domains;
        }

        private static List<Vector3> SampleDomainPositions(
            GenerationContext context,
            Bounds bounds,
            int sampleCount)
        {
            SamplingContext samplingContext = new(
                bounds,
                bounds.center,
                context.StyleSettings,
                sampleCount,
                context.Random,
                minimumCandidateCount: 0,
                candidateCountOverride: sampleCount);

            if (context.StyleSettings.algorithm is SamplingAlgorithm.Grid or SamplingAlgorithm.JitteredGrid)
                return SampleBoundedGrid(samplingContext, sampleCount);

            return SamplePositions(samplingContext);
        }

        private static List<Vector3> SampleBoundedGrid(SamplingContext context, int sampleCount)
        {
            List<Vector3> positions = new(sampleCount);

            float cellSize = context.Grid.cellSize;

            if (sampleCount <= 0 || cellSize <= 0f)
                return positions;

            float width = Mathf.Max(MinValue, context.Bounds.size.x);
            float height = Mathf.Max(MinValue, context.Bounds.size.z);
            int fullColumns = Mathf.Max(1, Mathf.FloorToInt(width / cellSize) + 1);
            int fullRows = Mathf.Max(1, Mathf.FloorToInt(height / cellSize) + 1);
            long fullCount = (long)fullColumns * fullRows;
            int columns = fullColumns;
            int rows = fullRows;

            if (fullCount > sampleCount)
            {
                columns = Mathf.Min(
                    fullColumns,
                    Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(sampleCount * fullColumns / (float)fullRows))));
                rows = Mathf.Min(fullRows, Mathf.Max(1, Mathf.CeilToInt(sampleCount / (float)columns)));
            }

            bool jittered = context.StyleSettings.algorithm == SamplingAlgorithm.JitteredGrid;
            float jitter = jittered ? Mathf.Clamp01(context.Grid.jitterAmount) : 0f;

            for (int row = 0; row < rows && positions.Count < sampleCount; row++)
            {
                int sourceRow = MapGridIndex(row, rows, fullRows);

                for (int column = 0; column < columns && positions.Count < sampleCount; column++)
                {
                    int sourceColumn = MapGridIndex(column, columns, fullColumns);
                    float x = context.Bounds.min.x + sourceColumn * cellSize;
                    float z = context.Bounds.min.z + sourceRow * cellSize;

                    if (jitter > 0f)
                    {
                        float jitterRadius = cellSize * jitter;
                        x += context.Random.Range(-jitterRadius, jitterRadius);
                        z += context.Random.Range(-jitterRadius, jitterRadius);
                    }

                    positions.Add(new Vector3(
                        Mathf.Clamp(x, context.Bounds.min.x, context.Bounds.max.x),
                        context.Bounds.min.y,
                        Mathf.Clamp(z, context.Bounds.min.z, context.Bounds.max.z)));
                }
            }

            return positions;
        }

        private static int MapGridIndex(int index, int selectedCount, int fullCount)
        {
            if (selectedCount <= 1 || fullCount <= 1)
                return 0;

            return Mathf.RoundToInt(index * (fullCount - 1f) / (selectedCount - 1f));
        }

        private static int[] AllocateSampleCounts(IReadOnlyList<WallSamplingDomain> domains, int totalCount)
        {
            int[] counts = new int[domains.Count];

            if (domains.Count == 0 || totalCount <= 0)
                return counts;

            int assigned = 0;

            if (totalCount >= domains.Count)
            {
                for (int i = 0; i < counts.Length; i++)
                    counts[i] = 1;

                assigned = domains.Count;
            }

            int remaining = totalCount - assigned;
            float totalWeight = 0f;

            for (int i = 0; i < domains.Count; i++)
                totalWeight += Mathf.Max(MinValue, domains[i].Weight);

            float[] fractions = new float[domains.Count];

            for (int i = 0; i < domains.Count; i++)
            {
                float exact = remaining * Mathf.Max(MinValue, domains[i].Weight) / totalWeight;
                int whole = Mathf.FloorToInt(exact);
                counts[i] += whole;
                assigned += whole;
                fractions[i] = exact - whole;
            }

            while (assigned < totalCount)
            {
                int bestIndex = 0;

                for (int i = 1; i < fractions.Length; i++)
                {
                    if (fractions[i] > fractions[bestIndex])
                        bestIndex = i;
                }

                counts[bestIndex]++;
                fractions[bestIndex] = -1f;
                assigned++;
            }

            return counts;
        }

        private static bool TryCreateEncodedSamplingBounds(
            float minHorizontal,
            float maxHorizontal,
            float minY,
            float maxY,
            out Bounds bounds) =>
            TryCreateSamplingBounds(minHorizontal, maxHorizontal, minY, maxY, 0f, out bounds);

        private static Bounds IntersectBounds(Bounds first, Bounds second)
        {
            Vector3 min = Vector3.Max(first.min, second.min);
            Vector3 max = Vector3.Min(first.max, second.max);
            Bounds result = new();
            result.SetMinMax(min, max);
            return result;
        }

        private readonly struct WallSamplingDomain
        {
            public WallSurfaceSource Source { get; }
            public WallSurfaceSampleAxis Axis { get; }
            public Bounds Bounds { get; }
            public float Weight { get; }

            public WallSamplingDomain(
                WallSurfaceSource source,
                WallSurfaceSampleAxis axis,
                Bounds bounds,
                float weight)
            {
                Source = source;
                Axis = axis;
                Bounds = bounds;
                Weight = weight;
            }
        }

        private static float StopAndReadMilliseconds(Stopwatch stopwatch)
        {
            if (stopwatch == null)
                return 0f;

            stopwatch.Stop();
            return (float)stopwatch.Elapsed.TotalMilliseconds;
        }
    }
}

