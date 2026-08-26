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
    internal static class RequiredRelationCandidateFactory
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

        private static Bounds CreateSamplingBounds(
            GenerationContext context,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            float horizontalMargin = rule.MaximumDistance +
                                     Mathf.Max(asset.Width, asset.Depth) * 0.5f;
            Bounds bounds = anchor.Bounds;
            bounds.Expand(new Vector3(horizontalMargin * 2f, 0f, horizontalMargin * 2f));

            if (rule.RequireSameSupportSurface &&
                anchor.SupportSurface &&
                SupportSurfaceSampling.TryGetColliderBounds(
                    anchor.SupportSurface,
                    context.TargetBounds,
                    out _,
                    out Bounds supportBounds))
            {
                InsetHorizontal(ref supportBounds, asset);
                IntersectHorizontal(ref bounds, supportBounds);
            }

            IntersectHorizontal(ref bounds, context.TargetBounds);
            return bounds;
        }

        private static List<Vector3> CreatePositions(
            GenerationContext context,
            Bounds bounds,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            List<Vector3> positions = new();
            HashSet<PositionIdentity> identities = new();
            AddCompatibleSupportCenters(context, positions, identities, bounds, asset);
            AddSidePositions(positions, identities, bounds, asset, anchor, rule);

            int xCount = GetSampleCount(bounds.size.x, Mathf.Max(asset.Width, 0.1f));
            int zCount = GetSampleCount(bounds.size.z, Mathf.Max(asset.Depth, 0.1f));

            for (int z = 0; z < zCount; z++)
            {
                float zPosition = Interpolate(bounds.min.z, bounds.max.z, z, zCount);
                for (int x = 0; x < xCount; x++)
                {
                    AddPosition(positions, identities, bounds, new Vector3(
                        Interpolate(bounds.min.x, bounds.max.x, x, xCount),
                        anchor.Position.y,
                        zPosition));
                }
            }

            OrderPositions(context, positions, asset, anchor, rule);
            return positions;
        }

        private static void AddCompatibleSupportCenters(
            GenerationContext context,
            ICollection<Vector3> positions,
            ISet<PositionIdentity> identities,
            Bounds samplingBounds,
            AssetDefinition asset)
        {
            foreach (SupportSurfaceSamplingEntry surface in SupportSurfaceSampling.Collect(
                         context,
                         new[] { asset },
                         asset.PlacementType))
            {
                Vector3 center = surface.Bounds.center;
                if (center.x < samplingBounds.min.x || center.x > samplingBounds.max.x ||
                    center.z < samplingBounds.min.z || center.z > samplingBounds.max.z)
                {
                    continue;
                }

                AddPosition(positions, identities, samplingBounds, center);
            }
        }

        private static Bounds CreateVolumeSamplingBounds(
            GenerationContext context,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            float margin = rule.MaximumDistance + AssetAttemptPlanner.Dimensions(asset).magnitude * 0.5f;
            Bounds bounds = anchor.Bounds;
            bounds.Expand(margin * 2f);
            Intersect(ref bounds, context.TargetBounds);
            return bounds;
        }

        private static List<Vector3> CreateVolumePositions(
            GenerationContext context,
            Bounds bounds,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            int xCount = GetSampleCount(bounds.size.x, Mathf.Max(asset.Width, 0.1f));
            int yCount = GetSampleCount(bounds.size.y, Mathf.Max(asset.Height, 0.1f));
            int zCount = GetSampleCount(bounds.size.z, Mathf.Max(asset.Depth, 0.1f));
            List<Vector3> positions = new(xCount * yCount * zCount);

            for (int y = 0; y < yCount; y++)
            {
                float yPosition = Interpolate(bounds.min.y, bounds.max.y, y, yCount);
                for (int z = 0; z < zCount; z++)
                {
                    float zPosition = Interpolate(bounds.min.z, bounds.max.z, z, zCount);
                    for (int x = 0; x < xCount; x++)
                    {
                        positions.Add(new Vector3(
                            Interpolate(bounds.min.x, bounds.max.x, x, xCount),
                            yPosition,
                            zPosition));
                    }
                }
            }

            OrderPositions(context, positions, asset, anchor, rule);
            return positions;
        }

        private static void OrderPositions(
            GenerationContext context,
            List<Vector3> positions,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            if (rule.Alignment == AssetRelativeAlignment.Random)
            {
                context.Random.Shuffle(positions);
                return;
            }

            positions.Sort((left, right) => ComparePositions(left, right, asset, anchor, rule));
        }

        private static void OrderSeeds(
            GenerationContext context,
            List<CandidateSeed> seeds,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            if (rule.Alignment == AssetRelativeAlignment.Random)
            {
                context.Random.Shuffle(seeds);
                return;
            }

            seeds.Sort((left, right) =>
                ComparePositions(left.Position, right.Position, asset, anchor, rule));
        }

        private static void AddSidePositions(
            List<Vector3> positions,
            ISet<PositionIdentity> identities,
            Bounds bounds,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            Vector3 forward = HorizontalDirection(anchor.Forward, Vector3.forward);
            Vector3 right = HorizontalDirection(anchor.Right, Vector3.right);
            AssetRelativeSide[] sides =
            {
                AssetRelativeSide.Front,
                AssetRelativeSide.Back,
                AssetRelativeSide.Left,
                AssetRelativeSide.Right
            };
            float candidateHalfExtent = Mathf.Max(asset.Width, asset.Depth) * 0.5f;

            foreach (AssetRelativeSide side in sides)
            {
                if (rule.Side != AssetRelativeSide.Any && !rule.AllowsSide(side))
                    continue;

                Vector3 outward = side switch
                {
                    AssetRelativeSide.Front => forward,
                    AssetRelativeSide.Back => -forward,
                    AssetRelativeSide.Left => -right,
                    _ => right
                };
                Vector3 tangent = side is AssetRelativeSide.Front or AssetRelativeSide.Back
                    ? right
                    : forward;
                float anchorOutwardExtent = ProjectedHorizontalExtent(anchor.Bounds.extents, outward);
                float anchorTangentExtent = ProjectedHorizontalExtent(anchor.Bounds.extents, tangent);

                float[] radialOffsets =
                {
                    anchorOutwardExtent * 0.35f,
                    anchorOutwardExtent * 0.65f,
                    anchorOutwardExtent * 0.85f,
                    anchorOutwardExtent + Mathf.Clamp(
                        Mathf.Max(candidateHalfExtent + 0.005f, rule.MinimumDistance),
                        rule.MinimumDistance,
                        rule.MaximumDistance),
                    anchorOutwardExtent + rule.MaximumDistance
                };
                float[] tangentFactors = { 0f, -0.4f, 0.4f, -0.75f, 0.75f };

                foreach (float radialOffset in radialOffsets)
                {
                    foreach (float tangentFactor in tangentFactors)
                    {
                        Vector3 position = anchor.Position +
                                           outward * radialOffset +
                                           tangent * (anchorTangentExtent * tangentFactor);
                        position.y = anchor.Position.y;
                        AddPosition(positions, identities, bounds, position);
                    }
                }
            }
        }

        private static int ComparePositions(
            Vector3 left,
            Vector3 right,
            AssetDefinition asset,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            int sideComparison = GetSidePenalty(left, anchor, rule)
                .CompareTo(GetSidePenalty(right, anchor, rule));
            if (sideComparison != 0)
                return sideComparison;

            float preferredDistance = rule.RequireSameSupportSurface
                ? Mathf.Clamp(
                    Mathf.Max(asset.Width, asset.Depth) * 0.5f + 0.005f,
                    rule.MinimumDistance,
                    rule.MaximumDistance)
                : (rule.MinimumDistance + rule.MaximumDistance) * 0.5f;
            int distanceComparison = Mathf.Abs(DistanceToBounds(left, anchor.Bounds) - preferredDistance)
                .CompareTo(Mathf.Abs(DistanceToBounds(right, anchor.Bounds) - preferredDistance));
            if (distanceComparison != 0)
                return distanceComparison;

            int alignmentComparison = GetAlignmentValue(left, anchor, rule)
                .CompareTo(GetAlignmentValue(right, anchor, rule));
            if (alignmentComparison != 0)
                return alignmentComparison;

            int zComparison = left.z.CompareTo(right.z);
            return zComparison != 0 ? zComparison : left.x.CompareTo(right.x);
        }

        private static float GetAlignmentValue(
            Vector3 position,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            Vector3 offset = position - anchor.Position;
            Vector3 forward = HorizontalDirection(anchor.Forward, Vector3.forward);
            Vector3 right = HorizontalDirection(anchor.Right, Vector3.right);

            if (rule.Alignment == AssetRelativeAlignment.Center)
            {
                AssetRelativeSide matchedSide = GetDominantSide(offset, forward, right, rule);
                if (matchedSide is AssetRelativeSide.Above or AssetRelativeSide.Below)
                    return Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude;

                return matchedSide is AssetRelativeSide.Left or AssetRelativeSide.Right
                    ? Mathf.Abs(Vector3.Dot(offset, forward))
                    : Mathf.Abs(Vector3.Dot(offset, right));
            }

            Vector3 tangentAxis = rule.Side is AssetRelativeSide.Left or AssetRelativeSide.Right
                ? forward
                : right;
            float tangent = Vector3.Dot(offset, tangentAxis);
            float end = ProjectedHorizontalExtent(anchor.Bounds.extents, tangentAxis);
            float preferred = rule.Alignment == AssetRelativeAlignment.End ? end : -end;
            return Mathf.Abs(tangent - preferred);
        }

        private static AssetRelativeSide GetDominantSide(
            Vector3 offset,
            Vector3 forward,
            Vector3 right,
            AssetRelativePlacementRule rule)
        {
            float forwardDistance = Vector3.Dot(offset, forward);
            float rightDistance = Vector3.Dot(offset, right);
            float verticalDistance = offset.y;
            float horizontalMagnitude = Mathf.Max(
                Mathf.Abs(forwardDistance),
                Mathf.Abs(rightDistance));

            if (rule.UsesVerticalSides && Mathf.Abs(verticalDistance) >= horizontalMagnitude)
            {
                return verticalDistance >= 0f
                    ? AssetRelativeSide.Above
                    : AssetRelativeSide.Below;
            }

            return Mathf.Abs(forwardDistance) >= Mathf.Abs(rightDistance)
                ? forwardDistance >= 0f
                    ? AssetRelativeSide.Front
                    : AssetRelativeSide.Back
                : rightDistance >= 0f
                    ? AssetRelativeSide.Right
                    : AssetRelativeSide.Left;
        }

        private static int GetSidePenalty(
            Vector3 position,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule) =>
            RelativeAnchorProvider.MatchesSide(position, anchor, rule) ? 0 : 1;

        private static void CollectSurfaces(
            GenerationContext context,
            PlacementType placementType,
            Vector3 position,
            List<SurfacePoint> points,
            IGenerationProfiler profiler)
        {
            if (context.Area.UsesAllMatchingSurfaceSearch)
            {
                if (placementType == PlacementType.Ceiling)
                    context.Area.CollectCeilingSurfaces(position, points, profiler);
                else
                    context.Area.CollectFloorSurfaces(position, points, profiler);
                return;
            }

            bool projected = placementType == PlacementType.Ceiling
                ? context.Area.TryProjectToCeiling(position, out SurfacePoint point, profiler)
                : context.Area.TryProjectToFloor(position, out point, profiler);
            if (projected)
                points.Add(point);
        }

        private static int GetSampleCount(float length, float assetSize) =>
            Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0f, length) / assetSize) + 1, 3, MaximumAxisSamples);

        private static float Interpolate(float minimum, float maximum, int index, int count) =>
            count <= 1 ? (minimum + maximum) * 0.5f : Mathf.Lerp(minimum, maximum, index / (float)(count - 1));

        private static float DistanceToBounds(Vector3 position, Bounds bounds) =>
            Vector3.Distance(position, bounds.ClosestPoint(position));

        private static Vector3 HorizontalDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : fallback;
        }

        private static float ProjectedHorizontalExtent(Vector3 extents, Vector3 direction) =>
            Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.z) * extents.z;

        private static void AddPosition(
            ICollection<Vector3> positions,
            ISet<PositionIdentity> identities,
            Bounds bounds,
            Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            if (identities.Add(new PositionIdentity(position)))
                positions.Add(position);
        }

        private static void IntersectHorizontal(ref Bounds bounds, Bounds other)
        {
            float minX = Mathf.Max(bounds.min.x, other.min.x);
            float maxX = Mathf.Min(bounds.max.x, other.max.x);
            float minZ = Mathf.Max(bounds.min.z, other.min.z);
            float maxZ = Mathf.Min(bounds.max.z, other.max.z);

            if (minX > maxX || minZ > maxZ)
                return;

            bounds.SetMinMax(
                new Vector3(minX, bounds.min.y, minZ),
                new Vector3(maxX, bounds.max.y, maxZ));
        }

        private static void Intersect(ref Bounds bounds, Bounds other)
        {
            Vector3 minimum = Vector3.Max(bounds.min, other.min);
            Vector3 maximum = Vector3.Min(bounds.max, other.max);
            if (minimum.x > maximum.x || minimum.y > maximum.y || minimum.z > maximum.z)
                return;

            bounds.SetMinMax(minimum, maximum);
        }

        private static void InsetHorizontal(ref Bounds bounds, AssetDefinition asset)
        {
            float inset = Mathf.Max(asset.Width, asset.Depth) * 0.5f + 0.002f;
            if (bounds.size.x <= inset * 2f || bounds.size.z <= inset * 2f)
                return;

            bounds.SetMinMax(
                new Vector3(bounds.min.x + inset, bounds.min.y, bounds.min.z + inset),
                new Vector3(bounds.max.x - inset, bounds.max.y, bounds.max.z - inset));
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

        private readonly struct PositionIdentity : System.IEquatable<PositionIdentity>
        {
            private readonly int _x;
            private readonly int _z;

            public PositionIdentity(Vector3 position)
            {
                _x = Mathf.RoundToInt(position.x * PositionQuantization);
                _z = Mathf.RoundToInt(position.z * PositionQuantization);
            }

            public bool Equals(PositionIdentity other) => _x == other._x && _z == other._z;

            public override bool Equals(object obj) => obj is PositionIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_x * 397) ^ _z;
                }
            }
        }

        private readonly struct WallSeedIdentity : System.IEquatable<WallSeedIdentity>
        {
            private readonly Collider _collider;
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;

            public WallSeedIdentity(CandidateSeed seed)
            {
                _collider = seed.SurfaceCollider;
                _x = Mathf.RoundToInt(seed.Position.x * PositionQuantization);
                _y = Mathf.RoundToInt(seed.Position.y * PositionQuantization);
                _z = Mathf.RoundToInt(seed.Position.z * PositionQuantization);
            }

            public bool Equals(WallSeedIdentity other) =>
                _collider == other._collider && _x == other._x && _y == other._y && _z == other._z;

            public override bool Equals(object obj) => obj is WallSeedIdentity other && Equals(other);

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
    }
}
