using System.Collections.Generic;
using UnityEngine;

namespace Genix.Sampling.PoissonSampling
{
    /// <summary>Stateless entry point for bounded Bridson Poisson-disk sampling.</summary>
    internal sealed class BridsonPoissonDiskSampler : ISampler
    {
        public List<Vector3> SamplePositions(SamplingContext context) =>
            new ProgressiveBridsonPoissonDiskSampler(context)
                .SamplePositions(context.CandidateCount);
    }
}
