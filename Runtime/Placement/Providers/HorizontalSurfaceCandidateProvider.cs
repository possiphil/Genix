using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Profiling;
using Genix.Sampling;
using Genix.Sampling.PoissonSampling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Placement.Providers
{
    internal sealed class PlacementTargetCandidateProvider : CandidateProviderBase
    {
        private readonly PlacementTarget _targets;
        private HorizontalSurfaceCandidateProvider _floorProvider;
        private WallCandidateProvider _wallProvider;
        private CeilingCandidateProvider _ceilingProvider;
        private InsideSpaceCandidateProvider _insideSpaceProvider;

        public PlacementTargetCandidateProvider(
            PlacementTarget targets,
            int requestedCount = -1,
            int minimumCandidateCount = -1,
            int candidateCount = -1)
            : base(requestedCount, minimumCandidateCount, candidateCount)
        {
            _targets = targets & PlacementTarget.All;
        }

        public override List<CandidateSeed> CreateCandidateSeeds(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null,
            IGenerationProfiler profiler = null)
        {
            List<CandidateSeed> seeds = new();
            List<PlacementType> activeTypes = GetActivePlacementTypes();

            if ((_targets & PlacementTarget.Floor) != 0)
                seeds.AddRange(CreateFloorProvider(context, activeTypes).CreateCandidateSeeds(context, diagnostics, profiler));

            if ((_targets & PlacementTarget.Wall) != 0)
                seeds.AddRange(CreateWallProvider(context, activeTypes).CreateCandidateSeeds(context, diagnostics, profiler));

            if ((_targets & PlacementTarget.Ceiling) != 0)
                seeds.AddRange(CreateCeilingProvider(context, activeTypes).CreateCandidateSeeds(context, diagnostics, profiler));

            if ((_targets & PlacementTarget.InsideSpace) != 0)
                seeds.AddRange(CreateInsideSpaceProvider(context, activeTypes).CreateCandidateSeeds(context, diagnostics, profiler));

            ShuffleIfNeeded(seeds, context);
            return seeds;
        }

        private HorizontalSurfaceCandidateProvider CreateFloorProvider(
            GenerationContext context,
            IReadOnlyList<PlacementType> activeTypes)
        {
            if (_floorProvider != null)
                return _floorProvider;

            CandidateBudget budget = CreateBudget(context, PlacementType.Floor, activeTypes);
            _floorProvider = new HorizontalSurfaceCandidateProvider(
                budget.RequestedCount,
                budget.MinimumCandidateCount,
                budget.CandidateCount);
            return _floorProvider;
        }

        private WallCandidateProvider CreateWallProvider(
            GenerationContext context,
            IReadOnlyList<PlacementType> activeTypes)
        {
            if (_wallProvider != null)
                return _wallProvider;

            CandidateBudget budget = CreateBudget(context, PlacementType.Wall, activeTypes);
            _wallProvider = new WallCandidateProvider(
                budget.RequestedCount,
                budget.MinimumCandidateCount,
                budget.CandidateCount);
            return _wallProvider;
        }

        private CeilingCandidateProvider CreateCeilingProvider(
            GenerationContext context,
            IReadOnlyList<PlacementType> activeTypes)
        {
            if (_ceilingProvider != null)
                return _ceilingProvider;

            CandidateBudget budget = CreateBudget(context, PlacementType.Ceiling, activeTypes);
            _ceilingProvider = new CeilingCandidateProvider(
                budget.RequestedCount,
                budget.MinimumCandidateCount,
                budget.CandidateCount);
            return _ceilingProvider;
        }

        private InsideSpaceCandidateProvider CreateInsideSpaceProvider(
            GenerationContext context,
            IReadOnlyList<PlacementType> activeTypes)
        {
            if (_insideSpaceProvider != null)
                return _insideSpaceProvider;

            CandidateBudget budget = CreateBudget(context, PlacementType.InsideSpace, activeTypes);
            _insideSpaceProvider = new InsideSpaceCandidateProvider(
                budget.RequestedCount,
                budget.MinimumCandidateCount,
                budget.CandidateCount);
            return _insideSpaceProvider;
        }

        private List<PlacementType> GetActivePlacementTypes()
        {
            List<PlacementType> result = new();

            if ((_targets & PlacementTarget.Floor) != 0)
                result.Add(PlacementType.Floor);

            if ((_targets & PlacementTarget.Wall) != 0)
                result.Add(PlacementType.Wall);

            if ((_targets & PlacementTarget.Ceiling) != 0)
                result.Add(PlacementType.Ceiling);

            if ((_targets & PlacementTarget.InsideSpace) != 0)
                result.Add(PlacementType.InsideSpace);

            return result;
        }

        private CandidateBudget CreateBudget(
            GenerationContext context,
            PlacementType placementType,
            IReadOnlyList<PlacementType> activeTypes)
        {
            int rootRequestedCount = Mathf.Max(1, GetRequestedCount(context));
            int requestedCount = GetRequestedObjectCount(context, placementType, activeTypes, rootRequestedCount);
            float requestedRatio = requestedCount / (float)rootRequestedCount;
            int minimumCandidateCount = Mathf.CeilToInt(GetMinimumCandidateCount(context) * requestedRatio);
            int candidateCount = Mathf.CeilToInt(GetCandidateCount(context) * requestedRatio);

            return new CandidateBudget(
                Mathf.Max(1, requestedCount),
                Mathf.Max(1, minimumCandidateCount),
                Mathf.Max(1, candidateCount));
        }

        private static int GetRequestedObjectCount(
            GenerationContext context,
            PlacementType placementType,
            IReadOnlyList<PlacementType> activeTypes,
            int rootRequestedCount)
        {
            if (activeTypes == null || activeTypes.Count <= 1)
                return rootRequestedCount;

            if (context.TargetDistributionMode == TargetDistributionMode.Weighted)
                return GetWeightedRequestedCount(context, placementType, activeTypes, rootRequestedCount);

            return Mathf.CeilToInt(rootRequestedCount / (float)activeTypes.Count);
        }

        private static int GetWeightedRequestedCount(
            GenerationContext context,
            PlacementType placementType,
            IReadOnlyList<PlacementType> activeTypes,
            int rootRequestedCount)
        {
            int targetWeight = GetWeight(context, placementType);
            int totalWeight = 0;

            foreach (PlacementType type in activeTypes)
                totalWeight += GetWeight(context, type);

            if (targetWeight <= 0 || totalWeight <= 0)
                return Mathf.CeilToInt(rootRequestedCount / (float)activeTypes.Count);

            return Mathf.CeilToInt(rootRequestedCount * (targetWeight / (float)totalWeight));
        }

        private static int GetWeight(GenerationContext context, PlacementType placementType)
        {
            PlacementTarget target = placementType switch
            {
                PlacementType.Floor => PlacementTarget.Floor,
                PlacementType.Wall => PlacementTarget.Wall,
                PlacementType.Ceiling => PlacementTarget.Ceiling,
                PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                _ => PlacementTarget.None
            };

            return context.TargetDistributionMode == TargetDistributionMode.Weighted
                ? context.TargetDistributionWeights.GetWeight(target)
                : 1;
        }

        private readonly struct CandidateBudget
        {
            public int RequestedCount { get; }
            public int MinimumCandidateCount { get; }
            public int CandidateCount { get; }

            public CandidateBudget(int requestedCount, int minimumCandidateCount, int candidateCount)
            {
                RequestedCount = requestedCount;
                MinimumCandidateCount = minimumCandidateCount;
                CandidateCount = candidateCount;
            }
        }
    }

    internal sealed class HorizontalSurfaceCandidateProvider : CandidateProviderBase
    {
        private readonly Dictionary<SurfaceRegion, ProgressiveBridsonPoissonDiskSampler> _poissonSamplers = new();
        private ProgressiveBridsonPoissonDiskSampler _allSurfacePoissonSampler;

        public HorizontalSurfaceCandidateProvider(
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
                profiler.AddSeedGenerationTime(PlacementType.Floor, StopAndReadMilliseconds(providerStopwatch));
                return seeds;
            }

            foreach (SurfaceRegion region in context.Area.FloorRegions)
            {
                SamplingContext samplingContext = CreateSamplingContext(context, region.Bounds, region.Bounds.center, 0f, diagnostics);
                Stopwatch samplingStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                List<Vector3> positions = SampleSurfacePositions(context, region, samplingContext);
                profiler.AddSamplingTime(PlacementType.Floor, StopAndReadMilliseconds(samplingStopwatch));
                profiler.RecordRawSamples(PlacementType.Floor, positions.Count);

                foreach (Vector3 rawPosition in positions)
                {
                    Stopwatch projectionStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                    bool projected = context.Area.TryProjectToFloor(rawPosition, region, out SurfacePoint surfacePoint, profiler);
                    projectionStopwatch?.Stop();
                    profiler.RecordProjection(
                        PlacementType.Floor,
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
                        PlacementType.Floor);
                    profiler.RecordCandidateSeeds(PlacementType.Floor, 1);
                }
            }

            ShuffleIfNeeded(seeds, context);
            profiler.AddSeedGenerationTime(PlacementType.Floor, StopAndReadMilliseconds(providerStopwatch));
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
            profiler.AddSamplingTime(PlacementType.Floor, StopAndReadMilliseconds(samplingStopwatch));
            profiler.RecordRawSamples(PlacementType.Floor, positions.Count);

            List<SurfacePoint> surfacePoints = new();

            foreach (Vector3 rawPosition in positions)
            {
                surfacePoints.Clear();
                Stopwatch projectionStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                int projectedCount = context.Area.CollectFloorSurfaces(rawPosition, surfacePoints, profiler);
                profiler.RecordProjection(
                    PlacementType.Floor,
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
                        PlacementType.Floor);
                    profiler.RecordCandidateSeeds(PlacementType.Floor, 1);
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
