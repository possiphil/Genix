using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Editor.Generation;
using Genix.Editor.Assets;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Evaluation
{
    internal static partial class GenerationResultEvaluator
    {
        private static GenerationEvaluationCheckRecord EvaluateSupportSemantics(
            IReadOnlyList<GeneratedEntry> entries)
        {
            int violations = 0;

            foreach (GeneratedEntry entry in entries)
            {
                if (!entry.Asset || entry.Asset.PlacementType == PlacementType.InsideSpace)
                    continue;

                PlacementSurfaceDescriptor support = entry.Support;
                if (support && !support.AcceptsAsset(entry.Asset))
                {
                    violations++;
                    continue;
                }

                if (entry.Asset.ForbiddenSupportAnyCategories.Count > 0 ||
                    entry.Asset.RequiredSupportNoneCategories.Count > 0)
                {
                    violations++;
                    continue;
                }

                if (entry.Asset.ForbiddenSupportTags.Any(tag => support && support.HasTag(tag)))
                {
                    violations++;
                    continue;
                }

                if (!PlacementSupportRules.MatchesRequiredSupportTags(entry.Asset, support))
                    violations++;
            }

            return Record(
                "Support Semantics",
                violations == 0,
                violations,
                violations == 0
                    ? "Every generated object uses a compatible semantic support."
                    : $"{violations} generated objects use an incompatible or missing semantic support.");
        }

        private static GenerationEvaluationCheckRecord EvaluateSpatialSource(IAreaSource areaSource)
        {
            if (areaSource is not IAreaSourceIntegrityStatus status)
                return Unavailable("Spatial Source Integrity", "The selected area provider does not expose source integrity.");

            return Record(
                "Spatial Source Integrity",
                status.UsedAuthoritativeSpatialData,
                status.UsedAuthoritativeSpatialData ? 0 : 1,
                status.SpatialDataStatusMessage);
        }

        private static GenerationEvaluationCheckRecord EvaluateExclusions(
            PlacementArea area,
            IReadOnlyList<GeneratedEntry> entries)
        {
            IReadOnlyList<PlacementExclusionRegion> regions = PlacementExclusionRegion.Collect(area.WorldBounds);
            int violations = entries.Count(entry => entry.Asset && regions.Any(region =>
                region.Intersects(entry.Bounds, entry.Asset.PlacementType, entry.Asset) ||
                entry.Asset.ReserveClearance && region.Intersects(
                    entry.Asset.CreateClearanceBounds(entry.Root.position, entry.Root.rotation),
                    entry.Asset.PlacementType,
                    entry.Asset)));
            return Record(
                "Exclusion Regions",
                violations == 0,
                violations,
                regions.Count == 0
                    ? "No active exclusion region intersects this target."
                    : violations == 0
                        ? $"All generated object and clearance volumes remain outside {regions.Count} active exclusion regions."
                        : $"{violations} generated object or clearance volumes intersect an active exclusion region.");
        }

        private static GenerationEvaluationCheckRecord EvaluateLimits(
            AssetPool pool,
            IReadOnlyList<GeneratedEntry> entries)
        {
            int violations = 0;
            Dictionary<AssetDefinition, int> counts = entries
                .Where(entry => entry.Asset)
                .GroupBy(entry => entry.Asset)
                .ToDictionary(group => group.Key, group => group.Count());

            foreach ((AssetDefinition asset, int count) in counts)
            {
                if (asset.LimitPlacements && count > asset.MaxPlacements)
                    violations++;
            }

            if (pool)
            {
                foreach (AssetPoolTagLimit limit in pool.TagPlacementLimits.Where(limit => limit?.IsConfigured == true))
                {
                    int count = entries.Count(entry => entry.Asset && limit.Matches(entry.Asset));
                    if (count < limit.MinPlacements || count > limit.MaxPlacements)
                        violations++;
                }
            }

            foreach (IGrouping<PlacementSurfaceDescriptor, GeneratedEntry> group in entries
                         .Where(entry => entry.Support)
                         .GroupBy(entry => entry.Support))
            {
                PlacementSurfaceDescriptor support = group.Key;
                if (support.LimitCapacity && group.Count() > support.MaxCapacity)
                    violations++;

                foreach (PlacementSurfaceCapacityRule rule in support.AssetCapacityRules.Where(rule => rule?.IsConfigured == true))
                {
                    if (group.Count(entry => rule.Matches(entry.Asset)) > rule.MaxCapacity)
                        violations++;
                }
            }

            return Record(
                "Placement Limits",
                violations == 0,
                violations,
                violations == 0
                    ? "Asset, tag-group, and support-surface limits are satisfied."
                    : $"{violations} asset, tag-group, or support-surface limits are violated.");
        }
    }
}
