using System;
using Genix.Profiling;

namespace Genix.Editor.Generation
{
    /// <summary>Supplies optional run instrumentation without coupling the designer package to developer tooling.</summary>
    public interface IGenerationInstrumentationProvider
    {
        /// <summary>Indicates whether newly started generation runs should be instrumented.</summary>
        bool IsEnabled { get; }

        /// <summary>Creates the profiler used by one generation run.</summary>
        IGenerationProfiler CreateProfiler();

        /// <summary>Receives the completed profiler after the generation run ends.</summary>
        void Store(IGenerationProfiler profiler);
    }

    /// <summary>Hosts the optional instrumentation provider installed by Genix DevTools.</summary>
    public static class GenerationInstrumentation
    {
        private static IGenerationInstrumentationProvider _provider;

        /// <summary>Indicates whether an installed provider currently requests instrumentation.</summary>
        public static bool IsEnabled => _provider?.IsEnabled == true;

        /// <summary>Registers the provider responsible for subsequent generation runs.</summary>
        public static void RegisterProvider(IGenerationInstrumentationProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Removes the provider when it is still the active registration.</summary>
        public static void UnregisterProvider(IGenerationInstrumentationProvider provider)
        {
            if (ReferenceEquals(_provider, provider))
                _provider = null;
        }

        internal static IGenerationProfiler CreateProfiler()
        {
            if (!IsEnabled)
                return NullGenerationProfiler.Instance;

            return _provider.CreateProfiler() ?? NullGenerationProfiler.Instance;
        }

        internal static void Store(IGenerationProfiler profiler)
        {
            if (profiler is { IsEnabled: true })
                _provider?.Store(profiler);
        }
    }
}
