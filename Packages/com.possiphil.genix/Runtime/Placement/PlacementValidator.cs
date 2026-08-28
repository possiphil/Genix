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
    public static partial class PlacementValidator
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


        internal static bool FitsTargetHeight(Bounds candidateBounds, Bounds targetBounds)
        {
            return candidateBounds.min.y >= targetBounds.min.y - ContactTolerance &&
                   candidateBounds.max.y <= targetBounds.max.y + ContactTolerance;
        }



    }
}
