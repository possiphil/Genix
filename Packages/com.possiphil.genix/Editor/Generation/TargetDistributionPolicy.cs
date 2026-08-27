using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Editor.Generation
{
    /// <summary>Allocates requested counts and selects candidate pools for random, balanced, and weighted targets.</summary>
    internal static class TargetDistributionPolicy
    {
        public static bool IsActive(GenerationContext context)
        {
            return context.TargetDistributionMode != TargetDistributionMode.Random;
        }

        public static PlacementTarget GetUsableTargets(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets)
        {
            PlacementTarget result = PlacementTarget.None;
            AddUsableTarget(context, assets, PlacementTarget.Floor, PlacementType.Floor, ref result);
            AddUsableTarget(context, assets, PlacementTarget.Wall, PlacementType.Wall, ref result);
            AddUsableTarget(context, assets, PlacementTarget.Ceiling, PlacementType.Ceiling, ref result);
            AddUsableTarget(context, assets, PlacementTarget.InsideSpace, PlacementType.InsideSpace, ref result);
            return result;
        }

        public static PlacementTarget GetUsableTargetsForValidation(
            GenerationRequest request,
            IReadOnlyList<AssetDefinition> assets)
        {
            PlacementTarget result = PlacementTarget.None;
            AddUsableTarget(request, assets, PlacementTarget.Floor, PlacementType.Floor, ref result);
            AddUsableTarget(request, assets, PlacementTarget.Wall, PlacementType.Wall, ref result);
            AddUsableTarget(request, assets, PlacementTarget.Ceiling, PlacementType.Ceiling, ref result);
            AddUsableTarget(request, assets, PlacementTarget.InsideSpace, PlacementType.InsideSpace, ref result);
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
                !context.Area.SupportsPlacementType(placementType) ||
                !HasAssets(assets, placementType))
            {
                return;
            }

            result |= target;
        }

        private static void AddUsableTarget(
            GenerationRequest request,
            IReadOnlyList<AssetDefinition> assets,
            PlacementTarget target,
            PlacementType placementType,
            ref PlacementTarget result)
        {
            if ((request.PlacementTargets & target) == 0 ||
                request.TargetDistributionMode == TargetDistributionMode.Weighted &&
                request.TargetDistributionWeights.GetWeight(target) <= 0 ||
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

    /// <summary>Tracks exact and weighted accepted-placement budgets across semantic support tags.</summary>
    internal sealed class SupportDistributionState
    {
        private readonly IReadOnlyList<SupportDistributionRule> _rules;
        private readonly int[] _targets;
        private readonly int[] _placed;
        private int _activeGroup;

        public bool IsActive { get; }
        public int GroupCount => _targets.Length;
        public System.Predicate<CandidateSeed> ActiveSeedFilter { get; }

        private SupportDistributionState(GenerationContext context)
        {
            SupportDistributionSettings settings = context.SupportDistribution;
            IsActive = true;
            _rules = settings.Rules;
            _targets = new int[_rules.Count + 1];
            _placed = new int[_targets.Length];
            ActiveSeedFilter = MatchesActiveGroup;
            AllocateTargets(Mathf.Max(0, context.Count), settings.DefaultWeight);
        }

        public static SupportDistributionState Create(GenerationContext context) =>
            context?.SupportDistribution?.IsEnabled == true ? new SupportDistributionState(context) : null;

        public bool TrySelectUnderfilled(ISet<int> excluded, out int group)
        {
            group = -1;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < _targets.Length; i++)
            {
                if ((excluded?.Contains(i) ?? false) || _placed[i] >= _targets[i])
                    continue;

                float score = (_targets[i] - _placed[i]) / (float)Mathf.Max(1, _targets[i]);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                group = i;
            }

            return group >= 0;
        }

        public bool Matches(CandidateSeed seed, int group)
        {
            PlacementSurfaceDescriptor descriptor = PlacementSupportRules.GetDescriptor(seed.SurfaceCollider);
            return Matches(descriptor, group);
        }

        private bool Matches(PlacementSurfaceDescriptor descriptor, int group)
        {
            int matchedGroup = _rules.Count;

            if (descriptor)
            {
                for (int i = 0; i < _rules.Count; i++)
                {
                    SemanticTag tag = _rules[i].SupportTag;
                    if (tag && descriptor.SurfaceTags.Contains(tag))
                    {
                        matchedGroup = i;
                        break;
                    }
                }
            }

            return matchedGroup == group;
        }

        public void SelectGroup(int group) => _activeGroup = group;

        public void RecordPlacement(int group)
        {
            if (group >= 0 && group < _placed.Length)
                _placed[group]++;
        }

        public void RecordPlacement(PlacementCandidate candidate)
        {
            PlacementSurfaceDescriptor descriptor = PlacementSupportRules.GetDescriptor(candidate.SurfaceCollider);
            for (int group = 0; group < _placed.Length; group++)
            {
                if (!Matches(descriptor, group))
                    continue;

                _placed[group]++;
                return;
            }
        }

        public int[] CreateCheckpoint() => (int[])_placed.Clone();

        public void Restore(int[] checkpoint)
        {
            if (checkpoint == null || checkpoint.Length != _placed.Length)
                return;

            System.Array.Copy(checkpoint, _placed, _placed.Length);
        }

        public string GetLabel(int group) => group >= 0 && group < _rules.Count
            ? _rules[group].SupportTag.DisplayName
            : "Default / Other Surfaces";

        public int GetTarget(int group) => group >= 0 && group < _targets.Length ? _targets[group] : 0;
        public int GetPlaced(int group) => group >= 0 && group < _placed.Length ? _placed[group] : 0;

        public string FormatBudgets() => string.Join(", ", Enumerable.Range(0, GroupCount)
            .Select(group => $"{GetLabel(group)} {GetPlaced(group)}/{GetTarget(group)}"));

        private bool MatchesActiveGroup(CandidateSeed seed) => Matches(seed, _activeGroup);

        private void AllocateTargets(int count, int defaultWeight)
        {
            int remaining = count;

            for (int i = 0; i < _rules.Count && remaining > 0; i++)
            {
                SupportDistributionRule rule = _rules[i];
                if (rule.Mode != SupportDistributionRuleMode.ExactCount)
                    continue;

                int allocated = Mathf.Min(rule.Value, remaining);
                _targets[i] = allocated;
                remaining -= allocated;
            }

            if (remaining <= 0)
                return;

            List<(int Group, int Weight)> weights = new();
            for (int i = 0; i < _rules.Count; i++)
            {
                SupportDistributionRule rule = _rules[i];
                if (rule.Mode == SupportDistributionRuleMode.Weight && rule.Value > 0)
                    weights.Add((i, rule.Value));
            }

            if (defaultWeight > 0)
                weights.Add((_rules.Count, defaultWeight));

            int totalWeight = weights.Sum(entry => entry.Weight);
            if (totalWeight <= 0)
            {
                _targets[_rules.Count] += remaining;
                return;
            }

            int assigned = 0;
            List<(int Group, float Remainder)> remainders = new();
            foreach ((int group, int weight) in weights)
            {
                float exact = remaining * weight / (float)totalWeight;
                int whole = Mathf.FloorToInt(exact);
                _targets[group] += whole;
                assigned += whole;
                remainders.Add((group, exact - whole));
            }

            foreach ((int group, _) in remainders
                         .OrderByDescending(entry => entry.Remainder)
                         .ThenBy(entry => entry.Group)
                         .Take(remaining - assigned))
            {
                _targets[group]++;
            }
        }
    }
}
