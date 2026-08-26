using System;
using Genix.Areas;
using Genix.Assets;
using Genix.Styles;
using UnityEngine;

namespace Genix.Core
{
    /// <summary>Serializable designer settings captured by a <see cref="GenerationPreset"/>.</summary>
    [Serializable]
    public struct GenerationPresetSettings : IEquatable<GenerationPresetSettings>
    {
        [SerializeField, Tooltip("Asset pool used by generation.")]
        private AssetPool assetPool;
        [SerializeField, Tooltip("Generation style that controls sampling and spacing.")]
        private StylePreset stylePreset;
        [SerializeField, Min(1), Tooltip("Number of objects requested by a generation run.")]
        private int objectCount;
        [SerializeField, Tooltip("Surface and volume target types available to generation.")]
        private PlacementTarget placementTargets;
        [SerializeField, Tooltip("Policy used to distribute objects among multiple placement targets.")]
        private TargetDistributionMode targetDistributionMode;
        [SerializeField, Tooltip("Relative target weights used when distribution is Weighted.")]
        private TargetDistributionWeights targetDistributionWeights;
        [SerializeField, Tooltip("Optional accepted-placement distribution across explicitly tagged support surfaces and all remaining surfaces.")]
        private SupportDistributionSettings supportDistribution;
        [SerializeField, Tooltip("Method used to convert SFS voxel layers into placement regions.")]
        private AreaDecompositionMode areaDecompositionMode;
        [SerializeField, Tooltip("Source from which floor, wall, and ceiling surfaces are discovered.")]
        private SurfaceDiscoveryMode surfaceDiscoveryMode;
        [SerializeField, Tooltip("Layers eligible to provide floor placement surfaces.")]
        private LayerMask floorSurfaceLayers;
        [SerializeField, Tooltip("Layers eligible to provide wall placement surfaces.")]
        private LayerMask wallSurfaceLayers;
        [SerializeField, Tooltip("Layers eligible to provide ceiling placement surfaces.")]
        private LayerMask ceilingSurfaceLayers;
        [SerializeField, Range(0f, 89.9f), Tooltip("Maximum slope from upward-facing horizontal that counts as floor.")]
        private float floorSurfaceAngleDegrees;
        [SerializeField, Range(0f, 89.9f), Tooltip("Maximum slope from downward-facing horizontal that counts as ceiling.")]
        private float ceilingSurfaceAngleDegrees;
        [SerializeField, Tooltip("Objects that may act as relative-placement anchors. Selected Objects uses the selection at generation time.")]
        private RelativePlacementSource relativePlacementSource;
        [SerializeField, Min(0.1f), Tooltip("Maximum three-dimensional distance from a relative-placement anchor.")]
        private float relativeRadius;
        [SerializeField, Tooltip("Scene layers eligible to act as relative-placement anchors.")]
        private LayerMask relativeSceneLayers;
        [SerializeField, Tooltip("Use a deterministic seed instead of creating a random seed per run.")]
        private bool useFixedSeed;
        [SerializeField, Tooltip("Deterministic seed used when Use Fixed Seed is enabled.")]
        private int randomSeed;
        [SerializeField, Tooltip("Keep the largest valid partial result when the requested count cannot be reached.")]
        private bool bestEffort;

        /// <summary>Gets a general-purpose initial configuration for newly created preset assets.</summary>
        public static GenerationPresetSettings Default => new(
            null,
            null,
            20,
            PlacementTarget.All,
            TargetDistributionMode.Random,
            TargetDistributionWeights.Default,
            AreaDecompositionMode.Fast,
            SurfaceDiscoveryMode.AllMatchingSurfacesInVolume,
            ~0,
            ~0,
            ~0,
            60f,
            60f,
            RelativePlacementSource.None,
            2f,
            ~0,
            false,
            12345,
            true,
            SupportDistributionSettings.Disabled);

        /// <summary>Gets the asset pool.</summary>
        public readonly AssetPool AssetPool => assetPool;
        /// <summary>Gets the generation style preset.</summary>
        public readonly StylePreset StylePreset => stylePreset;
        /// <summary>Gets the requested object count.</summary>
        public readonly int ObjectCount => Mathf.Max(1, objectCount);
        /// <summary>Gets the selected placement targets.</summary>
        public readonly PlacementTarget PlacementTargets => placementTargets & PlacementTarget.All;
        /// <summary>Gets the target-distribution policy.</summary>
        public readonly TargetDistributionMode TargetDistributionMode => targetDistributionMode;
        /// <summary>Gets the target-distribution weights.</summary>
        public readonly TargetDistributionWeights TargetDistributionWeights => targetDistributionWeights;
        /// <summary>Gets semantic support-surface distribution.</summary>
        public readonly SupportDistributionSettings SupportDistribution =>
            supportDistribution?.Copy() ?? SupportDistributionSettings.Disabled;
        /// <summary>Gets the area-decomposition mode.</summary>
        public readonly AreaDecompositionMode AreaDecompositionMode => areaDecompositionMode;
        /// <summary>Gets the surface-discovery mode.</summary>
        public readonly SurfaceDiscoveryMode SurfaceDiscoveryMode => surfaceDiscoveryMode;
        /// <summary>Gets the floor surface layers.</summary>
        public readonly LayerMask FloorSurfaceLayers => floorSurfaceLayers;
        /// <summary>Gets the wall surface layers.</summary>
        public readonly LayerMask WallSurfaceLayers => wallSurfaceLayers;
        /// <summary>Gets the ceiling surface layers.</summary>
        public readonly LayerMask CeilingSurfaceLayers => ceilingSurfaceLayers;
        /// <summary>Gets the floor classification angle in degrees.</summary>
        public readonly float FloorSurfaceAngleDegrees => Mathf.Clamp(floorSurfaceAngleDegrees, 0f, 89.9f);
        /// <summary>Gets the ceiling classification angle in degrees.</summary>
        public readonly float CeilingSurfaceAngleDegrees => Mathf.Clamp(ceilingSurfaceAngleDegrees, 0f, 89.9f);
        /// <summary>Gets the relative-placement anchor source.</summary>
        public readonly RelativePlacementSource RelativePlacementSource => relativePlacementSource;
        /// <summary>Gets the relative-placement radius.</summary>
        public readonly float RelativeRadius => Mathf.Max(0.1f, relativeRadius);
        /// <summary>Gets the relative-placement scene layers.</summary>
        public readonly LayerMask RelativeSceneLayers => relativeSceneLayers;
        /// <summary>Indicates whether a fixed seed is used.</summary>
        public readonly bool UseFixedSeed => useFixedSeed;
        /// <summary>Gets the deterministic seed.</summary>
        public readonly int RandomSeed => randomSeed;
        /// <summary>Indicates whether partial valid plans are allowed.</summary>
        public readonly bool BestEffort => bestEffort;

        /// <summary>Creates a complete generation-preset settings snapshot.</summary>
        public GenerationPresetSettings(
            AssetPool assetPool,
            StylePreset stylePreset,
            int objectCount,
            PlacementTarget placementTargets,
            TargetDistributionMode targetDistributionMode,
            TargetDistributionWeights targetDistributionWeights,
            AreaDecompositionMode areaDecompositionMode,
            SurfaceDiscoveryMode surfaceDiscoveryMode,
            LayerMask floorSurfaceLayers,
            LayerMask wallSurfaceLayers,
            LayerMask ceilingSurfaceLayers,
            float floorSurfaceAngleDegrees,
            float ceilingSurfaceAngleDegrees,
            RelativePlacementSource relativePlacementSource,
            float relativeRadius,
            LayerMask relativeSceneLayers,
            bool useFixedSeed,
            int randomSeed,
            bool bestEffort,
            SupportDistributionSettings supportDistribution = null)
        {
            this.assetPool = assetPool;
            this.stylePreset = stylePreset;
            this.objectCount = Mathf.Max(1, objectCount);
            this.placementTargets = placementTargets & PlacementTarget.All;
            this.targetDistributionMode = NormalizeEnum(targetDistributionMode, TargetDistributionMode.Random);
            this.targetDistributionWeights = new TargetDistributionWeights(
                targetDistributionWeights.Floor,
                targetDistributionWeights.Wall,
                targetDistributionWeights.Ceiling,
                targetDistributionWeights.InsideSpace);
            this.supportDistribution = supportDistribution?.Copy() ?? SupportDistributionSettings.Disabled;
            this.areaDecompositionMode = NormalizeEnum(areaDecompositionMode, AreaDecompositionMode.Fast);
            this.surfaceDiscoveryMode = NormalizeEnum(
                surfaceDiscoveryMode,
                SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);
            this.floorSurfaceLayers = floorSurfaceLayers;
            this.wallSurfaceLayers = wallSurfaceLayers;
            this.ceilingSurfaceLayers = ceilingSurfaceLayers;
            this.floorSurfaceAngleDegrees = Mathf.Clamp(floorSurfaceAngleDegrees, 0f, 89.9f);
            this.ceilingSurfaceAngleDegrees = Mathf.Clamp(ceilingSurfaceAngleDegrees, 0f, 89.9f);
            this.relativePlacementSource = NormalizeEnum(relativePlacementSource, RelativePlacementSource.None);
            this.relativeRadius = Mathf.Max(0.1f, relativeRadius);
            this.relativeSceneLayers = relativeSceneLayers;
            this.useFixedSeed = useFixedSeed;
            this.randomSeed = randomSeed;
            this.bestEffort = bestEffort;
        }

        /// <summary>Returns a copy with validated enum, range, and weight values.</summary>
        public readonly GenerationPresetSettings Sanitized() => new(
            assetPool,
            stylePreset,
            objectCount,
            placementTargets,
            targetDistributionMode,
            targetDistributionWeights,
            areaDecompositionMode,
            surfaceDiscoveryMode,
            floorSurfaceLayers,
            wallSurfaceLayers,
            ceilingSurfaceLayers,
            floorSurfaceAngleDegrees,
            ceilingSurfaceAngleDegrees,
            relativePlacementSource,
            relativeRadius,
            relativeSceneLayers,
            useFixedSeed,
            randomSeed,
            bestEffort,
            supportDistribution);

        /// <inheritdoc />
        public readonly bool Equals(GenerationPresetSettings other)
        {
            return assetPool == other.assetPool &&
                   stylePreset == other.stylePreset &&
                   ObjectCount == other.ObjectCount &&
                   PlacementTargets == other.PlacementTargets &&
                   TargetDistributionMode == other.TargetDistributionMode &&
                   TargetDistributionWeights.Floor == other.TargetDistributionWeights.Floor &&
                   TargetDistributionWeights.Wall == other.TargetDistributionWeights.Wall &&
                   TargetDistributionWeights.Ceiling == other.TargetDistributionWeights.Ceiling &&
                   TargetDistributionWeights.InsideSpace == other.TargetDistributionWeights.InsideSpace &&
                   SupportDistributionEquals(SupportDistribution, other.SupportDistribution) &&
                   AreaDecompositionMode == other.AreaDecompositionMode &&
                   SurfaceDiscoveryMode == other.SurfaceDiscoveryMode &&
                   FloorSurfaceLayers.value == other.FloorSurfaceLayers.value &&
                   WallSurfaceLayers.value == other.WallSurfaceLayers.value &&
                   CeilingSurfaceLayers.value == other.CeilingSurfaceLayers.value &&
                   Mathf.Approximately(FloorSurfaceAngleDegrees, other.FloorSurfaceAngleDegrees) &&
                   Mathf.Approximately(CeilingSurfaceAngleDegrees, other.CeilingSurfaceAngleDegrees) &&
                   RelativePlacementSource == other.RelativePlacementSource &&
                   Mathf.Approximately(RelativeRadius, other.RelativeRadius) &&
                   RelativeSceneLayers.value == other.RelativeSceneLayers.value &&
                   UseFixedSeed == other.UseFixedSeed &&
                   RandomSeed == other.RandomSeed &&
                   BestEffort == other.BestEffort;
        }

        /// <inheritdoc />
        public override readonly bool Equals(object obj) =>
            obj is GenerationPresetSettings other && Equals(other);

        /// <inheritdoc />
        public override readonly int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + ObjectCount;
                hash = hash * 31 + (int)PlacementTargets;
                hash = hash * 31 + (int)TargetDistributionMode;
                hash = hash * 31 + RandomSeed;
                hash = hash * 31 + (UseFixedSeed ? 1 : 0);
                return hash;
            }
        }

        private static T NormalizeEnum<T>(T value, T fallback) where T : struct, Enum =>
            Enum.IsDefined(typeof(T), value) ? value : fallback;

        private static bool SupportDistributionEquals(
            SupportDistributionSettings left,
            SupportDistributionSettings right)
        {
            if (left.IsEnabled != right.IsEnabled || left.DefaultWeight != right.DefaultWeight ||
                left.Rules.Count != right.Rules.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Rules.Count; i++)
            {
                SupportDistributionRule leftRule = left.Rules[i];
                SupportDistributionRule rightRule = right.Rules[i];
                if (leftRule.SupportTag != rightRule.SupportTag ||
                    leftRule.Mode != rightRule.Mode ||
                    leftRule.Value != rightRule.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Persists reusable, scene-independent Genix Generator settings as a Unity asset.
    /// </summary>
    /// <remarks>
    /// Target areas and profiling controls are intentionally excluded because they are scene- or run-specific.
    /// </remarks>
    [CreateAssetMenu(menuName = "Genix/Generation Preset", fileName = "Generation Preset")]
    public sealed class GenerationPreset : ScriptableObject
    {
        [SerializeField] private GenerationPresetSettings settings = GenerationPresetSettings.Default;

        /// <summary>Gets the normalized settings snapshot stored by this preset.</summary>
        public GenerationPresetSettings Settings => settings.Sanitized();

        /// <summary>Initializes or replaces the stored settings.</summary>
        /// <param name="value">Complete settings snapshot captured from the generator.</param>
        public void Apply(GenerationPresetSettings value)
        {
            settings = value.Sanitized();
        }

        /// <summary>Determines whether this preset equals the supplied generator state.</summary>
        public bool Matches(GenerationPresetSettings value) => Settings.Equals(value.Sanitized());

        private void OnValidate()
        {
            settings = settings.Sanitized();
        }
    }
}
