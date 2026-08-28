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
    internal static partial class OutdoorEvaluationSetupUtility
    {
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
    }
}

