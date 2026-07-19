using System.Collections.Generic;
using System.Linq;
using Genix.Core;
using Genix.Geometry;
using Genix.Layouts;
using UnityEngine;

namespace Genix.Placement
{
    internal readonly struct RelativeAnchor
    {
        public Vector3 Position { get; }
        public Bounds Bounds { get; }
        public string Name { get; }

        public RelativeAnchor(Vector3 position, Bounds bounds, string name)
        {
            Position = position;
            Bounds = bounds;
            Name = string.IsNullOrWhiteSpace(name) ? "Relative Anchor" : name;
        }
    }

    public static class RelativeAnchorProvider
    {
        internal static bool IsCandidateInRange(
            PlacementCandidate candidate,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (context?.RelativePlacement == null || !context.RelativePlacement.IsEnabled)
                return true;

            float radius = Mathf.Max(0.01f, context.RelativePlacement.Radius);

            foreach (RelativeAnchor anchor in EnumerateAnchors(context))
            {
                if (DistanceToAnchor(candidate.Position, anchor) > radius)
                    continue;

                relatedObjectName = anchor.Name;
                return true;
            }

            return false;
        }

        public static bool HasAnyAnchor(GenerationContext context)
        {
            if (context?.RelativePlacement == null || !context.RelativePlacement.IsEnabled)
                return true;

            return EnumerateAnchors(context).Any();
        }

        internal static bool TryFindNearestAnchor(
            GenerationContext context,
            Vector3 position,
            out RelativeAnchor nearestAnchor)
        {
            nearestAnchor = default;

            if (context?.RelativePlacement == null || !context.RelativePlacement.IsEnabled)
                return false;

            float nearestDistance = float.PositiveInfinity;

            foreach (RelativeAnchor anchor in EnumerateAnchors(context))
            {
                float distance = DistanceToAnchor(position, anchor);

                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearestAnchor = anchor;
            }

            return nearestDistance < float.PositiveInfinity;
        }

        internal static IReadOnlyList<RelativeAnchor> CollectSceneAnchors(GenerationContext context)
        {
            if (context?.RelativePlacement == null || !context.RelativePlacement.UsesSceneObjects)
                return System.Array.Empty<RelativeAnchor>();

            List<RelativeAnchor> anchors = new();
            HashSet<Transform> seenTransforms = new();

            foreach (Collider collider in Object.FindObjectsByType<Collider>())
            {
                if (!IsUsableSceneCollider(collider, context) ||
                    !seenTransforms.Add(collider.transform))
                {
                    continue;
                }

                anchors.Add(CreateAnchor(collider.transform, collider.bounds));
            }

            return anchors;
        }

        internal static IReadOnlyList<RelativeAnchor> CollectSelectedAnchors(GenerationContext context)
        {
            if (context?.RelativePlacement == null || !context.RelativePlacement.UsesSelectedObjects)
                return System.Array.Empty<RelativeAnchor>();

            return context.RelativePlacement.SelectedTransforms
                .Where(transform => transform)
                .Distinct()
                .Select(transform =>
                {
                    BoundsUtility.TryGetCombinedBounds(transform, out Bounds bounds);
                    return CreateAnchor(transform, bounds);
                })
                .ToList();
        }

        private static IEnumerable<RelativeAnchor> EnumerateAnchors(GenerationContext context)
        {
            if (context.RelativePlacement.UsesGeneratedObjects)
            {
                foreach (RelativeAnchor anchor in EnumerateGeneratedAnchors(context))
                    yield return anchor;
            }

            if (context.RelativePlacement.UsesSceneObjects)
            {
                foreach (RelativeAnchor anchor in context.SceneRelativeAnchors)
                    yield return anchor;
            }

            if (context.RelativePlacement.UsesSelectedObjects)
            {
                foreach (RelativeAnchor anchor in context.SelectedRelativeAnchors)
                    yield return anchor;
            }
        }

        private static IEnumerable<RelativeAnchor> EnumerateGeneratedAnchors(GenerationContext context)
        {
            foreach (PlannedObject plannedObject in context.Plan.Objects)
                yield return new RelativeAnchor(
                    plannedObject.Bounds.Center,
                    plannedObject.Bounds.ToAxisAlignedBounds(),
                    plannedObject.ObjectName);

            if (!context.GeneratedParent)
                yield break;

            foreach (Transform child in context.GeneratedParent)
            {
                if (!child || !BoundsUtility.TryGetCombinedBounds(child, out Bounds bounds))
                    continue;

                yield return CreateAnchor(child, bounds);
            }
        }

        private static bool IsUsableSceneCollider(Collider collider, GenerationContext context)
        {
            if (!collider ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy ||
                (context.RelativePlacement.SceneLayers.value & (1 << collider.gameObject.layer)) == 0)
            {
                return false;
            }

            if (HasDontSaveHideFlags(collider.transform) ||
                collider.GetComponentInParent<GeneratedObjectMetadata>() ||
                context.GeneratedParent && collider.transform.IsChildOf(context.GeneratedParent) ||
                context.AreaSource.IsSourceCollider(collider))
            {
                return false;
            }

            return true;
        }

        private static RelativeAnchor CreateAnchor(Transform transform, Bounds bounds)
        {
            if (bounds.size == Vector3.zero)
                bounds = new Bounds(transform ? transform.position : Vector3.zero, Vector3.zero);

            return new RelativeAnchor(bounds.center, bounds, transform ? transform.name : string.Empty);
        }

        private static float DistanceToAnchor(Vector3 position, RelativeAnchor anchor)
        {
            Vector3 closestPoint = anchor.Bounds.ClosestPoint(position);
            return Vector3.Distance(position, closestPoint);
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
