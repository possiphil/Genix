using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using UnityEngine;

namespace Genix.Editor.Generation
{
    internal static class TargetDistributionPolicy
    {
        public static bool IsActive(GenerationContext context)
        {
            return context.GenerationMode == GenerationMode.TargetPlacement &&
                   context.TargetDistributionMode != TargetDistributionMode.Random;
        }

        public static PlacementTarget GetUsableTargets(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets)
        {
            if (context.GenerationMode != GenerationMode.TargetPlacement)
                return PlacementTarget.Floor;

            PlacementTarget result = PlacementTarget.None;
            AddUsableTarget(context, assets, PlacementTarget.Floor, PlacementType.Floor, ref result);
            AddUsableTarget(context, assets, PlacementTarget.Wall, PlacementType.Wall, ref result);
            AddUsableTarget(context, assets, PlacementTarget.Ceiling, PlacementType.Ceiling, ref result);
            AddUsableTarget(context, assets, PlacementTarget.InsideSpace, PlacementType.InsideSpace, ref result);
            return result;
        }

        public static List<PlacementType> GetPlacementTypes(
            GenerationContext context,
            PlacementTarget usableTargets)
        {
            List<PlacementType> result = new();
            AddPlacementType(context, usableTargets, PlacementTarget.Floor, PlacementType.Floor, result);
            AddPlacementType(context, usableTargets, PlacementTarget.Wall, PlacementType.Wall, result);
            AddPlacementType(context, usableTargets, PlacementTarget.Ceiling, PlacementType.Ceiling, result);
            AddPlacementType(context, usableTargets, PlacementTarget.InsideSpace, PlacementType.InsideSpace, result);
            return result;
        }

        public static Dictionary<PlacementType, int> CreateTargets(
            GenerationContext context,
            IReadOnlyList<PlacementType> placementTypes)
        {
            Dictionary<PlacementType, int> targets = placementTypes
                .ToDictionary(type => type, _ => 0);

            if (placementTypes.Count == 0 || context.Count <= 0)
                return targets;

            List<WeightedTarget> weights = placementTypes
                .Select(type => new WeightedTarget(type, GetWeight(context, type)))
                .Where(weight => weight.Weight > 0)
                .ToList();
            int totalWeight = weights.Sum(weight => weight.Weight);

            if (totalWeight <= 0)
                return targets;

            List<TargetRemainder> remainders = new();
            int assigned = 0;

            foreach (WeightedTarget weight in weights)
            {
                float exact = context.Count * (weight.Weight / (float)totalWeight);
                int whole = Mathf.FloorToInt(exact);
                targets[weight.Type] = whole;
                assigned += whole;
                remainders.Add(new TargetRemainder(weight.Type, exact - whole, context.Random.Value));
            }

            foreach (TargetRemainder remainder in remainders
                         .OrderByDescending(value => value.Fraction)
                         .ThenByDescending(value => value.RandomTieBreaker)
                         .Take(Mathf.Max(0, context.Count - assigned)))
            {
                targets[remainder.Type]++;
            }

            return targets;
        }

        public static bool TrySelectTarget(
            GenerationContext context,
            IReadOnlyDictionary<PlacementType, int> targets,
            IReadOnlyDictionary<PlacementType, int> placed,
            IReadOnlyDictionary<PlacementType, CandidatePool> pools,
            ISet<PlacementType> exhausted,
            out PlacementType selected)
        {
            List<TargetOption> options = new();

            foreach (KeyValuePair<PlacementType, int> target in targets)
            {
                if (exhausted.Contains(target.Key) ||
                    !pools.TryGetValue(target.Key, out CandidatePool pool) ||
                    pool.Count <= 0)
                {
                    continue;
                }

                int current = placed.TryGetValue(target.Key, out int count) ? count : 0;
                int remaining = Mathf.Max(0, target.Value - current);

                if (remaining > 0)
                    options.Add(new TargetOption(target.Key, remaining));
            }

            if (options.Count == 0)
            {
                selected = default;
                return false;
            }

            int value = context.Random.Range(0, options.Sum(option => option.Remaining));

            foreach (TargetOption option in options)
            {
                value -= option.Remaining;

                if (value < 0)
                {
                    selected = option.Type;
                    return true;
                }
            }

            selected = options[^1].Type;
            return true;
        }

        public static List<PlacementType> GetOverflowTypes(
            IEnumerable<PlacementType> placementTypes,
            IReadOnlyDictionary<PlacementType, CandidatePool> pools,
            ISet<PlacementType> exhausted,
            GenerationContext context)
        {
            List<PlacementType> result = placementTypes
                .Where(type =>
                    !exhausted.Contains(type) &&
                    pools.TryGetValue(type, out CandidatePool pool) &&
                    pool.Count > 0)
                .ToList();
            context.Random.Shuffle(result);
            return result;
        }

        public static bool HasAssets(
            IReadOnlyList<AssetDefinition> assets,
            PlacementType placementType)
        {
            return assets.Any(asset => asset && asset.Prefab && asset.PlacementType == placementType);
        }

        public static string FormatTargets(
            IReadOnlyDictionary<PlacementType, int> targets,
            IReadOnlyDictionary<PlacementType, int> placed)
        {
            return string.Join(", ", targets.Select(target =>
            {
                int count = placed.TryGetValue(target.Key, out int current) ? current : 0;
                return $"{target.Key} {count}/{target.Value}";
            }));
        }

        private static void AddUsableTarget(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            PlacementTarget target,
            PlacementType placementType,
            ref PlacementTarget result)
        {
            if ((context.PlacementTargets & target) == 0 ||
                context.TargetDistributionMode == TargetDistributionMode.Weighted &&
                context.TargetDistributionWeights.GetWeight(target) <= 0 ||
                !HasAssets(assets, placementType))
            {
                return;
            }

            result |= target;
        }

        private static void AddPlacementType(
            GenerationContext context,
            PlacementTarget usableTargets,
            PlacementTarget target,
            PlacementType placementType,
            ICollection<PlacementType> result)
        {
            if ((usableTargets & target) == 0)
                return;

            if (context.TargetDistributionMode == TargetDistributionMode.Weighted &&
                context.TargetDistributionWeights.GetWeight(target) <= 0)
            {
                return;
            }

            result.Add(placementType);
        }

        private static int GetWeight(GenerationContext context, PlacementType type)
        {
            if (context.TargetDistributionMode == TargetDistributionMode.Balanced)
                return 1;

            PlacementTarget target = type switch
            {
                PlacementType.Floor => PlacementTarget.Floor,
                PlacementType.Wall => PlacementTarget.Wall,
                PlacementType.Ceiling => PlacementTarget.Ceiling,
                PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                _ => PlacementTarget.None
            };
            return context.TargetDistributionWeights.GetWeight(target);
        }

        private readonly struct WeightedTarget
        {
            public PlacementType Type { get; }
            public int Weight { get; }

            public WeightedTarget(PlacementType type, int weight)
            {
                Type = type;
                Weight = weight;
            }
        }

        private readonly struct TargetRemainder
        {
            public PlacementType Type { get; }
            public float Fraction { get; }
            public float RandomTieBreaker { get; }

            public TargetRemainder(PlacementType type, float fraction, float randomTieBreaker)
            {
                Type = type;
                Fraction = fraction;
                RandomTieBreaker = randomTieBreaker;
            }
        }

        private readonly struct TargetOption
        {
            public PlacementType Type { get; }
            public int Remaining { get; }

            public TargetOption(PlacementType type, int remaining)
            {
                Type = type;
                Remaining = remaining;
            }
        }
    }
}
