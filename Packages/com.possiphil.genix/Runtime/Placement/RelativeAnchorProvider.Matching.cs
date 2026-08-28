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

    }
}
