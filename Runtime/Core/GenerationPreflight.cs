using System;

namespace Genix.Core
{
    public static class GenerationPreflight
    {
        public static bool IsValid(GenerationRequest request, out string error)
        {
            if (request == null)
            {
                error = "Generation could not start because the generation request is missing.";
                return false;
            }

            if (request.AreaSource == null || !request.AreaSource.ParentTransform)
            {
                error = "Generation could not start because no valid target area/location is selected. Choose a target area with an SFS anchor in the Genix window.";
                return false;
            }

            if (!request.AssetPool)
            {
                error = "Generation could not start because no asset pool is selected. Choose an asset pool in the Genix window.";
                return false;
            }

            if (request.AssetPool.IsStatic && !request.AssetPool.HasValidStaticAssets)
            {
                error = $"Generation could not start because static asset pool '{request.AssetPool.name}' is empty. Add at least one asset to the pool.";
                return false;
            }

            if (request.ObjectCount <= 0)
            {
                error = "Generation could not start because Object Count must be greater than zero.";
                return false;
            }

            if (!IsValidGenerationMode(request.GenerationMode))
            {
                error = $"Unsupported generation mode: {request.GenerationMode}.";
                return false;
            }

            if (request.GenerationMode == GenerationMode.TargetPlacement &&
                (request.PlacementTargets & PlacementTarget.All) == PlacementTarget.None)
            {
                error = "Target Placement could not start because no placement targets are selected. Choose at least Floor, Wall, Ceiling, or Inside Space.";
                return false;
            }

            if (request.GenerationMode == GenerationMode.TargetPlacement &&
                request.TargetDistributionMode == TargetDistributionMode.Weighted &&
                GetActiveTargetWeightSum(request.PlacementTargets, request.TargetDistributionWeights) <= 0)
            {
                error = "Weighted Target Distribution could not start because all selected target weights are zero. Increase at least one selected target weight.";
                return false;
            }

            if (request.RelativePlacement.IsEnabled)
                return IsValidRelativePlacementRequest(request, out error);

            error = null;
            return true;
        }

        private static bool IsValidRelativePlacementRequest(GenerationRequest request, out string error)
        {
            if (request.RelativePlacement.Source == RelativePlacementSource.SelectedObjects &&
                request.RelativePlacement.SelectedTransforms.Count == 0)
            {
                error = "Relative Placement could not start because Selected Objects is active, but no scene objects are selected.";
                return false;
            }

            if (request.RelativePlacement.Radius <= 0f)
            {
                error = $"Relative Placement could not start because the radius must be greater than zero. Current radius: {request.RelativePlacement.Radius}.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsValidGenerationMode(GenerationMode generationMode)
        {
            return Enum.IsDefined(typeof(GenerationMode), generationMode);
        }

        private static int GetActiveTargetWeightSum(
            PlacementTarget placementTargets,
            TargetDistributionWeights weights)
        {
            int sum = 0;

            if ((placementTargets & PlacementTarget.Floor) != 0)
                sum += weights.Floor;

            if ((placementTargets & PlacementTarget.Wall) != 0)
                sum += weights.Wall;

            if ((placementTargets & PlacementTarget.Ceiling) != 0)
                sum += weights.Ceiling;

            if ((placementTargets & PlacementTarget.InsideSpace) != 0)
                sum += weights.InsideSpace;

            return sum;
        }
    }
}
