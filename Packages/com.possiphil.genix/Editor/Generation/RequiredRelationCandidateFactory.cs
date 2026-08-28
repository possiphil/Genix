using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Placement.Providers;
using Genix.Profiling;
using Genix.Semantics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Editor.Generation
{
    /// <summary>Creates a bounded local fallback pool for mandatory asset relationships.</summary>
    internal static partial class RequiredRelationCandidateFactory
    {
        private const int MaximumAxisSamples = 7;
        private const int MaximumCandidateSeeds = 160;
        private const float PositionQuantization = 10_000f;

        public static List<CandidateSeed> Create(
            GenerationContext context,
            AssetDefinition asset,
            RelativeAnchor anchor,
            IGenerationProfiler profiler)
        {
            List<CandidateSeed> seeds = new();
            AssetRelativePlacementRule rule = asset ? asset.AssetRelativePlacement : null;

            if (context?.Area == null ||
                rule?.IsConfigured != true ||
                asset.PlacementType is not (PlacementType.Floor or PlacementType.Wall or
                    PlacementType.Ceiling or PlacementType.InsideSpace))
            {
                return seeds;
            }

            profiler ??= NullGenerationProfiler.Instance;
            if (asset.PlacementType == PlacementType.Wall)
                return CreateWallSeeds(context, asset, anchor, profiler);
            if (asset.PlacementType == PlacementType.InsideSpace)
                return CreateInsideSpaceSeeds(context, asset, anchor, profiler);

            Stopwatch generationStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            Bounds samplingBounds = CreateSamplingBounds(context, asset, anchor, rule);
            List<Vector3> positions = CreatePositions(context, samplingBounds, asset, anchor, rule);
            List<SurfacePoint> surfacePoints = new();
            HashSet<SeedIdentity> identities = new();

            profiler.RecordRawSamples(asset.PlacementType, positions.Count);

            foreach (Vector3 position in positions)
            {
                surfacePoints.Clear();
                Stopwatch projectionStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                CollectSurfaces(context, asset.PlacementType, position, surfacePoints, profiler);
                projectionStopwatch?.Stop();
                profiler.RecordProjection(
                    asset.PlacementType,
                    surfacePoints.Count > 0,
                    projectionStopwatch != null ? (float)projectionStopwatch.Elapsed.TotalMilliseconds : 0f);

                foreach (SurfacePoint point in surfacePoints)
                {
                    if (rule.RequireSameSupportSurface &&
                        PlacementSupportRules.GetDescriptor(point.SurfaceCollider) != anchor.SupportSurface)
                    {
                        continue;
                    }

                    CandidateSeed seed = new(
                        point.Position,
                        Quaternion.identity,
                        point.SurfaceCollider,
                        point.Normal,
                        point.VoxelLayer,
                        asset.PlacementType);
                    if (!PlacementSupportRules.TryValidateCompatibility(seed, asset, out _, out _) ||
                        !RelativeAnchorProvider.IsPotentialSeedForAnchor(context, seed, asset, anchor) ||
                        !identities.Add(new SeedIdentity(point)))
                    {
                        continue;
                    }

                    seeds.Add(seed);
                    if (seeds.Count >= MaximumCandidateSeeds)
                        break;
                }

                if (seeds.Count >= MaximumCandidateSeeds)
                    break;
            }

            profiler.RecordCandidateSeeds(asset.PlacementType, seeds.Count);
            generationStopwatch?.Stop();
            profiler.AddSeedGenerationTime(
                asset.PlacementType,
                generationStopwatch != null ? (float)generationStopwatch.Elapsed.TotalMilliseconds : 0f);
            return seeds;
        }

        private static List<CandidateSeed> CreateInsideSpaceSeeds(
            GenerationContext context,
            AssetDefinition asset,
            RelativeAnchor anchor,
            IGenerationProfiler profiler)
        {
            Stopwatch generationStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
            AssetRelativePlacementRule rule = asset.AssetRelativePlacement;
            Bounds bounds = CreateVolumeSamplingBounds(context, asset, anchor, rule);
            List<Vector3> positions = CreateVolumePositions(context, bounds, asset, anchor, rule);
            List<CandidateSeed> seeds = new();

            profiler.RecordRawSamples(PlacementType.InsideSpace, positions.Count);
            foreach (Vector3 position in positions)
            {
                Stopwatch projectionStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                bool inside = context.Area.ContainsVolumePoint(position);
                projectionStopwatch?.Stop();
                profiler.RecordProjection(
                    PlacementType.InsideSpace,
                    inside,
                    projectionStopwatch != null ? (float)projectionStopwatch.Elapsed.TotalMilliseconds : 0f);
                if (!inside)
                    continue;

                CandidateSeed seed = new(
                    position,
                    Quaternion.identity,
                    surfaceNormal: Vector3.up,
                    placementType: PlacementType.InsideSpace);
                if (!RelativeAnchorProvider.IsPotentialSeedForAnchor(context, seed, asset, anchor))
                    continue;

                seeds.Add(seed);
                if (seeds.Count >= MaximumCandidateSeeds)
                    break;
            }

            profiler.RecordCandidateSeeds(PlacementType.InsideSpace, seeds.Count);
            generationStopwatch?.Stop();
            profiler.AddSeedGenerationTime(
                PlacementType.InsideSpace,
                generationStopwatch != null ? (float)generationStopwatch.Elapsed.TotalMilliseconds : 0f);
            return seeds;
        }
    }
}
