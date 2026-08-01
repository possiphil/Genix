using Genix.Areas;
using Genix.Assets;
using Genix.Styles;
using UnityEngine;

namespace Genix.Core
{
    public sealed class GenerationRequest
    {
        public IAreaSource AreaSource { get; }
        public AreaBuildSettings AreaBuildSettings { get; }
        public AssetPool AssetPool { get; }
        public int ObjectCount { get; }

        public GenerationMode GenerationMode { get; }
        public GenerationPerformanceMode PerformanceMode { get; }
        public PlacementTarget PlacementTargets { get; }
        public TargetDistributionMode TargetDistributionMode { get; }
        public TargetDistributionWeights TargetDistributionWeights { get; }
        public string StyleName { get; }
        public StyleSettings StyleSettings { get; }
        public RelativePlacementSettings RelativePlacement { get; }
        public bool UseRandomSeed { get; }
        public int RandomSeed { get; }
        public bool BestEffort { get; }
        public bool DetailedDiagnostics { get; }

        public GenerationRequest(
            IAreaSource areaSource,
            AssetPool assetPool,
            int objectCount,
            GenerationMode generationMode,
            PlacementTarget placementTargets,
            TargetDistributionMode targetDistributionMode,
            TargetDistributionWeights targetDistributionWeights,
            StyleSettings styleSettings,
            AreaBuildSettings areaBuildSettings,
            RelativePlacementSettings relativePlacement = null,
            string styleName = "",
            bool useRandomSeed = false,
            int randomSeed = 0,
            bool bestEffort = true,
            GenerationPerformanceMode performanceMode = GenerationPerformanceMode.Accurate,
            bool detailedDiagnostics = false)
        {
            AreaSource = areaSource;
            AreaBuildSettings = areaBuildSettings;
            AssetPool = assetPool;
            ObjectCount = objectCount;
            GenerationMode = generationMode;
            PerformanceMode = performanceMode;
            PlacementTargets = placementTargets;
            TargetDistributionMode = targetDistributionMode;
            TargetDistributionWeights = targetDistributionWeights;
            StyleName = styleName;
            StyleSettings = styleSettings;
            RelativePlacement = relativePlacement ?? RelativePlacementSettings.Disabled;
            UseRandomSeed = useRandomSeed;
            RandomSeed = randomSeed;
            BestEffort = bestEffort;
            DetailedDiagnostics = detailedDiagnostics;
        }
    }
}
