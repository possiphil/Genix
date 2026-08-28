using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Geometry;
using Genix.Profiling;
using Genix.Sampling;
using Genix.Semantics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Placement
{
    public static partial class PlacementValidator
    {
        private static bool TryFindOverlappingFixedObject(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            OrientedBounds obstacleBounds = CreateObstacleBounds(candidate, candidateBounds);
            obstacleBounds = InsetBounds(obstacleBounds, ContactTolerance);

            if (!HasPotentialFixedObstacle(candidate, obstacleBounds, context))
                return false;

            int hitCount = OverlapBox(
                obstacleBounds,
                out Collider[] hits);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = hits[i];

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

            if (!HasPotentialFixedObstacle(candidate, obstacleBounds, context))
                return false;

            int hitCount = OverlapBox(
                obstacleBounds,
                out Collider[] hits);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = hits[i];

                if (ShouldIgnoreFixedCollider(hit, candidate, context))
                    continue;

                relatedObjectName = hit.name;
                return true;
            }

            return false;
        }

        private static bool HasPotentialFixedObstacle(
            PlacementCandidate candidate,
            OrientedBounds obstacleBounds,
            GenerationContext context)
        {
            SceneObjectIndex fixedObjects = context.FixedSceneObjects;

            if (fixedObjects == null || fixedObjects.Count == 0)
                return false;

            Bounds queryBounds = obstacleBounds.ToAxisAlignedBounds();

            foreach (SceneObjectIndex.Entry entry in fixedObjects.Query(queryBounds))
            {
                if (!entry.Collider || entry.Collider == candidate.SurfaceCollider)
                    continue;

                if (entry.Bounds.Intersects(queryBounds))
                    return true;
            }

            return false;
        }

        private static int OverlapBox(
            OrientedBounds bounds,
            out Collider[] hits)
        {
            hits = GetOverlapBuffer();
            int hitCount = Physics.OverlapBoxNonAlloc(
                bounds.Center,
                bounds.Extents,
                hits,
                bounds.Rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            if (hitCount < hits.Length)
                return hitCount;

            hits = Physics.OverlapBox(
                bounds.Center,
                bounds.Extents,
                bounds.Rotation,
                ~0,
                QueryTriggerInteraction.Ignore);
            return hits.Length;
        }

        private static Collider[] GetOverlapBuffer()
        {
            if (_overlapBuffer == null || _overlapBuffer.Length == 0)
                _overlapBuffer = new Collider[InitialOverlapBufferSize];

            return _overlapBuffer;
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

        private static OrientedBounds InsetBounds(OrientedBounds bounds, float inset)
        {
            Vector3 size = bounds.Size - Vector3.one * (Mathf.Max(0f, inset) * 2f);
            return new OrientedBounds(bounds.Center, size, bounds.Rotation);
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
    }
}
