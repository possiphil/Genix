using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Assets;
using Genix.Geometry;
using Genix.Orientation;
using Genix.Placement;
using Genix.Semantics;
using Genix.Styles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Evaluation
{
    /// <summary>Builds the reproducible semantic configuration used by the outdoor thesis evaluation.</summary>
    internal static partial class OutdoorEvaluationSetupUtility
    {
        internal const string ScenePath =
            "Packages/com.possiphil.genix.devtools/Evaluation/Scenes/RealWorld/OutdoorEnvironment.unity";
        internal const string PoolPath = "Assets/Genix/Assets/Pools/EvalOutdoorPool.asset";
        internal const string PresetPath = "Assets/Genix/Generation Presets/OutdoorEval.asset";

        private const string RequestAssetPath = "Assets/Genix/Evaluations/OutdoorSetup.request.txt";
        private const string TriggerAssetPath = "Assets/Genix/Evaluations/OutdoorSetupTrigger.cs";
        private const string DefinitionFolder = "Assets/Genix/Assets/Definitions";
        private const string TagFolder = "Assets/Genix/Assets/Tags/Values";
        private const string CategoryFolder = "Assets/Genix/Assets/Tags/Categories";
        private const string NaturalStylePath = "Assets/Genix/Presets/Natural.asset";
        private const string WaterPlacementMeshPath =
            "Assets/Genix/Evaluations/Generated/Outdoor Water Placement Region.asset";
        private const string SemanticRootName = "Genix Outdoor Semantics";
        private const int SetupRevision = 10;
        private const int RequestedObjectCount = 60;
        private const int FloorCount = 54;
        private const int WallCount = 6;
        private const int MaximumBollardStationCount = 6;
        private const int MaximumBollardCount = MaximumBollardStationCount * 2;
        private const float BollardSpacing = 5f;
        private const float BollardPathOffset = 2.15f;
        private const int WaterGridResolution = 32;
        private const float MinimumWaterDepth = 0.2f;

        private static readonly string[] TemperateTreeNames =
        {
            "Big Bare Tree",
            "Bushy Tree",
            "Eiche",
            "Pine Tree",
            "Small Bare Tree1",
            "Small Bare Tree2",
            "Tall Tree",
            "Tree"
        };

        private static readonly string[] PalmTreeNames =
        {
            "Big Palm Tree1",
            "Big Palm Tree2",
            "Small Palm Tree"
        };

        private static readonly string[] BushNames =
        {
            "SmallBush1",
            "SmallBush2",
            "MediumBush1",
            "MediumBush2",
            "MediumBush3",
            "LargeBush1",
            "LargeBush2"
        };

        private static readonly string[] GroundRockNames =
        {
            "Small Ground Rock",
            "Medium Ground Rock",
            "Large Ground Rock",
            "Boulder"
        };

        [InitializeOnLoadMethod]
        private static void QueueRequestedSetup()
        {
            if (!File.Exists(RequestAssetPath))
                return;

            EditorApplication.delayCall += RunRequestedSetup;
        }

        internal static string Prepare()
        {
            EnsureFolder("Assets/Genix/Assets/Pools");
            EnsureFolder("Assets/Genix/Generation Presets");
            EnsureFolder("Assets/Genix/Evaluations/Generated");

            TagCategory environmentCategory = LoadCategory("Environment Type");
            TagCategory supportCategory = LoadCategory("Support Type");
            TagCategory roleCategory = LoadCategory("Role");
            TagCategory functionCategory = LoadCategory("Function");
            TagCategory themeCategory = LoadCategory("Theme");
            TagCategory sizeCategory = LoadCategory("Size");

            SemanticTag outdoor = GetOrCreateTag("Environment Type", "Outdoor", environmentCategory);
            SemanticTag terrainSupport = LoadTag("Support Type", "Terrain");
            SemanticTag pathSupport = GetOrCreateTag("Support Type", "Path", supportCategory);
            SemanticTag waterSupport = GetOrCreateTag("Support Type", "Water", supportCategory);
            SemanticTag parkingSpot = GetOrCreateTag("Function", "Parking Spot", functionCategory);
            SemanticTag camping = GetOrCreateTag("Function", "Camping", functionCategory);
            SemanticTag vehicle = GetOrCreateTag("Role", "Vehicle", roleCategory);

            SemanticTag natural = LoadTag("Theme", "Natural");
            SemanticTag vegetation = LoadTag("Role", "Vegetation");
            SemanticTag decoration = LoadTag("Role", "Decoration");
            SemanticTag furniture = LoadTag("Role", "Furniture");
            SemanticTag structure = LoadTag("Role", "Structure");
            SemanticTag gameplay = LoadTag("Role", "Gameplay");
            SemanticTag signage = LoadTag("Role", "Signage");
            SemanticTag pathFunction = LoadTag("Function", "Path");
            SemanticTag restArea = LoadTag("Function", "Rest Area");

            Dictionary<string, SemanticTag> sizeTags = new(StringComparer.Ordinal)
            {
                ["Tiny"] = LoadTag("Size", "Tiny"),
                ["Small"] = LoadTag("Size", "Small"),
                ["Medium"] = LoadTag("Size", "Medium"),
                ["Large"] = LoadTag("Size", "Large"),
                ["Huge"] = LoadTag("Size", "Huge")
            };

            Dictionary<string, AssetDefinition> definitions = LoadDefinitions(
                TemperateTreeNames
                    .Concat(PalmTreeNames)
                    .Concat(BushNames)
                    .Concat(GroundRockNames)
                    .Concat(new[]
                    {
                        "Cliff Rock",
                        "Bench",
                        "Bollard",
                        "Campfire",
                        "Fallen Log",
                        "Lilypad",
                        "Peugeot",
                        "Trail Sign"
                    }));

            foreach (string name in TemperateTreeNames.Concat(PalmTreeNames))
            {
                ConfigureDefinition(
                    definitions[name],
                    new[] { outdoor, natural, vegetation, ResolveSizeTag(definitions[name], sizeTags) },
                    terrainSupport,
                    PlacementType.Floor,
                    OrientationMode.None,
                    SurfaceFitMode.Adaptive,
                    SurfaceAlignmentMode.KeepUpright,
                    maxHeightDifference: 1.1f,
                    minimumSupport: 0.65f,
                    randomYaw: true,
                    heightMode: SurfaceHeightMode.Lowest,
                    sinkOffset: 0.08f);
            }

            foreach (string name in BushNames)
            {
                ConfigureDefinition(
                    definitions[name],
                    new[] { outdoor, natural, vegetation, ResolveSizeTag(definitions[name], sizeTags) },
                    terrainSupport,
                    PlacementType.Floor,
                    OrientationMode.None,
                    SurfaceFitMode.Adaptive,
                    SurfaceAlignmentMode.KeepUpright,
                    maxHeightDifference: 0.55f,
                    minimumSupport: 0.6f,
                    randomYaw: true,
                    heightMode: SurfaceHeightMode.Lowest,
                    sinkOffset: name.StartsWith("Large", StringComparison.Ordinal) ? 0.1f :
                        name.StartsWith("Medium", StringComparison.Ordinal) ? 0.08f : 0.06f);
            }

            foreach (string name in GroundRockNames)
            {
                ConfigureDefinition(
                    definitions[name],
                    new[] { outdoor, natural, decoration, ResolveSizeTag(definitions[name], sizeTags) },
                    terrainSupport,
                    PlacementType.Floor,
                    OrientationMode.None,
                    SurfaceFitMode.Adaptive,
                    SurfaceAlignmentMode.AlignToSurface,
                    maxHeightDifference: 0.8f,
                    minimumSupport: 0.55f,
                    randomYaw: true,
                    heightMode: SurfaceHeightMode.Lowest,
                    sinkOffset: name.StartsWith("Large", StringComparison.Ordinal) ? 0.08f :
                        name.StartsWith("Medium", StringComparison.Ordinal) ? 0.06f : 0.04f);
            }

            AssetDefinition cliffRock = definitions["Cliff Rock"];
            ConfigureDefinition(
                cliffRock,
                new[] { outdoor, natural, decoration, ResolveSizeTag(cliffRock, sizeTags) },
                terrainSupport,
                PlacementType.Wall,
                OrientationMode.None,
                SurfaceFitMode.Adaptive,
                SurfaceAlignmentMode.AlignToSurface,
                maxHeightDifference: 0.5f,
                minimumSupport: 0.75f,
                randomYaw: false,
                randomRoll: false,
                heightMode: SurfaceHeightMode.Lowest,
                sinkOffset: 0.24f);
            cliffRock.SetPlacementLimit(true, WallCount);

            AssetDefinition bench = definitions["Bench"];
            ConfigureDefinition(
                bench,
                new[] { outdoor, natural, furniture, pathFunction, restArea, ResolveSizeTag(bench, sizeTags) },
                terrainSupport,
                PlacementType.Floor,
                OrientationMode.None,
                SurfaceFitMode.Adaptive,
                SurfaceAlignmentMode.KeepUpright,
                maxHeightDifference: 0.35f,
                minimumSupport: 0.8f,
                randomYaw: false,
                heightMode: SurfaceHeightMode.Lowest,
                sinkOffset: 0.03f);
            bench.SetPlacementLimit(true, 2);
            bench.AssetRelativePlacement.ConfigureTag(
                restArea,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                0.1f,
                AssetRelativeFacing.Any);
            bench.AssetRelativePlacement.SetRequireInsideAnchorBounds(true);
            bench.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.AtMost, 2);
            bench.PathPlacement.Configure(
                pathFunction,
                1.25f,
                5f,
                PathPlacementSide.Any,
                PathPlacementFacing.TowardPath,
                10f);

            AssetDefinition bollard = definitions["Bollard"];
            ConfigureDefinition(
                bollard,
                new[] { outdoor, natural, structure, pathFunction, ResolveSizeTag(bollard, sizeTags) },
                terrainSupport,
                PlacementType.Floor,
                OrientationMode.None,
                SurfaceFitMode.Adaptive,
                SurfaceAlignmentMode.KeepUpright,
                maxHeightDifference: 0.25f,
                minimumSupport: 0.8f,
                randomYaw: false);
            bollard.SetPlacementLimit(true, MaximumBollardCount);
            bollard.AssetRelativePlacement.ConfigureTag(
                pathFunction,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                0.75f,
                AssetRelativeFacing.Any);
            bollard.AssetRelativePlacement.ConfigurePathStations(
                PathPlacementSide.BothSides,
                BollardSpacing,
                BollardPathOffset,
                2.5f,
                MaximumBollardStationCount);
            bollard.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);

            AssetDefinition campfire = definitions["Campfire"];
            ConfigureDefinition(
                campfire,
                new[] { outdoor, natural, gameplay, camping, restArea, ResolveSizeTag(campfire, sizeTags) },
                terrainSupport,
                PlacementType.Floor,
                OrientationMode.None,
                SurfaceFitMode.Adaptive,
                SurfaceAlignmentMode.KeepUpright,
                maxHeightDifference: 0.45f,
                minimumSupport: 0.75f,
                randomYaw: true,
                heightMode: SurfaceHeightMode.Lowest,
                sinkOffset: 0.02f);
            campfire.SetPlacementLimit(true, 1);
            campfire.AssetRelativePlacement.ConfigureTag(
                restArea,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                0.1f,
                AssetRelativeFacing.Any);
            campfire.AssetRelativePlacement.SetRequireInsideAnchorBounds(true);
            campfire.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);

            AssetDefinition fallenLog = definitions["Fallen Log"];
            ConfigureDefinition(
                fallenLog,
                new[] { outdoor, natural, vegetation, restArea, ResolveSizeTag(fallenLog, sizeTags) },
                terrainSupport,
                PlacementType.Floor,
                OrientationMode.None,
                SurfaceFitMode.Adaptive,
                SurfaceAlignmentMode.AlignToSurface,
                maxHeightDifference: 0.65f,
                minimumSupport: 0.55f,
                randomYaw: true,
                heightMode: SurfaceHeightMode.Lowest,
                sinkOffset: 0.05f);
            fallenLog.SetPrefabRotationOffset(new Vector3(0f, 0f, 90f));
            fallenLog.SetPlacementLimit(true, 2);
            fallenLog.AssetRelativePlacement.ConfigureAsset(
                campfire,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                1.25f,
                6f,
                AssetRelativeFacing.Any);
            fallenLog.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 2);

            AssetDefinition lilypad = definitions["Lilypad"];
            ConfigureDefinition(
                lilypad,
                new[] { outdoor, natural, vegetation, ResolveSizeTag(lilypad, sizeTags) },
                waterSupport,
                PlacementType.Floor,
                OrientationMode.None,
                SurfaceFitMode.Strict,
                SurfaceAlignmentMode.KeepUpright,
                maxHeightDifference: 0.05f,
                minimumSupport: 0.9f,
                randomYaw: true);
            lilypad.SetPlacementLimit(true, 8);

            AssetDefinition peugeot = definitions["Peugeot"];
            ConfigureDefinition(
                peugeot,
                new[] { outdoor, natural, vehicle, ResolveSizeTag(peugeot, sizeTags) },
                terrainSupport,
                PlacementType.Floor,
                OrientationMode.MatchSupportForward,
                SurfaceFitMode.Adaptive,
                SurfaceAlignmentMode.KeepUpright,
                maxHeightDifference: 0.3f,
                minimumSupport: 0.8f,
                randomYaw: false,
                sinkOffset: 0.04f);
            peugeot.SetPlacementLimit(true, 1);
            peugeot.AssetRelativePlacement.ConfigureTag(
                parkingSpot,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                0.1f,
                AssetRelativeFacing.MatchForward);
            peugeot.AssetRelativePlacement.SetRequireInsideAnchorBounds(true);
            peugeot.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);

            AssetDefinition trailSign = definitions["Trail Sign"];
            ConfigureDefinition(
                trailSign,
                new[] { outdoor, natural, signage, pathFunction, ResolveSizeTag(trailSign, sizeTags) },
                terrainSupport,
                PlacementType.Floor,
                OrientationMode.None,
                SurfaceFitMode.Adaptive,
                SurfaceAlignmentMode.KeepUpright,
                maxHeightDifference: 0.25f,
                minimumSupport: 0.8f,
                randomYaw: false);
            trailSign.SetPlacementLimit(true, 3);
            trailSign.PathPlacement.Configure(
                pathFunction,
                1.5f,
                3f,
                PathPlacementSide.Right,
                PathPlacementFacing.AlongPath,
                10f,
                3f);
            AssetSpacingRule trailSignSpacing = new();
            trailSignSpacing.ConfigureAsset(trailSign, 5f);
            trailSign.SetSpacingRules(new[] { trailSignSpacing });

            foreach (AssetDefinition definition in definitions.Values)
                EditorUtility.SetDirty(definition);

            IReadOnlyList<AssetDefinition> evaluationAssets = TemperateTreeNames
                .Concat(BushNames)
                .Concat(GroundRockNames)
                .Concat(new[]
                {
                    "Cliff Rock",
                    "Bench",
                    "Bollard",
                    "Campfire",
                    "Fallen Log",
                    "Lilypad",
                    "Peugeot",
                    "Trail Sign"
                })
                .Select(name => definitions[name])
                .ToArray();

            AssetPool pool = GetOrCreatePool(
                evaluationAssets,
                signage,
                furniture,
                vehicle,
                camping);
            GenerationPreset preset = GetOrCreatePreset(pool, waterSupport);

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene outdoorScene = SceneManager.GetSceneByPath(ScenePath);
            bool restoreScenes = !outdoorScene.IsValid() || !outdoorScene.isLoaded;

            if (restoreScenes)
            {
                if (!SaveLoadedScenes())
                    throw new InvalidOperationException("Save or close untitled scenes before preparing Outdoor evaluation.");

                outdoorScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            try
            {
                int bollardCount = ConfigureScene(
                    outdoorScene,
                    outdoor,
                    natural,
                    environmentCategory,
                    themeCategory,
                    terrainSupport,
                    pathSupport,
                    waterSupport,
                    pathFunction,
                    restArea,
                    parkingSpot);
                EditorSceneManager.MarkSceneDirty(outdoorScene);
                EditorSceneManager.SaveScene(outdoorScene);

                AssetDatabase.SaveAssets();
                ThesisEvaluationSuiteFactory.CreateOrRefresh(out string suiteSummary);
                AssetDatabase.SaveAssets();

                return $"Prepared Outdoor evaluation revision {SetupRevision}: 27 active asset definitions, " +
                       "three physical support classes and two semantic placement regions, " +
                       $"two reusable path sources with {bollardCount} constrained paired bollard placements, " +
                       $"{RequestedObjectCount} requested placements, and {suiteSummary}";
            }
            finally
            {
                if (restoreScenes && originalSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        internal static string PrepareRequested()
        {
            string summary = Prepare();
            AssetDatabase.DeleteAsset(RequestAssetPath);
            AssetDatabase.DeleteAsset(TriggerAssetPath);
            return summary;
        }

        private static void RunRequestedSetup()
        {
            if (!File.Exists(RequestAssetPath))
                return;

            try
            {
                string summary = PrepareRequested();
                Debug.Log(summary);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Outdoor evaluation setup failed: {exception}");
            }
        }

        private readonly struct PathFrame
        {
            private readonly IReadOnlyList<Vector3> points;
            private readonly Vector3 entry;
            private readonly Vector3 inward;

            public float MaximumProgress { get; }

            public PathFrame(
                IReadOnlyList<Vector3> points,
                Vector3 entry,
                Vector3 inward,
                float maximumProgress)
            {
                this.points = points;
                this.entry = entry;
                this.inward = inward;
                MaximumProgress = maximumProgress;
            }

            public Vector3 Sample(float progress)
            {
                float clamped = Mathf.Clamp(progress, 0f, MaximumProgress);
                const float window = 0.8f;
                IReadOnlyList<Vector3> sourcePoints = points;
                Vector3 sourceEntry = entry;
                Vector3 sourceInward = inward;
                Vector3[] nearby = sourcePoints
                    .Where(point => Mathf.Abs(Vector3.Dot(point - sourceEntry, sourceInward) - clamped) <= window)
                    .ToArray();
                if (nearby.Length == 0)
                {
                    nearby = sourcePoints
                        .OrderBy(point => Mathf.Abs(Vector3.Dot(point - sourceEntry, sourceInward) - clamped))
                        .Take(16)
                        .ToArray();
                }

                Vector3 result = Average(nearby);
                result.y = nearby.Max(point => point.y);
                return result;
            }

            public Vector3 Tangent(float progress)
            {
                Vector3 before = Sample(Mathf.Max(0f, progress - 1.25f));
                Vector3 after = Sample(Mathf.Min(MaximumProgress, progress + 1.25f));
                Vector3 tangent = Vector3.ProjectOnPlane(after - before, Vector3.up).normalized;
                if (tangent.sqrMagnitude <= 0.001f)
                    tangent = inward;
                if (Vector3.Dot(tangent, inward) < 0f)
                    tangent = -tangent;
                return tangent;
            }
        }

        private readonly struct PathStation
        {
            public Vector3 Position { get; }
            public Vector3 Forward { get; }

            public PathStation(Vector3 position, Vector3 forward)
            {
                Position = position;
                Forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            }
        }
    }
}
