using System;
using Genix.Profiling;

namespace Genix.Editor.Profiling
{
    internal static class GenerationProfilerService
    {
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
    }
}
