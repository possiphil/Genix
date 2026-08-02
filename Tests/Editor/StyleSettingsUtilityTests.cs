using System;
using Genix.Placement;
using Genix.Editor.State;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SamplingArea)]
    public sealed class StyleSettingsUtilityTests
    {
        [TestCase(SamplingAlgorithm.Grid)]
        [TestCase(SamplingAlgorithm.JitteredGrid)]
        [TestCase(SamplingAlgorithm.Random)]
        [TestCase(SamplingAlgorithm.Cluster)]
        [TestCase(SamplingAlgorithm.BridsonPoissonDisk)]
        public void ClearUnusedSettingsPreservesOnlyRelevantAlgorithmFields(SamplingAlgorithm algorithm)
        {
            StyleSettings settings = CreateSettings(algorithm);

            StyleSettingsUtility.ClearUnusedSettings(ref settings);

            Assert.That(settings.algorithm, Is.EqualTo(algorithm));
            Assert.That(settings.description, Is.EqualTo("description"));
            Assert.That(settings.placement.useFixedObjectClearance, Is.True);

            switch (algorithm)
            {
                case SamplingAlgorithm.Grid:
                    Assert.That(settings.grid.cellSize, Is.EqualTo(2f));
                    Assert.That(settings.grid.jitterAmount, Is.Zero);
                    Assert.That(settings.candidates.multiplier, Is.Zero);
                    Assert.That(settings.cluster.count, Is.Zero);
                    Assert.That(settings.poisson.minDistance, Is.Zero);
                    break;
                case SamplingAlgorithm.JitteredGrid:
                    Assert.That(settings.grid.jitterAmount, Is.EqualTo(0.25f));
                    Assert.That(settings.candidates.multiplier, Is.Zero);
                    Assert.That(settings.cluster.count, Is.Zero);
                    Assert.That(settings.poisson.minDistance, Is.Zero);
                    break;
                case SamplingAlgorithm.Random:
                    Assert.That(settings.candidates.multiplier, Is.EqualTo(3));
                    Assert.That(settings.grid.cellSize, Is.Zero);
                    Assert.That(settings.cluster.count, Is.Zero);
                    Assert.That(settings.poisson.minDistance, Is.Zero);
                    break;
                case SamplingAlgorithm.Cluster:
                    Assert.That(settings.candidates.multiplier, Is.EqualTo(3));
                    Assert.That(settings.cluster.count, Is.EqualTo(4));
                    Assert.That(settings.grid.cellSize, Is.Zero);
                    Assert.That(settings.poisson.minDistance, Is.Zero);
                    break;
                case SamplingAlgorithm.BridsonPoissonDisk:
                    Assert.That(settings.candidates.multiplier, Is.EqualTo(3));
                    Assert.That(settings.poisson.minDistance, Is.EqualTo(1.5f));
                    Assert.That(settings.grid.cellSize, Is.Zero);
                    Assert.That(settings.cluster.count, Is.Zero);
                    break;
            }
        }

        [Test]
        public void ClearUnusedSettingsRejectsUnknownAlgorithm()
        {
            StyleSettings settings = CreateSettings((SamplingAlgorithm)999);

            Assert.Throws<ArgumentOutOfRangeException>(() => StyleSettingsUtility.ClearUnusedSettings(ref settings));
        }

        [Test]
        public void AreEqualIgnoresDisabledFixedClearanceDistance()
        {
            StyleSettings first = CreateSettings(SamplingAlgorithm.Random);
            StyleSettings second = first;
            first.placement = new PlacementSettings(false, 1f);
            second.placement = new PlacementSettings(false, 999f);

            Assert.That(StyleSettingsUtility.AreEqual(first, second), Is.True);
        }

        [Test]
        public void AreEqualDetectsActiveFixedClearanceDistance()
        {
            StyleSettings first = CreateSettings(SamplingAlgorithm.Random);
            StyleSettings second = first;
            first.placement = new PlacementSettings(true, 1f);
            second.placement = new PlacementSettings(true, 2f);

            Assert.That(StyleSettingsUtility.AreEqual(first, second), Is.False);
        }

        [Test]
        public void AreEqualUsesSmallFloatTolerance()
        {
            StyleSettings first = CreateSettings(SamplingAlgorithm.Grid);
            StyleSettings second = first;
            second.grid.cellSize += 0.00001f;

            Assert.That(StyleSettingsUtility.AreEqual(first, second), Is.True);

            second.grid.cellSize += 0.001f;
            Assert.That(StyleSettingsUtility.AreEqual(first, second), Is.False);
        }

        [Test]
        public void StylePresetRestoreDefaultsReturnsToInitializedSettings()
        {
            StylePreset preset = ScriptableObject.CreateInstance<StylePreset>();

            try
            {
                preset.Initialize(CreateSettings(SamplingAlgorithm.Random));
                preset.Apply(CreateSettings(SamplingAlgorithm.Grid));
                preset.RestoreDefaults();

                Assert.That(preset.Settings.algorithm, Is.EqualTo(SamplingAlgorithm.Random));
                Assert.That(preset.Settings.candidates.multiplier, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void StylePresetApplyClearsSettingsUnusedByAlgorithm()
        {
            StylePreset preset = ScriptableObject.CreateInstance<StylePreset>();

            try
            {
                preset.Apply(CreateSettings(SamplingAlgorithm.Random));

                Assert.That(preset.Settings.candidates.multiplier, Is.EqualTo(3));
                Assert.That(preset.Settings.grid.cellSize, Is.Zero);
                Assert.That(preset.Settings.cluster.count, Is.Zero);
                Assert.That(preset.Settings.poisson.minDistance, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void StylePresetCanPromoteCurrentSettingsToDefaults()
        {
            StylePreset preset = ScriptableObject.CreateInstance<StylePreset>();

            try
            {
                preset.Initialize(CreateSettings(SamplingAlgorithm.Random));
                preset.Apply(CreateSettings(SamplingAlgorithm.Grid));
                preset.SetCurrentSettingsAsDefaults();
                preset.Apply(CreateSettings(SamplingAlgorithm.Cluster));
                preset.RestoreDefaults();

                Assert.That(preset.Settings.algorithm, Is.EqualTo(SamplingAlgorithm.Grid));
                Assert.That(preset.Settings.grid.cellSize, Is.EqualTo(2f));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void StyleEditStateReportsNoChangesImmediatelyAfterLoadingPreset()
        {
            StylePreset preset = ScriptableObject.CreateInstance<StylePreset>();

            try
            {
                preset.Initialize(CreateSettings(SamplingAlgorithm.Cluster));
                StyleEditState state = new();
                state.LoadFromPreset(preset);

                Assert.That(state.HasPendingChanges, Is.False);
                Assert.That(state.HasDescriptionChanged(), Is.False);
                Assert.That(state.HasAlgorithmChanged(), Is.False);
                Assert.That(state.HasPlacementSettingsChanged(), Is.False);
                Assert.That(state.HasPlacementUseFixedObjectClearanceChanged(), Is.False);
                Assert.That(state.HasPlacementFixedObjectDistanceChanged(), Is.False);
                Assert.That(state.HasCandidateSettingsChanged(), Is.False);
                Assert.That(state.HasCandidateMultiplierChanged(), Is.False);
                Assert.That(state.HasMinimumCandidatesChanged(), Is.False);
                Assert.That(state.HasShuffleCandidatesChanged(), Is.False);
                Assert.That(state.HasGridSettingsChanged(), Is.False);
                Assert.That(state.HasGridCellSizeChanged(), Is.False);
                Assert.That(state.HasGridJitterChanged(), Is.False);
                Assert.That(state.HasClusterSettingsChanged(), Is.False);
                Assert.That(state.HasClusterCountChanged(), Is.False);
                Assert.That(state.HasClusterRadiusChanged(), Is.False);
                Assert.That(state.HasClusterUseMinCenterDistanceChanged(), Is.False);
                Assert.That(state.HasClusterMinCenterDistanceChanged(), Is.False);
                Assert.That(state.HasPoissonSettingsChanged(), Is.False);
                Assert.That(state.HasPoissonMinDistanceChanged(), Is.False);
                Assert.That(state.HasPoissonAttemptsChanged(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void StyleEditStateIdentifiesEveryChangedSettingsGroup()
        {
            StylePreset preset = ScriptableObject.CreateInstance<StylePreset>();

            try
            {
                preset.Initialize(CreateSettings(SamplingAlgorithm.Cluster));
                StyleEditState state = new();
                state.LoadFromPreset(preset);
                state.EditingSettings = new StyleSettings(
                    "changed",
                    SamplingAlgorithm.BridsonPoissonDisk,
                    new PlacementSettings(false, 9f),
                    new CandidateSettings(8, 30, false),
                    new GridSettings(5f, 0.8f),
                    new ClusterSettings(9, 7f, false, 6f),
                    new PoissonSettings(4f, 60));

                state.UpdatePendingChanges();

                Assert.That(state.HasPendingChanges, Is.True);
                Assert.That(state.HasDescriptionChanged(), Is.True);
                Assert.That(state.HasAlgorithmChanged(), Is.True);
                Assert.That(state.HasPlacementUseFixedObjectClearanceChanged(), Is.True);
                Assert.That(state.HasCandidateMultiplierChanged(), Is.True);
                Assert.That(state.HasMinimumCandidatesChanged(), Is.True);
                Assert.That(state.HasShuffleCandidatesChanged(), Is.True);
                Assert.That(state.HasGridCellSizeChanged(), Is.True);
                Assert.That(state.HasGridJitterChanged(), Is.True);
                Assert.That(state.HasClusterCountChanged(), Is.True);
                Assert.That(state.HasClusterRadiusChanged(), Is.True);
                Assert.That(state.HasClusterUseMinCenterDistanceChanged(), Is.True);
                Assert.That(state.HasPoissonMinDistanceChanged(), Is.True);
                Assert.That(state.HasPoissonAttemptsChanged(), Is.True);

                state.DiscardChanges();
                Assert.That(state.HasPendingChanges, Is.False);
                Assert.That(state.EditingSettings.algorithm, Is.EqualTo(SamplingAlgorithm.Cluster));
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void StyleEditStateIgnoresDistancesWhoseControlsAreDisabled()
        {
            StylePreset preset = ScriptableObject.CreateInstance<StylePreset>();

            try
            {
                StyleSettings initial = CreateSettings(SamplingAlgorithm.Cluster);
                initial.placement = new PlacementSettings(false, 1f);
                initial.cluster = new ClusterSettings(4, 3f, false, 1f);
                preset.Initialize(initial);
                StyleEditState state = new();
                state.LoadFromPreset(preset);
                state.EditingSettings.placement = new PlacementSettings(false, 100f);
                state.EditingSettings.cluster = new ClusterSettings(4, 3f, false, 100f);

                Assert.That(state.HasPlacementFixedObjectDistanceChanged(), Is.False);
                Assert.That(state.HasClusterMinCenterDistanceChanged(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }

        private static StyleSettings CreateSettings(SamplingAlgorithm algorithm) => new(
            "description",
            algorithm,
            new PlacementSettings(true, 2f),
            new CandidateSettings(3, 12, true),
            new GridSettings(2f, 0.25f),
            new ClusterSettings(4, 3f, true, 1f),
            new PoissonSettings(1.5f, 30));
    }
}
