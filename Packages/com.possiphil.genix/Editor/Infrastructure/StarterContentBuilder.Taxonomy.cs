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

        private static bool IsCompatibleUsage(TagCategory category, TagCategoryUsage requested)
        {
            return requested switch
            {
                TagCategoryUsage.Asset => category.SupportsAssets,
                TagCategoryUsage.Surface => category.SupportsSurfaces,
                _ => category.SupportsAssets && category.SupportsSurfaces
            };
        }
    }
}

