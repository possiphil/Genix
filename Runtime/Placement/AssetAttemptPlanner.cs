using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Placement
{
    public static class AssetAttemptPlanner
    {
        private const float DimensionEpsilon = 0.001f;

        public static List<AssetDefinition> CreateOrder(
            IReadOnlyList<AssetDefinition> assets,
            PlacementType placementType,
            GenerationRandom random)
        {
            List<AssetDefinition> sorted = assets
                .Where(asset => asset && asset.Prefab && asset.PlacementType == placementType)
                .OrderByDescending(asset => FootprintArea(placementType, Dimensions(asset)))
                .ThenByDescending(asset => MaxFootprintDimension(placementType, Dimensions(asset)))
                .ThenByDescending(asset => MinFootprintDimension(placementType, Dimensions(asset)))
                .ThenByDescending(asset => asset.Height)
                .ThenBy(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sorted.Count <= 1)
                return sorted;

            int startIndex = random.Range(0, sorted.Count);
            List<AssetDefinition> order = new() { sorted[startIndex] };
            List<AssetDefinition> smaller = sorted.Skip(startIndex + 1).ToList();
            random.Shuffle(smaller);
            order.AddRange(smaller);
            return order;
        }

        public static void PruneDominated(
            List<AssetDefinition> remaining,
            PlacementType placementType,
            AssetDefinition failedAsset,
            RejectionReason rejection)
        {
            if (remaining.Count == 0 || !failedAsset)
                return;

            Vector3 failedSize = Dimensions(failedAsset);

            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                if (ShouldPrune(remaining[i], placementType, failedSize, rejection))
                    remaining.RemoveAt(i);
            }
        }

        private static bool ShouldPrune(
            AssetDefinition asset,
            PlacementType placementType,
            Vector3 failedSize,
            RejectionReason rejection)
        {
            if (!asset || !asset.Prefab)
                return true;

            Vector3 size = Dimensions(asset);

            return rejection switch
            {
                RejectionReason.ExceedsTargetHeight => size.y >= failedSize.y - DimensionEpsilon,
                RejectionReason.OutsideTargetArea => DominatesFootprint(placementType, size, failedSize),
                RejectionReason.OutsideTargetVolume => DominatesVolume(size, failedSize),
                _ => false
            };
        }

        public static Vector3 Dimensions(AssetDefinition asset)
        {
            return new Vector3(
                Mathf.Max(0.01f, asset.Width),
                Mathf.Max(0.01f, asset.Height),
                Mathf.Max(0.01f, asset.Depth));
        }

        private static bool DominatesFootprint(
            PlacementType placementType,
            Vector3 size,
            Vector3 failedSize)
        {
            return placementType switch
            {
                PlacementType.Wall => size.x >= failedSize.x - DimensionEpsilon &&
                                      size.y >= failedSize.y - DimensionEpsilon,
                PlacementType.InsideSpace => DominatesVolume(size, failedSize),
                _ => size.x >= failedSize.x - DimensionEpsilon &&
                     size.z >= failedSize.z - DimensionEpsilon
            };
        }

        private static bool DominatesVolume(Vector3 size, Vector3 failedSize)
        {
            return size.x >= failedSize.x - DimensionEpsilon &&
                   size.y >= failedSize.y - DimensionEpsilon &&
                   size.z >= failedSize.z - DimensionEpsilon;
        }

        private static float FootprintArea(PlacementType placementType, Vector3 size) =>
            placementType switch
            {
                PlacementType.Wall => size.x * size.y,
                PlacementType.InsideSpace => size.x * size.y * size.z,
                _ => size.x * size.z
            };

        private static float MaxFootprintDimension(PlacementType placementType, Vector3 size) =>
            placementType switch
            {
                PlacementType.Wall => Mathf.Max(size.x, size.y),
                PlacementType.InsideSpace => Mathf.Max(size.x, Mathf.Max(size.y, size.z)),
                _ => Mathf.Max(size.x, size.z)
            };

        private static float MinFootprintDimension(PlacementType placementType, Vector3 size) =>
            placementType switch
            {
                PlacementType.Wall => Mathf.Min(size.x, size.y),
                PlacementType.InsideSpace => Mathf.Min(size.x, Mathf.Min(size.y, size.z)),
                _ => Mathf.Min(size.x, size.z)
            };
    }
}
