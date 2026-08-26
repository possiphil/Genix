using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Editor.Generation;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Evaluation
{
    /// <summary>Evaluates generated production metadata without changing generation decisions.</summary>
    internal static class GenerationResultEvaluator
    {
        private const float BoundsTolerance = 0.002f;
        private const float FacingTolerance = 1f;

        private sealed class GeneratedEntry
        {
            public Transform Root { get; set; }
            public GeneratedObjectMetadata Metadata { get; set; }
            public AssetDefinition Asset => Metadata ? Metadata.AssetDefinition : null;
            public PlacementSurfaceDescriptor Support => Metadata ? Metadata.SupportSurface : null;
            public OrientedBounds Bounds { get; set; }
            public OrientedBounds ContainmentBounds { get; set; }
            public Quaternion PlacementRotation { get; set; }
        }

        public static List<GenerationEvaluationCheckRecord> Evaluate(
            GenerationEvaluationScenario scenario,
            GenerationRequest request,
            GenerationDiagnostics diagnostics)
        {
            List<GenerationEvaluationCheckRecord> records = new();
            List<GeneratedEntry> entries = CollectEntries(request.AreaSource);
            EvaluationCheckSet enabled = scenario.Checks;

            AddIfEnabled(records, enabled, EvaluationCheckSet.Completion,
                EvaluateCompletion(scenario, request, diagnostics, entries.Count));
            AddIfEnabled(records, enabled, EvaluationCheckSet.Metadata,
                EvaluateMetadata(entries));

            if (!TryCreateContext(
                    request,
                    entries,
                    out GenerationContext context,
                    out List<AssetDefinition> resolvedAssets,
                    out string contextError))
            {
                AddUnavailable(records, enabled, EvaluationCheckSet.SpatialSourceIntegrity,
                    "Spatial Source Integrity", contextError);
                AddUnavailable(records, enabled, EvaluationCheckSet.TargetContainment, "Target Containment", contextError);
                AddUnavailable(records, enabled, EvaluationCheckSet.AssetRelations, "Asset Relations", contextError);
                AddUnavailable(records, enabled, EvaluationCheckSet.ExclusionRegions, "Exclusion Regions", contextError);
            }
            else
            {
                AddIfEnabled(records, enabled, EvaluationCheckSet.SpatialSourceIntegrity,
                    EvaluateSpatialSource(request.AreaSource));
                AddIfEnabled(records, enabled, EvaluationCheckSet.TargetContainment,
                    EvaluateContainment(context.Area, entries));
                AddIfEnabled(records, enabled, EvaluationCheckSet.AssetRelations,
                    EvaluateRelations(context, resolvedAssets, entries));
                AddIfEnabled(records, enabled, EvaluationCheckSet.ExclusionRegions,
                    EvaluateExclusions(context.Area, entries));
            }

            AddIfEnabled(records, enabled, EvaluationCheckSet.NonOverlap,
                EvaluateGeneratedOverlap(entries));
            AddIfEnabled(records, enabled, EvaluationCheckSet.SupportSemantics,
                EvaluateSupportSemantics(entries));
            AddIfEnabled(records, enabled, EvaluationCheckSet.PlacementLimits,
                EvaluateLimits(request.AssetPool, entries));
            AddIfEnabled(records, enabled, EvaluationCheckSet.SamplingSpacing,
                EvaluateSamplingSpacing(request, diagnostics));

            if (context != null)
            {
                AddIfEnabled(records, enabled, EvaluationCheckSet.RelativePlacement,
                    EvaluateRelativePlacement(context, entries));
            }
            else
            {
                AddUnavailable(records, enabled, EvaluationCheckSet.RelativePlacement,
                    "Relative Placement", contextError);
            }

            return records;
        }

        private static GenerationEvaluationCheckRecord EvaluateSamplingSpacing(
            GenerationRequest request,
            GenerationDiagnostics diagnostics)
        {
            float minimum = Mathf.Max(0f, request.StyleSettings.poisson.minDistance);
            if (minimum <= 0f || diagnostics == null)
                return Unavailable("Sampling Spacing", "No Poisson placement observations are available.");

            int violations = 0;
            IReadOnlyList<PlacementDiagnostic> placements = diagnostics.Placements;
            float thresholdSquared = Mathf.Max(0f, minimum - 0.001f);
            thresholdSquared *= thresholdSquared;

            for (int i = 0; i < placements.Count - 1; i++)
            for (int j = i + 1; j < placements.Count; j++)
            {
                if ((placements[i].Position - placements[j].Position).sqrMagnitude < thresholdSquared)
                    violations++;
            }

            return Record(
                "Sampling Spacing",
                violations == 0,
                violations,
                violations == 0
                    ? $"All accepted sample positions preserve the {minimum:F2} unit minimum distance."
                    : $"{violations} accepted sample pairs are closer than {minimum:F2} units.");
        }

        private static GenerationEvaluationCheckRecord EvaluateRelativePlacement(
            GenerationContext context,
            IReadOnlyList<GeneratedEntry> entries)
        {
            if (context.RelativePlacement?.IsEnabled != true)
                return Unavailable("Relative Placement", "Global relative placement is disabled.");

            int violations = entries.Count(entry => !RelativeAnchorProvider.IsCandidateInRange(
                new PlacementCandidate(
                    entry.Bounds.Center,
                    entry.PlacementRotation,
                    placementType: entry.Asset ? entry.Asset.PlacementType : PlacementType.Floor),
                context,
                out _));

            return Record(
                "Relative Placement",
                violations == 0,
                violations,
                violations == 0
                    ? $"All generated objects remain within {context.RelativePlacement.Radius:F2} units of a configured anchor."
                    : $"{violations} generated objects lie outside the configured relative-placement radius.");
        }

        private static GenerationEvaluationCheckRecord EvaluateCompletion(
            GenerationEvaluationScenario scenario,
            GenerationRequest request,
            GenerationDiagnostics diagnostics,
            int generatedCount)
        {
            int placed = diagnostics?.PlacedObjectCount ?? generatedCount;
            float ratio = request.ObjectCount > 0 ? placed / (float)request.ObjectCount : 0f;
            bool passed = ratio + 0.0001f >= scenario.MinimumCompletionRatio &&
                          ratio - 0.0001f <= scenario.MaximumCompletionRatio;
            return Record(
                "Completion",
                passed,
                passed ? 0 : 1,
                $"Placed {placed} of {request.ObjectCount} ({ratio:P1}); accepted interval " +
                $"{scenario.MinimumCompletionRatio:P0}-{scenario.MaximumCompletionRatio:P0}.");
        }

        private static GenerationEvaluationCheckRecord EvaluateMetadata(IReadOnlyList<GeneratedEntry> entries)
        {
            int violations = entries.Count(entry => !entry.Metadata || !entry.Asset || !entry.Asset.Prefab);
            return Record(
                "Generated Metadata",
                violations == 0,
                violations,
                violations == 0
                    ? $"All {entries.Count} generated roots retain asset and placement metadata."
                    : $"{violations} generated roots have incomplete metadata or asset references.");
        }

        private static GenerationEvaluationCheckRecord EvaluateContainment(
            PlacementArea area,
            IReadOnlyList<GeneratedEntry> entries)
        {
            int violations = entries.Count(entry => entry.Asset && !area.ContainsPlacementVolume(entry.ContainmentBounds));
            return Record(
                "Target Containment",
                violations == 0,
                violations,
                violations == 0
                    ? "Every generated placement bound remains inside the target volume."
                    : $"{violations} generated placement bounds extend outside the target volume.");
        }

        private static GenerationEvaluationCheckRecord EvaluateGeneratedOverlap(
            IReadOnlyList<GeneratedEntry> entries)
        {
            SpatialBoundsIndex index = new(capacity: entries.Count);
            int violations = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                GeneratedEntry entry = entries[i];
                Bounds query = Shrink(entry.Bounds).ToAxisAlignedBounds();

                foreach (int otherIndex in index.Query(query))
                {
                    if (HasGeneratedConflict(entry, entries[otherIndex]))
                        violations++;
                }

                index.Add(query, i);
            }

            return Record(
                "Generated Overlap",
                violations == 0,
                violations,
                violations == 0
                    ? "No generated placement bounds overlap."
                    : $"{violations} generated placement-bound pairs overlap.");
        }

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

        private static GenerationEvaluationCheckRecord EvaluateRelations(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> resolvedAssets,
            IReadOnlyList<GeneratedEntry> entries)
        {
            int violations = 0;
            Dictionary<(AssetDefinition Dependent, object Anchor), int> assignments = new();
            List<string> details = new();

            foreach (GeneratedEntry entry in entries)
            {
                AssetRelativePlacementRule rule = entry.Asset ? entry.Asset.AssetRelativePlacement : null;
                if (rule?.IsConfigured == true)
                {
                    if (!TryFindEvaluationAnchor(context, entry, rule, out RelativeAnchor anchor, out string relationError))
                    {
                        violations++;
                        AddDetail(details, $"{entry.Root.name}: {relationError}");
                    }
                    else
                    {
                        object identity = anchor.Identity ?? $"{anchor.Name}:{anchor.Position}";
                        (AssetDefinition, object) key = (entry.Asset, identity);
                        assignments.TryGetValue(key, out int count);
                        assignments[key] = count + 1;
                    }
                }

                if (!TryValidatePathPlacement(context, entry, out string pathError))
                {
                    violations++;
                    AddDetail(details, $"{entry.Root.name}: {pathError}");
                }
            }

            foreach (AssetDefinition dependent in resolvedAssets.Where(asset => asset).Distinct())
            {
                AssetRelativePlacementRule rule = dependent.AssetRelativePlacement;
                if (rule?.IsConfigured != true)
                    continue;

                foreach (RelativeAnchor anchor in RelativeAnchorProvider.CollectMatchingAssetAnchors(
                             context,
                             rule,
                             false,
                             dependent))
                {
                    object identity = anchor.Identity ?? $"{anchor.Name}:{anchor.Position}";
                    assignments.TryGetValue((dependent, identity), out int count);
                    if (count < rule.MinimumPerAnchor || count > rule.MaximumPerAnchor)
                    {
                        violations++;
                        AddDetail(
                            details,
                            $"{dependent.name} at {anchor.Name}: {count}, expected {rule.MinimumPerAnchor}-{rule.MaximumPerAnchor}");
                    }
                }
            }

            if (context.AssetPool)
            {
                foreach (AssetPoolAnchorGroupLimit group in context.AssetPool.AnchorGroupLimits
                             .Where(group => group?.IsConfigured == true))
                {
                    foreach (RelativeAnchor anchor in RelativeAnchorProvider.CollectMatchingAssetAnchors(
                                 context,
                                 group,
                                 includePlannedObjects: false))
                    {
                        int count = RelativeAnchorProvider.GetAssignedAssetTagCount(context, group, anchor);
                        if (count >= group.MinimumPerAnchor && count <= group.MaximumPerAnchor)
                            continue;

                        violations++;
                        AddDetail(
                            details,
                            $"{group.MemberTag.DisplayName} group at {anchor.Name}: {count}, expected " +
                            $"{group.MinimumPerAnchor}-{group.MaximumPerAnchor}");
                    }
                }
            }

            return Record(
                "Asset Relations",
                violations == 0,
                violations,
                violations == 0
                    ? "All generated relations, facing rules, and individual or grouped per-anchor cardinalities are satisfied."
                    : $"{violations} relation assignments, facing rules, or individual/grouped per-anchor cardinalities are invalid. " +
                      string.Join("; ", details));
        }

        private static GenerationEvaluationCheckRecord EvaluateSpatialSource(IAreaSource areaSource)
        {
            if (areaSource is not IAreaSourceEvaluationStatus status)
                return Unavailable("Spatial Source Integrity", "The selected area provider does not expose source integrity.");

            return Record(
                "Spatial Source Integrity",
                status.UsedAuthoritativeSpatialData,
                status.UsedAuthoritativeSpatialData ? 0 : 1,
                status.SpatialDataStatusMessage);
        }

        private static bool TryFindEvaluationAnchor(
            GenerationContext context,
            GeneratedEntry entry,
            AssetRelativePlacementRule rule,
            out RelativeAnchor result,
            out string error)
        {
            result = default;
            error = string.Empty;
            object previousIdentity = context.RequiredAssetRelationAnchorIdentity;

            try
            {
                if (!string.IsNullOrEmpty(entry.Metadata.RelationAnchorKey))
                {
                    bool foundPersistedAnchor = false;
                    foreach (RelativeAnchor candidate in RelativeAnchorProvider.CollectMatchingAssetAnchors(
                                 context,
                                 rule,
                                 false,
                                 entry.Asset))
                    {
                        if (candidate.PersistentIdentityKey != entry.Metadata.RelationAnchorKey)
                            continue;

                        foundPersistedAnchor = true;
                        context.RequiredAssetRelationAnchorIdentity = candidate.Identity;
                        if (!RelativeAnchorProvider.TryFindAssetAnchor(
                                context,
                                entry.Asset,
                                entry.Bounds.Center,
                                entry.Bounds.ToAxisAlignedBounds(),
                                entry.Support,
                                out RelativeAnchor persisted))
                        {
                            error = $"stored anchor {candidate.Name} no longer satisfies range, side, or support rules";
                            return false;
                        }

                        float facingAngle = GetFacingAngle(entry, persisted, rule);
                        if (facingAngle > rule.FacingVariationDegrees + FacingTolerance)
                        {
                            error = $"facing differs by {facingAngle:F1} degrees at stored anchor {candidate.Name} " +
                                    $"(allowed {rule.FacingVariationDegrees:F1}); object {entry.Bounds.Center:F2}, " +
                                    $"anchor {candidate.Position:F2}, forward " +
                                    $"{Vector3.ProjectOnPlane(entry.PlacementRotation * Vector3.forward, Vector3.up).normalized:F2}";
                            return false;
                        }

                        result = persisted;
                        return true;
                    }

                    error = foundPersistedAnchor
                        ? $"stored anchor {entry.Metadata.RelationAnchorKey} is invalid"
                        : $"stored anchor {entry.Metadata.RelationAnchorKey} was not found";
                    return false;
                }

                if (!RelativeAnchorProvider.TryFindAssetAnchor(
                        context,
                        entry.Asset,
                        entry.Bounds.Center,
                        entry.Bounds.ToAxisAlignedBounds(),
                        entry.Support,
                        out RelativeAnchor nearest))
                {
                    error = $"no valid anchor for {entry.Asset.name}";
                    return false;
                }

                result = nearest;
                if (MatchesFacing(entry, nearest, rule))
                    return true;

                // A persisted result does not retain the planner's transient anchor identity. When several
                // equivalent anchors are close together, the geometrically nearest one can differ from the
                // anchor used to derive the object's facing. Accept any fully matching anchor in that case.
                foreach (RelativeAnchor candidate in RelativeAnchorProvider.CollectMatchingAssetAnchors(
                             context,
                             rule,
                             false,
                             entry.Asset))
                {
                    if (candidate.Identity == null)
                        continue;

                    context.RequiredAssetRelationAnchorIdentity = candidate.Identity;
                    if (!RelativeAnchorProvider.TryFindAssetAnchor(
                            context,
                            entry.Asset,
                            entry.Bounds.Center,
                            entry.Bounds.ToAxisAlignedBounds(),
                            entry.Support,
                            out RelativeAnchor matched) ||
                        !MatchesFacing(entry, matched, rule))
                    {
                        continue;
                    }

                    result = matched;
                    return true;
                }

                error = $"facing does not match any valid anchor for {entry.Asset.name}";
                return false;
            }
            finally
            {
                context.RequiredAssetRelationAnchorIdentity = previousIdentity;
            }
        }

        private static void AddDetail(ICollection<string> details, string detail)
        {
            if (details.Count < 8)
                details.Add(detail);
        }

        private static bool TryValidatePathPlacement(
            GenerationContext context,
            GeneratedEntry entry,
            out string error)
        {
            error = string.Empty;
            PathPlacementRule rule = entry.Asset ? entry.Asset.PathPlacement : null;
            if (rule?.IsConfigured != true)
                return true;

            if (!PathPlacementSource.TryValidate(
                    context,
                    entry.Asset,
                    entry.Bounds.Center,
                    out RejectionReason rejection,
                    out string pathName))
            {
                error = $"path constraint {rejection} at {pathName}";
                return false;
            }

            if (!rule.UsesFacing || !PathPlacementSource.TryFindNearest(
                    context,
                    rule,
                    entry.Bounds.Center,
                    out PathPlacementFrame frame))
            {
                return true;
            }

            Vector3 actual = Vector3.ProjectOnPlane(
                entry.PlacementRotation * Vector3.forward,
                Vector3.up).normalized;
            Vector3 expected = rule.Facing switch
            {
                PathPlacementFacing.AlongPath => frame.Forward,
                PathPlacementFacing.AgainstPath => -frame.Forward,
                PathPlacementFacing.TowardPath => frame.Center - entry.Bounds.Center,
                PathPlacementFacing.AwayFromPath => entry.Bounds.Center - frame.Center,
                _ => actual
            };
            expected = Vector3.ProjectOnPlane(expected, Vector3.up).normalized;
            float angle = actual.sqrMagnitude <= 0.001f || expected.sqrMagnitude <= 0.001f
                ? 180f
                : Vector3.Angle(actual, expected);
            if (angle <= rule.FacingVariationDegrees + FacingTolerance)
                return true;

            error = $"path facing differs by {angle:F1} degrees (allowed {rule.FacingVariationDegrees:F1})";
            return false;
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

        private static bool TryCreateContext(
            GenerationRequest request,
            IReadOnlyCollection<GeneratedEntry> entries,
            out GenerationContext context,
            out List<AssetDefinition> assets,
            out string error)
        {
            context = null;
            assets = null;
            error = string.Empty;

            if (!GenerationAssetFilter.TryResolve(
                    request,
                    AssetCatalogService.GetOrCreate(),
                    out assets,
                    out error))
            {
                return false;
            }

            try
            {
                Transform parent = entries.FirstOrDefault()?.Root?.parent;
                context = GenerationContextFactory.Create(request, parent, assets);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool HasGeneratedConflict(GeneratedEntry first, GeneratedEntry second)
        {
            OrientedBounds firstBounds = Shrink(first.Bounds);
            OrientedBounds secondBounds = Shrink(second.Bounds);

            if (firstBounds.Intersects(secondBounds))
                return true;

            bool firstHasClearance = first.Asset && first.Asset.ReserveClearance;
            bool secondHasClearance = second.Asset && second.Asset.ReserveClearance;

            if (!firstHasClearance && !secondHasClearance)
                return false;

            OrientedBounds firstClearance = firstHasClearance
                ? Shrink(first.Asset.CreateClearanceBounds(first.Root.position, first.Root.rotation))
                : default;
            OrientedBounds secondClearance = secondHasClearance
                ? Shrink(second.Asset.CreateClearanceBounds(second.Root.position, second.Root.rotation))
                : default;

            return firstHasClearance && firstClearance.Intersects(secondBounds) ||
                   secondHasClearance && secondClearance.Intersects(firstBounds) ||
                   firstHasClearance && secondHasClearance && firstClearance.Intersects(secondClearance);
        }

        private static List<GeneratedEntry> CollectEntries(IAreaSource areaSource)
        {
            List<GeneratedEntry> entries = new();
            if (!GeneratedHierarchy.TryGet(areaSource, out Transform parent))
                return entries;

            foreach (Transform child in parent)
            {
                GeneratedObjectMetadata metadata = child ? child.GetComponent<GeneratedObjectMetadata>() : null;
                AssetDefinition asset = metadata ? metadata.AssetDefinition : null;
                Quaternion placementRotation = asset
                    ? asset.RemovePrefabRotationOffset(child.rotation)
                    : child.rotation;
                OrientedBounds bounds = asset
                    ? new OrientedBounds(
                        child.position + placementRotation * asset.BoundsCenterOffset,
                        asset.BoundsSize,
                        placementRotation)
                    : new OrientedBounds(child.position, Vector3.one * 0.01f, placementRotation);
                OrientedBounds containmentBounds = asset
                    ? RemoveSurfaceSink(bounds, placementRotation, metadata.PlacementTarget, asset.SurfaceSinkOffset)
                    : bounds;
                entries.Add(new GeneratedEntry
                {
                    Root = child,
                    Metadata = metadata,
                    Bounds = bounds,
                    ContainmentBounds = containmentBounds,
                    PlacementRotation = placementRotation
                });
            }

            return entries;
        }

        private static OrientedBounds RemoveSurfaceSink(
            OrientedBounds visualBounds,
            Quaternion placementRotation,
            PlacementTarget placementTarget,
            float sinkOffset)
        {
            if (sinkOffset <= 0f || placementTarget == PlacementTarget.InsideSpace)
                return visualBounds;

            Vector3 normal = placementTarget == PlacementTarget.Wall
                ? placementRotation * Vector3.forward
                : placementRotation * Vector3.up;
            return new OrientedBounds(
                visualBounds.Center + normal.normalized * sinkOffset,
                visualBounds.Size,
                visualBounds.Rotation);
        }

        private static bool MatchesFacing(
            GeneratedEntry entry,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            if (rule.Facing == AssetRelativeFacing.Any)
                return true;

            return GetFacingAngle(entry, anchor, rule) <= rule.FacingVariationDegrees + FacingTolerance;
        }

        private static float GetFacingAngle(
            GeneratedEntry entry,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            if (rule.Facing == AssetRelativeFacing.Any)
                return 0f;

            Vector3 actual = Vector3.ProjectOnPlane(entry.PlacementRotation * Vector3.forward, Vector3.up).normalized;
            Vector3 expected = rule.Facing switch
            {
                AssetRelativeFacing.Toward => Vector3.ProjectOnPlane(anchor.Position - entry.Bounds.Center, Vector3.up).normalized,
                AssetRelativeFacing.Away => Vector3.ProjectOnPlane(entry.Bounds.Center - anchor.Position, Vector3.up).normalized,
                AssetRelativeFacing.MatchForward => Vector3.ProjectOnPlane(anchor.Forward, Vector3.up).normalized,
                _ => actual
            };

            if (actual.sqrMagnitude <= 0.001f || expected.sqrMagnitude <= 0.001f)
                return 180f;

            return Vector3.Angle(actual, expected);
        }

        private static OrientedBounds Shrink(OrientedBounds bounds) => new(
            bounds.Center,
            new Vector3(
                Mathf.Max(0.01f, bounds.Size.x - BoundsTolerance * 2f),
                Mathf.Max(0.01f, bounds.Size.y - BoundsTolerance * 2f),
                Mathf.Max(0.01f, bounds.Size.z - BoundsTolerance * 2f)),
            bounds.Rotation);

        private static void AddIfEnabled(
            ICollection<GenerationEvaluationCheckRecord> records,
            EvaluationCheckSet enabled,
            EvaluationCheckSet flag,
            GenerationEvaluationCheckRecord record)
        {
            if ((enabled & flag) != 0)
                records.Add(record);
        }

        private static void AddUnavailable(
            ICollection<GenerationEvaluationCheckRecord> records,
            EvaluationCheckSet enabled,
            EvaluationCheckSet flag,
            string name,
            string reason)
        {
            if ((enabled & flag) != 0)
                records.Add(Unavailable(name, reason));
        }

        private static GenerationEvaluationCheckRecord Record(
            string name,
            bool passed,
            int violations,
            string message) => new()
        {
            name = name,
            status = passed ? EvaluationCheckStatus.Passed : EvaluationCheckStatus.Failed,
            violations = Mathf.Max(0, violations),
            message = message ?? string.Empty
        };

        private static GenerationEvaluationCheckRecord Unavailable(string name, string reason) => new()
        {
            name = name,
            status = EvaluationCheckStatus.NotApplicable,
            message = reason ?? string.Empty
        };
    }
}
