using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Assets;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Semantics;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Infrastructure
{
    internal sealed class StarterContentBuildResult
    {
        public bool Success;
        public string Error = string.Empty;
        public int CreatedCount;
        public int ReusedCount;
        public GenerationPreset GenerationPreset;
    }

    /// <summary>Builds the editable assets used by the first-run designer workflow.</summary>
    internal static partial class StarterContentBuilder
    {
        internal const string Root = ProjectContentPaths.Root + "/Starter Content";
        internal const string DefinitionsRoot = ProjectContentPaths.AssetsRoot + "/Starter Content/Definitions";
        internal const string PoolsRoot = ProjectContentPaths.AssetsRoot + "/Starter Content/Pools";
        internal const string CategoriesRoot = ProjectContentPaths.AssetsRoot + "/Starter Content/Tags/Categories";
        internal const string TagsRoot = ProjectContentPaths.AssetsRoot + "/Starter Content/Tags/Values";
        internal const string PrefabsRoot = Root + "/Prefabs";
        internal const string MaterialsRoot = Root + "/Materials";
        internal const string ScenesRoot = Root + "/Scenes";
        internal const string StarterRoomScenePath = ScenesRoot + "/Starter Room.unity";
        internal const string StarterPresetPath = ProjectContentPaths.GenerationPresets + "/Starter Room.asset";

        private const int StarterObjectCount = 8;

        internal static readonly IReadOnlyList<string> StarterAssetNames = new[]
        {
            "Desk",
            "Monitor",
            "Keyboard",
            "Mouse",
            "Coffee Mug",
            "Chair",
            "Cargo Box",
            "Warning Sign",
            "Ceiling Light"
        };

        public static bool IsInstalled =>
            AssetDatabase.LoadAssetAtPath<GenerationPreset>(StarterPresetPath);

        public static StarterContentBuildResult Build()
        {
            StarterContentBuildResult result = new();

            try
            {
                EnsureFolders();

                StarterTaxonomy taxonomy = BuildTaxonomy(result);
                Dictionary<string, StylePreset> styles = BuildStyles(result);
                StarterMaterials materials = BuildMaterials(result);
                StarterPrefabs prefabs = BuildPrefabs(materials, taxonomy, result);
                StarterDefinitions definitions = BuildDefinitions(prefabs, taxonomy, result);
                AssetPool starterPool = BuildPools(definitions, result);
                result.GenerationPreset = BuildGenerationPreset(starterPool, styles["Freeform"], result);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                AssetCatalogService.Refresh();
                result.Success = true;
            }
            catch (Exception exception)
            {
                result.Error = exception.Message;
                Debug.LogException(exception);
            }

            return result;
        }

        internal static GenerationPresetSettings CreateGenerationSettings(
            AssetPool pool,
            StylePreset style)
        {
            return new GenerationPresetSettings(
                pool,
                style,
                StarterObjectCount,
                PlacementTarget.Floor | PlacementTarget.Wall | PlacementTarget.Ceiling,
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
        }

        private static void EnsureFolders()
        {
            AssetFileService.EnsureFolders(
                Root,
                DefinitionsRoot,
                PoolsRoot,
                CategoriesRoot,
                TagsRoot,
                PrefabsRoot,
                MaterialsRoot,
                ScenesRoot,
                ProjectContentPaths.StylePresets,
                ProjectContentPaths.GenerationPresets);
        }
    }
}
