using System;
using Genix.Editor.Generation;
using Genix.Profiling;
using UnityEditor;

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
}
