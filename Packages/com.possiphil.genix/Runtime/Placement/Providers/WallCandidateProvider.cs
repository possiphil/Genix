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
    /// <summary>
    /// Samples SFS wall spans in boundary modes and matching colliders throughout the volume in all-surface mode.
    /// </summary>
    internal sealed partial class WallCandidateProvider : CandidateProviderBase
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
            diagnostics ??= NullDiagnosticsSink.Instance;
            profiler ??= NullGenerationProfiler.Instance;
            Stopwatch providerStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            List<CandidateSeed> seeds = new();
            int targetSeedCount = GetCandidateCount(context);
            List<Vector3> debugClusterCenters = new();

            if (context.Area.UsesAllMatchingSurfaceSearch)
            {
                CreateAllMatchingSurfaceSeeds(context, profiler, seeds, targetSeedCount);
                ShuffleIfNeeded(seeds, context);
                profiler.AddSeedGenerationTime(PlacementType.Wall, StopAndReadMilliseconds(providerStopwatch));
                return seeds;
            }

            WallSegment[] walls = CreateWallLines(context);
            WallSegmentLookup wallLookup = new(walls);
            float perimeterLength = wallLookup.PerimeterLength;

            if (perimeterLength > 0f)
            {
                CreateBoundarySeeds(
                    context,
                    profiler,
                    seeds,
                    wallLookup,
                    perimeterLength,
                    debugClusterCenters,
                    targetSeedCount);
            }

            diagnostics.RecordClusterCenters(debugClusterCenters);
            ShuffleIfNeeded(seeds, context);
            profiler.AddSeedGenerationTime(PlacementType.Wall, StopAndReadMilliseconds(providerStopwatch));
            return seeds;
        }
    }
}
