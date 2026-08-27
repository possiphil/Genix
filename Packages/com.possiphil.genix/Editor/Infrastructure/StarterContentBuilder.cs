using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Genix.Editor.Assets;
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
    internal static class StarterContentBuilder
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

        private static StarterTaxonomy BuildTaxonomy(StarterContentBuildResult result)
        {
            TagCategory environment = EnsureCategory("Environment", TagCategoryUsage.Asset, true, result);
            TagCategory support = EnsureCategory("Support Type", TagCategoryUsage.Surface, true, result);
            TagCategory role = EnsureCategory("Role", TagCategoryUsage.Asset, true, result);
            TagCategory function = EnsureCategory("Function", TagCategoryUsage.Asset, true, result);
            TagCategory theme = EnsureCategory("Theme", TagCategoryUsage.Asset, true, result);
            TagCategory size = EnsureCategory("Size", TagCategoryUsage.Asset, false, result);

            return new StarterTaxonomy
            {
                Indoor = EnsureTag("Indoor", environment, result),
                Outdoor = EnsureTag("Outdoor", environment, result),
                Floor = EnsureTag("Floor", support, result),
                Wall = EnsureTag("Wall", support, result),
                Ceiling = EnsureTag("Ceiling", support, result),
                Desktop = EnsureTag("Desktop", support, result),
                Shelf = EnsureTag("Shelf", support, result),
                Terrain = EnsureTag("Terrain", support, result),
                Path = EnsureTag("Path", support, result),
                Water = EnsureTag("Water", support, result),
                Prop = EnsureTag("Prop", role, result),
                Furniture = EnsureTag("Furniture", role, result),
                Decoration = EnsureTag("Decoration", role, result),
                Lighting = EnsureTag("Lighting", role, result),
                Signage = EnsureTag("Signage", role, result),
                Structure = EnsureTag("Structure", role, result),
                Vegetation = EnsureTag("Vegetation", role, result),
                Display = EnsureTag("Display", function, result),
                Utility = EnsureTag("Utility", function, result),
                FunctionPath = EnsureTag("Path", function, result),
                RestArea = EnsureTag("Rest Area", function, result),
                Natural = EnsureTag("Natural", theme, result),
                Industrial = EnsureTag("Industrial", theme, result),
                Minimal = EnsureTag("Minimal", theme, result),
                Urban = EnsureTag("Urban", theme, result),
                SciFi = EnsureTag("Sci-Fi", theme, result),
                Fantasy = EnsureTag("Fantasy", theme, result),
                Tiny = EnsureTag("Tiny", size, result),
                Small = EnsureTag("Small", size, result),
                Medium = EnsureTag("Medium", size, result),
                Large = EnsureTag("Large", size, result),
                Huge = EnsureTag("Huge", size, result)
            };
        }

        private static Dictionary<string, StylePreset> BuildStyles(StarterContentBuildResult result)
        {
            string[] names = { "Freeform", "Structured", "Organic", "Clustered", "Natural" };
            Dictionary<string, StylePreset> styles = new(StringComparer.OrdinalIgnoreCase);

            foreach (string name in names)
                styles[name] = EnsureStyle(name, CreateStyleSettings(name), result);

            return styles;
        }

        private static StyleSettings CreateStyleSettings(string name)
        {
            return name switch
            {
                "Structured" => new StyleSettings(
                    "Clean and regular placement on a visible grid.",
                    SamplingAlgorithm.Grid,
                    new PlacementSettings(false, 1f),
                    new CandidateSettings(1, 1, false),
                    new GridSettings(1f, 0f),
                    default,
                    default),
                "Organic" => new StyleSettings(
                    "Natural-looking placement with slight random variation.",
                    SamplingAlgorithm.JitteredGrid,
                    default,
                    new CandidateSettings(1, 1, false),
                    new GridSettings(0.75f, 0.25f),
                    default,
                    default),
                "Clustered" => new StyleSettings(
                    "Places objects in small groups instead of distributing them evenly.",
                    SamplingAlgorithm.Cluster,
                    default,
                    new CandidateSettings(25, 150, true),
                    default,
                    new ClusterSettings(4, 1.5f, true, 3.5f),
                    default),
                "Natural" => new StyleSettings(
                    "Evenly spaced organic placement with a minimum distance between objects.",
                    SamplingAlgorithm.BridsonPoissonDisk,
                    default,
                    new CandidateSettings(20, 150, true),
                    default,
                    default,
                    new PoissonSettings(1.2f, 30)),
                _ => new StyleSettings(
                    "Highly random placement using random sampling with validation.",
                    SamplingAlgorithm.Random,
                    default,
                    new CandidateSettings(50, 250, true),
                    default,
                    default,
                    default)
            };
        }

        private static StarterMaterials BuildMaterials(StarterContentBuildResult result)
        {
            return new StarterMaterials
            {
                Wall = EnsureMaterial("Wall", new Color32(215, 225, 235, 255), 0f, result),
                Floor = EnsureMaterial("Floor", new Color32(116, 130, 142, 255), 0f, result),
                Wood = EnsureMaterial("Wood", new Color32(92, 60, 43, 255), 0f, result),
                Dark = EnsureMaterial("Dark", new Color32(35, 39, 44, 255), 0.15f, result),
                Light = EnsureMaterial("Light", new Color32(228, 232, 235, 255), 0.05f, result),
                Blue = EnsureMaterial("Blue", new Color32(53, 112, 160, 255), 0.05f, result),
                Yellow = EnsureMaterial("Yellow", new Color32(230, 174, 45, 255), 0.05f, result),
                Orange = EnsureMaterial("Orange", new Color32(185, 94, 42, 255), 0.05f, result),
                Red = EnsureMaterial("Red", new Color32(180, 53, 49, 255), 0.05f, result)
            };
        }

        private static StarterPrefabs BuildPrefabs(
            StarterMaterials materials,
            StarterTaxonomy taxonomy,
            StarterContentBuildResult result)
        {
            return new StarterPrefabs
            {
                Desk = EnsurePrefab("Desk", root => BuildDesk(root, materials, taxonomy.Desktop), result),
                Monitor = EnsurePrefab("Monitor", root => BuildMonitor(root, materials), result),
                Keyboard = EnsurePrefab("Keyboard", root => BuildKeyboard(root, materials), result),
                Mouse = EnsurePrefab("Mouse", root => BuildMouse(root, materials), result),
                CoffeeMug = EnsurePrefab("Coffee Mug", root => BuildCoffeeMug(root, materials), result),
                Chair = EnsurePrefab("Chair", root => BuildChair(root, materials), result),
                CargoBox = EnsurePrefab("Cargo Box", root => BuildCargoBox(root, materials), result),
                WarningSign = EnsurePrefab("Warning Sign", root => BuildWarningSign(root, materials), result),
                CeilingLight = EnsurePrefab("Ceiling Light", root => BuildCeilingLight(root, materials), result)
            };
        }

        private static StarterDefinitions BuildDefinitions(
            StarterPrefabs prefabs,
            StarterTaxonomy taxonomy,
            StarterContentBuildResult result)
        {
            AssetDefinition desk = EnsureDefinition(
                "Desk", prefabs.Desk, PlacementType.Floor, false, 1,
                new[] { taxonomy.Indoor, taxonomy.Furniture, taxonomy.Minimal, taxonomy.Medium },
                new[] { taxonomy.Floor }, result);
            AssetDefinition monitor = EnsureDefinition(
                "Monitor", prefabs.Monitor, PlacementType.Floor, false, 1,
                new[] { taxonomy.Indoor, taxonomy.Prop, taxonomy.Display, taxonomy.Minimal, taxonomy.Small },
                new[] { taxonomy.Desktop }, result);
            AssetDefinition keyboard = EnsureDefinition(
                "Keyboard", prefabs.Keyboard, PlacementType.Floor, false, 1,
                new[] { taxonomy.Indoor, taxonomy.Prop, taxonomy.Utility, taxonomy.Minimal, taxonomy.Small },
                new[] { taxonomy.Desktop }, result);
            AssetDefinition mouse = EnsureDefinition(
                "Mouse", prefabs.Mouse, PlacementType.Floor, false, 1,
                new[] { taxonomy.Indoor, taxonomy.Prop, taxonomy.Utility, taxonomy.Minimal, taxonomy.Tiny },
                new[] { taxonomy.Desktop }, result);
            AssetDefinition coffeeMug = EnsureDefinition(
                "Coffee Mug", prefabs.CoffeeMug, PlacementType.Floor, true, 1,
                new[] { taxonomy.Indoor, taxonomy.Prop, taxonomy.Minimal, taxonomy.Tiny },
                new[] { taxonomy.Desktop }, result);
            AssetDefinition chair = EnsureDefinition(
                "Chair", prefabs.Chair, PlacementType.Floor, false, 1,
                new[] { taxonomy.Indoor, taxonomy.Furniture, taxonomy.Minimal, taxonomy.Medium },
                new[] { taxonomy.Floor }, result);
            AssetDefinition cargoBox = EnsureDefinition(
                "Cargo Box", prefabs.CargoBox, PlacementType.Floor, true, 2,
                new[] { taxonomy.Indoor, taxonomy.Prop, taxonomy.Industrial, taxonomy.Medium },
                new[] { taxonomy.Floor }, result);
            AssetDefinition warningSign = EnsureDefinition(
                "Warning Sign", prefabs.WarningSign, PlacementType.Wall, false, 1,
                new[] { taxonomy.Indoor, taxonomy.Signage, taxonomy.Utility, taxonomy.Industrial, taxonomy.Small },
                new[] { taxonomy.Wall }, result);
            AssetDefinition ceilingLight = EnsureDefinition(
                "Ceiling Light", prefabs.CeilingLight, PlacementType.Ceiling, false, 1,
                new[] { taxonomy.Indoor, taxonomy.Lighting, taxonomy.Utility, taxonomy.Minimal, taxonomy.Small },
                new[] { taxonomy.Ceiling }, result);

            ConfigureRelations(desk, monitor, keyboard, mouse, coffeeMug, chair);

            foreach (AssetDefinition definition in new[]
                     {
                         desk, monitor, keyboard, mouse, coffeeMug, chair, cargoBox, warningSign, ceilingLight
                     })
            {
                EditorUtility.SetDirty(definition);
            }

            return new StarterDefinitions
            {
                Desk = desk,
                Monitor = monitor,
                Keyboard = keyboard,
                Mouse = mouse,
                CoffeeMug = coffeeMug,
                Chair = chair,
                CargoBox = cargoBox,
                WarningSign = warningSign,
                CeilingLight = ceilingLight
            };
        }

        private static void ConfigureRelations(
            AssetDefinition desk,
            AssetDefinition monitor,
            AssetDefinition keyboard,
            AssetDefinition mouse,
            AssetDefinition coffeeMug,
            AssetDefinition chair)
        {
            monitor.AssetRelativePlacement.ConfigureAsset(
                desk, AssetRelativeAnchorSource.SceneAnchors, AssetRelativeSide.Back,
                0f, 0.8f, AssetRelativeFacing.MatchForward, true);
            monitor.AssetRelativePlacement.SetAlignment(AssetRelativeAlignment.Center);
            monitor.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);

            keyboard.AssetRelativePlacement.ConfigureAsset(
                monitor, AssetRelativeAnchorSource.Any, AssetRelativeSide.Front,
                0f, 0.55f, AssetRelativeFacing.MatchForward, true);
            keyboard.AssetRelativePlacement.SetAlignment(AssetRelativeAlignment.Center);
            keyboard.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);

            mouse.AssetRelativePlacement.ConfigureAsset(
                keyboard, AssetRelativeAnchorSource.Any, AssetRelativeSide.Right,
                0.02f, 0.28f, AssetRelativeFacing.MatchForward, true);
            mouse.AssetRelativePlacement.SetAlignment(AssetRelativeAlignment.Center);
            mouse.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);

            coffeeMug.AssetRelativePlacement.ConfigureAsset(
                desk, AssetRelativeAnchorSource.SceneAnchors, AssetRelativeSide.Front,
                0f, 0.9f, AssetRelativeFacing.Any, true);
            coffeeMug.AssetRelativePlacement.SetSides(new[]
            {
                AssetRelativeSide.Front,
                AssetRelativeSide.Left,
                AssetRelativeSide.Right
            });
            coffeeMug.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.AtMost, 1);

            chair.AssetRelativePlacement.ConfigureAsset(
                desk, AssetRelativeAnchorSource.SceneAnchors, AssetRelativeSide.Front,
                0.1f, 1.2f, AssetRelativeFacing.Toward);
            chair.AssetRelativePlacement.SetAlignment(AssetRelativeAlignment.Center);
            chair.AssetRelativePlacement.SetFacingVariation(20f);
            chair.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
        }

        private static AssetPool BuildPools(StarterDefinitions definitions, StarterContentBuildResult result)
        {
            AssetDefinition[] all = definitions.All.ToArray();
            AssetDefinition[] generated = all.Where(asset => asset != definitions.Desk).ToArray();
            AssetDefinition[] floor = all.Where(asset => asset.PlacementType == PlacementType.Floor).ToArray();
            AssetDefinition[] wall = all.Where(asset => asset.PlacementType == PlacementType.Wall).ToArray();
            AssetDefinition[] ceiling = all.Where(asset => asset.PlacementType == PlacementType.Ceiling).ToArray();

            EnsurePool("All Assets", all, result);
            EnsurePool("Floor Assets", floor, result);
            EnsurePool("Wall Assets", wall, result);
            EnsurePool("Ceiling Assets", ceiling, result);
            return EnsurePool("Starter Room", generated, result);
        }

        private static GenerationPreset BuildGenerationPreset(
            AssetPool pool,
            StylePreset style,
            StarterContentBuildResult result)
        {
            GenerationPreset preset = AssetDatabase.LoadAssetAtPath<GenerationPreset>(StarterPresetPath);
            if (!preset)
            {
                preset = ScriptableObject.CreateInstance<GenerationPreset>();
                preset.name = "Starter Room";
                AssetDatabase.CreateAsset(preset, StarterPresetPath);
                result.CreatedCount++;
            }
            else
            {
                result.ReusedCount++;
            }

            preset.Apply(CreateGenerationSettings(pool, style));
            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static TagCategory EnsureCategory(
            string name,
            TagCategoryUsage usage,
            bool allowMultiple,
            StarterContentBuildResult result)
        {
            List<TagCategory> categories = AssetFileService.FindAssets<TagCategory>(ProjectContentPaths.AssetsRoot);
            TagCategory category = categories.FirstOrDefault(candidate =>
                candidate &&
                string.Equals(candidate.DisplayName, name, StringComparison.OrdinalIgnoreCase) &&
                IsCompatibleUsage(candidate, usage));
            if (category)
            {
                result.ReusedCount++;
                return category;
            }

            string assetName = categories.Any(candidate =>
                candidate && string.Equals(candidate.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                ? $"Starter {name}"
                : name;
            string path = $"{CategoriesRoot}/{AssetFileService.SanitizeName(assetName)}.asset";
            category = AssetDatabase.LoadAssetAtPath<TagCategory>(path);
            if (category)
            {
                result.ReusedCount++;
                return category;
            }

            category = ScriptableObject.CreateInstance<TagCategory>();
            category.name = assetName;
            category.Initialize(allowMultiple, usage);
            AssetDatabase.CreateAsset(category, path);
            result.CreatedCount++;
            return category;
        }

        private static SemanticTag EnsureTag(
            string name,
            TagCategory category,
            StarterContentBuildResult result)
        {
            SemanticTag existing = AssetFileService.FindAssets<SemanticTag>(ProjectContentPaths.AssetsRoot)
                .FirstOrDefault(tag =>
                    tag && tag.Category == category &&
                    string.Equals(tag.DisplayName, name, StringComparison.OrdinalIgnoreCase));
            if (existing)
            {
                result.ReusedCount++;
                return existing;
            }

            string folder = $"{TagsRoot}/{AssetFileService.SanitizeName(category.DisplayName)}";
            AssetFileService.EnsureFolder(folder);
            string path = $"{folder}/{AssetFileService.SanitizeName(name)}.asset";
            SemanticTag tag = ScriptableObject.CreateInstance<SemanticTag>();
            tag.name = name;
            tag.Initialize(category);
            AssetDatabase.CreateAsset(tag, path);
            result.CreatedCount++;
            return tag;
        }

        private static StylePreset EnsureStyle(
            string name,
            StyleSettings settings,
            StarterContentBuildResult result)
        {
            StylePreset existing = AssetFileService.FindAssets<StylePreset>(ProjectContentPaths.StylePresets)
                .FirstOrDefault(style =>
                    style && string.Equals(style.name, name, StringComparison.OrdinalIgnoreCase));
            if (existing)
            {
                result.ReusedCount++;
                return existing;
            }

            StylePreset style = ScriptableObject.CreateInstance<StylePreset>();
            style.name = name;
            style.Initialize(settings);
            AssetDatabase.CreateAsset(style, $"{ProjectContentPaths.StylePresets}/{name}.asset");
            result.CreatedCount++;
            return style;
        }

        private static Material EnsureMaterial(
            string name,
            Color color,
            float metallic,
            StarterContentBuildResult result)
        {
            string path = $"{MaterialsRoot}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing)
            {
                result.ReusedCount++;
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("HDRP/Lit") ??
                            Shader.Find("Standard") ??
                            Shader.Find("Sprites/Default");
            if (!shader)
                throw new InvalidOperationException("No compatible lit shader is available for Starter Content materials.");

            Material material = new(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.25f);

            AssetDatabase.CreateAsset(material, path);
            result.CreatedCount++;
            return material;
        }

        private static GameObject EnsurePrefab(
            string name,
            Action<GameObject> build,
            StarterContentBuildResult result)
        {
            string path = $"{PrefabsRoot}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing)
            {
                result.ReusedCount++;
                return existing;
            }

            GameObject root = new(name);
            try
            {
                build(root);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (!prefab)
                    throw new InvalidOperationException($"Unity could not save the Starter Content prefab '{name}'.");

                result.CreatedCount++;
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static AssetDefinition EnsureDefinition(
            string name,
            GameObject prefab,
            PlacementType placementType,
            bool randomYaw,
            int maximum,
            IEnumerable<SemanticTag> semanticTags,
            IEnumerable<SemanticTag> supportTags,
            StarterContentBuildResult result)
        {
            string path = $"{DefinitionsRoot}/{name}.asset";
            AssetDefinition definition = AssetDatabase.LoadAssetAtPath<AssetDefinition>(path);
            if (!definition)
            {
                definition = ScriptableObject.CreateInstance<AssetDefinition>();
                definition.name = name;
                AssetDatabase.CreateAsset(definition, path);
                result.CreatedCount++;
            }
            else
            {
                result.ReusedCount++;
            }

            bool hasBounds = AssetDefinitionFactory.TryGetPrefabBounds(
                prefab,
                out Vector3 boundsSize,
                out Vector3 boundsCenter);
            definition.Initialize(
                prefab,
                hasBounds ? boundsSize : Vector3.one,
                hasBounds ? boundsCenter : Vector3.zero);
            definition.SetRequiredSupportTags(supportTags);
            definition.SetPlacementLimit(true, maximum);
            foreach (SemanticTag tag in semanticTags)
                definition.AddTag(tag);

            SerializedObject serialized = new(definition);
            serialized.FindProperty("placementType").enumValueIndex = (int)placementType;
            serialized.FindProperty("randomYawRotation").boolValue = randomYaw;
            serialized.FindProperty("randomPitchRotation").boolValue = false;
            serialized.FindProperty("randomRollRotation").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static AssetPool EnsurePool(
            string name,
            IEnumerable<AssetDefinition> assets,
            StarterContentBuildResult result)
        {
            string path = $"{PoolsRoot}/{name}.asset";
            AssetPool pool = AssetDatabase.LoadAssetAtPath<AssetPool>(path);
            if (!pool)
            {
                pool = ScriptableObject.CreateInstance<AssetPool>();
                pool.Initialize(name, AssetPoolMode.Static);
                AssetDatabase.CreateAsset(pool, path);
                result.CreatedCount++;
            }
            else
            {
                result.ReusedCount++;
            }

            pool.AddStaticAssets(assets);
            EditorUtility.SetDirty(pool);
            return pool;
        }

        private static bool IsCompatibleUsage(TagCategory category, TagCategoryUsage requested)
        {
            return requested switch
            {
                TagCategoryUsage.Asset => category.SupportsAssets,
                TagCategoryUsage.Surface => category.SupportsSurfaces,
                _ => category.SupportsAssets && category.SupportsSurfaces
            };
        }

        private static void BuildDesk(GameObject root, StarterMaterials materials, SemanticTag desktop)
        {
            GameObject top = CreatePrimitive(
                root.transform, PrimitiveType.Cube, "Desktop", new Vector3(0f, 0.76f, 0f),
                new Vector3(1.6f, 0.08f, 0.8f), materials.Wood);
            top.AddComponent<PlacementSurfaceDescriptor>().SetSurfaceTags(new[] { desktop });

            foreach (float x in new[] { -0.68f, 0.68f })
            foreach (float z in new[] { -0.28f, 0.28f })
            {
                CreatePrimitive(
                    root.transform, PrimitiveType.Cube, "Leg", new Vector3(x, 0.37f, z),
                    new Vector3(0.08f, 0.74f, 0.08f), materials.Dark);
            }
        }

        private static void BuildMonitor(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Base", new Vector3(0f, 0.025f, 0f), new Vector3(0.3f, 0.05f, 0.18f), materials.Dark);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Stand", new Vector3(0f, 0.18f, 0f), new Vector3(0.06f, 0.3f, 0.06f), materials.Dark);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Screen", new Vector3(0f, 0.38f, 0f), new Vector3(0.62f, 0.38f, 0.07f), materials.Dark);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Display", new Vector3(0f, 0.38f, 0.038f), new Vector3(0.55f, 0.31f, 0.008f), materials.Blue, false);
        }

        private static void BuildKeyboard(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Keyboard", new Vector3(0f, 0.025f, 0f), new Vector3(0.48f, 0.05f, 0.18f), materials.Light);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Keybed", new Vector3(0f, 0.054f, 0f), new Vector3(0.42f, 0.012f, 0.13f), materials.Dark, false);
        }

        private static void BuildMouse(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Sphere, "Mouse", new Vector3(0f, 0.035f, 0f), new Vector3(0.09f, 0.07f, 0.14f), materials.Dark);
        }

        private static void BuildCoffeeMug(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Cup", new Vector3(0f, 0.065f, 0f), new Vector3(0.09f, 0.065f, 0.09f), materials.Yellow);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Handle", new Vector3(0.065f, 0.07f, 0f), new Vector3(0.05f, 0.055f, 0.025f), materials.Yellow, false);
        }

        private static void BuildChair(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Seat", new Vector3(0f, 0.46f, 0f), new Vector3(0.52f, 0.08f, 0.52f), materials.Blue);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Back", new Vector3(0f, 0.76f, -0.23f), new Vector3(0.52f, 0.52f, 0.08f), materials.Blue);
            foreach (float x in new[] { -0.2f, 0.2f })
            foreach (float z in new[] { -0.2f, 0.2f })
            {
                CreatePrimitive(root.transform, PrimitiveType.Cube, "Leg", new Vector3(x, 0.22f, z), new Vector3(0.06f, 0.44f, 0.06f), materials.Dark);
            }
        }

        private static void BuildCargoBox(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Crate", new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), materials.Orange);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Band", new Vector3(0f, 0.3f, 0.306f), new Vector3(0.08f, 0.5f, 0.012f), materials.Dark, false);
        }

        private static void BuildWarningSign(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Plate", new Vector3(0f, 0f, 0.025f), new Vector3(0.62f, 0.42f, 0.05f), materials.Red);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Inset", new Vector3(0f, 0f, 0.054f), new Vector3(0.48f, 0.28f, 0.008f), materials.Yellow, false);
        }

        private static void BuildCeilingLight(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Frame", new Vector3(0f, -0.04f, 0f), new Vector3(0.82f, 0.08f, 0.38f), materials.Dark);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Panel", new Vector3(0f, -0.085f, 0f), new Vector3(0.7f, 0.025f, 0.28f), materials.Light, false);
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool includeCollider = true)
        {
            GameObject child = GameObject.CreatePrimitive(type);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = localScale;

            if (child.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            if (!includeCollider && child.TryGetComponent(out Collider collider))
                UnityEngine.Object.DestroyImmediate(collider);

            return child;
        }

        private sealed class StarterTaxonomy
        {
            public SemanticTag Indoor;
            public SemanticTag Outdoor;
            public SemanticTag Floor;
            public SemanticTag Wall;
            public SemanticTag Ceiling;
            public SemanticTag Desktop;
            public SemanticTag Shelf;
            public SemanticTag Terrain;
            public SemanticTag Path;
            public SemanticTag Water;
            public SemanticTag Prop;
            public SemanticTag Furniture;
            public SemanticTag Decoration;
            public SemanticTag Lighting;
            public SemanticTag Signage;
            public SemanticTag Structure;
            public SemanticTag Vegetation;
            public SemanticTag Display;
            public SemanticTag Utility;
            public SemanticTag FunctionPath;
            public SemanticTag RestArea;
            public SemanticTag Natural;
            public SemanticTag Industrial;
            public SemanticTag Minimal;
            public SemanticTag Urban;
            public SemanticTag SciFi;
            public SemanticTag Fantasy;
            public SemanticTag Tiny;
            public SemanticTag Small;
            public SemanticTag Medium;
            public SemanticTag Large;
            public SemanticTag Huge;
        }

        private sealed class StarterMaterials
        {
            public Material Wall;
            public Material Floor;
            public Material Wood;
            public Material Dark;
            public Material Light;
            public Material Blue;
            public Material Yellow;
            public Material Orange;
            public Material Red;
        }

        private sealed class StarterPrefabs
        {
            public GameObject Desk;
            public GameObject Monitor;
            public GameObject Keyboard;
            public GameObject Mouse;
            public GameObject CoffeeMug;
            public GameObject Chair;
            public GameObject CargoBox;
            public GameObject WarningSign;
            public GameObject CeilingLight;
        }

        private sealed class StarterDefinitions
        {
            public AssetDefinition Desk;
            public AssetDefinition Monitor;
            public AssetDefinition Keyboard;
            public AssetDefinition Mouse;
            public AssetDefinition CoffeeMug;
            public AssetDefinition Chair;
            public AssetDefinition CargoBox;
            public AssetDefinition WarningSign;
            public AssetDefinition CeilingLight;

            public IEnumerable<AssetDefinition> All
            {
                get
                {
                    yield return Desk;
                    yield return Monitor;
                    yield return Keyboard;
                    yield return Mouse;
                    yield return CoffeeMug;
                    yield return Chair;
                    yield return CargoBox;
                    yield return WarningSign;
                    yield return CeilingLight;
                }
            }
        }
    }
}
