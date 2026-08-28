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
    internal static partial class StarterContentBuilder
    {
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
    }
}

