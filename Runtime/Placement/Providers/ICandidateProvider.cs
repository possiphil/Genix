using System.Collections.Generic;
using Genix.Core;
using Genix.Diagnostics;

namespace Genix.Placement.Providers
{
    internal interface ICandidateProvider
    {
        List<CandidateSeed> CreateCandidateSeeds(GenerationContext context, IDiagnosticsSink diagnostics = null);
    }
}