using System.Collections.Generic;
using UnityEngine;

namespace Genix.Sampling.PoissonSampling
{
    internal sealed class BridsonPoissonDiskSampler : ISampler
    {
        public List<Vector3> SamplePositions(SamplingContext context) =>
            new ProgressiveBridsonPoissonDiskSampler(context)
                .SamplePositions(context.CandidateCount);
    }
}
