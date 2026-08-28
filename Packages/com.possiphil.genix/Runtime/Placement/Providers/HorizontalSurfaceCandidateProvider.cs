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
    /// <summary>Compatibility provider that samples extracted horizontal regions without physics projection.</summary>
    internal sealed class HorizontalSurfaceCandidateProvider : CandidateProviderBase
    {
        private const float ReservedSupportBudgetRatio = 0.5f;
        private const int MinimumCandidatesPerSupport = 4;

        private readonly IReadOnlyList<AssetDefinition> _assets;
        private readonly Dictionary<SurfaceRegion, ProgressiveBridsonPoissonDiskSampler> _poissonSamplers = new();
        private readonly Dictionary<PlacementSurfaceDescriptor, ProgressiveBridsonPoissonDiskSampler> _supportPoissonSamplers = new();
        private List<SupportSurfaceSamplingEntry> _supportSurfaces;
        private ProgressiveBridsonPoissonDiskSampler _allSurfacePoissonSampler;

        public HorizontalSurfaceCandidateProvider(
            int requestedCount = -1,
            int minimumCandidateCount = -1,
            int candidateCount = -1,
            IReadOnlyList<AssetDefinition> assets = null)
            : base(requestedCount, minimumCandidateCount, candidateCount)
        {
            _assets = assets;
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
            SamplingContext fullSamplingContext = CreateSamplingContext(
                context,
                samplingBounds,
                samplingBounds.center,
                0f,
                diagnostics);
            IReadOnlyList<SupportSurfaceSamplingEntry> supportSurfaces = GetSupportSurfaces(context);
            int fullBudget = fullSamplingContext.CandidateCount;
            int reservedBudget = GetReservedSupportBudget(fullBudget, supportSurfaces.Count);
            int globalBudget = Mathf.Max(1, fullBudget - reservedBudget);
            SamplingContext globalSamplingContext = CreateSamplingContext(
                context,
                samplingBounds,
                samplingBounds.center,
                0f,
                Mathf.Max(1, GetRequestedCount(context)),
                0,
                globalBudget,
                diagnostics);
            Stopwatch samplingStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            List<Vector3> positions = SampleAllSurfacePositions(context, globalSamplingContext);
            profiler.AddSamplingTime(PlacementType.Floor, StopAndReadMilliseconds(samplingStopwatch));
            profiler.RecordRawSamples(PlacementType.Floor, positions.Count);

            List<SurfacePoint> surfacePoints = new();
            HashSet<SeedIdentity> identities = new();

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
                    AddUniqueFloorSeed(seeds, identities, surfacePoint, profiler);
                }
            }

            if (reservedBudget > 0)
                CreateReservedSupportSeeds(
                    context,
                    diagnostics,
                    profiler,
                    supportSurfaces,
                    reservedBudget,
                    seeds,
                    identities,
                    surfacePoints);
        }

        private IReadOnlyList<SupportSurfaceSamplingEntry> GetSupportSurfaces(GenerationContext context)
        {
            _supportSurfaces ??= SupportSurfaceSampling.Collect(
                context,
                _assets,
                PlacementType.Floor);
            return _supportSurfaces;
        }

        private void CreateReservedSupportSeeds(
            GenerationContext context,
            IDiagnosticsSink diagnostics,
            IGenerationProfiler profiler,
            IReadOnlyList<SupportSurfaceSamplingEntry> supportSurfaces,
            int reservedBudget,
            List<CandidateSeed> seeds,
            HashSet<SeedIdentity> identities,
            List<SurfacePoint> surfacePoints)
        {
            int baseQuota = reservedBudget / supportSurfaces.Count;
            int remainder = reservedBudget % supportSurfaces.Count;

            for (int surfaceIndex = 0; surfaceIndex < supportSurfaces.Count; surfaceIndex++)
            {
                SupportSurfaceSamplingEntry surface = supportSurfaces[surfaceIndex];
                int quota = baseQuota + (surfaceIndex < remainder ? 1 : 0);

                if (quota <= 0)
                    continue;

                Bounds samplingBounds = CreateSupportSamplingBounds(surface.Bounds);
                SamplingContext samplingContext = CreateSamplingContext(
                    context,
                    samplingBounds,
                    samplingBounds.center,
                    0f,
                    quota,
                    0,
                    quota,
                    diagnostics);
                Stopwatch samplingStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                List<Vector3> positions = SampleSupportPositions(context, surface, samplingContext, quota);
                profiler.AddSamplingTime(PlacementType.Floor, StopAndReadMilliseconds(samplingStopwatch));
                profiler.RecordRawSamples(PlacementType.Floor, positions.Count);

                foreach (Vector3 rawPosition in positions)
                {
                    surfacePoints.Clear();
                    Stopwatch projectionStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                    int projectedCount = context.Area.CollectFloorSurfaces(rawPosition, surfacePoints, profiler);
                    profiler.RecordProjection(
                        PlacementType.Floor,
                        projectedCount > 0,
                        StopAndReadMilliseconds(projectionStopwatch));

                    for (int pointIndex = 0; pointIndex < surfacePoints.Count; pointIndex++)
                    {
                        SurfacePoint point = surfacePoints[pointIndex];

                        if (PlacementSupportRules.GetDescriptor(point.SurfaceCollider) != surface.Descriptor)
                            continue;

                        AddUniqueFloorSeed(seeds, identities, point, profiler);
                    }
                }
            }
        }

        private List<Vector3> SampleSupportPositions(
            GenerationContext context,
            SupportSurfaceSamplingEntry surface,
            SamplingContext samplingContext,
            int quota)
        {
            List<Vector3> sampledPositions;

            if (context.StyleSettings.algorithm == SamplingAlgorithm.BridsonPoissonDisk)
            {
                if (!_supportPoissonSamplers.TryGetValue(surface.Descriptor, out ProgressiveBridsonPoissonDiskSampler sampler))
                {
                    sampler = new ProgressiveBridsonPoissonDiskSampler(samplingContext);
                    _supportPoissonSamplers[surface.Descriptor] = sampler;
                }

                sampledPositions = sampler.SamplePositions(quota);
            }
            else
            {
                sampledPositions = SamplePositions(samplingContext);
            }

            List<Vector3> positions = new(Mathf.Min(quota, sampledPositions.Count + 1));
            HashSet<SupportPositionIdentity> identities = new();
            AddSupportPosition(positions, identities, surface.Bounds.center, quota);

            foreach (Vector3 position in sampledPositions)
            {
                AddSupportPosition(positions, identities, position, quota);
                if (positions.Count >= quota)
                    break;
            }

            return positions;
        }

        private static void AddSupportPosition(
            ICollection<Vector3> positions,
            ISet<SupportPositionIdentity> identities,
            Vector3 position,
            int quota)
        {
            if (positions.Count < quota && identities.Add(new SupportPositionIdentity(position)))
                positions.Add(position);
        }

        private static int GetReservedSupportBudget(int fullBudget, int surfaceCount)
        {
            if (fullBudget <= 1 || surfaceCount <= 0)
                return 0;

            int desired = Mathf.Max(
                Mathf.CeilToInt(fullBudget * ReservedSupportBudgetRatio),
                surfaceCount * MinimumCandidatesPerSupport);
            return Mathf.Clamp(desired, 1, fullBudget - 1);
        }

        private static Bounds CreateSupportSamplingBounds(Bounds surfaceBounds)
        {
            return new Bounds(
                new Vector3(surfaceBounds.center.x, surfaceBounds.center.y, surfaceBounds.center.z),
                new Vector3(
                    Mathf.Max(0.001f, surfaceBounds.size.x),
                    SamplingBoundsHeight,
                    Mathf.Max(0.001f, surfaceBounds.size.z)));
        }

        private static void AddUniqueFloorSeed(
            List<CandidateSeed> seeds,
            ISet<SeedIdentity> identities,
            SurfacePoint surfacePoint,
            IGenerationProfiler profiler)
        {
            SeedIdentity identity = new(surfacePoint);

            if (!identities.Add(identity))
                return;

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

        private readonly struct SeedIdentity : System.IEquatable<SeedIdentity>
        {
            private readonly Collider _collider;
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;

            public SeedIdentity(SurfacePoint point)
            {
                _collider = point.SurfaceCollider;
                _x = Mathf.RoundToInt(point.Position.x * 10_000f);
                _y = Mathf.RoundToInt(point.Position.y * 10_000f);
                _z = Mathf.RoundToInt(point.Position.z * 10_000f);
            }

            public bool Equals(SeedIdentity other) =>
                _collider == other._collider && _x == other._x && _y == other._y && _z == other._z;

            public override bool Equals(object obj) => obj is SeedIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _collider ? _collider.GetHashCode() : 0;
                    hash = (hash * 397) ^ _x;
                    hash = (hash * 397) ^ _y;
                    return (hash * 397) ^ _z;
                }
            }
        }

        private readonly struct SupportPositionIdentity : System.IEquatable<SupportPositionIdentity>
        {
            private readonly int _x;
            private readonly int _z;

            public SupportPositionIdentity(Vector3 position)
            {
                _x = Mathf.RoundToInt(position.x * 10_000f);
                _z = Mathf.RoundToInt(position.z * 10_000f);
            }

            public bool Equals(SupportPositionIdentity other) => _x == other._x && _z == other._z;

            public override bool Equals(object obj) =>
                obj is SupportPositionIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_x * 397) ^ _z;
                }
            }
        }

    }
}
