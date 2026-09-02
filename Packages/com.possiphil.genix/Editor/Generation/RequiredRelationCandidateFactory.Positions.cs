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
            if (rule.UsesPathStations)
                AddPosition(positions, identities, bounds, anchor.Position);

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
    }
}
