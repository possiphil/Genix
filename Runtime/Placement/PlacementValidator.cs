using Genix.Assets;
using Genix.Core;
using Genix.Sampling;
using Genix.Geometry;
using UnityEngine;

namespace Genix.Placement
{
    public static class PlacementValidator
    {
        public static bool IsValidCandidate(PlacementCandidate candidate, Bounds candidateBounds, GenerationContext context)
        {
            return TryValidateCandidate(candidate, candidateBounds, context, out _, out _);
        }

        public static bool TryValidateCandidate(
            PlacementCandidate candidate,
            Bounds candidateBounds,
            GenerationContext context,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            return TryValidateCandidate(candidate, candidateBounds, context, null, out rejectionReason, out relatedObjectName);
        }

        public static bool TryValidateCandidate(
            PlacementCandidate candidate,
            Bounds candidateBounds,
            GenerationContext context,
            AssetDefinition asset,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            OrientedBounds orientedBounds = new(candidateBounds.center, candidateBounds.size, candidate.Rotation);

            return TryValidateCandidate(
                candidate,
                orientedBounds,
                context,
                asset,
                out rejectionReason,
                out relatedObjectName);
        }

        public static bool TryValidateCandidate(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            GenerationContext context,
            AssetDefinition asset,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            rejectionReason = RejectionReason.None;
            relatedObjectName = string.Empty;
            Bounds axisAlignedBounds = candidateBounds.ToAxisAlignedBounds();
            bool isWallPlacement = candidate.PlacementType == PlacementType.Wall;
            bool isInsideSpacePlacement = candidate.PlacementType == PlacementType.InsideSpace;

            if (!FitsTargetHeight(axisAlignedBounds, context.TargetBounds))
            {
                rejectionReason = RejectionReason.ExceedsTargetHeight;
                return false;
            }

            if (!isWallPlacement && !isInsideSpacePlacement && asset)
            {
                if (!context.Area.ContainsPlacementFootprint(candidate, asset))
                {
                    rejectionReason = RejectionReason.OutsideTargetArea;
                    return false;
                }
            }
            else if (!isWallPlacement && !isInsideSpacePlacement && !context.Area.ContainsFootprint(axisAlignedBounds))
            {
                rejectionReason = RejectionReason.OutsideTargetArea;
                return false;
            }

            if (!context.Area.ContainsPlacementVolume(candidateBounds))
            {
                rejectionReason = RejectionReason.OutsideTargetVolume;
                return false;
            }

            if (TryFindOverlappingGeneratedObject(candidateBounds, context, out relatedObjectName))
            {
                rejectionReason = RejectionReason.OverlapsGenerated;
                return false;
            }

            if (TryFindOverlappingFixedObject(candidate, candidateBounds, context, out relatedObjectName))
            {
                rejectionReason = RejectionReason.OverlapsFixed;
                return false;
            }

            if (TryFindTooCloseFixedObject(candidate, candidateBounds, context, out relatedObjectName))
            {
                rejectionReason = RejectionReason.TooCloseToFixed;
                return false;
            }

            if (TryFindTooCloseGeneratedObject(axisAlignedBounds, context, out relatedObjectName))
            {
                rejectionReason = RejectionReason.TooCloseToGenerated;
                return false;
            }

            if (!RelativeAnchorProvider.IsCandidateInRange(candidate, context, out relatedObjectName))
            {
                rejectionReason = RejectionReason.OutsideRelativeRadius;
                return false;
            }

            return true;
        }

        private static bool TryFindOverlappingFixedObject(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            OrientedBounds obstacleBounds = CreateObstacleBounds(candidate, candidateBounds);

            Collider[] hits = Physics.OverlapBox(
                obstacleBounds.Center,
                obstacleBounds.Extents,
                obstacleBounds.Rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                if (ShouldIgnoreFixedCollider(hit, candidate, context))
                    continue;

                relatedObjectName = hit.name;
                return true;
            }

            return false;
        }

        private static bool TryFindTooCloseFixedObject(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (!context.StyleSettings.placement.useFixedObjectClearance)
                return false;

            float minDistance = context.StyleSettings.placement.fixedObjectDistance;

            if (minDistance <= 0f)
                return false;

            OrientedBounds obstacleBounds = CreateObstacleBounds(candidate, candidateBounds, minDistance);

            Collider[] hits = Physics.OverlapBox(
                obstacleBounds.Center,
                obstacleBounds.Extents,
                obstacleBounds.Rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                if (ShouldIgnoreFixedCollider(hit, candidate, context))
                    continue;

                relatedObjectName = hit.name;
                return true;
            }

            return false;
        }

        private static OrientedBounds CreateObstacleBounds(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            float horizontalExpansion = 0f)
        {
            if (candidate.PlacementType == PlacementType.Wall)
            {
                Vector3 expandedSize = candidateBounds.Size + Vector3.one * (horizontalExpansion * 2f);
                return new OrientedBounds(
                    candidateBounds.Center,
                    expandedSize,
                    candidateBounds.Rotation);
            }

            Vector3 size = new(
                candidateBounds.Size.x + horizontalExpansion * 2f,
                candidateBounds.Size.y,
                candidateBounds.Size.z + horizontalExpansion * 2f);

            return new OrientedBounds(candidateBounds.Center, size, candidateBounds.Rotation);
        }

        private static bool ShouldIgnoreFixedCollider(Collider collider, PlacementCandidate candidate, GenerationContext context)
        {
            if (!collider)
                return true;

            if (HasDontSaveHideFlags(collider.transform))
                return true;

            if (collider == candidate.SurfaceCollider)
                return true;

            if (context.GeneratedParent && collider.transform.IsChildOf(context.GeneratedParent))
                return true;

            if (context.AreaSource.IsSourceCollider(collider))
                return true;

            return false;
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

        private static bool FitsTargetHeight(Bounds candidateBounds, Bounds targetBounds)
        {
            return candidateBounds.min.y >= targetBounds.min.y &&
                   candidateBounds.max.y <= targetBounds.max.y;
        }

        private static bool TryFindOverlappingGeneratedObject(
            OrientedBounds candidateBounds,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            foreach (PlannedObject plannedObject in context.Plan.Objects)
            {
                if (!candidateBounds.Intersects(plannedObject.Bounds))
                    continue;

                relatedObjectName = plannedObject.ObjectName;
                return true;
            }

            foreach (Transform child in context.GeneratedParent)
            {
                if (!BoundsUtility.TryGetRendererBounds(child, out Bounds objectBounds))
                    continue;

                if (!BoundsOverlap(candidateBounds, objectBounds))
                    continue;

                relatedObjectName = child.name;
                return true;
            }

            return false;
        }

        private static bool TryFindTooCloseGeneratedObject(
            Bounds candidateBounds,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (context.StyleSettings.algorithm != SamplingAlgorithm.BridsonPoissonDisk)
                return false;

            float minDistance = context.StyleSettings.poisson.minDistance;

            if (minDistance <= 0f)
                return false;

            foreach (PlannedObject plannedObject in context.Plan.Objects)
            {
                if (!IsCloserThanMinDistance(
                        candidateBounds,
                        plannedObject.Bounds.ToAxisAlignedBounds(),
                        minDistance))
                {
                    continue;
                }

                relatedObjectName = plannedObject.ObjectName;
                return true;
            }

            foreach (Transform child in context.GeneratedParent)
            {
                if (!BoundsUtility.TryGetRendererBounds(child, out Bounds objectBounds))
                    continue;

                if (!IsCloserThanMinDistance(candidateBounds, objectBounds, minDistance))
                    continue;

                relatedObjectName = child.name;
                return true;
            }

            return false;
        }

        private static bool IsCloserThanMinDistance(Bounds a, Bounds b, float minDistance)
        {
            float minDistanceSquared = minDistance * minDistance;

            float dx = a.center.x - b.center.x;
            float dz = a.center.z - b.center.z;

            return dx * dx + dz * dz < minDistanceSquared;
        }

        private static bool BoundsOverlap(OrientedBounds a, Bounds b)
        {
            return a.Intersects(b);
        }
    }
}
