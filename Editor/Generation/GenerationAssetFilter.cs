using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Extensions;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Editor.Generation
{
    internal static class GenerationAssetFilter
    {
        public static bool TryResolve(
            GenerationRequest request,
            AssetCatalog catalog,
            out List<AssetDefinition> assets,
            out string error)
        {
            List<AssetDefinition> poolAssets = request.AssetPool
                .ResolveAssets(catalog)
                .Where(asset => asset)
                .Distinct()
                .ToList();

            if (poolAssets.Count == 0)
            {
                assets = new List<AssetDefinition>();
                error = request.AssetPool.IsDynamic
                    ? $"Dynamic asset pool '{request.AssetPool.name}' does not match any assets. Check its filters and the asset catalog."
                    : $"Static asset pool '{request.AssetPool.name}' is empty. Add at least one asset before generating.";
                return false;
            }

            PlacementTarget requestedTargets = GetRequestedTargets(request);
            assets = poolAssets
                .Where(asset => asset.Prefab && MatchesTarget(asset, requestedTargets))
                .Where(asset => MatchesSemanticTags(request.AreaSource, asset))
                .ToList();

            if (assets.Count > 0)
            {
                error = string.Empty;
                return true;
            }

            error = BuildRejectionMessage(request, poolAssets, requestedTargets);
            return false;
        }

        public static IEnumerable<string> GetUnavailableTargetWarnings(
            GenerationRequest request,
            IReadOnlyList<AssetDefinition> assets)
        {
            if (request.GenerationMode != GenerationMode.TargetPlacement)
                yield break;

            foreach ((PlacementTarget target, PlacementType type) in TargetMappings)
            {
                if ((request.PlacementTargets & target) == 0 ||
                    request.TargetDistributionMode == TargetDistributionMode.Weighted &&
                    request.TargetDistributionWeights.GetWeight(target) <= 0 ||
                    TargetDistributionPolicy.HasAssets(assets, type))
                {
                    continue;
                }

                yield return
                    $"{target.ToDisplayName()} is selected, but asset pool '{request.AssetPool.name}' has no usable " +
                    $"{type.ToDisplayName()} assets after prefab and semantic tag filtering. This target will be skipped.";
            }
        }

        private static string BuildRejectionMessage(
            GenerationRequest request,
            IReadOnlyCollection<AssetDefinition> poolAssets,
            PlacementTarget requestedTargets)
        {
            int missingPrefabs = poolAssets.Count(asset => asset && !asset.Prefab);
            int wrongTarget = poolAssets.Count(asset =>
                asset && asset.Prefab && !MatchesTarget(asset, requestedTargets));
            int semanticMismatch = poolAssets.Count(asset =>
                asset &&
                asset.Prefab &&
                MatchesTarget(asset, requestedTargets) &&
                !MatchesSemanticTags(request.AreaSource, asset));
            List<string> reasons = new();

            if (wrongTarget > 0)
                reasons.Add($"{wrongTarget} use a different placement target");

            if (missingPrefabs > 0)
                reasons.Add($"{missingPrefabs} have no prefab reference");

            if (semanticMismatch > 0)
                reasons.Add($"{semanticMismatch} do not match the location's semantic tags");

            if (semanticMismatch == poolAssets.Count)
            {
                return
                    $"No assets in pool '{request.AssetPool.name}' match the selected location's semantic tags. " +
                    $"Location tags: {GetTagSummary(request.AreaSource)}. Add a matching tag to an asset or set its category to Any.";
            }

            string reasonText = reasons.Count > 0 ? string.Join(", ", reasons) : "no asset passed all filters";
            return
                $"Asset pool '{request.AssetPool.name}' contains {poolAssets.Count} assets, but none can be used. " +
                $"Rejected assets: {reasonText}. Selected targets: {requestedTargets.ToDisplayName()}. " +
                $"Location tags: {GetTagSummary(request.AreaSource)}.";
        }

        private static PlacementTarget GetRequestedTargets(GenerationRequest request)
        {
            if (request.GenerationMode == GenerationMode.TargetPlacement)
                return request.PlacementTargets & PlacementTarget.All;

            throw new ArgumentOutOfRangeException(nameof(request.GenerationMode));
        }

        private static bool MatchesTarget(AssetDefinition asset, PlacementTarget targets)
        {
            PlacementTarget assetTarget = asset.PlacementType switch
            {
                PlacementType.Floor => PlacementTarget.Floor,
                PlacementType.Wall => PlacementTarget.Wall,
                PlacementType.Ceiling => PlacementTarget.Ceiling,
                PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                _ => PlacementTarget.None
            };
            return (targets & assetTarget) != 0;
        }

        private static bool MatchesSemanticTags(IAreaSource areaSource, AssetDefinition asset)
        {
            return SemanticTagMatcher.MatchesAssetRequirements(
                asset,
                areaSource?.SemanticTags,
                areaSource?.AnyTagCategories);
        }

        private static string GetTagSummary(IAreaSource areaSource)
        {
            if (areaSource == null)
                return "None";

            List<string> labels = areaSource.SemanticTags
                .Where(tag => tag && tag.Category)
                .GroupBy(tag => tag.Category)
                .Select(group => $"{group.Key.DisplayName}: {string.Join(", ", group.Select(tag => tag.DisplayName).Distinct())}")
                .ToList();
            labels.AddRange(areaSource.AnyTagCategories
                .Where(category => category)
                .Select(category => $"{category.DisplayName}: Any"));
            return labels.Count > 0 ? string.Join("; ", labels) : "None";
        }

        private static readonly (PlacementTarget Target, PlacementType Type)[] TargetMappings =
        {
            (PlacementTarget.Floor, PlacementType.Floor),
            (PlacementTarget.Wall, PlacementType.Wall),
            (PlacementTarget.Ceiling, PlacementType.Ceiling),
            (PlacementTarget.InsideSpace, PlacementType.InsideSpace)
        };
    }
}
