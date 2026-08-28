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
        /// <summary>Determines whether the active relative-placement source contains any anchor.</summary>
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

        internal static bool TryFindAssetAnchor(
            GenerationContext context,
            AssetDefinition asset,
            Vector3 position,
            PlacementSurfaceDescriptor supportSurface,
            out RelativeAnchor nearestAnchor)
        {
            Bounds dependentBounds = new(position, AssetAttemptPlanner.Dimensions(asset));
            return TryFindAssetAnchor(
                context,
                asset,
                position,
                dependentBounds,
                supportSurface,
                out nearestAnchor);
        }

        internal static bool TryFindAssetAnchor(
            GenerationContext context,
            AssetDefinition asset,
            Vector3 position,
            Bounds dependentBounds,
            PlacementSurfaceDescriptor supportSurface,
            out RelativeAnchor nearestAnchor)
        {
            nearestAnchor = default;
            AssetRelativePlacementRule rule = asset ? asset.AssetRelativePlacement : null;

            if (context == null || rule?.IsConfigured != true)
                return false;

            float nearestDistance = float.PositiveInfinity;
            float nearestCenterDistance = float.PositiveInfinity;

            foreach (RelativeAnchor anchor in EnumerateAssetAnchors(context, asset, rule, position))
            {
                if (!MatchesRequiredAnchor(context, anchor))
                    continue;

                if (!anchor.Matches(rule))
                    continue;

                if (rule.RequireSameSupportSurface &&
                    (!supportSurface || anchor.SupportSurface != supportSurface))
                {
                    continue;
                }

                float distance = DistanceBetweenBounds(dependentBounds, anchor.Bounds);

                if (distance < rule.MinimumDistance ||
                    distance > rule.MaximumDistance ||
                    !MatchesSide(position, anchor, rule))
                {
                    continue;
                }

                float centerDistance = (position - anchor.Position).sqrMagnitude;
                if (!IsBetterAnchor(
                        distance,
                        centerDistance,
                        nearestDistance,
                        nearestCenterDistance))
                {
                    continue;
                }

                nearestDistance = distance;
                nearestCenterDistance = centerDistance;
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

        internal static AssetRelationAnchorIndex CollectAssetSceneAnchors(GenerationContext context)
        {
            AssetRelationAnchorIndex index = new();

            if (context == null)
                return index;

            foreach (AssetRelationAnchor descriptor in Object.FindObjectsByType<AssetRelationAnchor>())
            {
                if (!descriptor ||
                    !descriptor.isActiveAndEnabled ||
                    HasDontSaveHideFlags(descriptor.transform) ||
                    context.GeneratedParent && descriptor.transform.IsChildOf(context.GeneratedParent))
                {
                    continue;
                }

                if (!descriptor.TryGetBounds(out Bounds bounds))
                    continue;

                index.Add(new RelativeAnchor(
                    bounds.center,
                    bounds,
                    descriptor.name,
                    descriptor.Forward,
                    descriptor.Right,
                    descriptor.RepresentedAsset,
                    descriptor.AssetTags,
                    descriptor.SupportSurface,
                    descriptor,
                    AssetRelativeAnchorSource.SceneAnchors));
            }

            return index;
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
                    plannedObject.ObjectName,
                    plannedObject.Candidate.Rotation * Vector3.forward,
                    plannedObject.Candidate.Rotation * Vector3.right,
                    plannedObject.Asset,
                    supportSurface: PlacementSupportRules.GetDescriptor(plannedObject.Candidate.SurfaceCollider),
                    identity: GetPlannedIdentity(plannedObject),
                    source: AssetRelativeAnchorSource.GeneratedObjects);

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

            GeneratedObjectMetadata metadata = transform
                ? transform.GetComponent<GeneratedObjectMetadata>()
                : null;
            AssetDefinition asset = metadata ? metadata.AssetDefinition : null;
            Quaternion rotation = GetPlacementRotation(transform, asset);

            return new RelativeAnchor(
                bounds.center,
                bounds,
                transform ? transform.name : string.Empty,
                rotation * Vector3.forward,
                rotation * Vector3.right,
                asset,
                identity: transform,
                source: metadata
                    ? AssetRelativeAnchorSource.GeneratedObjects
                    : AssetRelativeAnchorSource.Any);
        }

        private static IEnumerable<RelativeAnchor> EnumerateAssetAnchors(
            GenerationContext context,
            AssetDefinition asset,
            AssetRelativePlacementRule rule,
            Vector3 position)
        {
            Bounds pointBounds = new(position, Vector3.zero);
            Bounds queryBounds = pointBounds;
            queryBounds.Expand(rule.MaximumDistance * 2f);

            if (rule.Source is AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.GeneratedObjects)
            {
                foreach (PlannedObject plannedObject in context.Plan.QuerySpatialSpacing(
                             pointBounds,
                             rule.MaximumDistance))
                {
                    yield return new RelativeAnchor(
                        plannedObject.Bounds.Center,
                        plannedObject.Bounds.ToAxisAlignedBounds(),
                        plannedObject.ObjectName,
                        plannedObject.Candidate.Rotation * Vector3.forward,
                        plannedObject.Candidate.Rotation * Vector3.right,
                        plannedObject.Asset,
                        supportSurface: PlacementSupportRules.GetDescriptor(
                            plannedObject.Candidate.SurfaceCollider),
                        identity: GetPlannedIdentity(plannedObject),
                        source: AssetRelativeAnchorSource.GeneratedObjects);
                }

                SceneObjectIndex generated = context.GeneratedSceneObjects;
                if (generated != null)
                {
                    foreach (SceneObjectIndex.Entry entry in generated.Query(queryBounds))
                    {
                        if (!entry.AssetDefinition)
                            continue;

                        Quaternion rotation = GetPlacementRotation(entry.Root, entry.AssetDefinition);

                        yield return new RelativeAnchor(
                            entry.Bounds.center,
                            entry.Bounds,
                            entry.ObjectName,
                            rotation * Vector3.forward,
                            rotation * Vector3.right,
                            entry.AssetDefinition,
                            supportSurface: entry.SupportSurface,
                            identity: entry.Root ? entry.Root : entry.ObjectName,
                            source: AssetRelativeAnchorSource.GeneratedObjects);
                    }
                }
            }

            if (rule.Source is AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.SceneAnchors)
            {
                foreach (RelativeAnchor anchor in context.AssetRelationAnchors.Query(queryBounds))
                    yield return anchor;

                if (rule.UsesPathStations)
                {
                    foreach (RelativeAnchor anchor in context.GetPathStationAnchors(asset))
                    {
                        if (anchor.Bounds.Intersects(queryBounds))
                            yield return anchor;
                    }
                }
            }
        }

        internal static IReadOnlyList<RelativeAnchor> CollectMatchingAssetAnchors(
            GenerationContext context,
            AssetRelativePlacementRule rule,
            bool includePlannedObjects = true,
            AssetDefinition dependentAsset = null)
        {
            if (context == null || rule?.IsConfigured != true)
                return System.Array.Empty<RelativeAnchor>();

            List<RelativeAnchor> anchors = new();
            HashSet<object> identities = new();

            void AddIfMatching(RelativeAnchor anchor)
            {
                if (!anchor.Matches(rule))
                    return;

                object identity = anchor.Identity ?? $"{anchor.Name}:{anchor.Position}";
                if (identities.Add(identity))
                    anchors.Add(anchor);
            }

            if (rule.Source is AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.GeneratedObjects)
            {
                if (includePlannedObjects)
                {
                    foreach (PlannedObject plannedObject in context.Plan.Objects)
                        AddIfMatching(CreatePlannedAnchor(plannedObject));
                }

                foreach (SceneObjectIndex.Entry entry in context.GeneratedSceneObjects.Entries)
                {
                    if (!entry.AssetDefinition)
                        continue;

                    Quaternion rotation = GetPlacementRotation(entry.Root, entry.AssetDefinition);
                    AddIfMatching(new RelativeAnchor(
                        entry.Bounds.center,
                        entry.Bounds,
                        entry.ObjectName,
                        rotation * Vector3.forward,
                        rotation * Vector3.right,
                        entry.AssetDefinition,
                        supportSurface: entry.SupportSurface,
                        identity: entry.Root ? entry.Root : entry.ObjectName,
                        source: AssetRelativeAnchorSource.GeneratedObjects));
                }
            }

            if (rule.Source is AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.SceneAnchors)
            {
                foreach (RelativeAnchor anchor in context.AssetRelationAnchors.Anchors)
                    AddIfMatching(anchor);

                if (rule.UsesPathStations && dependentAsset)
                {
                    foreach (RelativeAnchor anchor in context.GetPathStationAnchors(dependentAsset))
                    {
                        AddIfMatching(anchor);
                    }
                }
            }

            return anchors;
        }

        internal static IReadOnlyList<RelativeAnchor> CollectMatchingAssetAnchors(
            GenerationContext context,
            AssetPoolAnchorGroupLimit group,
            bool includePlannedObjects = true)
        {
            if (context == null || group?.IsConfigured != true)
                return System.Array.Empty<RelativeAnchor>();

            List<RelativeAnchor> anchors = new();
            HashSet<object> identities = new();

            void AddIfMatching(RelativeAnchor anchor)
            {
                if (!group.MatchesAnchor(anchor.Asset, anchor.AssetTags))
                    return;

                object identity = anchor.Identity ?? $"{anchor.Name}:{anchor.Position}";
                if (identities.Add(identity))
                    anchors.Add(anchor);
            }

            if (group.Source is AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.GeneratedObjects)
            {
                if (includePlannedObjects)
                {
                    foreach (PlannedObject plannedObject in context.Plan.Objects)
                        AddIfMatching(CreatePlannedAnchor(plannedObject));
                }

                foreach (SceneObjectIndex.Entry entry in context.GeneratedSceneObjects.Entries)
                {
                    if (!entry.AssetDefinition)
                        continue;

                    Quaternion rotation = GetPlacementRotation(entry.Root, entry.AssetDefinition);
                    AddIfMatching(new RelativeAnchor(
                        entry.Bounds.center,
                        entry.Bounds,
                        entry.ObjectName,
                        rotation * Vector3.forward,
                        rotation * Vector3.right,
                        entry.AssetDefinition,
                        supportSurface: entry.SupportSurface,
                        identity: entry.Root ? entry.Root : entry.ObjectName,
                        source: AssetRelativeAnchorSource.GeneratedObjects));
                }
            }

            if (group.Source is AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.SceneAnchors)
            {
                foreach (RelativeAnchor anchor in context.AssetRelationAnchors.Anchors)
                    AddIfMatching(anchor);
            }

            return anchors;
        }

        internal static RelativeAnchor CreatePlannedAnchor(PlannedObject plannedObject) => new(
            plannedObject.Bounds.Center,
            plannedObject.Bounds.ToAxisAlignedBounds(),
            plannedObject.ObjectName,
            plannedObject.Candidate.Rotation * Vector3.forward,
            plannedObject.Candidate.Rotation * Vector3.right,
            plannedObject.Asset,
            supportSurface: PlacementSupportRules.GetDescriptor(plannedObject.Candidate.SurfaceCollider),
            identity: GetPlannedIdentity(plannedObject),
            source: AssetRelativeAnchorSource.GeneratedObjects);
    }
}
