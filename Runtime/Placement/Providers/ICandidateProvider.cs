using System.Collections.Generic;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Profiling;

namespace Genix.Placement.Providers
{
    /// <summary>Creates inexpensive target-specific seeds before prefab geometry is known.</summary>
    internal interface ICandidateProvider
    {
        List<CandidateSeed> CreateCandidateSeeds(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null,
            IGenerationProfiler profiler = null);
    }
}
