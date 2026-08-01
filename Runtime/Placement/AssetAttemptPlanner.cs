using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Placement
{
    public static class AssetAttemptPlanner
    {
        private const float DimensionEpsilon = 0.001f;

        public static Catalog CreateCatalog(IReadOnlyList<AssetDefinition> assets) =>
            new(assets);

        public static List<AssetDefinition> CreateOrder(
            IReadOnlyList<AssetDefinition> assets,
            PlacementType placementType,
            GenerationRandom random)
        {
            return CreateCatalog(assets).CreateOrder(placementType, random);
        }

        public static void PruneDominated(
            List<AssetDefinition> remaining,
            PlacementType placementType,
            AssetDefinition failedAsset,
            RejectionReason rejection)
        {
            PruneDominated(remaining, 0, placementType, failedAsset, rejection);
        }

        public static void PruneDominated(
            List<AssetDefinition> remaining,
            int startIndex,
            PlacementType placementType,
            AssetDefinition failedAsset,
            RejectionReason rejection)
        {
            if (remaining.Count == 0 || !failedAsset)
                return;

            Vector3 failedSize = Dimensions(failedAsset);
            startIndex = Mathf.Clamp(startIndex, 0, remaining.Count);

            for (int i = remaining.Count - 1; i >= startIndex; i--)
            {
                if (ShouldPrune(remaining[i], placementType, failedAsset, failedSize, rejection))
                    remaining.RemoveAt(i);
            }
        }

        private static bool ShouldPrune(
            AssetDefinition asset,
            PlacementType placementType,
            AssetDefinition failedAsset,
            Vector3 failedSize,
            RejectionReason rejection)
        {
            if (!asset || !asset.Prefab)
                return true;

            if (ShouldPreserveAdaptiveCandidate(asset, placementType, failedAsset, rejection))
                return false;

            Vector3 size = Dimensions(asset);

            return rejection switch
            {
                RejectionReason.TooCloseToGenerated => true,
                RejectionReason.ExceedsTargetHeight => size.y >= failedSize.y - DimensionEpsilon,
                RejectionReason.OutsideTargetArea => DominatesFootprint(placementType, size, failedSize),
                RejectionReason.OutsideTargetVolume => DominatesVolume(size, failedSize),
                RejectionReason.OverlapsGenerated => DominatesVolume(size, failedSize),
                RejectionReason.OverlapsFixed => DominatesVolume(size, failedSize),
                RejectionReason.TooCloseToFixed => DominatesVolume(size, failedSize),
                _ => false
            };
        }

        private static bool ShouldPreserveAdaptiveCandidate(
            AssetDefinition asset,
            PlacementType placementType,
            AssetDefinition failedAsset,
            RejectionReason rejection)
        {
            return rejection == RejectionReason.OutsideTargetArea &&
                   placementType is PlacementType.Floor or PlacementType.Ceiling &&
                   failedAsset &&
                   failedAsset.SurfaceFitMode == SurfaceFitMode.Strict &&
                   asset.SurfaceFitMode == SurfaceFitMode.Adaptive;
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

        public sealed class Catalog
        {
            private readonly Dictionary<PlacementType, List<Entry>> _entriesByType = new();
            private readonly Dictionary<AssetDefinition, Vector3> _dimensionsByAsset = new();

            internal Catalog(IReadOnlyList<AssetDefinition> assets)
            {
                if (assets == null)
                    return;

                foreach (AssetDefinition asset in assets)
                {
                    if (!asset || !asset.Prefab)
                        continue;

                    Vector3 dimensions = Dimensions(asset);
                    _dimensionsByAsset[asset] = dimensions;

                    if (!_entriesByType.TryGetValue(asset.PlacementType, out List<Entry> entries))
                    {
                        entries = new List<Entry>();
                        _entriesByType[asset.PlacementType] = entries;
                    }

                    entries.Add(new Entry(asset, dimensions));
                }

                foreach (KeyValuePair<PlacementType, List<Entry>> entry in _entriesByType)
                    SortEntries(entry.Key, entry.Value);
            }

            public List<AssetDefinition> CreateOrder(
                PlacementType placementType,
                GenerationRandom random)
            {
                List<AssetDefinition> order = new();
                CreateOrder(placementType, random, order);
                return order;
            }

            public void CreateOrder(
                PlacementType placementType,
                GenerationRandom random,
                List<AssetDefinition> order)
            {
                if (order == null)
                    throw new ArgumentNullException(nameof(order));

                if (!_entriesByType.TryGetValue(placementType, out List<Entry> entries) ||
                    entries.Count == 0)
                {
                    order.Clear();
                    return;
                }

                order.Clear();

                int startIndex = random.Range(0, entries.Count);
                order.Add(entries[startIndex].Asset);
                int smallerStart = order.Count;

                for (int i = startIndex + 1; i < entries.Count; i++)
                    order.Add(entries[i].Asset);

                ShuffleRange(order, smallerStart, order.Count, random);
                int largerStart = order.Count;

                for (int i = 0; i < startIndex; i++)
                    order.Add(entries[i].Asset);

                ShuffleRange(order, largerStart, order.Count, random);
            }

            public void PruneDominated(
                List<AssetDefinition> remaining,
                PlacementType placementType,
                AssetDefinition failedAsset,
                RejectionReason rejection)
            {
                PruneDominated(remaining, 0, placementType, failedAsset, rejection);
            }

            public void PruneDominated(
                List<AssetDefinition> remaining,
                int startIndex,
                PlacementType placementType,
                AssetDefinition failedAsset,
                RejectionReason rejection)
            {
                if (remaining.Count == 0 || !failedAsset)
                    return;

                Vector3 failedSize = GetDimensions(failedAsset);
                startIndex = Mathf.Clamp(startIndex, 0, remaining.Count);

                for (int i = remaining.Count - 1; i >= startIndex; i--)
                {
                    if (ShouldPrune(
                            remaining[i],
                            placementType,
                            failedAsset,
                            failedSize,
                            rejection,
                            this))
                    {
                        remaining.RemoveAt(i);
                    }
                }
            }

            internal Vector3 GetDimensions(AssetDefinition asset)
            {
                if (asset && _dimensionsByAsset.TryGetValue(asset, out Vector3 dimensions))
                    return dimensions;

                return Dimensions(asset);
            }

            private static void ShuffleRange(
                IList<AssetDefinition> order,
                int startIndex,
                int endIndex,
                GenerationRandom random)
            {
                for (int i = endIndex - 1; i > startIndex; i--)
                {
                    int randomIndex = random.Range(startIndex, i + 1);
                    (order[i], order[randomIndex]) = (order[randomIndex], order[i]);
                }
            }

            private static void SortEntries(PlacementType placementType, List<Entry> entries)
            {
                entries.Sort((left, right) =>
                {
                    int result = FootprintArea(placementType, right.Dimensions)
                        .CompareTo(FootprintArea(placementType, left.Dimensions));

                    if (result != 0)
                        return result;

                    result = MaxFootprintDimension(placementType, right.Dimensions)
                        .CompareTo(MaxFootprintDimension(placementType, left.Dimensions));

                    if (result != 0)
                        return result;

                    result = MinFootprintDimension(placementType, right.Dimensions)
                        .CompareTo(MinFootprintDimension(placementType, left.Dimensions));

                    if (result != 0)
                        return result;

                    result = right.Asset.Height.CompareTo(left.Asset.Height);

                    return result != 0
                        ? result
                        : string.Compare(left.Asset.AssetName, right.Asset.AssetName, StringComparison.OrdinalIgnoreCase);
                });
            }
        }

        private static bool ShouldPrune(
            AssetDefinition asset,
            PlacementType placementType,
            AssetDefinition failedAsset,
            Vector3 failedSize,
            RejectionReason rejection,
            Catalog catalog)
        {
            if (!asset || !asset.Prefab)
                return true;

            if (ShouldPreserveAdaptiveCandidate(asset, placementType, failedAsset, rejection))
                return false;

            Vector3 size = catalog.GetDimensions(asset);

            return rejection switch
            {
                RejectionReason.TooCloseToGenerated => true,
                RejectionReason.ExceedsTargetHeight => size.y >= failedSize.y - DimensionEpsilon,
                RejectionReason.OutsideTargetArea => DominatesFootprint(placementType, size, failedSize),
                RejectionReason.OutsideTargetVolume => DominatesVolume(size, failedSize),
                RejectionReason.OverlapsGenerated => DominatesVolume(size, failedSize),
                RejectionReason.OverlapsFixed => DominatesVolume(size, failedSize),
                RejectionReason.TooCloseToFixed => DominatesVolume(size, failedSize),
                _ => false
            };
        }

        private readonly struct Entry
        {
            public AssetDefinition Asset { get; }
            public Vector3 Dimensions { get; }

            public Entry(AssetDefinition asset, Vector3 dimensions)
            {
                Asset = asset;
                Dimensions = dimensions;
            }
        }
    }
}
