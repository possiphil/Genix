using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>Describes one explicit physical support surface that warrants a reserved candidate share.</summary>
    internal readonly struct SupportSurfaceSamplingEntry
    {
        public PlacementSurfaceDescriptor Descriptor { get; }
        public Collider RepresentativeCollider { get; }
        public Bounds Bounds { get; }

        public SupportSurfaceSamplingEntry(
            PlacementSurfaceDescriptor descriptor,
            Collider representativeCollider,
            Bounds bounds)
        {
            Descriptor = descriptor;
            RepresentativeCollider = representativeCollider;
            Bounds = bounds;
        }
    }

    /// <summary>Finds small semantic support surfaces that would otherwise be under-sampled by area-weighted projection.</summary>
    internal static class SupportSurfaceSampling
    {
        private const float MinimumHorizontalArea = 0.0001f;

        public static List<SupportSurfaceSamplingEntry> Collect(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementType placementType)
        {
            List<SupportSurfaceSamplingEntry> entries = new();

            if (context == null || assets == null || assets.Count == 0)
                return entries;

            foreach (PlacementSurfaceDescriptor descriptor in
                     Object.FindObjectsByType<PlacementSurfaceDescriptor>())
            {
                if (!descriptor ||
                    !descriptor.isActiveAndEnabled ||
                    !TryGetColliderBounds(descriptor, context.TargetBounds, out Collider representative, out Bounds bounds) ||
                    bounds.size.x * bounds.size.z < MinimumHorizontalArea ||
                    !SupportsAnyConstrainedAsset(descriptor, representative, placementType, assets))
                {
                    continue;
                }

                entries.Add(new SupportSurfaceSamplingEntry(descriptor, representative, bounds));
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        public static string CreateCacheKey(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementType placementType)
        {
            List<SupportSurfaceSamplingEntry> entries = Collect(context, assets, placementType);
            string assetRules = CreateAssetRulesKey(assets, placementType);

            if (entries.Count == 0)
                return $"support:none:{assetRules}";

            string surfaces = string.Join(";", entries.Select(entry =>
            {
                Bounds bounds = entry.Bounds;
                string tags = string.Join(",", entry.Descriptor.SurfaceTags
                    .Where(tag => tag)
                    .Select(tag => tag.DisplayName)
                    .OrderBy(name => name));
                return $"{GetHierarchyPath(entry.Descriptor.transform)}:" +
                       $"{VectorKey(bounds.center)}:{VectorKey(bounds.size)}:{tags}";
            }));
            return $"support:{assetRules}:{surfaces}";
        }

        private static string CreateAssetRulesKey(
            IReadOnlyList<AssetDefinition> assets,
            PlacementType placementType)
        {
            if (assets == null)
                return "assets:none";

            return string.Join(";", assets
                .Where(asset => asset && asset.Prefab && asset.PlacementType == placementType)
                .OrderBy(asset => asset.AssetName)
                .Select(asset =>
                {
                    string required = string.Join(",", asset.RequiredSupportTags
                        .Where(tag => tag && tag.Category)
                        .Select(tag => $"{tag.Category.DisplayName}/{tag.DisplayName}")
                        .OrderBy(name => name));
                    string forbidden = string.Join(",", asset.ForbiddenSupportTags
                        .Where(tag => tag && tag.Category)
                        .Select(tag => $"{tag.Category.DisplayName}/{tag.DisplayName}")
                        .OrderBy(name => name));
                    return $"{asset.AssetName}[{required}][{forbidden}]";
                }));
        }

        private static bool SupportsAnyConstrainedAsset(
            PlacementSurfaceDescriptor descriptor,
            Collider representative,
            PlacementType placementType,
            IReadOnlyList<AssetDefinition> assets)
        {
            CandidateSeed seed = new(
                representative.bounds.center,
                Quaternion.identity,
                representative,
                placementType == PlacementType.Ceiling ? Vector3.down : Vector3.up,
                placementType: placementType);

            foreach (AssetDefinition asset in assets)
            {
                if (!asset ||
                    !asset.Prefab ||
                    asset.PlacementType != placementType ||
                    !HasSpecificSupportRequirement(asset, descriptor))
                {
                    continue;
                }

                if (PlacementSupportRules.TryValidateCompatibility(seed, asset, out _, out _))
                    return true;
            }

            return false;
        }

        private static bool HasSpecificSupportRequirement(
            AssetDefinition asset,
            PlacementSurfaceDescriptor descriptor)
        {
            if (descriptor.AllowedAssetTags.Count > 0)
                return true;

            foreach (SemanticTag tag in asset.RequiredSupportTags)
            {
                if (tag && tag.SupportsSurfaces)
                    return true;
            }

            return false;
        }

        internal static bool TryGetColliderBounds(
            PlacementSurfaceDescriptor descriptor,
            Bounds targetBounds,
            out Collider representative,
            out Bounds combinedBounds)
        {
            representative = null;
            combinedBounds = default;
            bool found = false;

            foreach (Collider collider in descriptor.GetComponentsInChildren<Collider>())
            {
                if (!collider ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy ||
                    !collider.bounds.Intersects(targetBounds))
                {
                    continue;
                }

                if (!found)
                {
                    representative = collider;
                    combinedBounds = collider.bounds;
                    found = true;
                    continue;
                }

                combinedBounds.Encapsulate(collider.bounds);
            }

            return found;
        }

        private static int CompareEntries(
            SupportSurfaceSamplingEntry left,
            SupportSurfaceSamplingEntry right)
        {
            return string.CompareOrdinal(
                GetHierarchyPath(left.Descriptor.transform),
                GetHierarchyPath(right.Descriptor.transform));
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (!transform)
                return string.Empty;

            string path = $"{transform.GetSiblingIndex()}:{transform.name}";

            while (transform.parent)
            {
                transform = transform.parent;
                path = $"{transform.GetSiblingIndex()}:{transform.name}/{path}";
            }

            return path;
        }

        private static string VectorKey(Vector3 value) =>
            $"{FloatKey(value.x)},{FloatKey(value.y)},{FloatKey(value.z)}";

        private static int FloatKey(float value) => Mathf.RoundToInt(value * 10_000f);
    }
}
