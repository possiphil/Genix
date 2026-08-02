using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Orientation;
using Genix.Profiling;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>Creates candidate instances.</summary>
    public static class CandidateFactory
    {
        private const int RandomRotationAttempts = 8;

        /// <summary>Builds an asset-specific placement candidate from a reusable seed.</summary>
        public static PlacementCandidate Create(
            CandidateSeed seed,
            GenerationContext context,
            AssetDefinition asset,
            int rotationIndex,
            int rotationCount,
            float yawBase,
            IGenerationProfiler profiler = null)
        {
            Vector3 surfaceNormal = seed.SurfaceNormal.sqrMagnitude <= 0.001f
                ? Vector3.up
                : seed.SurfaceNormal.normalized;
            Quaternion baseRotation = CreateBaseRotation(
                seed,
                context,
                asset,
                rotationIndex,
                rotationCount,
                yawBase);
            Vector3 position = CreatePosition(seed, context, asset, surfaceNormal);
            bool hasSurfaceFit = false;
            SurfaceFitResult surfaceFit = default;

            if (TryApplyAdaptiveSurfaceFit(
                    seed,
                    context,
                    asset,
                    baseRotation,
                    ref surfaceNormal,
                    ref position,
                    out surfaceFit,
                    profiler))
            {
                hasSurfaceFit = true;
                position -= surfaceNormal * asset.SurfaceSinkOffset;
            }

            Quaternion rotation = seed.PlacementType == PlacementType.InsideSpace
                ? baseRotation
                : AlignToSurface(baseRotation, surfaceNormal);

            return new PlacementCandidate(
                position,
                rotation,
                seed.SurfaceCollider,
                surfaceNormal,
                seed.VoxelLayer,
                seed.PlacementType,
                hasSurfaceFit,
                surfaceFit);
        }

        /// <summary>Builds the candidate's oriented bounds from the asset dimensions.</summary>
        public static OrientedBounds GetBounds(PlacementCandidate candidate, AssetDefinition asset) =>
            new(candidate.Position, AssetAttemptPlanner.Dimensions(asset), candidate.Rotation);

        /// <summary>Returns the number of deterministic rotation variants evaluated for this asset and target.</summary>
        public static int GetRotationAttemptCount(
            GenerationContext context,
            AssetDefinition asset,
            PlacementType placementType)
        {
            if (placementType == PlacementType.Wall || FacesRelativeAnchor(context, asset))
                return 1;

            return UsesRandomRotation(context, asset, placementType) ? RandomRotationAttempts : 1;
        }

        /// <summary>Determines whether the orientation mode randomizes yaw.</summary>
        public static bool UsesRandomYaw(
            GenerationContext context,
            AssetDefinition asset,
            PlacementType placementType)
        {
            return asset.RandomYawRotation &&
                   placementType != PlacementType.Wall &&
                   !FacesRelativeAnchor(context, asset);
        }

        private static bool UsesRandomRotation(
            GenerationContext context,
            AssetDefinition asset,
            PlacementType placementType)
        {
            if (FacesRelativeAnchor(context, asset))
                return false;

            if (placementType == PlacementType.InsideSpace)
            {
                return asset.RandomYawRotation ||
                       asset.RandomPitchRotation ||
                       asset.RandomRollRotation;
            }

            return UsesRandomYaw(context, asset, placementType);
        }

        private static Vector3 CreatePosition(
            CandidateSeed seed,
            GenerationContext context,
            AssetDefinition asset,
            Vector3 surfaceNormal)
        {
            if (seed.PlacementType == PlacementType.InsideSpace)
                return seed.Position;

            if (seed.PlacementType != PlacementType.Wall)
                return seed.Position + surfaceNormal * (Mathf.Max(0.01f, asset.Height) * 0.5f);

            float offset = asset.PlacementHeight;

            if (asset.UseHeightOffset)
            {
                float maxOffset = Mathf.Max(0f, asset.MaxHeightOffset);
                offset += context.Random.Range(-maxOffset, maxOffset);
            }

            Vector3 position = seed.Position + surfaceNormal * (asset.Depth * 0.5f);
            position.y += offset;
            return position;
        }

        private static Quaternion CreateBaseRotation(
            CandidateSeed seed,
            GenerationContext context,
            AssetDefinition asset,
            int rotationIndex,
            int rotationCount,
            float yawBase)
        {
            if (seed.PlacementType == PlacementType.Wall)
                return seed.Rotation;

            Quaternion rotation = seed.Rotation;

            if (FacesRelativeAnchor(context, asset) &&
                RelativeAnchorProvider.TryFindNearestAnchor(context, seed.Position, out RelativeAnchor anchor))
            {
                Vector3 direction = anchor.Position - seed.Position;

                if (seed.PlacementType != PlacementType.InsideSpace)
                    direction.y = 0f;

                rotation = direction.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(direction)
                    : Quaternion.identity;
            }
            else if (seed.PlacementType == PlacementType.InsideSpace)
            {
                rotation = CreateInsideSpaceRotation(context, asset, rotation, rotationIndex, rotationCount, yawBase);
            }
            else if (UsesRandomYaw(context, asset, seed.PlacementType))
            {
                float step = rotationCount > 1 ? 360f / rotationCount : 0f;
                float yaw = Mathf.Repeat(yawBase + step * rotationIndex, 360f);
                rotation = Quaternion.Euler(0f, yaw, 0f) * rotation;
            }

            return rotation;
        }

        private static bool TryApplyAdaptiveSurfaceFit(
            CandidateSeed seed,
            GenerationContext context,
            AssetDefinition asset,
            Quaternion baseRotation,
            ref Vector3 surfaceNormal,
            ref Vector3 position,
            out SurfaceFitResult fit,
            IGenerationProfiler profiler)
        {
            fit = default;

            if (!asset ||
                asset.SurfaceFitMode != SurfaceFitMode.Adaptive ||
                seed.PlacementType is not (PlacementType.Floor or PlacementType.Ceiling))
            {
                return false;
            }

            if (!context.SurfaceFitCache.TryEvaluate(
                    context.Area,
                    seed.Position,
                    baseRotation,
                    asset,
                    seed.SurfaceCollider,
                    seed.VoxelLayer,
                    seed.PlacementType,
                    out fit,
                    profiler))
            {
                return false;
            }

            surfaceNormal = asset.SurfaceAlignmentMode == SurfaceAlignmentMode.KeepUpright
                ? GetUprightNormal(seed.PlacementType)
                : fit.Normal;
            position = fit.Position + surfaceNormal * (Mathf.Max(0.01f, asset.Height) * 0.5f);
            return true;
        }

        private static Quaternion CreateInsideSpaceRotation(
            GenerationContext context,
            AssetDefinition asset,
            Quaternion baseRotation,
            int rotationIndex,
            int rotationCount,
            float yawBase)
        {
            float step = rotationCount > 1 ? 360f / rotationCount : 0f;
            float yaw = asset.RandomYawRotation
                ? Mathf.Repeat(yawBase + step * rotationIndex, 360f)
                : 0f;
            float pitch = asset.RandomPitchRotation
                ? context.Random.Range(0f, 360f)
                : 0f;
            float roll = asset.RandomRollRotation
                ? context.Random.Range(0f, 360f)
                : 0f;

            return Quaternion.Euler(pitch, yaw, roll) * baseRotation;
        }

        private static bool FacesRelativeAnchor(GenerationContext context, AssetDefinition asset)
        {
            return asset.OrientationMode == OrientationMode.FaceTarget &&
                   context.RelativePlacement.IsEnabled;
        }

        private static Quaternion AlignToSurface(Quaternion rotation, Vector3 surfaceNormal)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, surfaceNormal);

            if (forward.sqrMagnitude <= 0.001f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal);

            if (forward.sqrMagnitude <= 0.001f)
                forward = Vector3.ProjectOnPlane(Vector3.right, surfaceNormal);

            return Quaternion.LookRotation(forward.normalized, surfaceNormal);
        }

        private static Vector3 GetUprightNormal(PlacementType placementType)
        {
            return placementType == PlacementType.Ceiling ? Vector3.down : Vector3.up;
        }
    }
}
