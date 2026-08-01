using System;
using Genix.Assets;
using Genix.Core;
using Genix.Sampling;
using Genix.Geometry;
using Genix.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Placement
{
    public static class PlacementValidator
    {
        private const int InitialOverlapBufferSize = 64;

        [ThreadStatic] private static Collider[] _overlapBuffer;

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
            bool isWallPlacement = candidate.PlacementType == PlacementType.Wall;
            bool isInsideSpacePlacement = candidate.PlacementType == PlacementType.InsideSpace;
            PlacementType placementType = candidate.PlacementType;

            long stepStart = StartValidationStep(profiler);
            if (!FitsTargetHeight(axisAlignedBounds, context.TargetBounds))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.Height, stepStart);
                rejectionReason = RejectionReason.ExceedsTargetHeight;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.Height, stepStart);

            stepStart = StartValidationStep(profiler);
            if (TryFindTooClosePlannedObject(axisAlignedBounds, context, out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.PlannedSpacing, stepStart);
                rejectionReason = RejectionReason.TooCloseToGenerated;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.PlannedSpacing, stepStart);

            stepStart = StartValidationStep(profiler);
            if (!context.Area.ContainsPlacementVolume(candidateBounds))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.Volume, stepStart);
                rejectionReason = RejectionReason.OutsideTargetVolume;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.Volume, stepStart);

            stepStart = StartValidationStep(profiler);
            if (!RelativeAnchorProvider.IsCandidateInRange(candidate, context, out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.Relative, stepStart);
                rejectionReason = RejectionReason.OutsideRelativeRadius;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.Relative, stepStart);

            stepStart = StartValidationStep(profiler);
            if (TryFindOverlappingGeneratedObject(candidateBounds, context, out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.GeneratedOverlap, stepStart);
                rejectionReason = RejectionReason.OverlapsGenerated;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.GeneratedOverlap, stepStart);

            stepStart = StartValidationStep(profiler);
            if (TryFindTooCloseGeneratedSceneObject(axisAlignedBounds, context, out relatedObjectName))
            {
                RecordValidationStep(profiler, placementType, ValidationProfileStep.GeneratedSceneSpacing, stepStart);
                rejectionReason = RejectionReason.TooCloseToGenerated;
                return false;
            }
            RecordValidationStep(profiler, placementType, ValidationProfileStep.GeneratedSceneSpacing, stepStart);

            if (!isWallPlacement && !isInsideSpacePlacement && asset)
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

            return true;
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
            return TryFindTooClosePlannedObject(seedBounds, context, out relatedObjectName);
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
            return TryFindTooCloseGeneratedSceneObject(seedBounds, context, out relatedObjectName);
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
                if (!BoundsOverlap(candidateBounds, sceneObject.Bounds))
                    continue;

                relatedObjectName = sceneObject.ObjectName;
                return true;
            }

            return false;
        }

        private static bool TryFindTooClosePlannedObject(
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

            foreach (PlannedObject plannedObject in context.Plan.QueryHorizontalSpacing(candidateBounds, minDistance))
            {
                if (!IsCloserThanMinDistance(
                        candidateBounds.center,
                        plannedObject.Bounds.Center,
                        minDistance))
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

            Bounds verticalBounds = generatedSceneObjects.HasBounds
                ? generatedSceneObjects.Bounds
                : context.TargetBounds;
            Bounds queryBounds = CreateHorizontalSpacingQueryBounds(
                candidateBounds,
                minDistance,
                verticalBounds);

            foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.Query(queryBounds))
            {
                if (!IsCloserThanMinDistance(candidateBounds, sceneObject.Bounds, minDistance))
                    continue;

                relatedObjectName = sceneObject.ObjectName;
                return true;
            }

            return false;
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

        private static bool IsCloserThanMinDistance(Bounds a, Bounds b, float minDistance)
        {
            return IsCloserThanMinDistance(a.center, b.center, minDistance);
        }

        private static bool IsCloserThanMinDistance(Vector3 a, Vector3 b, float minDistance)
        {
            float minDistanceSquared = minDistance * minDistance;

            float dx = a.x - b.x;
            float dz = a.z - b.z;

            return dx * dx + dz * dz < minDistanceSquared;
        }

        private static bool BoundsOverlap(OrientedBounds a, Bounds b)
        {
            return a.Intersects(b);
        }
    }
}
