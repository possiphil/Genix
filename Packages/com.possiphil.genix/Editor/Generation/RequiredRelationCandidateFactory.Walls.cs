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
    internal static partial class RequiredRelationCandidateFactory
    {
        private static List<CandidateSeed> CreateWallSeeds(
            GenerationContext context,
            AssetDefinition asset,
            RelativeAnchor anchor,
            IGenerationProfiler profiler)
        {
            List<CandidateSeed> localSeeds = CreateWallRegionSeeds(
                context,
                asset,
                anchor,
                profiler);
            if (localSeeds.Count > 0)
                return localSeeds;

            const int wallCandidateBudget = MaximumCandidateSeeds * 8;
            List<CandidateSeed> generated = new WallCandidateProvider(
                    requestedCount: 1,
                    minimumCandidateCount: 0,
                    candidateCount: wallCandidateBudget)
                .CreateCandidateSeeds(context, NullDiagnosticsSink.Instance, profiler);

            List<CandidateSeed> result = generated
                .Where(seed =>
                    PlacementSupportRules.TryValidateCompatibility(seed, asset, out _, out _) &&
                    RelativeAnchorProvider.IsPotentialSeedForAnchor(context, seed, asset, anchor))
                .ToList();
            OrderSeeds(context, result, asset, anchor, asset.AssetRelativePlacement);
            if (result.Count > MaximumCandidateSeeds)
                result.RemoveRange(MaximumCandidateSeeds, result.Count - MaximumCandidateSeeds);
            return result;
        }

        private static List<CandidateSeed> CreateWallRegionSeeds(
            GenerationContext context,
            AssetDefinition asset,
            RelativeAnchor anchor,
            IGenerationProfiler profiler)
        {
            if (context.Area.UsesAllMatchingSurfaceSearch)
                return CreateWallSourceSeeds(context, asset, anchor, profiler);

            List<CandidateSeed> seeds = new();
            HashSet<WallSeedIdentity> identities = new();
            AssetRelativePlacementRule rule = asset.AssetRelativePlacement;
            float sampleSpacing = Mathf.Clamp(asset.Width * 0.5f, 0.05f, 0.25f);
            float searchRadius = rule.MaximumDistance + asset.Width * 0.5f;

            foreach (SurfaceRegion region in context.Area.WallRegions)
            {
                Vector3 segment = region.WallEnd - region.WallStart;
                float length = segment.magnitude;
                if (length <= 0.001f)
                    continue;

                Vector3 direction = segment / length;
                float anchorDistance = Mathf.Clamp(
                    Vector3.Dot(anchor.Position - region.WallStart, direction),
                    0f,
                    length);
                float minimum = Mathf.Max(0f, anchorDistance - searchRadius);
                float maximum = Mathf.Min(length, anchorDistance + searchRadius);
                int sampleCount = Mathf.Clamp(
                    Mathf.CeilToInt((maximum - minimum) / sampleSpacing) + 1,
                    3,
                    MaximumCandidateSeeds);
                List<float> sampleHeights = CreateWallSampleHeights(
                    region.Bounds,
                    anchor,
                    rule,
                    asset,
                    region.WallStart.y);

                foreach (float sampleHeight in sampleHeights)
                {
                    for (int i = 0; i < sampleCount && seeds.Count < MaximumCandidateSeeds; i++)
                    {
                        float wallDistance = Interpolate(minimum, maximum, i, sampleCount);
                        Vector3 position = region.WallStart + direction * wallDistance;
                        position.y = sampleHeight;
                        bool projected = context.Area.TryProjectToWall(
                            position,
                            region.Normal,
                            region.VoxelLayer,
                            out SurfacePoint point,
                            profiler);
                        profiler.RecordRawSamples(PlacementType.Wall, 1);
                        profiler.RecordProjection(PlacementType.Wall, projected, 0f);
                        if (!projected)
                            continue;

                        CandidateSeed seed = new(
                            point.Position,
                            Quaternion.LookRotation(point.Normal, Vector3.up),
                            point.SurfaceCollider,
                            point.Normal,
                            point.VoxelLayer,
                            PlacementType.Wall);
                        if (!PlacementSupportRules.TryValidateCompatibility(seed, asset, out _, out _) ||
                            rule.UsesVerticalSides &&
                            !RelativeAnchorProvider.IsPotentialSeedForAnchor(context, seed, asset, anchor) ||
                            !identities.Add(new WallSeedIdentity(seed)))
                        {
                            continue;
                        }

                        seeds.Add(seed);
                    }

                    if (seeds.Count >= MaximumCandidateSeeds)
                        break;
                }
            }

            OrderSeeds(context, seeds, asset, anchor, rule);
            profiler.RecordCandidateSeeds(PlacementType.Wall, seeds.Count);
            return seeds;
        }

        private static List<CandidateSeed> CreateWallSourceSeeds(
            GenerationContext context,
            AssetDefinition asset,
            RelativeAnchor anchor,
            IGenerationProfiler profiler)
        {
            List<CandidateSeed> seeds = new();
            HashSet<WallSeedIdentity> identities = new();
            List<SurfacePoint> points = new(2);
            AssetRelativePlacementRule rule = asset.AssetRelativePlacement;
            float sampleSpacing = Mathf.Clamp(asset.Width * 0.5f, 0.05f, 0.25f);
            float searchRadius = rule.MaximumDistance + asset.Width * 0.5f;

            foreach (WallSurfaceSource source in context.Area.WallSurfaceSources)
            {
                if (!source.Collider || source.IsTerrain)
                    continue;

                SampleWallSourceAxis(
                    context,
                    asset,
                    anchor,
                    source,
                    WallSurfaceSampleAxis.X,
                    source.Bounds.min.z,
                    source.Bounds.max.z,
                    anchor.Position.z,
                    searchRadius,
                    sampleSpacing,
                    points,
                    identities,
                    seeds,
                    profiler);
                SampleWallSourceAxis(
                    context,
                    asset,
                    anchor,
                    source,
                    WallSurfaceSampleAxis.Z,
                    source.Bounds.min.x,
                    source.Bounds.max.x,
                    anchor.Position.x,
                    searchRadius,
                    sampleSpacing,
                    points,
                    identities,
                    seeds,
                    profiler);

                if (seeds.Count >= MaximumCandidateSeeds)
                    break;
            }

            OrderSeeds(context, seeds, asset, anchor, rule);
            profiler.RecordCandidateSeeds(PlacementType.Wall, seeds.Count);
            return seeds;
        }

        private static void SampleWallSourceAxis(
            GenerationContext context,
            AssetDefinition asset,
            RelativeAnchor anchor,
            WallSurfaceSource source,
            WallSurfaceSampleAxis axis,
            float sourceMinimum,
            float sourceMaximum,
            float anchorCoordinate,
            float searchRadius,
            float sampleSpacing,
            List<SurfacePoint> points,
            ISet<WallSeedIdentity> identities,
            ICollection<CandidateSeed> seeds,
            IGenerationProfiler profiler)
        {
            float minimum = Mathf.Max(sourceMinimum, anchorCoordinate - searchRadius);
            float maximum = Mathf.Min(sourceMaximum, anchorCoordinate + searchRadius);
            if (maximum < minimum)
                return;

            int sampleCount = Mathf.Clamp(
                Mathf.CeilToInt((maximum - minimum) / sampleSpacing) + 1,
                3,
                MaximumCandidateSeeds);
            float defaultSampleY = Mathf.Clamp(
                anchor.Bounds.center.y,
                source.Bounds.min.y,
                source.Bounds.max.y);
            List<float> sampleHeights = CreateWallSampleHeights(
                source.Bounds,
                anchor,
                asset.AssetRelativePlacement,
                asset,
                defaultSampleY);

            foreach (float sampleHeight in sampleHeights)
            {
                for (int i = 0; i < sampleCount && seeds.Count < MaximumCandidateSeeds; i++)
                {
                    float horizontal = Interpolate(minimum, maximum, i, sampleCount);
                    Vector3 encodedPosition = new(horizontal, 0f, sampleHeight);
                    points.Clear();
                    int projected = context.Area.CollectWallSurfaces(
                        source,
                        axis,
                        encodedPosition,
                        points,
                        profiler);
                    profiler.RecordRawSamples(PlacementType.Wall, 1);
                    profiler.RecordProjection(PlacementType.Wall, projected > 0, 0f);

                    foreach (SurfacePoint point in points)
                    {
                        CandidateSeed seed = new(
                            point.Position,
                            Quaternion.LookRotation(point.Normal, Vector3.up),
                            point.SurfaceCollider,
                            point.Normal,
                            point.VoxelLayer,
                            PlacementType.Wall);
                        if (!PlacementSupportRules.TryValidateCompatibility(seed, asset, out _, out _) ||
                            asset.AssetRelativePlacement.UsesVerticalSides &&
                            !RelativeAnchorProvider.IsPotentialSeedForAnchor(context, seed, asset, anchor) ||
                            !identities.Add(new WallSeedIdentity(seed)))
                        {
                            continue;
                        }

                        seeds.Add(seed);
                        if (seeds.Count >= MaximumCandidateSeeds)
                            break;
                    }
                }

                if (seeds.Count >= MaximumCandidateSeeds)
                    break;
            }
        }

        private static List<float> CreateWallSampleHeights(
            Bounds wallBounds,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule,
            AssetDefinition asset,
            float defaultHeight)
        {
            if (!rule.UsesVerticalSides)
                return new List<float> { defaultHeight };

            float margin = Mathf.Max(asset.Height * 0.5f, 0.01f);
            float minimum = Mathf.Max(wallBounds.min.y + margin, anchor.Position.y - rule.MaximumDistance);
            float maximum = Mathf.Min(wallBounds.max.y - margin, anchor.Position.y + rule.MaximumDistance);
            if (maximum < minimum)
                return new List<float>();

            int count = GetSampleCount(maximum - minimum, Mathf.Max(asset.Height, 0.1f));
            List<float> heights = new(count);
            for (int i = 0; i < count; i++)
                heights.Add(Interpolate(minimum, maximum, i, count));

            return heights;
        }
    }
}

