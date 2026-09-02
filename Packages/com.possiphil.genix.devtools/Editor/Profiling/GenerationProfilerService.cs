using System;
using Genix.Areas;
using Genix.Core;
using Genix.Editor.Generation;
using Genix.Profiling;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Profiling
{
    /// <summary>Owns profiling enablement, recorder creation, and last-run profile handoff.</summary>
    [InitializeOnLoad]
    internal static class GenerationProfilerService
    {
        private static readonly InstrumentationProvider Provider = new();

        static GenerationProfilerService()
        {
            GenerationInstrumentation.RegisterProvider(Provider);
        }

        public static event Action Changed;

        public static bool ProfilingEnabled { get; private set; }
        public static GenerationProfile LastProfile { get; private set; }

        public static void SetProfilingEnabled(bool enabled)
        {
            if (ProfilingEnabled == enabled)
                return;

            ProfilingEnabled = enabled;
            Changed?.Invoke();
        }

        public static GenerationProfilerRecorder CreateRecorderIfEnabled()
        {
            if (!ProfilingEnabled)
                return null;

            return new GenerationProfilerRecorder();
        }

        /// <summary>Enables instrumentation only while one synchronous generation action executes.</summary>
        public static GenerationProfile CaptureOnce(Action run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));

            bool wasEnabled = ProfilingEnabled;
            ClearLastProfile();

            try
            {
                SetProfilingEnabled(true);
                run();
                return LastProfile;
            }
            finally
            {
                SetProfilingEnabled(wasEnabled);
            }
        }

        public static void Store(GenerationProfilerRecorder recorder)
        {
            if (recorder == null)
                return;

            LastProfile = recorder.Profile;
            Changed?.Invoke();
        }

        public static void ClearLastProfile()
        {
            LastProfile = null;
            Changed?.Invoke();
        }

        private sealed class InstrumentationProvider : IGenerationInstrumentationProvider
        {
            public bool IsEnabled => ProfilingEnabled;

            public IGenerationProfiler CreateProfiler() => CreateRecorderIfEnabled();

            public void Store(IGenerationProfiler profiler)
            {
                if (profiler is GenerationProfilerRecorder recorder)
                    GenerationProfilerService.Store(recorder);
            }
        }
    }

    internal enum GenerationProfilerRunType
    {
        Preview,
        Generate
    }

    /// <summary>Builds and profiles one production generation workflow from a reusable preset.</summary>
    internal static class GenerationProfilerRunService
    {
        /// <summary>Profiles one Preview or Generate operation through the production editor workflow.</summary>
        public static bool TryRun(
            IAreaSource areaSource,
            GenerationPreset preset,
            GenerationProfilerRunType runType,
            out string error)
        {
            if (!TryCreateRequest(areaSource, preset, out GenerationRequest request, out error))
                return false;

            try
            {
                GenerationProfile profile = GenerationProfilerService.CaptureOnce(() =>
                {
                    if (runType == GenerationProfilerRunType.Generate)
                        GenerationWorkflow.Generate(request);
                    else
                        GenerationWorkflow.Preview(request);
                });

                if (profile != null)
                    return true;

                error = "The run ended before profiling could start. Check the Console for the generation error.";
                return false;
            }
            catch (Exception exception)
            {
                error = $"The profile run failed: {exception.Message}";
                return false;
            }
        }

        internal static bool TryCreateRequest(
            IAreaSource areaSource,
            GenerationPreset preset,
            out GenerationRequest request,
            out string error)
        {
            request = null;
            if (!preset)
            {
                error = "Select a Generation Preset before profiling.";
                return false;
            }

            GenerationPresetSettings settings = preset.Settings;
            if (!settings.StylePreset)
            {
                error = $"Generation Preset '{preset.name}' has no Generation Style.";
                return false;
            }

            LayerMask combinedLayers = settings.FloorSurfaceLayers |
                                       settings.WallSurfaceLayers |
                                       settings.CeilingSurfaceLayers;
            AreaBuildSettings areaSettings = new(
                settings.AreaDecompositionMode,
                combinedLayers,
                settings.FloorSurfaceLayers,
                settings.WallSurfaceLayers,
                settings.CeilingSurfaceLayers,
                floorNormalYThreshold: Mathf.Cos(settings.FloorSurfaceAngleDegrees * Mathf.Deg2Rad),
                ceilingNormalYThreshold: -Mathf.Cos(settings.CeilingSurfaceAngleDegrees * Mathf.Deg2Rad),
                surfaceDiscoveryMode: settings.SurfaceDiscoveryMode);
            Transform[] selectedTransforms = settings.RelativePlacementSource == RelativePlacementSource.SelectedObjects
                ? Selection.transforms
                : Array.Empty<Transform>();
            RelativePlacementSettings relativePlacement = new(
                settings.RelativePlacementSource,
                settings.RelativeRadius,
                settings.RelativeSceneLayers,
                selectedTransforms);

            request = new GenerationRequest(
                areaSource,
                settings.AssetPool,
                settings.ObjectCount,
                settings.PlacementTargets,
                settings.TargetDistributionMode,
                settings.TargetDistributionWeights,
                settings.StylePreset.Settings,
                areaSettings,
                relativePlacement,
                settings.StylePreset.name,
                settings.UseFixedSeed,
                settings.RandomSeed,
                settings.BestEffort,
                detailedDiagnostics: false,
                supportDistribution: settings.SupportDistribution);

            return GenerationPreflight.IsValid(request, out error);
        }
    }
}
