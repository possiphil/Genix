using System;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Generation;
using Genix.Semantics;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class GenerationPresetTests
    {
        [Test]
        public void SettingsNormalizeInvalidRangesFlagsAndEnums()
        {
            GenerationPresetSettings settings = new(
                null,
                null,
                -20,
                (PlacementTarget)(1 << 20),
                (TargetDistributionMode)99,
                new TargetDistributionWeights(-1, 2, -3, 4),
                (AreaDecompositionMode)99,
                (SurfaceDiscoveryMode)99,
                LayerMask(1),
                LayerMask(2),
                LayerMask(3),
                -10f,
                120f,
                (RelativePlacementSource)99,
                -5f,
                LayerMask(4),
                true,
                42,
                false);

            Assert.That(settings.ObjectCount, Is.EqualTo(1));
            Assert.That(settings.PlacementTargets, Is.EqualTo(PlacementTarget.None));
            Assert.That(settings.TargetDistributionMode, Is.EqualTo(TargetDistributionMode.Random));
            Assert.That(settings.TargetDistributionWeights.Floor, Is.Zero);
            Assert.That(settings.TargetDistributionWeights.Wall, Is.EqualTo(2));
            Assert.That(settings.AreaDecompositionMode, Is.EqualTo(AreaDecompositionMode.Fast));
            Assert.That(settings.SurfaceDiscoveryMode, Is.EqualTo(SurfaceDiscoveryMode.AllMatchingSurfacesInVolume));
            Assert.That(settings.FloorSurfaceAngleDegrees, Is.Zero);
            Assert.That(settings.CeilingSurfaceAngleDegrees, Is.EqualTo(89.9f));
            Assert.That(settings.RelativePlacementSource, Is.EqualTo(RelativePlacementSource.None));
            Assert.That(settings.RelativeRadius, Is.EqualTo(0.1f));
        }

        [Test]
        public void PresetRoundTripPreservesEveryCapturedSetting()
        {
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            StylePreset style = ScriptableObject.CreateInstance<StylePreset>();
            GenerationPreset preset = ScriptableObject.CreateInstance<GenerationPreset>();

            try
            {
                GenerationPresetSettings expected = CreateSettings(pool, style, 37);

                preset.Apply(expected);

                Assert.That(preset.Settings, Is.EqualTo(expected));
                Assert.That(preset.Matches(expected), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(style);
                Object.DestroyImmediate(pool);
            }
        }

        [Test]
        public void MatchesDetectsChangedGeneratorState()
        {
            GenerationPreset preset = ScriptableObject.CreateInstance<GenerationPreset>();

            try
            {
                preset.Apply(CreateSettings(null, null, 20));

                Assert.That(preset.Matches(CreateSettings(null, null, 20)), Is.True);
                Assert.That(preset.Matches(CreateSettings(null, null, 21)), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void PresetRoundTripPreservesIndependentSupportDistributionRules()
        {
            TagCategory category = ScriptableObject.CreateInstance<TagCategory>();
            SemanticTag tag = ScriptableObject.CreateInstance<SemanticTag>();
            GenerationPreset preset = ScriptableObject.CreateInstance<GenerationPreset>();

            try
            {
                category.Initialize(true, TagCategoryUsage.Surface);
                tag.name = "Desktop";
                tag.Initialize(category);
                SupportDistributionSettings distribution = new(
                    true,
                    3,
                    new[] { new SupportDistributionRule(tag, SupportDistributionRuleMode.ExactCount, 2) });
                GenerationPresetSettings expected = CreateSettings(null, null, 12, distribution);

                preset.Apply(expected);
                SupportDistributionSettings actual = preset.Settings.SupportDistribution;

                Assert.That(actual.IsEnabled, Is.True);
                Assert.That(actual.DefaultWeight, Is.EqualTo(3));
                Assert.That(actual.Rules, Has.Count.EqualTo(1));
                Assert.That(actual.Rules[0].SupportTag, Is.EqualTo(tag));
                Assert.That(actual.Rules[0].Value, Is.EqualTo(2));
                Assert.That(preset.Matches(expected), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(tag);
                Object.DestroyImmediate(category);
            }
        }

        [Test]
        public void DefaultPreferenceResolvesPresetByGuidAfterMove()
        {
            GenerationPreset previousDefault = GenerationPresetPreferences.GetDefault();
            string suffix = Guid.NewGuid().ToString("N");
            string originalPath = $"Assets/__GenixGenerationPreset_{suffix}.asset";
            string movedPath = $"Assets/__GenixGenerationPresetMoved_{suffix}.asset";
            GenerationPreset preset = ScriptableObject.CreateInstance<GenerationPreset>();

            try
            {
                AssetDatabase.CreateAsset(preset, originalPath);
                GenerationPresetPreferences.SetDefault(preset);

                Assert.That(GenerationPresetPreferences.GetDefault(), Is.EqualTo(preset));
                Assert.That(AssetDatabase.MoveAsset(originalPath, movedPath), Is.Empty);
                Assert.That(GenerationPresetPreferences.GetDefault(), Is.EqualTo(preset));

                GenerationPresetPreferences.ClearDefault();
                Assert.That(GenerationPresetPreferences.GetDefault(), Is.Null);
            }
            finally
            {
                if (previousDefault)
                    GenerationPresetPreferences.SetDefault(previousDefault);
                else
                    GenerationPresetPreferences.ClearDefault();

                AssetDatabase.DeleteAsset(movedPath);
                AssetDatabase.DeleteAsset(originalPath);
                AssetDatabase.SaveAssets();
            }
        }

        private static GenerationPresetSettings CreateSettings(
            AssetPool pool,
            StylePreset style,
            int objectCount,
            SupportDistributionSettings supportDistribution = null)
        {
            return new GenerationPresetSettings(
                pool,
                style,
                objectCount,
                PlacementTarget.Floor | PlacementTarget.Wall,
                TargetDistributionMode.Weighted,
                new TargetDistributionWeights(3, 2, 1, 0),
                AreaDecompositionMode.Precise,
                SurfaceDiscoveryMode.NearSfsBoundaries,
                LayerMask(7),
                LayerMask(8),
                LayerMask(9),
                35f,
                45f,
                RelativePlacementSource.SceneObjects,
                8.5f,
                LayerMask(10),
                true,
                -123456,
                false,
                supportDistribution);
        }

        private static LayerMask LayerMask(int value) => new() { value = value };
    }
}
