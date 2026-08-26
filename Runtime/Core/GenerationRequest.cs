using Genix.Areas;
using Genix.Assets;
using Genix.Styles;
using UnityEngine;

namespace Genix.Core
{
    /// <summary>
    /// Describes one generation operation through get-only run parameters before spatial data and assets are resolved.
    /// </summary>
    /// <remarks>
    /// A request fixes references and scalar choices for one invocation but does not deep-copy referenced
    /// Unity objects. Use <see cref="GenerationPreflight"/> to validate it and
    /// <see cref="GenerationContextFactory"/> to resolve its area and scene-dependent state.
    /// </remarks>
    public sealed class GenerationRequest
    {
        /// <summary>Gets area source.</summary>
        public IAreaSource AreaSource { get; }
        /// <summary>Gets area build settings.</summary>
        public AreaBuildSettings AreaBuildSettings { get; }
        /// <summary>Gets asset pool.</summary>
        public AssetPool AssetPool { get; }
        /// <summary>Gets the number of object items.</summary>
        public int ObjectCount { get; }

        /// <summary>Gets placement targets.</summary>
        public PlacementTarget PlacementTargets { get; }
        /// <summary>Gets target distribution mode.</summary>
        public TargetDistributionMode TargetDistributionMode { get; }
        /// <summary>Gets target distribution weights.</summary>
        public TargetDistributionWeights TargetDistributionWeights { get; }
        /// <summary>Gets optional semantic support-surface distribution.</summary>
        public SupportDistributionSettings SupportDistribution { get; }
        /// <summary>Gets style name.</summary>
        public string StyleName { get; }
        /// <summary>Gets style settings.</summary>
        public StyleSettings StyleSettings { get; }
        /// <summary>Gets relative placement.</summary>
        public RelativePlacementSettings RelativePlacement { get; }
        /// <summary>Indicates whether fixed seed.</summary>
        public bool UseFixedSeed { get; }
        /// <summary>Gets random seed.</summary>
        public int RandomSeed { get; }
        /// <summary>Indicates whether best effort.</summary>
        public bool BestEffort { get; }
        /// <summary>Indicates whether detailed diagnostics.</summary>
        public bool DetailedDiagnostics { get; }

        /// <summary>Creates a generation request.</summary>
        /// <param name="areaSource">Provider for the target area's spatial representation.</param>
        /// <param name="assetPool">Pool from which compatible asset definitions are resolved.</param>
        /// <param name="objectCount">Requested number of accepted placements.</param>
        /// <param name="placementTargets">Allowed surface and volume target types.</param>
        /// <param name="targetDistributionMode">Policy for sharing placements among selected targets.</param>
        /// <param name="targetDistributionWeights">Relative target weights used by weighted distribution.</param>
        /// <param name="styleSettings">Sampling and spacing configuration.</param>
        /// <param name="areaBuildSettings">Surface-discovery and area-construction configuration.</param>
        /// <param name="relativePlacement">Optional proximity constraint relative to generated or scene objects.</param>
        /// <param name="styleName">Display name recorded in diagnostics and profiling.</param>
        /// <param name="useFixedSeed">Whether to use <paramref name="randomSeed"/> instead of creating a new seed.</param>
        /// <param name="randomSeed">Deterministic seed used when <paramref name="useFixedSeed"/> is true.</param>
        /// <param name="bestEffort">Whether a valid partial plan may be returned.</param>
        /// <param name="detailedDiagnostics">Whether per-attempt diagnostic geometry should be retained.</param>
        /// <param name="supportDistribution">Optional placement budgets for explicitly tagged and default support surfaces.</param>
        public GenerationRequest(
            IAreaSource areaSource,
            AssetPool assetPool,
            int objectCount,
            PlacementTarget placementTargets,
            TargetDistributionMode targetDistributionMode,
            TargetDistributionWeights targetDistributionWeights,
            StyleSettings styleSettings,
            AreaBuildSettings areaBuildSettings,
            RelativePlacementSettings relativePlacement = null,
            string styleName = "",
            bool useFixedSeed = false,
            int randomSeed = 0,
            bool bestEffort = true,
            bool detailedDiagnostics = false,
            SupportDistributionSettings supportDistribution = null)
        {
            AreaSource = areaSource;
            AreaBuildSettings = areaBuildSettings;
            AssetPool = assetPool;
            ObjectCount = objectCount;
            PlacementTargets = placementTargets;
            TargetDistributionMode = targetDistributionMode;
            TargetDistributionWeights = targetDistributionWeights;
            SupportDistribution = supportDistribution?.Copy() ?? SupportDistributionSettings.Disabled;
            StyleName = styleName;
            StyleSettings = styleSettings;
            RelativePlacement = relativePlacement ?? RelativePlacementSettings.Disabled;
            UseFixedSeed = useFixedSeed;
            RandomSeed = randomSeed;
            BestEffort = bestEffort;
            DetailedDiagnostics = detailedDiagnostics;
        }
    }
}
