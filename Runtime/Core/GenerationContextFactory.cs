using System;
using System.Collections.Generic;
using System.Diagnostics;
using Genix.Areas;
using Genix.Assets;
using Genix.Placement;
using UnityEngine;

namespace Genix.Core
{
    /// <summary>Resolves a validated request into the spatial and scene state used by placement planning.</summary>
    public static class GenerationContextFactory
    {
        /// <summary>Builds a context without restricting area work to a known resolved asset set.</summary>
        /// <param name="request">Validated generation request.</param>
        /// <param name="generatedParent">Parent that owns previously and newly generated objects.</param>
        /// <returns>A context ready for candidate generation and placement planning.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The parent or area source is invalid, or the area cannot be built.</exception>
        public static GenerationContext Create(
            GenerationRequest request,
            Transform generatedParent)
        {
            return Create(request, generatedParent, null);
        }

        /// <summary>Builds a context and limits target-specific area work to the supplied assets.</summary>
        /// <param name="request">Validated generation request.</param>
        /// <param name="generatedParent">Parent that owns previously and newly generated objects.</param>
        /// <param name="assets">Resolved compatible assets, or <see langword="null"/> when not yet available.</param>
        /// <returns>A context ready for candidate generation and placement planning.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The parent or area source is invalid, or the area cannot be built.</exception>
        public static GenerationContext Create(
            GenerationRequest request,
            Transform generatedParent,
            IReadOnlyList<AssetDefinition> assets)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!generatedParent)
                throw new ArgumentException("Generation could not start because the generated object parent could not be created.", nameof(generatedParent));

            if (request.AreaSource == null)
                throw new ArgumentException("Generation could not start because no target area/location is selected.", nameof(request));

            AreaBuildProfile areaBuildProfile = new();
            AreaBuildSettings areaSettings = request.AreaBuildSettings
                .WithPlacementTargets(GetEffectivePlacementTargets(request, assets))
                .WithProfile(areaBuildProfile);
            Stopwatch areaStopwatch = Stopwatch.StartNew();
            bool areaBuilt = request.AreaSource.TryBuildArea(
                areaSettings,
                out Genix.Areas.PlacementArea area,
                out string error);

            if (!areaBuilt)
            {
                areaStopwatch.Stop();
                throw new ArgumentException(error, nameof(request));
            }

            Stopwatch sceneIndexStopwatch = Stopwatch.StartNew();
            float fixedIndexExpansion = CalculateFixedIndexExpansion(request, assets);
            SceneObjectIndex generatedSceneObjects = SceneObjectIndex.CollectGeneratedCached(generatedParent);
            SceneObjectIndex fixedSceneObjects = SceneObjectIndex.CollectFixedCached(
                request.AreaSource,
                generatedParent,
                area.WorldBounds,
                fixedIndexExpansion);
            sceneIndexStopwatch.Stop();
            areaBuildProfile.AddStepTime(
                AreaBuildProfileStep.SceneIndex,
                (float)sceneIndexStopwatch.Elapsed.TotalMilliseconds);
            areaStopwatch.Stop();

            return new GenerationContext(
                request,
                generatedParent,
                area,
                (float)areaStopwatch.Elapsed.TotalMilliseconds,
                areaBuildProfile,
                generatedSceneObjects,
                fixedSceneObjects);
        }

        private static PlacementTarget GetEffectivePlacementTargets(
            GenerationRequest request,
            IReadOnlyList<AssetDefinition> assets)
        {
            PlacementTarget targets = request.PlacementTargets & PlacementTarget.All;

            if (request.TargetDistributionMode == TargetDistributionMode.Weighted)
            {
                targets = RemoveZeroWeightTargets(targets, request.TargetDistributionWeights);
            }

            if (assets == null || assets.Count == 0)
                return targets == PlacementTarget.None ? request.PlacementTargets & PlacementTarget.All : targets;

            PlacementTarget assetTargets = PlacementTarget.None;

            foreach (AssetDefinition asset in assets)
            {
                if (!asset || !asset.Prefab)
                    continue;

                assetTargets |= ToPlacementTarget(asset.PlacementType);
            }

            PlacementTarget effective = targets & assetTargets;
            return effective == PlacementTarget.None
                ? targets
                : effective;
        }

        private static PlacementTarget RemoveZeroWeightTargets(
            PlacementTarget targets,
            TargetDistributionWeights weights)
        {
            PlacementTarget result = targets;

            if ((targets & PlacementTarget.Floor) != 0 && weights.Floor <= 0)
                result &= ~PlacementTarget.Floor;

            if ((targets & PlacementTarget.Wall) != 0 && weights.Wall <= 0)
                result &= ~PlacementTarget.Wall;

            if ((targets & PlacementTarget.Ceiling) != 0 && weights.Ceiling <= 0)
                result &= ~PlacementTarget.Ceiling;

            if ((targets & PlacementTarget.InsideSpace) != 0 && weights.InsideSpace <= 0)
                result &= ~PlacementTarget.InsideSpace;

            return result;
        }

        private static PlacementTarget ToPlacementTarget(PlacementType placementType) =>
            placementType switch
            {
                PlacementType.Floor => PlacementTarget.Floor,
                PlacementType.Wall => PlacementTarget.Wall,
                PlacementType.Ceiling => PlacementTarget.Ceiling,
                PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                _ => PlacementTarget.None
            };

        private static float CalculateFixedIndexExpansion(
            GenerationRequest request,
            IReadOnlyList<AssetDefinition> assets)
        {
            float expansion = 0f;

            if (assets != null)
            {
                foreach (AssetDefinition asset in assets)
                {
                    if (!asset || !asset.Prefab)
                        continue;

                    Vector3 dimensions = AssetAttemptPlanner.Dimensions(asset);
                    expansion = Mathf.Max(expansion, dimensions.x, dimensions.y, dimensions.z);
                }
            }

            if (request.StyleSettings.placement.useFixedObjectClearance)
            {
                expansion += Mathf.Max(0f, request.StyleSettings.placement.fixedObjectDistance);
            }

            return expansion;
        }
    }
}
