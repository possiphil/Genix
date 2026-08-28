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
    }
}

