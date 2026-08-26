using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Genix.Editor.Assets;
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
    internal static class OutdoorEvaluationSetupUtility
    {
        internal const string ScenePath =
            "Packages/com.possiphil.genix/Evaluation/Scenes/RealWorld/OutdoorEnvironment.unity";
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

        [MenuItem("Tools/Genix/Evaluation/Prepare Outdoor Evaluation")]
        private static void RunFromMenu()
        {
            try
            {
                string summary = Prepare();
                Debug.Log(summary);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
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

        private static int ConfigureScene(
            Scene scene,
            SemanticTag outdoor,
            SemanticTag natural,
            TagCategory environmentCategory,
            TagCategory themeCategory,
            SemanticTag terrainSupport,
            SemanticTag pathSupport,
            SemanticTag waterSupport,
            SemanticTag pathFunction,
            SemanticTag restArea,
            SemanticTag parkingSpot)
        {
            GameObject terrainObject = FindSceneObject(scene, "Terrain");
            GameObject pathObject = FindSceneObject(scene, "Path");
            GameObject waterObject = FindSceneObject(scene, "Water");
            GameObject boundaryLeft = FindSceneObject(scene, "Boundary Left");
            if (!terrainObject || !pathObject || !waterObject || !boundaryLeft)
            {
                throw new InvalidOperationException(
                    "OutdoorEnvironment must contain Terrain, Path, Water, and Boundary Left objects.");
            }

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            BoxCollider boundary = boundaryLeft.GetComponent<BoxCollider>();
            Collider waterCollider = waterObject.GetComponent<Collider>();
            if (!terrain || !boundary || !waterCollider)
            {
                throw new InvalidOperationException(
                    "Outdoor Terrain, Water collider, or Boundary Left collider is missing.");
            }

            int placementLayer = LayerMask.NameToLayer("Placement Surface");
            if (placementLayer < 0)
                placementLayer = 8;

            SetLayerRecursively(terrainObject, placementLayer);
            SetLayerRecursively(pathObject, placementLayer);
            SetLayerRecursively(waterObject, placementLayer);
            ConfigureSurface(terrainObject, terrainSupport);
            RemoveSurfaceDescriptor(pathObject);
            RemoveExclusionRegions(pathObject);
            RemoveSurfaceDescriptor(waterObject);

            Transform[] pathSegments = GetPathSegments(pathObject);
            if (pathSegments.Length == 0)
                throw new InvalidOperationException("Outdoor Path contains no spline segments.");

            foreach (Transform segment in pathSegments)
            {
                ConfigureSurface(segment.gameObject, pathSupport);
                PlacementExclusionRegion exclusion =
                    segment.GetComponent<PlacementExclusionRegion>() ??
                    segment.gameObject.AddComponent<PlacementExclusionRegion>();
                exclusion.ConfigureChildColliders(PlacementTarget.Floor);
                exclusion.SetExemptAssetTags(new[] { pathFunction });
                EditorUtility.SetDirty(exclusion);
            }

            Transform sceneRoot = pathObject.transform.root;
            Transform previousSemanticRoot = sceneRoot.Find(SemanticRootName);
            if (previousSemanticRoot)
                UnityEngine.Object.DestroyImmediate(previousSemanticRoot.gameObject);

            GameObject semanticRootObject = new(SemanticRootName);
            SceneManager.MoveGameObjectToScene(semanticRootObject, scene);
            semanticRootObject.transform.SetParent(sceneRoot, false);
            Transform semanticRoot = semanticRootObject.transform;

            ConfigureBridgeExclusion(pathObject, pathSegments, semanticRoot);

            IReadOnlyList<(Transform Segment, PathFrame Frame)> pathFrames = pathSegments
                .Select(segment => (segment, CreatePathFrame(CollectPathPoints(segment.gameObject), boundary, terrain)))
                .OrderByDescending(entry => entry.Item2.MaximumProgress)
                .ToArray();
            IReadOnlyList<PathFrame> frames = pathFrames
                .Select(entry => entry.Frame)
                .OrderByDescending(frame => frame.MaximumProgress)
                .ToArray();
            foreach ((Transform segment, PathFrame segmentFrame) in pathFrames)
            {
                PathPlacementSource source = segment.GetComponent<PathPlacementSource>() ??
                                             segment.gameObject.AddComponent<PathPlacementSource>();
                source.SetPathTags(new[] { pathFunction });
                source.SetWorldPoints(CreatePathStations(new[] { segmentFrame }, 0.5f, 0f)
                    .Select(station => station.Position));
                EditorUtility.SetDirty(source);
            }

            PathFrame frame = frames[0];
            float side = ChooseTrailSide(frame, terrain, waterCollider);

            float parkingProgress = Mathf.Clamp(frame.MaximumProgress * 0.12f, 4f, 6f);
            CreatePathRegionAnchor(
                semanticRoot,
                "Parking Region",
                frame,
                terrain,
                parkingProgress,
                side,
                lateralDistance: 6.5f,
                size: new Vector2(5.5f, 9f),
                parkingSpot,
                facePath: false);

            const float restAreaFraction = 0.58f;
            float restAreaSide = ResolveUsableSide(
                frame,
                terrain,
                waterCollider,
                restAreaFraction,
                -side);
            float restProgress = Mathf.Clamp(
                frame.MaximumProgress * restAreaFraction,
                4f,
                frame.MaximumProgress - 1f);
            CreatePathRegionAnchor(
                semanticRoot,
                "Rest Area Region",
                frame,
                terrain,
                restProgress,
                restAreaSide,
                lateralDistance: 7f,
                size: new Vector2(12f, 8f),
                restArea,
                facePath: true);
            CreateWaterPlacementRegion(
                semanticRoot,
                terrain,
                waterCollider,
                waterSupport,
                placementLayer);

            Transform locationAnchor = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Environment" && item.parent && item.parent.name == "Anchors");
            if (locationAnchor)
            {
                SemanticTagSet tagSet = locationAnchor.GetComponent<SemanticTagSet>() ??
                                        locationAnchor.gameObject.AddComponent<SemanticTagSet>();
                tagSet.SetTagsForCategory(environmentCategory, new[] { outdoor });
                tagSet.SetTagsForCategory(themeCategory, new[] { natural });
                EditorUtility.SetDirty(tagSet);
            }

            return MaximumBollardCount;
        }

        private static AssetPool GetOrCreatePool(
            IReadOnlyList<AssetDefinition> assets,
            SemanticTag signage,
            SemanticTag furniture,
            SemanticTag vehicle,
            SemanticTag camping)
        {
            AssetPool pool = AssetDatabase.LoadAssetAtPath<AssetPool>(PoolPath);
            if (!pool)
            {
                pool = ScriptableObject.CreateInstance<AssetPool>();
                pool.Initialize("Eval Outdoor Pool", AssetPoolMode.Static);
                AssetDatabase.CreateAsset(pool, PoolPath);
            }

            pool.Initialize("Eval Outdoor Pool", AssetPoolMode.Static);
            SetObjectArray(new SerializedObject(pool), "staticAssets", assets.Cast<UnityEngine.Object>());

            AssetPoolTagLimit signLimit = new();
            signLimit.Configure(signage, 3, 3);
            AssetPoolTagLimit benchLimit = new();
            benchLimit.Configure(furniture, 2, 2);
            AssetPoolTagLimit vehicleLimit = new();
            vehicleLimit.Configure(vehicle, 1, 1);
            AssetPoolTagLimit campLimit = new();
            campLimit.Configure(camping, 1, 1);
            pool.SetTagPlacementLimits(new[]
            {
                signLimit,
                benchLimit,
                vehicleLimit,
                campLimit
            });
            pool.SetAnchorGroupLimits(Array.Empty<AssetPoolAnchorGroupLimit>());
            EditorUtility.SetDirty(pool);
            return pool;
        }

        private static GenerationPreset GetOrCreatePreset(AssetPool pool, SemanticTag water)
        {
            StylePreset style = AssetDatabase.LoadAssetAtPath<StylePreset>(NaturalStylePath);
            if (!style)
                throw new InvalidOperationException("Natural style preset is missing.");

            SupportDistributionSettings supportDistribution = new(
                true,
                1,
                new[]
                {
                    new SupportDistributionRule(water, SupportDistributionRuleMode.ExactCount, 8)
                });
            LayerMask placementLayer = 1 << 8;
            GenerationPresetSettings settings = new(
                pool,
                style,
                RequestedObjectCount,
                PlacementTarget.Floor | PlacementTarget.Wall,
                TargetDistributionMode.Weighted,
                new TargetDistributionWeights(FloorCount, WallCount, 0, 0),
                AreaDecompositionMode.Fast,
                SurfaceDiscoveryMode.AllMatchingSurfacesInVolume,
                placementLayer,
                placementLayer,
                placementLayer,
                45f,
                60f,
                RelativePlacementSource.None,
                2f,
                ~0,
                true,
                12345,
                true,
                supportDistribution);

            GenerationPreset preset = AssetDatabase.LoadAssetAtPath<GenerationPreset>(PresetPath);
            if (!preset)
            {
                preset = ScriptableObject.CreateInstance<GenerationPreset>();
                preset.name = "OutdoorEval";
                AssetDatabase.CreateAsset(preset, PresetPath);
            }

            preset.Apply(settings);
            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static void ConfigureDefinition(
            AssetDefinition definition,
            IEnumerable<SemanticTag> semanticTags,
            SemanticTag requiredSupport,
            PlacementType placementType,
            OrientationMode orientation,
            SurfaceFitMode fit,
            SurfaceAlignmentMode alignment,
            float maxHeightDifference,
            float minimumSupport,
            bool randomYaw,
            bool randomRoll = false,
            SurfaceHeightMode heightMode = SurfaceHeightMode.Average,
            float sinkOffset = 0f)
        {
            SerializedObject serialized = new(definition);
            SetObjectArray(serialized, "semanticTags", semanticTags.Cast<UnityEngine.Object>(), apply: false);
            SetObjectArray(serialized, "anyTagCategories", Array.Empty<UnityEngine.Object>(), apply: false);
            SetEnum(serialized, "placementType", placementType);
            SetEnum(serialized, "orientationMode", orientation);
            SetEnum(serialized, "surfaceFitMode", fit);
            SetEnum(serialized, "surfaceAlignmentMode", alignment);
            SetEnum(serialized, "surfaceHeightMode", heightMode);
            SetEnum(serialized, "wallVerticalPlacementMode", WallVerticalPlacementMode.FullWall);
            SetFloat(serialized, "placementHeight", 0f);
            SetFloat(serialized, "maxSurfaceHeightDifference", maxHeightDifference);
            SetFloat(serialized, "minSurfaceSupport", minimumSupport);
            SetFloat(serialized, "surfaceSinkOffset", sinkOffset);
            SetBool(serialized, "randomYawRotation", randomYaw);
            SetBool(serialized, "randomPitchRotation", false);
            SetBool(serialized, "randomRollRotation", randomRoll);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            definition.SetRequiredSupportTags(new[] { requiredSupport });
            definition.SetForbiddenSupportTags(Array.Empty<SemanticTag>());
            definition.SetRequiredSupportNoneCategories(Array.Empty<TagCategory>());
            definition.SetForbiddenSupportAnyCategories(Array.Empty<TagCategory>());
            definition.SetSpacingRules(Array.Empty<AssetSpacingRule>());
            definition.SetPlacementLimit(false, 1);
            definition.SetWallProximity(WallProximityMode.AnyDistance, 0f);
            definition.AssetRelativePlacement.Disable();
            definition.AssetRelativePlacement.DisablePathStations();
            definition.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Unlimited, 1);
            definition.PathPlacement.Disable();
            definition.RemoveMissingTags();
            EditorUtility.SetDirty(definition);
        }

        private static void ConfigureSurface(GameObject gameObject, SemanticTag supportTag)
        {
            PlacementSurfaceDescriptor descriptor = gameObject.GetComponent<PlacementSurfaceDescriptor>() ??
                                                    gameObject.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetSurfaceTags(new[] { supportTag });
            descriptor.SetAllowedAssetTags(Array.Empty<SemanticTag>());
            descriptor.SetForbiddenAssetTags(Array.Empty<SemanticTag>());
            descriptor.SetCapacity(false, 0);
            descriptor.SetAssetCapacityRules(Array.Empty<PlacementSurfaceCapacityRule>());
            EditorUtility.SetDirty(descriptor);
        }

        private static void RemoveSurfaceDescriptor(GameObject gameObject)
        {
            foreach (PlacementSurfaceDescriptor descriptor in
                     gameObject.GetComponents<PlacementSurfaceDescriptor>())
            {
                UnityEngine.Object.DestroyImmediate(descriptor);
            }
        }

        private static void RemoveExclusionRegions(GameObject gameObject)
        {
            foreach (PlacementExclusionRegion region in gameObject.GetComponents<PlacementExclusionRegion>())
                UnityEngine.Object.DestroyImmediate(region);
        }

        private static void CreatePathRegionAnchor(
            Transform parent,
            string name,
            PathFrame frame,
            Terrain terrain,
            float progress,
            float side,
            float lateralDistance,
            Vector2 size,
            SemanticTag regionTag,
            bool facePath)
        {
            Vector3 center = frame.Sample(progress);
            Vector3 tangent = frame.Tangent(progress);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 position = GetTerrainPoint(terrain, center + right * lateralDistance * side);
            Vector3 forward = facePath ? -right * side : tangent;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude <= 0.001f)
                forward = tangent;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            position.y += 0.05f;

            GameObject region = new(name);
            region.transform.SetParent(parent, true);
            region.transform.SetPositionAndRotation(
                position,
                rotation);
            AssetRelationAnchor anchor = region.AddComponent<AssetRelationAnchor>();
            anchor.SetAssetTags(new[] { regionTag });
            anchor.SetCustomBounds(
                true,
                Vector3.zero,
                new Vector3(size.x, 20f, size.y));
            EditorUtility.SetDirty(anchor);
        }

        private static void CreateWaterPlacementRegion(
            Transform parent,
            Terrain terrain,
            Collider water,
            SemanticTag supportTag,
            int layer)
        {
            Bounds waterBounds = water.bounds;
            Bounds terrainBounds = new(
                terrain.transform.position + terrain.terrainData.size * 0.5f,
                terrain.terrainData.size);
            const float edgeInset = 0.35f;
            float minimumX = waterBounds.min.x + edgeInset;
            float maximumX = waterBounds.max.x - edgeInset;
            float minimumZ = waterBounds.min.z + edgeInset;
            float maximumZ = waterBounds.max.z - edgeInset;
            float surfaceY = waterBounds.max.y;
            Vector3 origin = new(waterBounds.center.x, surfaceY + 0.03f, waterBounds.center.z);

            List<Vector3> vertices = new();
            List<int> triangles = new();
            for (int z = 0; z < WaterGridResolution; z++)
            {
                float z0 = Mathf.Lerp(minimumZ, maximumZ, z / (float)WaterGridResolution);
                float z1 = Mathf.Lerp(minimumZ, maximumZ, (z + 1f) / WaterGridResolution);
                for (int x = 0; x < WaterGridResolution; x++)
                {
                    float x0 = Mathf.Lerp(minimumX, maximumX, x / (float)WaterGridResolution);
                    float x1 = Mathf.Lerp(minimumX, maximumX, (x + 1f) / WaterGridResolution);
                    if (!IsExposedWaterCell(terrain, terrainBounds, surfaceY, x0, x1, z0, z1))
                        continue;

                    int first = vertices.Count;
                    vertices.Add(new Vector3(x0 - origin.x, 0f, z0 - origin.z));
                    vertices.Add(new Vector3(x1 - origin.x, 0f, z0 - origin.z));
                    vertices.Add(new Vector3(x0 - origin.x, 0f, z1 - origin.z));
                    vertices.Add(new Vector3(x1 - origin.x, 0f, z1 - origin.z));
                    triangles.Add(first);
                    triangles.Add(first + 2);
                    triangles.Add(first + 1);
                    triangles.Add(first + 1);
                    triangles.Add(first + 2);
                    triangles.Add(first + 3);
                }
            }

            if (triangles.Count == 0)
                throw new InvalidOperationException("Outdoor Water exposes no stable continuous placement region.");

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(WaterPlacementMeshPath);
            if (!mesh)
            {
                mesh = new Mesh { name = "Outdoor Water Placement Region" };
                AssetDatabase.CreateAsset(mesh, WaterPlacementMeshPath);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            GameObject region = new("Water Placement Region");
            region.layer = layer;
            region.transform.SetParent(parent, true);
            region.transform.SetPositionAndRotation(origin, Quaternion.identity);
            MeshCollider regionCollider = region.AddComponent<MeshCollider>();
            regionCollider.sharedMesh = mesh;
            regionCollider.convex = false;
            ConfigureSurface(region, supportTag);
        }

        private static bool IsExposedWaterCell(
            Terrain terrain,
            Bounds terrainBounds,
            float surfaceY,
            float x0,
            float x1,
            float z0,
            float z1)
        {
            return IsExposedWaterPoint(terrain, terrainBounds, surfaceY, x0, z0) &&
                   IsExposedWaterPoint(terrain, terrainBounds, surfaceY, x1, z0) &&
                   IsExposedWaterPoint(terrain, terrainBounds, surfaceY, x0, z1) &&
                   IsExposedWaterPoint(terrain, terrainBounds, surfaceY, x1, z1);
        }

        private static bool IsExposedWaterPoint(
            Terrain terrain,
            Bounds terrainBounds,
            float surfaceY,
            float x,
            float z)
        {
            if (x < terrainBounds.min.x || x > terrainBounds.max.x ||
                z < terrainBounds.min.z || z > terrainBounds.max.z)
            {
                return false;
            }

            Vector3 point = new(x, surfaceY, z);
            float terrainY = terrain.SampleHeight(point) + terrain.transform.position.y;
            return surfaceY - terrainY >= MinimumWaterDepth;
        }

        private static PathFrame CreatePathFrame(
            IReadOnlyList<Vector3> points,
            BoxCollider boundary,
            Terrain terrain)
        {
            if (points.Count < 4)
                throw new InvalidOperationException("Path meshes did not expose enough geometry for trailhead setup.");

            Vector3 terrainCenter = terrain.transform.position + terrain.terrainData.size * 0.5f;
            Vector3 inward = Vector3.ProjectOnPlane(terrainCenter - boundary.bounds.center, Vector3.up).normalized;
            if (inward.sqrMagnitude <= 0.001f)
                inward = Vector3.right;

            float minimum = points.Min(point => Vector3.Dot(point, inward));
            Vector3 entry = Average(points.Where(point => Vector3.Dot(point, inward) <= minimum + 1.1f));
            float maximumProgress = points.Max(point => Vector3.Dot(point - entry, inward));
            if (maximumProgress < 2f)
                throw new InvalidOperationException("Path does not extend far enough away from Boundary Left.");

            return new PathFrame(points, entry, inward, maximumProgress);
        }

        private static Transform[] GetPathSegments(GameObject path) => path.transform
            .Cast<Transform>()
            .Where(child => child.GetComponents<Component>().Any(component =>
                component && component.GetType().FullName == "UnityEngine.Splines.SplineContainer"))
            .ToArray();

        private static void ConfigureBridgeExclusion(
            GameObject path,
            IReadOnlyCollection<Transform> pathSegments,
            Transform semanticRoot)
        {
            HashSet<Transform> segments = new(pathSegments);
            Transform[] bridgeRoots = path.transform
                .Cast<Transform>()
                .Where(child => !segments.Contains(child))
                .ToArray();

            Bounds combined = default;
            bool hasBounds = false;
            foreach (Transform bridgeRoot in bridgeRoots)
            {
                RemoveSurfaceDescriptor(bridgeRoot.gameObject);
                if (!BoundsUtility.TryGetRendererBounds(bridgeRoot, out Bounds bounds, true, false))
                    continue;

                if (!hasBounds)
                {
                    combined = bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(bounds);
                }
            }

            if (!hasBounds)
                return;

            GameObject exclusionObject = new("Bridge Exclusion Region");
            exclusionObject.transform.SetParent(semanticRoot, true);
            exclusionObject.transform.SetPositionAndRotation(combined.center, Quaternion.identity);
            PlacementExclusionRegion exclusion = exclusionObject.AddComponent<PlacementExclusionRegion>();
            exclusion.ConfigureBox(
                Vector3.zero,
                combined.size + new Vector3(0.4f, 0.4f, 0.4f),
                PlacementTarget.Floor | PlacementTarget.Wall);
            exclusion.SetExemptAssetTags(Array.Empty<SemanticTag>());
            EditorUtility.SetDirty(exclusion);
        }

        private static List<PathStation> CreatePathStations(
            IEnumerable<PathFrame> frames,
            float spacing,
            float endpointMargin)
        {
            List<PathStation> stations = new();
            foreach (PathFrame frame in frames)
            {
                List<Vector3> polyline = new();
                const float sampleStep = 0.5f;
                for (float progress = 0f; progress < frame.MaximumProgress; progress += sampleStep)
                    AddDistinctPoint(polyline, frame.Sample(progress));
                AddDistinctPoint(polyline, frame.Sample(frame.MaximumProgress));

                if (polyline.Count < 2)
                    continue;

                float[] distances = new float[polyline.Count];
                for (int i = 1; i < polyline.Count; i++)
                {
                    distances[i] = distances[i - 1] +
                                   Vector3.ProjectOnPlane(polyline[i] - polyline[i - 1], Vector3.up).magnitude;
                }

                float length = distances[^1];
                if (length < 1f)
                    continue;

                float firstDistance = Mathf.Min(endpointMargin, length * 0.5f);
                float lastDistance = Mathf.Max(firstDistance, length - endpointMargin);
                if (lastDistance - firstDistance < spacing * 0.5f)
                {
                    stations.Add(SamplePolyline(polyline, distances, length * 0.5f));
                    continue;
                }

                for (float distance = firstDistance; distance <= lastDistance + 0.01f; distance += spacing)
                    stations.Add(SamplePolyline(polyline, distances, distance));
            }

            return stations;
        }

        private static PathStation SamplePolyline(
            IReadOnlyList<Vector3> points,
            IReadOnlyList<float> distances,
            float distance)
        {
            int next = 1;
            while (next < distances.Count && distances[next] < distance)
                next++;
            next = Mathf.Clamp(next, 1, points.Count - 1);
            int previous = next - 1;
            float segmentLength = Mathf.Max(0.0001f, distances[next] - distances[previous]);
            float t = Mathf.Clamp01((distance - distances[previous]) / segmentLength);
            Vector3 position = Vector3.Lerp(points[previous], points[next], t);
            Vector3 forward = Vector3.ProjectOnPlane(points[next] - points[previous], Vector3.up).normalized;
            return new PathStation(position, forward);
        }

        private static void AddDistinctPoint(List<Vector3> points, Vector3 point)
        {
            if (points.Count == 0 ||
                Vector3.ProjectOnPlane(points[^1] - point, Vector3.up).sqrMagnitude > 0.01f)
            {
                points.Add(point);
            }
        }

        private static IReadOnlyList<Vector3> CollectPathPoints(GameObject path)
        {
            List<Vector3> points = new();
            foreach (MeshFilter filter in path.GetComponentsInChildren<MeshFilter>(true))
                AddMeshPoints(filter.sharedMesh, filter.transform, points);

            foreach (MeshCollider collider in path.GetComponentsInChildren<MeshCollider>(true))
                AddMeshPoints(collider.sharedMesh, collider.transform, points);

            if (points.Count > 0)
                return points;

            foreach (Renderer renderer in path.GetComponentsInChildren<Renderer>(true))
            {
                Bounds bounds = renderer.bounds;
                points.Add(bounds.min);
                points.Add(bounds.max);
                points.Add(new Vector3(bounds.min.x, bounds.center.y, bounds.max.z));
                points.Add(new Vector3(bounds.max.x, bounds.center.y, bounds.min.z));
            }

            return points;
        }

        private static void AddMeshPoints(Mesh mesh, Transform transform, ICollection<Vector3> points)
        {
            if (!mesh)
                return;

            Vector3[] vertices = mesh.vertices;
            int step = Mathf.Max(1, vertices.Length / 5000);
            for (int i = 0; i < vertices.Length; i += step)
                points.Add(transform.TransformPoint(vertices[i]));
        }

        private static float ChooseTrailSide(PathFrame frame, Terrain terrain, Collider water)
        {
            float progress = Mathf.Clamp(frame.MaximumProgress * 0.08f, 2f, 4.5f);
            float positive = ScoreSide(frame, terrain, water, progress, 1f, 3.2f);
            float negative = ScoreSide(frame, terrain, water, progress, -1f, 3.2f);
            return positive >= negative ? 1f : -1f;
        }

        private static float ResolveUsableSide(
            PathFrame frame,
            Terrain terrain,
            Collider water,
            float progressFraction,
            float preferredSide)
        {
            float progress = Mathf.Clamp(frame.MaximumProgress * progressFraction, 2f, frame.MaximumProgress - 1f);
            float preferred = ScoreSide(frame, terrain, water, progress, preferredSide, 2.45f);
            float opposite = ScoreSide(frame, terrain, water, progress, -preferredSide, 2.45f);
            return preferred >= opposite ? preferredSide : -preferredSide;
        }

        private static float ScoreSide(
            PathFrame frame,
            Terrain terrain,
            Collider water,
            float progress,
            float side,
            float offset)
        {
            Vector3 center = frame.Sample(progress);
            Vector3 right = Vector3.Cross(Vector3.up, frame.Tangent(progress)).normalized;
            Vector3 point = center + right * offset * side;
            Bounds terrainBounds = new(
                terrain.transform.position + terrain.terrainData.size * 0.5f,
                terrain.terrainData.size);
            if (!terrainBounds.Contains(new Vector3(point.x, terrainBounds.center.y, point.z)))
                return float.NegativeInfinity;

            Vector3 terrainPoint = GetTerrainPoint(terrain, point);
            float score = -GetTerrainSteepness(terrain, terrainPoint);
            if (water && water.bounds.Contains(new Vector3(point.x, water.bounds.center.y, point.z)) &&
                terrainPoint.y <= water.bounds.max.y + 0.15f)
            {
                score -= 1000f;
            }

            return score;
        }

        private static Vector3 GetTerrainPoint(Terrain terrain, Vector3 position)
        {
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        private static float GetTerrainSteepness(Terrain terrain, Vector3 position)
        {
            Vector3 local = position - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return terrain.terrainData.GetSteepness(
                Mathf.Clamp01(local.x / Mathf.Max(0.01f, size.x)),
                Mathf.Clamp01(local.z / Mathf.Max(0.01f, size.z)));
        }

        private static Vector3 Average(IEnumerable<Vector3> values)
        {
            Vector3 total = Vector3.zero;
            int count = 0;
            foreach (Vector3 value in values)
            {
                total += value;
                count++;
            }

            return count > 0 ? total / count : Vector3.zero;
        }

        private static Dictionary<string, AssetDefinition> LoadDefinitions(IEnumerable<string> names)
        {
            Dictionary<string, AssetDefinition> definitions = new(StringComparer.Ordinal);
            List<string> missing = new();
            foreach (string name in names.Distinct())
            {
                AssetDefinition definition = AssetDatabase.LoadAssetAtPath<AssetDefinition>(
                    $"{DefinitionFolder}/{name}.asset");
                if (definition)
                    definitions[name] = definition;
                else
                    missing.Add(name);
            }

            if (missing.Count > 0)
                throw new InvalidOperationException($"Missing Outdoor Genix assets: {string.Join(", ", missing)}.");

            return definitions;
        }

        private static SemanticTag ResolveSizeTag(
            AssetDefinition definition,
            IReadOnlyDictionary<string, SemanticTag> tags)
        {
            float largest = Mathf.Max(definition.BoundsSize.x, definition.BoundsSize.y, definition.BoundsSize.z);
            string key = largest switch
            {
                < 0.75f => "Tiny",
                < 1.5f => "Small",
                < 3f => "Medium",
                < 6f => "Large",
                _ => "Huge"
            };
            return tags[key];
        }

        private static TagCategory LoadCategory(string name)
        {
            TagCategory category = AssetDatabase.LoadAssetAtPath<TagCategory>($"{CategoryFolder}/{name}.asset");
            return category ? category : throw new InvalidOperationException($"Tag category '{name}' is missing.");
        }

        private static SemanticTag LoadTag(string category, string name)
        {
            SemanticTag tag = AssetDatabase.LoadAssetAtPath<SemanticTag>($"{TagFolder}/{category}/{name}.asset");
            return tag ? tag : throw new InvalidOperationException($"Semantic tag '{category}/{name}' is missing.");
        }

        private static SemanticTag GetOrCreateTag(string categoryName, string name, TagCategory category)
        {
            string folder = $"{TagFolder}/{categoryName}";
            EnsureFolder(folder);
            string path = $"{folder}/{name}.asset";
            SemanticTag tag = AssetDatabase.LoadAssetAtPath<SemanticTag>(path);
            if (!tag)
            {
                tag = ScriptableObject.CreateInstance<SemanticTag>();
                tag.name = name;
                tag.Initialize(category);
                AssetDatabase.CreateAsset(tag, path);
            }
            else
            {
                tag.SetCategory(category);
            }

            EditorUtility.SetDirty(tag);
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();
            catalog.AddTag(tag);
            EditorUtility.SetDirty(catalog);
            return tag;
        }

        private static GameObject FindSceneObject(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(gameObject => gameObject.name == name);

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        private static bool SaveLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && string.IsNullOrWhiteSpace(scene.path))
                    return false;
            }

            return EditorSceneManager.SaveOpenScenes();
        }

        private static void SetObjectArray(
            SerializedObject serialized,
            string propertyName,
            IEnumerable<UnityEngine.Object> values,
            bool apply = true)
        {
            SerializedProperty property = serialized.FindProperty(propertyName) ??
                                          throw new InvalidOperationException(
                                              $"Serialized property '{propertyName}' is missing on {serialized.targetObject.name}.");
            UnityEngine.Object[] normalized = values?.Where(value => value).Distinct().ToArray() ??
                                                Array.Empty<UnityEngine.Object>();
            property.arraySize = normalized.Length;
            for (int i = 0; i < normalized.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = normalized[i];

            if (apply)
                serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum<T>(SerializedObject serialized, string name, T value) where T : struct, Enum =>
            serialized.FindProperty(name).enumValueIndex = Convert.ToInt32(value);

        private static void SetFloat(SerializedObject serialized, string name, float value) =>
            serialized.FindProperty(name).floatValue = value;

        private static void SetBool(SerializedObject serialized, string name, bool value) =>
            serialized.FindProperty(name).boolValue = value;

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"Invalid Unity asset folder '{path}'.");

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
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
