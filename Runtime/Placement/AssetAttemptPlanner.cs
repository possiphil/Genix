using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>Builds deterministic, weighted asset-attempt orders for placement candidates.</summary>
    public static class AssetAttemptPlanner
    {
        /// <summary>Creates catalog.</summary>
        public static Catalog CreateCatalog(IReadOnlyList<AssetDefinition> assets) =>
            new(assets);

        /// <summary>Creates order.</summary>
        public static List<AssetDefinition> CreateOrder(
            IReadOnlyList<AssetDefinition> assets,
            PlacementType placementType,
            GenerationRandom random)
        {
            return CreateCatalog(assets).CreateOrder(placementType, random);
        }

        /// <summary>Removes remaining asset attempts that can no longer produce a valid placement.</summary>
        public static void PruneRemaining(
            List<AssetDefinition> remaining,
            int startIndex,
            RejectionReason rejection)
        {
            if (remaining == null || remaining.Count == 0)
                return;

            startIndex = Mathf.Clamp(startIndex, 0, remaining.Count);

            for (int i = remaining.Count - 1; i >= startIndex; i--)
            {
                if (ShouldPrune(remaining[i], rejection))
                    remaining.RemoveAt(i);
            }
        }

        private static bool ShouldPrune(
            AssetDefinition asset,
            RejectionReason rejection)
        {
            if (!asset || !asset.Prefab)
                return true;

            return rejection == RejectionReason.TooCloseToGenerated;
        }

        /// <summary>Returns the number of voxel cells along each axis.</summary>
        public static Vector3 Dimensions(AssetDefinition asset)
        {
            return new Vector3(
                Mathf.Max(0.01f, asset.Width),
                Mathf.Max(0.01f, asset.Height),
                Mathf.Max(0.01f, asset.Depth));
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

        /// <summary>Indexes eligible asset definitions by placement type for repeated planning queries.</summary>
        public sealed class Catalog
        {
            private readonly Dictionary<PlacementType, List<Entry>> _entriesByType = new();

            internal Catalog(IReadOnlyList<AssetDefinition> assets)
            {
                if (assets == null)
                    return;

                foreach (AssetDefinition asset in assets)
                {
                    if (!asset || !asset.Prefab)
                        continue;

                    Vector3 dimensions = Dimensions(asset);

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

            /// <summary>Creates order.</summary>
            public List<AssetDefinition> CreateOrder(
                PlacementType placementType,
                GenerationRandom random)
            {
                List<AssetDefinition> order = new();
                CreateOrder(placementType, random, order);
                return order;
            }

            /// <summary>Creates order.</summary>
            public void CreateOrder(
                PlacementType placementType,
                GenerationRandom random,
                List<AssetDefinition> order,
                Func<AssetDefinition, bool> isAvailable = null)
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
                AddIfAvailable(order, entries[startIndex].Asset, isAvailable);
                int smallerStart = order.Count;

                for (int i = startIndex + 1; i < entries.Count; i++)
                    AddIfAvailable(order, entries[i].Asset, isAvailable);

                ShuffleRange(order, smallerStart, order.Count, random);
                int largerStart = order.Count;

                for (int i = 0; i < startIndex; i++)
                    AddIfAvailable(order, entries[i].Asset, isAvailable);

                ShuffleRange(order, largerStart, order.Count, random);
            }

            private static void AddIfAvailable(
                ICollection<AssetDefinition> order,
                AssetDefinition asset,
                Func<AssetDefinition, bool> isAvailable)
            {
                if (isAvailable == null || isAvailable(asset))
                    order.Add(asset);
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
