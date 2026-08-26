using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Orientation;
using Genix.Profiling;
using Genix.Semantics;
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
            Quaternion baseRotation = CreateBaseRotation(
                seed,
                context,
                asset,
                rotationIndex,
                rotationCount,
                yawBase,
                out object relationAnchorIdentity);
            PlacementCandidate candidate = CreateWithBaseRotation(
                seed,
                context,
                asset,
                baseRotation,
                relationAnchorIdentity,
                profiler);

            if (relationAnchorIdentity != null ||
                seed.PlacementType == PlacementType.Wall ||
                asset.AssetRelativePlacement?.UsesFacing != true ||
                !RelativeAnchorProvider.TryFindAssetAnchor(
                    context,
                    asset,
                    candidate.Position,
                    GetBounds(candidate, asset).ToAxisAlignedBounds(),
                    PlacementSupportRules.GetDescriptor(candidate.SurfaceCollider),
                    out RelativeAnchor finalAnchor))
            {
                return candidate;
            }

            Quaternion anchoredRotation = CreateAssetRelativeRotationWithVariation(
                seed,
                context,
                asset,
                finalAnchor);
            return CreateWithBaseRotation(
                seed,
                context,
                asset,
                anchoredRotation,
                finalAnchor.Identity,
                profiler);
        }

        private static PlacementCandidate CreateWithBaseRotation(
            CandidateSeed seed,
            GenerationContext context,
            AssetDefinition asset,
            Quaternion baseRotation,
            object relationAnchorIdentity,
            IGenerationProfiler profiler)
        {
            Vector3 surfaceNormal = seed.SurfaceNormal.sqrMagnitude <= 0.001f
                ? Vector3.up
                : seed.SurfaceNormal.normalized;
            Quaternion initialRotation = AlignCandidateRotation(seed.PlacementType, baseRotation, surfaceNormal);
            Vector3 position = CreatePosition(seed, context, asset, surfaceNormal, initialRotation);
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
            }

            if (seed.PlacementType != PlacementType.InsideSpace)
                position -= surfaceNormal * asset.SurfaceSinkOffset;

            Quaternion rotation = AlignCandidateRotation(seed.PlacementType, baseRotation, surfaceNormal);

            return new PlacementCandidate(
                position,
                rotation,
                seed.SurfaceCollider,
                surfaceNormal,
                seed.VoxelLayer,
                seed.PlacementType,
                hasSurfaceFit,
                surfaceFit,
                relationAnchorIdentity);
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
            if (UsesContextualFacing(context, asset, placementType))
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
                   !UsesContextualFacing(context, asset, placementType);
        }

        /// <summary>Determines whether a deterministic random base angle is required for this target.</summary>
        public static bool UsesRandomPlanarRotation(
            GenerationContext context,
            AssetDefinition asset,
            PlacementType placementType)
        {
            if (UsesContextualFacing(context, asset, placementType))
                return false;

            return placementType == PlacementType.Wall
                ? asset.RandomRollRotation
                : UsesRandomYaw(context, asset, placementType);
        }

        private static bool UsesRandomRotation(
            GenerationContext context,
            AssetDefinition asset,
            PlacementType placementType)
        {
            if (UsesContextualFacing(context, asset, placementType))
                return false;

            if (placementType == PlacementType.InsideSpace)
            {
                return asset.RandomYawRotation ||
                       asset.RandomPitchRotation ||
                       asset.RandomRollRotation;
            }

            if (placementType == PlacementType.Wall)
                return asset.RandomRollRotation;

            return UsesRandomYaw(context, asset, placementType);
        }

        private static Vector3 CreatePosition(
            CandidateSeed seed,
            GenerationContext context,
            AssetDefinition asset,
            Vector3 surfaceNormal,
            Quaternion rotation)
        {
            if (seed.PlacementType == PlacementType.InsideSpace)
                return seed.Position;

            if (seed.PlacementType != PlacementType.Wall)
                return seed.Position + surfaceNormal * (Mathf.Max(0.01f, asset.Height) * 0.5f);

            Vector3 position = seed.Position + surfaceNormal * (asset.Depth * 0.5f);
            float verticalHalfExtent = GetVerticalHalfExtent(asset, rotation);

            if (asset.WallVerticalPlacementMode == WallVerticalPlacementMode.FixedHeight)
            {
                position.y = context.TargetBounds.min.y +
                             Mathf.Max(0f, asset.PlacementHeight) +
                             verticalHalfExtent;
                return position;
            }

            if (asset.WallVerticalPlacementMode == WallVerticalPlacementMode.HeightRange)
            {
                float height = Mathf.Lerp(
                    asset.WallMinHeight,
                    asset.WallMaxHeight,
                    CreateStableWallHeightFactor(seed, context.RandomSeed));
                position.y = context.TargetBounds.min.y + height + verticalHalfExtent;
                return position;
            }

            float offset = verticalHalfExtent + asset.PlacementHeight;
            position.y += offset;
            return position;
        }

        private static float CreateStableWallHeightFactor(CandidateSeed seed, int randomSeed) =>
            CreateStableFactor(seed, randomSeed, 0u);

        private static float CreateStableFactor(CandidateSeed seed, int randomSeed, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u ^ salt;
                hash = (hash ^ (uint)randomSeed) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(seed.Position.x * 1000f)) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(seed.Position.y * 1000f)) * 16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(seed.Position.z * 1000f)) * 16777619u;
                return (hash & 0x00ffffffu) / 16777215f;
            }
        }

        private static Quaternion CreateBaseRotation(
            CandidateSeed seed,
            GenerationContext context,
            AssetDefinition asset,
            int rotationIndex,
            int rotationCount,
            float yawBase,
            out object relationAnchorIdentity)
        {
            relationAnchorIdentity = null;
            Vector3 relationQueryPosition = GetRelationQueryPosition(seed, asset);
            if (seed.PlacementType != PlacementType.Wall &&
                asset.AssetRelativePlacement.UsesFacing &&
                RelativeAnchorProvider.TryFindAssetAnchor(
                    context,
                    asset,
                    relationQueryPosition,
                    PlacementSupportRules.GetDescriptor(seed.SurfaceCollider),
                    out RelativeAnchor assetAnchor))
            {
                relationAnchorIdentity = assetAnchor.Identity;
                return CreateAssetRelativeRotationWithVariation(seed, context, asset, assetAnchor);
            }

            if (seed.PlacementType != PlacementType.Wall &&
                asset.PathPlacement.UsesFacing &&
                PathPlacementSource.TryFindNearest(
                    context,
                    asset.PathPlacement,
                    relationQueryPosition,
                    out PathPlacementFrame pathFrame))
            {
                return CreatePathRelativeRotationWithVariation(seed, context, asset, pathFrame);
            }

            if (asset.OrientationMode == OrientationMode.MatchSupportForward)
            {
                PlacementSurfaceDescriptor descriptor = PlacementSupportRules.GetDescriptor(seed.SurfaceCollider);

                if (PlacementSupportRules.TryGetSupportForward(seed, descriptor, out Vector3 supportForward))
                {
                    Vector3 normal = seed.SurfaceNormal.sqrMagnitude > 0.001f
                        ? seed.SurfaceNormal.normalized
                        : seed.PlacementType == PlacementType.Ceiling ? Vector3.down : Vector3.up;
                    return Quaternion.LookRotation(supportForward, normal);
                }
            }

            if (seed.PlacementType == PlacementType.Wall)
            {
                if (!UsesRandomPlanarRotation(context, asset, seed.PlacementType))
                    return seed.Rotation;

                float step = rotationCount > 1 ? 360f / rotationCount : 0f;
                float roll = Mathf.Repeat(yawBase + step * rotationIndex, 360f);
                return seed.Rotation * Quaternion.AngleAxis(roll, Vector3.forward);
            }

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

        private static Quaternion CreateAssetRelativeRotationWithVariation(
            CandidateSeed seed,
            GenerationContext context,
            AssetDefinition asset,
            RelativeAnchor anchor)
        {
            Quaternion relationRotation = CreateAssetRelativeRotation(
                seed,
                asset.AssetRelativePlacement.Facing,
                anchor);
            float maxDeviation = asset.AssetRelativePlacement.FacingVariationDegrees;
            if (maxDeviation <= 0f)
                return relationRotation;

            float deviation = Mathf.Lerp(
                -maxDeviation,
                maxDeviation,
                CreateStableFactor(seed, context.RandomSeed, 0x9e3779b9u));
            Vector3 axis = seed.PlacementType == PlacementType.Ceiling
                ? Vector3.down
                : Vector3.up;
            return Quaternion.AngleAxis(deviation, axis) * relationRotation;
        }

        private static Quaternion CreatePathRelativeRotationWithVariation(
            CandidateSeed seed,
            GenerationContext context,
            AssetDefinition asset,
            PathPlacementFrame frame)
        {
            Vector3 direction = asset.PathPlacement.Facing switch
            {
                PathPlacementFacing.AlongPath => frame.Forward,
                PathPlacementFacing.AgainstPath => -frame.Forward,
                PathPlacementFacing.TowardPath => frame.Center - seed.Position,
                PathPlacementFacing.AwayFromPath => seed.Position - frame.Center,
                _ => seed.Rotation * Vector3.forward
            };
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude <= 0.001f)
                return seed.Rotation;

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            float variation = asset.PathPlacement.FacingVariationDegrees;
            if (variation <= 0f)
                return rotation;

            float deviation = Mathf.Lerp(
                -variation,
                variation,
                CreateStableFactor(seed, context.RandomSeed, 0x85ebca6bu));
            return Quaternion.AngleAxis(deviation, Vector3.up) * rotation;
        }

        private static Vector3 GetRelationQueryPosition(CandidateSeed seed, AssetDefinition asset)
        {
            if (seed.PlacementType is PlacementType.InsideSpace or PlacementType.Wall)
                return seed.Position;

            Vector3 normal = seed.SurfaceNormal.sqrMagnitude > 0.001f
                ? seed.SurfaceNormal.normalized
                : seed.PlacementType == PlacementType.Ceiling ? Vector3.down : Vector3.up;
            return seed.Position + normal * (Mathf.Max(0.01f, asset.Height) * 0.5f);
        }

        private static Quaternion CreateAssetRelativeRotation(
            CandidateSeed seed,
            AssetRelativeFacing facing,
            RelativeAnchor anchor)
        {
            Vector3 direction = facing switch
            {
                AssetRelativeFacing.Toward => anchor.Position - seed.Position,
                AssetRelativeFacing.Away => seed.Position - anchor.Position,
                AssetRelativeFacing.MatchForward => anchor.Forward,
                _ => seed.Rotation * Vector3.forward
            };

            if (seed.PlacementType != PlacementType.InsideSpace)
                direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
                return seed.Rotation;

            Vector3 up = seed.PlacementType == PlacementType.Ceiling
                ? Vector3.down
                : Vector3.up;
            return Quaternion.LookRotation(direction.normalized, up);
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
                seed.PlacementType == PlacementType.InsideSpace)
            {
                return false;
            }

            bool isWall = seed.PlacementType == PlacementType.Wall;
            Quaternion fitRotation = isWall
                ? AlignCandidateRotation(seed.PlacementType, baseRotation, surfaceNormal)
                : baseRotation;
            Vector3 surfaceCenter = isWall
                ? position - surfaceNormal * (Mathf.Max(0.01f, asset.Depth) * 0.5f)
                : seed.Position;

            if (!context.SurfaceFitCache.TryEvaluate(
                    context.Area,
                    surfaceCenter,
                    fitRotation,
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
                ? GetUprightNormal(seed.PlacementType, fit.Normal, seed.SurfaceNormal)
                : fit.Normal;
            float normalOffset = isWall ? asset.Depth : asset.Height;
            position = fit.Position + surfaceNormal * (Mathf.Max(0.01f, normalOffset) * 0.5f);
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

        private static bool UsesContextualFacing(
            GenerationContext context,
            AssetDefinition asset,
            PlacementType placementType)
        {
            return (placementType != PlacementType.Wall &&
                    (asset.AssetRelativePlacement.UsesFacing || asset.PathPlacement.UsesFacing)) ||
                   FacesRelativeAnchor(context, asset) ||
                   asset.OrientationMode == OrientationMode.MatchSupportForward;
        }

        private static Quaternion AlignCandidateRotation(
            PlacementType placementType,
            Quaternion rotation,
            Vector3 surfaceNormal)
        {
            return placementType switch
            {
                PlacementType.InsideSpace => rotation,
                PlacementType.Wall => AlignToWall(rotation, surfaceNormal),
                _ => AlignToSurface(rotation, surfaceNormal)
            };
        }

        private static Quaternion AlignToWall(Quaternion rotation, Vector3 surfaceNormal)
        {
            Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f
                ? surfaceNormal.normalized
                : Vector3.forward;
            Vector3 up = Vector3.ProjectOnPlane(rotation * Vector3.up, normal);

            if (up.sqrMagnitude <= 0.001f)
                up = Vector3.ProjectOnPlane(Vector3.up, normal);

            if (up.sqrMagnitude <= 0.001f)
                up = Vector3.ProjectOnPlane(Vector3.right, normal);

            return Quaternion.LookRotation(normal, up.normalized);
        }

        private static float GetVerticalHalfExtent(AssetDefinition asset, Quaternion rotation)
        {
            Vector3 extents = AssetAttemptPlanner.Dimensions(asset) * 0.5f;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            return Mathf.Abs(right.y) * extents.x +
                   Mathf.Abs(up.y) * extents.y +
                   Mathf.Abs(forward.y) * extents.z;
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

        private static Vector3 GetUprightNormal(
            PlacementType placementType,
            Vector3 fittedNormal,
            Vector3 fallbackNormal)
        {
            if (placementType != PlacementType.Wall)
                return placementType == PlacementType.Ceiling ? Vector3.down : Vector3.up;

            Vector3 horizontalNormal = Vector3.ProjectOnPlane(fittedNormal, Vector3.up);

            if (horizontalNormal.sqrMagnitude <= 0.001f)
                horizontalNormal = Vector3.ProjectOnPlane(fallbackNormal, Vector3.up);

            return horizontalNormal.sqrMagnitude > 0.001f
                ? horizontalNormal.normalized
                : Vector3.forward;
        }
    }
}
