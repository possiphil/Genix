using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Geometry;
using Genix.Layouts;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Placement
{
    public static partial class RelativeAnchorProvider
    {
        /// <summary>
        /// Determines whether an asset has no relative dependency or can resolve that dependency
        /// from the current plan, previous output, or an explicit scene anchor.
        /// </summary>
        public static bool CanAttemptAsset(
            GenerationContext context,
            AssetDefinition asset)
        {
            AssetRelativePlacementRule rule = asset ? asset.AssetRelativePlacement : null;
            return context != null &&
                   (rule?.IsConfigured != true || HasMatchingAssetAnchor(context, asset, rule));
        }

        internal static bool ShouldPrioritizeAsset(
            GenerationContext context,
            AssetDefinition asset)
        {
            AssetRelativePlacementRule rule = asset ? asset.AssetRelativePlacement : null;
            return context != null &&
                   rule?.IsConfigured == true &&
                   context.Plan.GetAssetCount(asset) == 0;
        }

        private static bool HasMatchingAssetAnchor(
            GenerationContext context,
            AssetDefinition asset,
            AssetRelativePlacementRule rule)
        {
            if (rule.Source is AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.GeneratedObjects)
            {
                bool hasPlanned = rule.TargetScope switch
                {
                    AssetRelativeTargetScope.Asset => context.Plan.GetAssetCount(rule.TargetAsset) > 0,
                    AssetRelativeTargetScope.AssetTag => context.Plan.GetAssetTagCount(rule.TargetTag) > 0,
                    _ => false
                };
                bool hasGenerated = rule.TargetScope switch
                {
                    AssetRelativeTargetScope.Asset => context.GeneratedSceneObjects.ContainsAsset(rule.TargetAsset),
                    AssetRelativeTargetScope.AssetTag => context.GeneratedSceneObjects.ContainsAssetTag(rule.TargetTag),
                    _ => false
                };

                if (hasPlanned || hasGenerated)
                    return true;
            }

            return (rule.Source is AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.SceneAnchors) &&
                   (context.AssetRelationAnchors.HasMatch(rule) ||
                    rule.UsesPathStations && asset &&
                    context.GetPathStationAnchors(asset).Count > 0);
        }

        private static bool Contains(RelativeAnchor anchor, OrientedBounds candidateBounds)
        {
            if (anchor.Identity is AssetRelationAnchor sceneAnchor)
                return sceneAnchor.Contains(candidateBounds);

            Vector3 extents = candidateBounds.Extents;
            Quaternion rotation = candidateBounds.Rotation;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = candidateBounds.Center + rotation * Vector3.Scale(
                    extents,
                    new Vector3(x, y, z));
                if (!anchor.Bounds.Contains(corner))
                    return false;
            }

            return true;
        }

        internal static bool MatchesSide(
            Vector3 position,
            RelativeAnchor anchor,
            AssetRelativePlacementRule rule)
        {
            if (rule.Side == AssetRelativeSide.Any)
                return true;

            Vector3 direction = position - anchor.Position;
            Vector3 forward = Vector3.ProjectOnPlane(anchor.Forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(anchor.Right, Vector3.up).normalized;

            if (forward.sqrMagnitude <= 0.001f)
                forward = Vector3.forward;
            if (right.sqrMagnitude <= 0.001f)
                right = Vector3.right;

            float forwardDistance = Vector3.Dot(direction, forward);
            float rightDistance = Vector3.Dot(direction, right);
            float verticalDistance = direction.y;
            float horizontalMagnitude = Mathf.Max(
                Mathf.Abs(forwardDistance),
                Mathf.Abs(rightDistance));

            AssetRelativeSide candidateSide;
            if (rule.UsesVerticalSides && Mathf.Abs(verticalDistance) >= horizontalMagnitude)
            {
                if (Mathf.Abs(verticalDistance) <= 0.0001f)
                    return false;

                candidateSide = verticalDistance >= 0f
                    ? AssetRelativeSide.Above
                    : AssetRelativeSide.Below;
            }
            else
            {
                if (horizontalMagnitude <= 0.0001f)
                    return false;

                candidateSide = Mathf.Abs(forwardDistance) >= Mathf.Abs(rightDistance)
                    ? forwardDistance >= 0f
                        ? AssetRelativeSide.Front
                        : AssetRelativeSide.Back
                    : rightDistance >= 0f
                        ? AssetRelativeSide.Right
                        : AssetRelativeSide.Left;
            }

            return rule.AllowsSide(candidateSide);
        }

        internal static int GetAssignedAssetCount(
            GenerationContext context,
            AssetDefinition dependentAsset,
            AssetRelativePlacementRule rule,
            RelativeAnchor anchor)
        {
            int assigned = 0;

            foreach (PlannedObject plannedObject in context.Plan.Objects)
            {
                if (plannedObject.Asset != dependentAsset)
                    continue;

                if (plannedObject.RelationAnchorIdentity != null)
                {
                    if (Equals(plannedObject.RelationAnchorIdentity, anchor.Identity))
                        assigned++;
                    continue;
                }

                if (
                    !TryFindAssetAnchor(
                        context,
                        dependentAsset,
                        plannedObject.Candidate.Position,
                        plannedObject.Bounds.ToAxisAlignedBounds(),
                        PlacementSupportRules.GetDescriptor(plannedObject.Candidate.SurfaceCollider),
                        out RelativeAnchor assignedAnchor) ||
                    !IsSameAnchor(anchor, assignedAnchor))
                {
                    continue;
                }

                assigned++;
            }

            foreach (SceneObjectIndex.Entry entry in context.GeneratedSceneObjects.Entries)
            {
                if (entry.AssetDefinition != dependentAsset)
                    continue;

                GeneratedObjectMetadata metadata = entry.Root
                    ? entry.Root.GetComponent<GeneratedObjectMetadata>()
                    : null;
                if (metadata && !string.IsNullOrEmpty(metadata.RelationAnchorKey))
                {
                    if (metadata.RelationAnchorKey == anchor.PersistentIdentityKey)
                        assigned++;
                    continue;
                }

                Vector3 position = entry.Root ? entry.Root.position : entry.Bounds.center;
                if (!TryFindAssetAnchor(
                        context,
                        dependentAsset,
                        position,
                        entry.Bounds,
                        entry.SupportSurface,
                        out RelativeAnchor assignedAnchor) ||
                    !IsSameAnchor(anchor, assignedAnchor))
                {
                    continue;
                }

                assigned++;
            }

            return assigned;
        }

        internal static int GetAssignedAssetTagCount(
            GenerationContext context,
            AssetPoolAnchorGroupLimit group,
            RelativeAnchor anchor)
        {
            if (context == null || group?.IsConfigured != true)
                return 0;

            int assigned = 0;
            foreach (PlannedObject plannedObject in context.Plan.Objects)
            {
                if (!group.MatchesMember(plannedObject.Asset))
                    continue;

                if (plannedObject.RelationAnchorIdentity != null)
                {
                    if (Equals(plannedObject.RelationAnchorIdentity, anchor.Identity))
                        assigned++;
                    continue;
                }

                AssetRelativePlacementRule rule = plannedObject.Asset.AssetRelativePlacement;
                if (rule?.IsConfigured == true &&
                    TryFindAssetAnchor(
                        context,
                        plannedObject.Asset,
                        plannedObject.Candidate.Position,
                        plannedObject.Bounds.ToAxisAlignedBounds(),
                        PlacementSupportRules.GetDescriptor(plannedObject.Candidate.SurfaceCollider),
                        out RelativeAnchor assignedAnchor) &&
                    IsSameAnchor(anchor, assignedAnchor))
                {
                    assigned++;
                }
            }

            foreach (SceneObjectIndex.Entry entry in context.GeneratedSceneObjects.Entries)
            {
                if (!group.MatchesMember(entry.AssetDefinition))
                    continue;

                GeneratedObjectMetadata metadata = entry.Root
                    ? entry.Root.GetComponent<GeneratedObjectMetadata>()
                    : null;
                if (metadata && !string.IsNullOrEmpty(metadata.RelationAnchorKey))
                {
                    if (metadata.RelationAnchorKey == anchor.PersistentIdentityKey)
                        assigned++;
                    continue;
                }

                AssetRelativePlacementRule rule = entry.AssetDefinition.AssetRelativePlacement;
                Vector3 position = entry.Root ? entry.Root.position : entry.Bounds.center;
                if (rule?.IsConfigured == true &&
                    TryFindAssetAnchor(
                        context,
                        entry.AssetDefinition,
                        position,
                        entry.Bounds,
                        entry.SupportSurface,
                        out RelativeAnchor assignedAnchor) &&
                    IsSameAnchor(anchor, assignedAnchor))
                {
                    assigned++;
                }
            }

            return assigned;
        }

        private static bool HasRemainingPerAnchorCapacity(
            GenerationContext context,
            AssetDefinition dependentAsset,
            AssetRelativePlacementRule rule,
            RelativeAnchor anchor)
        {
            return !rule.HasMaximumPerAnchor ||
                   GetAssignedAssetCount(context, dependentAsset, rule, anchor) < rule.MaximumPerAnchor;
        }

        private static bool IsSameAnchor(RelativeAnchor first, RelativeAnchor second)
        {
            if (first.Identity != null || second.Identity != null)
                return Equals(first.Identity, second.Identity);

            return first.Name == second.Name &&
                   (first.Position - second.Position).sqrMagnitude <= 0.000001f;
        }

        private static string GetPlannedIdentity(PlannedObject plannedObject) =>
            $"planned:{plannedObject.ObjectName}";

        private static bool IsBetterAnchor(
            float distance,
            float centerDistance,
            float bestDistance,
            float bestCenterDistance)
        {
            if (distance < bestDistance - AnchorDistanceTieTolerance)
                return true;

            return Mathf.Abs(distance - bestDistance) <= AnchorDistanceTieTolerance &&
                   centerDistance < bestCenterDistance;
        }

        private static bool MatchesRequiredAnchor(
            GenerationContext context,
            RelativeAnchor anchor,
            object candidateAnchorIdentity = null)
        {
            object requiredIdentity = context.RequiredAssetRelationAnchorIdentity ?? candidateAnchorIdentity;
            return requiredIdentity == null || Equals(requiredIdentity, anchor.Identity);
        }

        private static float DistanceToAnchor(Vector3 position, RelativeAnchor anchor)
        {
            Vector3 closestPoint = anchor.Bounds.ClosestPoint(position);
            return Vector3.Distance(position, closestPoint);
        }

        private static float DistanceBetweenBounds(Bounds first, Bounds second)
        {
            float x = Mathf.Max(0f, Mathf.Max(first.min.x - second.max.x, second.min.x - first.max.x));
            float y = Mathf.Max(0f, Mathf.Max(first.min.y - second.max.y, second.min.y - first.max.y));
            float z = Mathf.Max(0f, Mathf.Max(first.min.z - second.max.z, second.min.z - first.max.z));
            return Mathf.Sqrt(x * x + y * y + z * z);
        }

        private static bool HasDontSaveHideFlags(Transform transform)
        {
            while (transform)
            {
                if ((transform.gameObject.hideFlags & HideFlags.DontSave) != 0)
                    return true;

                transform = transform.parent;
            }

            return false;
        }
    }
}
