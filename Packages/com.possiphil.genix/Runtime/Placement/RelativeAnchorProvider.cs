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
    internal readonly struct RelativeAnchor
    {
        public Vector3 Position { get; }
        public Bounds Bounds { get; }
        public string Name { get; }
        public Vector3 Forward { get; }
        public Vector3 Right { get; }
        public AssetDefinition Asset { get; }
        public IReadOnlyList<Genix.Semantics.SemanticTag> AssetTags { get; }
        public PlacementSurfaceDescriptor SupportSurface { get; }
        public object Identity { get; }
        public string PersistentIdentityKey { get; }
        public AssetRelativeAnchorSource Source { get; }

        public RelativeAnchor(
            Vector3 position,
            Bounds bounds,
            string name,
            Vector3 forward = default,
            Vector3 right = default,
            AssetDefinition asset = null,
            IReadOnlyList<Genix.Semantics.SemanticTag> assetTags = null,
            PlacementSurfaceDescriptor supportSurface = null,
            object identity = null,
            AssetRelativeAnchorSource source = AssetRelativeAnchorSource.Any)
        {
            Position = position;
            Bounds = bounds;
            Name = string.IsNullOrWhiteSpace(name) ? "Relative Anchor" : name;
            Forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            Right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
            Asset = asset;
            AssetTags = assetTags ?? System.Array.Empty<Genix.Semantics.SemanticTag>();
            SupportSurface = supportSurface;
            Identity = identity;
            PersistentIdentityKey = RelativeAnchorProvider.GetPersistentIdentityKey(identity);
            Source = source;
        }

        public bool Matches(AssetRelativePlacementRule rule) =>
            rule != null && rule.Matches(Asset, AssetTags);
    }

    internal sealed class AssetRelationAnchorIndex
    {
        private readonly List<RelativeAnchor> _anchors = new();
        private readonly SpatialBoundsIndex _spatialIndex = new();
        private readonly HashSet<AssetDefinition> _assets = new();
        private readonly HashSet<Genix.Semantics.SemanticTag> _assetTags = new();

        public int Count => _anchors.Count;
        public IReadOnlyList<RelativeAnchor> Anchors => _anchors;

        public void Add(RelativeAnchor anchor)
        {
            _anchors.Add(anchor);
            _spatialIndex.Add(anchor.Bounds, _anchors.Count - 1);

            if (anchor.Asset)
            {
                _assets.Add(anchor.Asset);

                foreach (Genix.Semantics.SemanticTag tag in anchor.Asset.SemanticTags)
                {
                    if (tag && tag.Category && tag.Category.SupportsAssets)
                        _assetTags.Add(tag);
                }
            }

            foreach (Genix.Semantics.SemanticTag tag in anchor.AssetTags)
            {
                if (tag && tag.Category && tag.Category.SupportsAssets)
                    _assetTags.Add(tag);
            }
        }

        public IEnumerable<RelativeAnchor> Query(Bounds bounds)
        {
            foreach (int index in _spatialIndex.Query(bounds))
                yield return _anchors[index];
        }

        public bool HasMatch(AssetRelativePlacementRule rule) => rule.TargetScope switch
        {
            AssetRelativeTargetScope.Asset => rule.TargetAsset && _assets.Contains(rule.TargetAsset),
            AssetRelativeTargetScope.AssetTag => rule.TargetTag && _assetTags.Contains(rule.TargetTag),
            _ => false
        };
    }

    /// <summary>Resolves scene and planned-object anchors used by relative-placement constraints.</summary>
    public static class RelativeAnchorProvider
    {
        private const float AnchorDistanceTieTolerance = 0.05f;

        internal static string GetPersistentIdentityKey(object identity)
        {
            if (identity is string text)
            {
                return text.StartsWith("planned:", System.StringComparison.Ordinal)
                    ? $"generated:{text.Substring("planned:".Length)}"
                    : text;
            }

            Transform transform = identity switch
            {
                Transform value => value,
                Component component => component.transform,
                GameObject gameObject => gameObject.transform,
                _ => null
            };
            if (!transform)
                return string.Empty;

            if (transform.GetComponent<GeneratedObjectMetadata>())
                return $"generated:{transform.name}";

            Stack<string> path = new();
            for (Transform current = transform; current; current = current.parent)
                path.Push($"{current.name}[{current.GetSiblingIndex()}]");

            string scenePath = transform.gameObject.scene.path;
            return $"scene:{scenePath}|{string.Join("/", path)}";
        }

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

        internal static bool TryValidateCandidate(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            AssetDefinition asset,
            GenerationContext context,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            rejectionReason = RejectionReason.None;
            relatedObjectName = string.Empty;

            if (!IsCandidateInRange(candidate, context, out relatedObjectName))
            {
                rejectionReason = RejectionReason.OutsideRelativeRadius;
                return false;
            }

            AssetRelativePlacementRule rule = asset ? asset.AssetRelativePlacement : null;

            if (rule?.IsConfigured != true)
                return true;

            bool foundMatchingAnchor = false;
            bool foundAnchorOnRequiredSupport = false;
            bool foundAnchorInRange = false;
            bool foundAnchorOnRequiredSide = false;
            bool foundAnchorInsideRequiredBounds = false;
            float nearestValidDistance = float.PositiveInfinity;
            float nearestValidCenterDistance = float.PositiveInfinity;
            RelativeAnchor nearestValidAnchor = default;
            PlacementSurfaceDescriptor candidateSupport = rule.RequireSameSupportSurface
                ? PlacementSupportRules.GetDescriptor(candidate.SurfaceCollider)
                : null;

            if (rule.RequireSameSupportSurface && !candidateSupport)
            {
                rejectionReason = RejectionReason.DifferentAssetRelationSupportSurface;
                relatedObjectName = candidate.SurfaceCollider ? candidate.SurfaceCollider.name : string.Empty;
                return false;
            }

            foreach (RelativeAnchor anchor in EnumerateAssetAnchors(context, asset, rule, candidate.Position))
            {
                if (!MatchesRequiredAnchor(context, anchor, candidate.RelationAnchorIdentity))
                    continue;

                if (!anchor.Matches(rule))
                    continue;

                foundMatchingAnchor = true;

                if (rule.RequireSameSupportSurface && anchor.SupportSurface != candidateSupport)
                {
                    if (string.IsNullOrEmpty(relatedObjectName))
                        relatedObjectName = anchor.Name;
                    continue;
                }

                foundAnchorOnRequiredSupport = true;
                float distance = DistanceBetweenBounds(
                    candidateBounds.ToAxisAlignedBounds(),
                    anchor.Bounds);

                if (distance < rule.MinimumDistance || distance > rule.MaximumDistance)
                    continue;

                foundAnchorInRange = true;

                if (!MatchesSide(candidate.Position, anchor, rule))
                    continue;

                foundAnchorOnRequiredSide = true;
                if (rule.RequireInsideAnchorBounds && !Contains(anchor, candidateBounds))
                    continue;

                foundAnchorInsideRequiredBounds = true;
                float centerDistance = (candidate.Position - anchor.Position).sqrMagnitude;
                if (IsBetterAnchor(
                        distance,
                        centerDistance,
                        nearestValidDistance,
                        nearestValidCenterDistance))
                {
                    nearestValidDistance = distance;
                    nearestValidCenterDistance = centerDistance;
                    nearestValidAnchor = anchor;
                }
            }

            if (foundAnchorInsideRequiredBounds)
            {
                relatedObjectName = nearestValidAnchor.Name;
                if (!HasRemainingPerAnchorCapacity(context, asset, rule, nearestValidAnchor))
                {
                    rejectionReason = RejectionReason.AssetRelationAnchorCapacityReached;
                    return false;
                }

                if (context.AssetPool && context.AssetPool.TryGetReachedAnchorGroupLimit(
                        asset,
                        context,
                        nearestValidAnchor,
                        out AssetPoolAnchorGroupLimit reachedGroup))
                {
                    rejectionReason = RejectionReason.AssetRelationGroupCapacityReached;
                    relatedObjectName = $"{nearestValidAnchor.Name} ({reachedGroup.MemberTag.DisplayName})";
                    return false;
                }

                return true;
            }

            rejectionReason = !foundMatchingAnchor
                ? HasMatchingAssetAnchor(context, asset, rule)
                    ? RejectionReason.OutsideAssetRelationRange
                    : RejectionReason.MissingAssetRelationAnchor
                : !foundAnchorOnRequiredSupport
                    ? RejectionReason.DifferentAssetRelationSupportSurface
                    : !foundAnchorInRange
                        ? RejectionReason.OutsideAssetRelationRange
                        : !foundAnchorOnRequiredSide
                            ? RejectionReason.WrongAssetRelationSide
                            : RejectionReason.OutsideAssetRelationBounds;
            if (string.IsNullOrEmpty(relatedObjectName))
            {
                relatedObjectName = rule.TargetScope == AssetRelativeTargetScope.Asset
                    ? rule.TargetAsset ? rule.TargetAsset.AssetName : "Asset Relation"
                    : rule.TargetTag ? rule.TargetTag.DisplayName : "Asset Relation";
            }
            return false;
        }

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

        internal static bool IsPotentialSeedForAnchor(
            GenerationContext context,
            CandidateSeed seed,
            AssetDefinition dependentAsset,
            RelativeAnchor anchor)
        {
            AssetRelativePlacementRule rule = dependentAsset ? dependentAsset.AssetRelativePlacement : null;
            if (rule?.IsConfigured != true || seed.PlacementType != dependentAsset.PlacementType)
                return false;

            PlacementSurfaceDescriptor supportSurface =
                PlacementSupportRules.GetDescriptor(seed.SurfaceCollider);
            if (rule.RequireSameSupportSurface && supportSurface != anchor.SupportSurface)
            {
                return false;
            }

            float assetMargin = AssetAttemptPlanner.Dimensions(dependentAsset).magnitude * 0.5f;
            float distance = DistanceToAnchor(seed.Position, anchor);
            return distance <= rule.MaximumDistance + assetMargin &&
                   distance + assetMargin >= rule.MinimumDistance &&
                   MatchesSide(seed.Position, anchor, rule) &&
                   IsPreferredAnchorForPosition(
                       context,
                       dependentAsset,
                       rule,
                       seed.Position,
                       supportSurface,
                       anchor);
        }

        private static bool IsPreferredAnchorForPosition(
            GenerationContext context,
            AssetDefinition dependentAsset,
            AssetRelativePlacementRule rule,
            Vector3 position,
            PlacementSurfaceDescriptor supportSurface,
            RelativeAnchor requiredAnchor)
        {
            bool found = false;
            float nearestDistance = float.PositiveInfinity;
            float nearestCenterDistance = float.PositiveInfinity;
            RelativeAnchor nearestAnchor = default;

            foreach (RelativeAnchor anchor in EnumerateAssetAnchors(
                         context,
                         dependentAsset,
                         rule,
                         position))
            {
                if (!anchor.Matches(rule) ||
                    rule.RequireSameSupportSurface && anchor.SupportSurface != supportSurface ||
                    !MatchesSide(position, anchor, rule))
                {
                    continue;
                }

                float distance = DistanceToAnchor(position, anchor);
                if (distance < rule.MinimumDistance || distance > rule.MaximumDistance)
                    continue;

                float centerDistance = (position - anchor.Position).sqrMagnitude;
                if (!IsBetterAnchor(
                        distance,
                        centerDistance,
                        nearestDistance,
                        nearestCenterDistance))
                {
                    continue;
                }

                found = true;
                nearestDistance = distance;
                nearestCenterDistance = centerDistance;
                nearestAnchor = anchor;
            }

            return !found || IsSameAnchor(requiredAnchor, nearestAnchor);
        }

        private static Quaternion GetPlacementRotation(Transform root, AssetDefinition asset)
        {
            if (!root)
                return Quaternion.identity;

            return asset
                ? asset.RemovePrefabRotationOffset(root.rotation)
                : root.rotation;
        }

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
