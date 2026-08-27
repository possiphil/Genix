using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Sampling;
using Genix.Geometry;
using Genix.Profiling;
using Genix.Semantics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Placement
{
    /// <summary>Validates placement constraints.</summary>
    public static class PlacementValidator
    {
        private const int InitialOverlapBufferSize = 64;
        private const float ContactTolerance = 0.001f;

        [ThreadStatic] private static Collider[] _overlapBuffer;

        /// <summary>Determines whether valid candidate.</summary>
        public static bool IsValidCandidate(PlacementCandidate candidate, Bounds candidateBounds, GenerationContext context)
        {
            return TryValidateCandidate(candidate, candidateBounds, context, out _, out _);
        }

        /// <summary>Attempts to validate candidate.</summary>
        public static bool TryValidateCandidate(
            PlacementCandidate candidate,
            Bounds candidateBounds,
            GenerationContext context,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            return TryValidateCandidate(candidate, candidateBounds, context, null, out rejectionReason, out relatedObjectName);
        }

        /// <summary>Attempts to validate candidate.</summary>
        public static bool TryValidateCandidate(
            PlacementCandidate candidate,
            Bounds candidateBounds,
            GenerationContext context,
            AssetDefinition asset,
            out RejectionReason rejectionReason,
            out string relatedObjectName,
            IGenerationProfiler profiler = null)
        {
            OrientedBounds orientedBounds = new(candidateBounds.center, candidateBounds.size, candidate.Rotation);

            return TryValidateCandidate(
                candidate,
                orientedBounds,
                context,
                asset,
                out rejectionReason,
                out relatedObjectName,
                profiler);
        }

        /// <summary>Attempts to validate candidate.</summary>
        public static bool TryValidateCandidate(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            GenerationContext context,
            AssetDefinition asset,
            out RejectionReason rejectionReason,
            out string relatedObjectName,
            IGenerationProfiler profiler = null)
        {
            rejectionReason = RejectionReason.None;
            relatedObjectName = string.Empty;
            Bounds axisAlignedBounds = candidateBounds.ToAxisAlignedBounds();
            OrientedBounds containmentBounds = RemoveSurfaceSink(candidateBounds, candidate, asset);
            Bounds containmentAabb = containmentBounds.ToAxisAlignedBounds();
            bool isWallPlacement = candidate.PlacementType == PlacementType.Wall;
            bool isInsideSpacePlacement = candidate.PlacementType == PlacementType.InsideSpace;
            PlacementType placementType = candidate.PlacementType;

            long stepStart = StartValidationStep(profiler);
            if (!FitsTargetHeight(containmentAabb, context.TargetBounds))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.Height, stepStart);
                rejectionReason = RejectionReason.ExceedsTargetHeight;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.Height, stepStart);

            stepStart = StartValidationStep(profiler);
            if (TryFindExclusionRegion(candidateBounds, placementType, context, asset, out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.Exclusion, stepStart);
                rejectionReason = RejectionReason.InsideExclusionRegion;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.Exclusion, stepStart);

            if (asset && asset.WallProximityMode != WallProximityMode.AnyDistance)
            {
                stepStart = StartValidationStep(profiler);
                bool wallRelationshipValid = WallProximityRules.TryValidate(
                    asset,
                    candidateBounds,
                    context,
                    out rejectionReason,
                    out relatedObjectName);
                RecordValidationStep(profiler, placementType, ValidationProfileStep.WallRelationship, stepStart);

                if (!wallRelationshipValid)
                    return false;
            }

            stepStart = StartValidationStep(profiler);
            if (TryFindTooClosePlannedObject(axisAlignedBounds, placementType, context, out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.PlannedSpacing, stepStart);
                rejectionReason = RejectionReason.TooCloseToGenerated;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.PlannedSpacing, stepStart);

            stepStart = StartValidationStep(profiler);
            if (!context.Area.ContainsPlacementVolume(containmentBounds))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.Volume, stepStart);
                rejectionReason = RejectionReason.OutsideTargetVolume;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.Volume, stepStart);

            if (asset && asset.ReserveClearance)
            {
                stepStart = StartValidationStep(profiler);
                OrientedBounds clearanceBounds = asset.CreateClearanceBounds(candidate);

                if (!context.Area.ContainsClearanceVolume(clearanceBounds))
                {
                    RecordValidationStep(profiler, placementType, ValidationProfileStep.Clearance, stepStart);
                    rejectionReason = RejectionReason.ClearanceOutsideTargetVolume;
                    return false;
                }

                RecordValidationStep(profiler, placementType, ValidationProfileStep.Clearance, stepStart);
            }

            stepStart = StartValidationStep(profiler);
            if (!RelativeAnchorProvider.TryValidateCandidate(
                    candidate,
                    candidateBounds,
                    asset,
                    context,
                    out rejectionReason,
                    out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.Relative, stepStart);
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.Relative, stepStart);
            string matchedRelativeAnchorName = relatedObjectName;

            stepStart = StartValidationStep(profiler);
            if (!PathPlacementSource.TryValidate(
                    context,
                    asset,
                    candidate.Position,
                    out rejectionReason,
                    out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.Relative, stepStart);
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.Relative, stepStart);
            if (string.IsNullOrEmpty(relatedObjectName))
                relatedObjectName = matchedRelativeAnchorName;

            stepStart = StartValidationStep(profiler);
            if (TryFindOverlappingGeneratedObject(candidate, candidateBounds, context, out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.GeneratedOverlap, stepStart);
                rejectionReason = RejectionReason.OverlapsGenerated;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.GeneratedOverlap, stepStart);

            stepStart = StartValidationStep(profiler);
            if (TryFindTooCloseGeneratedSceneObject(
                    axisAlignedBounds,
                    placementType,
                    candidate.SurfaceCollider,
                    context,
                    out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.GeneratedSceneSpacing, stepStart);
                rejectionReason = RejectionReason.TooCloseToGenerated;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.GeneratedSceneSpacing, stepStart);

            stepStart = StartValidationStep(profiler);
            if (TryFindAssetSpacingViolation(
                    axisAlignedBounds,
                    placementType,
                    candidate.SurfaceCollider,
                    asset,
                    context,
                    out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.AssetSpacing, stepStart);
                rejectionReason = RejectionReason.AssetSpacingViolation;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.AssetSpacing, stepStart);

            bool requiresAssetSurfaceValidation =
                !isInsideSpacePlacement &&
                asset &&
                (!isWallPlacement || asset.SurfaceFitMode == SurfaceFitMode.Adaptive);

            if (requiresAssetSurfaceValidation)
            {
                stepStart = StartValidationStep(profiler);
                if (!context.Area.ContainsPlacementFootprint(candidate, asset, profiler))
                {
                    RecordValidationStep(profiler, placementType, ValidationProfileStep.SurfaceFit, stepStart);
                    rejectionReason = asset.SurfaceFitMode == SurfaceFitMode.Adaptive
                        ? RejectionReason.InsufficientSurfaceSupport
                        : RejectionReason.OutsideTargetArea;
                    return false;
                }
                RecordValidationStep(profiler, placementType, ValidationProfileStep.SurfaceFit, stepStart);
            }
            else if (!isWallPlacement && !isInsideSpacePlacement)
            {
                stepStart = StartValidationStep(profiler);
                bool containsFootprint = context.Area.ContainsFootprint(axisAlignedBounds);
                RecordValidationStep(profiler, placementType, ValidationProfileStep.Footprint, stepStart);

                if (!containsFootprint)
                {
                    rejectionReason = RejectionReason.OutsideTargetArea;
                    return false;
                }
            }

            stepStart = StartValidationStep(profiler);
            if (TryFindClearanceViolation(
                    candidate,
                    candidateBounds,
                    asset,
                    context,
                    out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.Clearance, stepStart);
                rejectionReason = RejectionReason.ClearanceBlocked;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.Clearance, stepStart);

            stepStart = StartValidationStep(profiler);
            if (TryFindOverlappingFixedObject(candidate, candidateBounds, context, out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.FixedOverlap, stepStart);
                rejectionReason = RejectionReason.OverlapsFixed;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.FixedOverlap, stepStart);

            stepStart = StartValidationStep(profiler);
            if (TryFindTooCloseFixedObject(candidate, candidateBounds, context, out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.FixedSpacing, stepStart);
                rejectionReason = RejectionReason.TooCloseToFixed;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.FixedSpacing, stepStart);

            relatedObjectName = matchedRelativeAnchorName;
            return true;
        }

        private static OrientedBounds RemoveSurfaceSink(
            OrientedBounds visualBounds,
            PlacementCandidate candidate,
            AssetDefinition asset)
        {
            if (!asset || asset.SurfaceSinkOffset <= 0f || candidate.PlacementType == PlacementType.InsideSpace)
                return visualBounds;

            Vector3 normal = candidate.SurfaceNormal.sqrMagnitude > 0.001f
                ? candidate.SurfaceNormal.normalized
                : candidate.PlacementType == PlacementType.Wall
                    ? candidate.Rotation * Vector3.forward
                    : candidate.Rotation * Vector3.up;
            return new OrientedBounds(
                visualBounds.Center + normal * asset.SurfaceSinkOffset,
                visualBounds.Size,
                visualBounds.Rotation);
        }

        internal static bool TryRejectByPlannedSpacing(
            CandidateSeed seed,
            AssetDefinition asset,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (!asset || seed.PlacementType == PlacementType.Wall)
                return false;

            Bounds seedBounds = new(seed.Position, AssetAttemptPlanner.Dimensions(asset));
            return TryFindTooClosePlannedObject(seedBounds, seed.PlacementType, context, out relatedObjectName);
        }

        internal static bool TryRejectByGeneratedSceneSpacing(
            CandidateSeed seed,
            AssetDefinition asset,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (!asset || seed.PlacementType == PlacementType.Wall)
                return false;

            Bounds seedBounds = new(seed.Position, AssetAttemptPlanner.Dimensions(asset));
            return TryFindTooCloseGeneratedSceneObject(
                seedBounds,
                seed.PlacementType,
                seed.SurfaceCollider,
                context,
                out relatedObjectName);
        }

        private static long StartValidationStep(IGenerationProfiler profiler) =>
            profiler is { IsEnabled: true } ? Stopwatch.GetTimestamp() : 0L;

        private static void RecordValidationStep(
            IGenerationProfiler profiler,
            PlacementType placementType,
            ValidationProfileStep step,
            long startTimestamp)
        {
            if (profiler is not { IsEnabled: true } || startTimestamp <= 0L)
                return;

            float milliseconds = (float)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency);
            profiler.RecordValidationStep(
                placementType,
                step,
                milliseconds);
        }

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

        internal static bool FitsTargetHeight(Bounds candidateBounds, Bounds targetBounds)
        {
            return candidateBounds.min.y >= targetBounds.min.y - ContactTolerance &&
                   candidateBounds.max.y <= targetBounds.max.y + ContactTolerance;
        }

        private static bool TryFindOverlappingGeneratedObject(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            Bounds axisAlignedBounds = candidateBounds.ToAxisAlignedBounds();

            foreach (PlannedObject plannedObject in context.Plan.Query(axisAlignedBounds))
            {
                if (!candidateBounds.Intersects(plannedObject.Bounds))
                    continue;

                relatedObjectName = plannedObject.ObjectName;
                return true;
            }

            SceneObjectIndex generatedSceneObjects = context.GeneratedSceneObjects;

            if (generatedSceneObjects == null || generatedSceneObjects.Count == 0)
                return false;

            foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.Query(axisAlignedBounds))
            {
                if (IsSupportingGeneratedObject(sceneObject, candidate.SurfaceCollider))
                    continue;

                if (!BoundsOverlap(candidateBounds, sceneObject.Bounds))
                    continue;

                relatedObjectName = sceneObject.ObjectName;
                return true;
            }

            return false;
        }

        private static bool TryFindTooClosePlannedObject(
            Bounds candidateBounds,
            PlacementType placementType,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (context.StyleSettings.algorithm != SamplingAlgorithm.BridsonPoissonDisk)
                return false;

            float minDistance = context.StyleSettings.poisson.minDistance;

            if (minDistance <= 0f)
                return false;

            bool includeHeight = UsesThreeDimensionalSpacing(placementType);
            IEnumerable<PlannedObject> nearbyObjects = includeHeight
                ? context.Plan.QuerySpatialSpacing(candidateBounds, minDistance)
                : context.Plan.QueryHorizontalSpacing(candidateBounds, minDistance);

            foreach (PlannedObject plannedObject in nearbyObjects)
            {
                if (!IsCloserThanMinDistance(
                        candidateBounds.center,
                        plannedObject.Bounds.Center,
                        minDistance,
                        includeHeight))
                {
                    continue;
                }

                relatedObjectName = plannedObject.ObjectName;
                return true;
            }

            return false;
        }

        private static bool TryFindTooCloseGeneratedSceneObject(
            Bounds candidateBounds,
            PlacementType placementType,
            Collider surfaceCollider,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (context.StyleSettings.algorithm != SamplingAlgorithm.BridsonPoissonDisk)
                return false;

            float minDistance = context.StyleSettings.poisson.minDistance;

            if (minDistance <= 0f)
                return false;

            SceneObjectIndex generatedSceneObjects = context.GeneratedSceneObjects;

            if (generatedSceneObjects == null || generatedSceneObjects.Count == 0)
                return false;

            Bounds queryBounds;

            bool includeHeight = UsesThreeDimensionalSpacing(placementType);

            if (includeHeight)
            {
                queryBounds = candidateBounds;
                queryBounds.Expand(minDistance * 2f);
            }
            else
            {
                Bounds verticalBounds = generatedSceneObjects.HasBounds
                    ? generatedSceneObjects.Bounds
                    : context.TargetBounds;
                queryBounds = CreateHorizontalSpacingQueryBounds(
                    candidateBounds,
                    minDistance,
                    verticalBounds);
            }

            foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.Query(queryBounds))
            {
                if (IsSupportingGeneratedObject(sceneObject, surfaceCollider))
                    continue;

                if (!IsCloserThanMinDistance(
                        candidateBounds.center,
                        sceneObject.Bounds.center,
                        minDistance,
                        includeHeight))
                    continue;

                relatedObjectName = sceneObject.ObjectName;
                return true;
            }

            return false;
        }

        private static bool TryFindAssetSpacingViolation(
            Bounds candidateBounds,
            PlacementType placementType,
            Collider surfaceCollider,
            AssetDefinition asset,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (!asset)
                return false;

            SceneObjectIndex generatedSceneObjects = context.GeneratedSceneObjects;
            float searchRadius = Mathf.Max(
                asset.MaxSpacingDistance,
                context.Plan.MaxAssetSpacingDistance,
                generatedSceneObjects?.MaxAssetSpacingDistance ?? 0f);

            if (searchRadius <= 0f)
                return false;

            bool includeHeight = UsesThreeDimensionalSpacing(placementType);
            IEnumerable<PlannedObject> plannedObjects = includeHeight
                ? context.Plan.QuerySpatialSpacing(candidateBounds, searchRadius)
                : context.Plan.QueryHorizontalSpacing(candidateBounds, searchRadius);

            foreach (PlannedObject plannedObject in plannedObjects)
            {
                float requiredDistance = GetRequiredAssetSpacing(asset, plannedObject.Asset);

                if (requiredDistance <= 0f ||
                    !IsCloserThanMinDistance(
                        candidateBounds.center,
                        plannedObject.Bounds.Center,
                        requiredDistance,
                        includeHeight))
                {
                    continue;
                }

                relatedObjectName = plannedObject.ObjectName;
                return true;
            }

            if (generatedSceneObjects == null || generatedSceneObjects.Count == 0)
                return false;

            Bounds queryBounds = includeHeight
                ? ExpandBounds(candidateBounds, searchRadius)
                : CreateHorizontalSpacingQueryBounds(
                    candidateBounds,
                    searchRadius,
                    generatedSceneObjects.HasBounds ? generatedSceneObjects.Bounds : context.TargetBounds);

            foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.Query(queryBounds))
            {
                if (!sceneObject.AssetDefinition || IsSupportingGeneratedObject(sceneObject, surfaceCollider))
                    continue;

                float requiredDistance = GetRequiredAssetSpacing(asset, sceneObject.AssetDefinition);

                if (requiredDistance <= 0f ||
                    !IsCloserThanMinDistance(
                        candidateBounds.center,
                        sceneObject.Bounds.center,
                        requiredDistance,
                        includeHeight))
                {
                    continue;
                }

                relatedObjectName = sceneObject.ObjectName;
                return true;
            }

            return false;
        }

        private static float GetRequiredAssetSpacing(AssetDefinition first, AssetDefinition second) =>
            first && second
                ? Mathf.Max(first.GetMinimumSpacingTo(second), second.GetMinimumSpacingTo(first))
                : 0f;

        private static bool TryFindClearanceViolation(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            AssetDefinition asset,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;
            bool hasCandidateClearance = asset && asset.ReserveClearance;
            bool hasExistingClearance = context.Plan.HasClearanceBounds ||
                                        context.GeneratedSceneObjects?.HasClearanceBounds == true;

            if (!hasCandidateClearance && !hasExistingClearance)
                return false;

            OrientedBounds candidateClearance = hasCandidateClearance
                ? asset.CreateClearanceBounds(candidate)
                : default;
            Bounds candidateVisualAabb = candidateBounds.ToAxisAlignedBounds();

            if (hasCandidateClearance)
            {
                if (TryFindExclusionRegion(
                        candidateClearance,
                        candidate.PlacementType,
                        context,
                        asset,
                        out relatedObjectName))
                {
                    return true;
                }

                Bounds clearanceAabb = candidateClearance.ToAxisAlignedBounds();

                foreach (PlannedObject plannedObject in context.Plan.Query(clearanceAabb))
                {
                    if (!candidateClearance.Intersects(plannedObject.Bounds))
                        continue;

                    relatedObjectName = plannedObject.ObjectName;
                    return true;
                }

                foreach (PlannedObject plannedObject in context.Plan.QueryClearance(clearanceAabb))
                {
                    OrientedBounds existingClearance = plannedObject.Asset.CreateClearanceBounds(
                        plannedObject.Candidate);

                    if (!candidateClearance.Intersects(existingClearance))
                        continue;

                    relatedObjectName = plannedObject.ObjectName;
                    return true;
                }

                SceneObjectIndex generatedSceneObjects = context.GeneratedSceneObjects;

                if (generatedSceneObjects != null)
                {
                    foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.Query(clearanceAabb))
                    {
                        if (IsSupportingGeneratedObject(sceneObject, candidate.SurfaceCollider) ||
                            !candidateClearance.Intersects(sceneObject.Bounds))
                        {
                            continue;
                        }

                        relatedObjectName = sceneObject.ObjectName;
                        return true;
                    }

                    foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.QueryClearance(clearanceAabb))
                    {
                        if (!sceneObject.AssetDefinition || !sceneObject.Root ||
                            IsSupportingGeneratedObject(sceneObject, candidate.SurfaceCollider))
                        {
                            continue;
                        }

                        OrientedBounds existingClearance = sceneObject.AssetDefinition.CreateClearanceBounds(
                            sceneObject.Root.position,
                            sceneObject.Root.rotation);

                        if (!candidateClearance.Intersects(existingClearance))
                            continue;

                        relatedObjectName = sceneObject.ObjectName;
                        return true;
                    }
                }

                if (TryFindFixedClearanceBlocker(candidate, candidateClearance, context, out relatedObjectName))
                    return true;
            }

            foreach (PlannedObject plannedObject in context.Plan.QueryClearance(candidateVisualAabb))
            {
                OrientedBounds existingClearance = plannedObject.Asset.CreateClearanceBounds(
                    plannedObject.Candidate);

                if (!existingClearance.Intersects(candidateBounds))
                    continue;

                relatedObjectName = plannedObject.ObjectName;
                return true;
            }

            SceneObjectIndex existingSceneObjects = context.GeneratedSceneObjects;

            if (existingSceneObjects == null)
                return false;

            foreach (SceneObjectIndex.Entry sceneObject in existingSceneObjects.QueryClearance(candidateVisualAabb))
            {
                if (!sceneObject.AssetDefinition || !sceneObject.Root ||
                    IsSupportingGeneratedObject(sceneObject, candidate.SurfaceCollider))
                {
                    continue;
                }

                OrientedBounds existingClearance = sceneObject.AssetDefinition.CreateClearanceBounds(
                    sceneObject.Root.position,
                    sceneObject.Root.rotation);

                if (!existingClearance.Intersects(candidateBounds))
                    continue;

                relatedObjectName = sceneObject.ObjectName;
                return true;
            }

            return false;
        }

        private static bool TryFindFixedClearanceBlocker(
            PlacementCandidate candidate,
            OrientedBounds clearanceBounds,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (!HasPotentialFixedObstacle(candidate, clearanceBounds, context))
                return false;

            int hitCount = OverlapBox(clearanceBounds, out Collider[] hits);

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

        private static Bounds ExpandBounds(Bounds bounds, float radius)
        {
            bounds.Expand(Mathf.Max(0f, radius) * 2f);
            return bounds;
        }

        private static bool TryFindExclusionRegion(
            OrientedBounds candidateBounds,
            PlacementType placementType,
            GenerationContext context,
            AssetDefinition asset,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (context?.ExclusionRegions == null || context.ExclusionRegions.Count == 0)
                return false;

            foreach (PlacementExclusionRegion region in context.ExclusionRegions)
            {
                if (!region || !region.Intersects(candidateBounds, placementType, asset))
                    continue;

                relatedObjectName = region.name;
                return true;
            }

            return false;
        }

        private static bool IsSupportingGeneratedObject(
            SceneObjectIndex.Entry sceneObject,
            Collider surfaceCollider)
        {
            if (!surfaceCollider || !sceneObject.Root)
                return false;

            PlacementSurfaceDescriptor descriptor = PlacementSupportRules.GetDescriptor(surfaceCollider);
            return descriptor && descriptor.transform.IsChildOf(sceneObject.Root);
        }

        private static Bounds CreateHorizontalSpacingQueryBounds(
            Bounds candidateBounds,
            float minDistance,
            Bounds verticalBounds)
        {
            float expansion = minDistance * 2f;
            Vector3 min = candidateBounds.min;
            Vector3 max = candidateBounds.max;

            min.x -= expansion;
            min.z -= expansion;
            max.x += expansion;
            max.z += expansion;

            if (verticalBounds.size.y > 0f)
            {
                min.y = Mathf.Min(min.y, verticalBounds.min.y);
                max.y = Mathf.Max(max.y, verticalBounds.max.y);
            }

            Bounds queryBounds = default;
            queryBounds.SetMinMax(min, max);
            return queryBounds;
        }

        private static bool IsCloserThanMinDistance(
            Vector3 a,
            Vector3 b,
            float minDistance,
            bool includeHeight)
        {
            float minDistanceSquared = minDistance * minDistance;

            float dx = a.x - b.x;
            float dy = includeHeight ? a.y - b.y : 0f;
            float dz = a.z - b.z;

            return dx * dx + dy * dy + dz * dz < minDistanceSquared;
        }

        private static bool UsesThreeDimensionalSpacing(PlacementType placementType) =>
            placementType is PlacementType.Wall or PlacementType.InsideSpace;

        private static bool BoundsOverlap(OrientedBounds a, Bounds b)
        {
            return a.Intersects(b);
        }
    }
}
