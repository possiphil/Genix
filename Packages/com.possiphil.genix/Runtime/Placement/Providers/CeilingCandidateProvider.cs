using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Profiling;
using Genix.Sampling;
using Genix.Sampling.PoissonSampling;
using Genix.Semantics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Placement.Providers
{
    /// <summary>Generates ceiling seeds and projects them upward onto valid downward-facing surfaces.</summary>
    internal sealed class CeilingCandidateProvider : CandidateProviderBase
    {
        private readonly Dictionary<SurfaceRegion, ProgressiveBridsonPoissonDiskSampler> _poissonSamplers = new();
        private ProgressiveBridsonPoissonDiskSampler _allSurfacePoissonSampler;

        public CeilingCandidateProvider(
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

            if (context.Area.UsesAllMatchingSurfaceSearch)
            {
                CreateAllSurfaceSeeds(context, diagnostics, profiler, seeds);
                ShuffleIfNeeded(seeds, context);
                profiler.AddSeedGenerationTime(PlacementType.Ceiling, StopAndReadMilliseconds(providerStopwatch));
                return seeds;
            }

            foreach (SurfaceRegion region in context.Area.CeilingRegions)
            {
                SamplingContext samplingContext = CreateSamplingContext(context, region.Bounds, region.Bounds.center, 0f, diagnostics);
                Stopwatch samplingStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                List<Vector3> positions = SampleSurfacePositions(context, region, samplingContext);
                profiler.AddSamplingTime(PlacementType.Ceiling, StopAndReadMilliseconds(samplingStopwatch));
                profiler.RecordRawSamples(PlacementType.Ceiling, positions.Count);

                foreach (Vector3 rawPosition in positions)
                {
                    Stopwatch projectionStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                    bool projected = context.Area.TryProjectToCeiling(rawPosition, region, out SurfacePoint surfacePoint, profiler);
                    projectionStopwatch?.Stop();
                    profiler.RecordProjection(
                        PlacementType.Ceiling,
                        projected,
                        projectionStopwatch != null ? (float)projectionStopwatch.Elapsed.TotalMilliseconds : 0f);

                    if (!projected)
                        continue;

                    AddSeed(
                        seeds,
                        surfacePoint.Position,
                        Quaternion.identity,
                        surfacePoint.SurfaceCollider,
                        surfacePoint.Normal,
                        surfacePoint.VoxelLayer,
                        PlacementType.Ceiling);
                    profiler.RecordCandidateSeeds(PlacementType.Ceiling, 1);
                }
            }

            ShuffleIfNeeded(seeds, context);
            profiler.AddSeedGenerationTime(PlacementType.Ceiling, StopAndReadMilliseconds(providerStopwatch));
            return seeds;
        }

        private void CreateAllSurfaceSeeds(
            GenerationContext context,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            List<CandidateSeed> seeds)
        {
            Bounds samplingBounds = CreateAllSurfaceSamplingBounds(context.TargetBounds);
            SamplingContext samplingContext = CreateSamplingContext(
                context,
                samplingBounds,
                samplingBounds.center,
                0f,
                diagnostics);
            Stopwatch samplingStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            List<Vector3> positions = SampleAllSurfacePositions(context, samplingContext);
            profiler.AddSamplingTime(PlacementType.Ceiling, StopAndReadMilliseconds(samplingStopwatch));
            profiler.RecordRawSamples(PlacementType.Ceiling, positions.Count);

            List<SurfacePoint> surfacePoints = new();

            foreach (Vector3 rawPosition in positions)
            {
                surfacePoints.Clear();
                Stopwatch projectionStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                int projectedCount = context.Area.CollectCeilingSurfaces(rawPosition, surfacePoints, profiler);
                profiler.RecordProjection(
                    PlacementType.Ceiling,
                    projectedCount > 0,
                    StopAndReadMilliseconds(projectionStopwatch));

                for (int i = 0; i < surfacePoints.Count; i++)
                {
                    SurfacePoint surfacePoint = surfacePoints[i];
                    AddSeed(
                        seeds,
                        surfacePoint.Position,
                        Quaternion.identity,
                        surfacePoint.SurfaceCollider,
                        surfacePoint.Normal,
                        surfacePoint.VoxelLayer,
                        PlacementType.Ceiling);
                    profiler.RecordCandidateSeeds(PlacementType.Ceiling, 1);
                }
            }
        }

        private List<Vector3> SampleAllSurfacePositions(
            GenerationContext context,
            SamplingContext samplingContext)
        {
            if (context.StyleSettings.algorithm != SamplingAlgorithm.BridsonPoissonDisk ||
                GetCandidateCountOverride() <= 0)
            {
                return SamplePositions(samplingContext);
            }

            _allSurfacePoissonSampler ??= new ProgressiveBridsonPoissonDiskSampler(samplingContext);
            return _allSurfacePoissonSampler.SamplePositions(samplingContext.CandidateCount);
        }

        private List<Vector3> SampleSurfacePositions(
            GenerationContext context,
            SurfaceRegion region,
            SamplingContext samplingContext)
        {
            if (context.StyleSettings.algorithm != SamplingAlgorithm.BridsonPoissonDisk ||
                GetCandidateCountOverride() <= 0)
            {
                return SamplePositions(samplingContext);
            }

            if (!_poissonSamplers.TryGetValue(region, out ProgressiveBridsonPoissonDiskSampler sampler))
            {
                sampler = new ProgressiveBridsonPoissonDiskSampler(samplingContext);
                _poissonSamplers[region] = sampler;
            }

            return sampler.SamplePositions(samplingContext.CandidateCount);
        }

        private static float StopAndReadMilliseconds(Stopwatch stopwatch)
        {
            if (stopwatch == null)
                return 0f;

            stopwatch.Stop();
            return (float)stopwatch.Elapsed.TotalMilliseconds;
        }

        private static Bounds CreateAllSurfaceSamplingBounds(Bounds targetBounds)
        {
            return new Bounds(
                targetBounds.center,
                new Vector3(
                    Mathf.Max(SamplingBoundsHeight, targetBounds.size.x),
                    SamplingBoundsHeight,
                    Mathf.Max(SamplingBoundsHeight, targetBounds.size.z)));
        }
    }
}

