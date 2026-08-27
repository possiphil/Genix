using System.Collections.Generic;
using UnityEngine;

namespace Genix.Sampling
{
    /// <summary>Produces two-dimensional sample positions within a supplied world-space sampling context.</summary>
    internal interface ISampler
    {
        List<Vector3> SamplePositions(SamplingContext context);
    }
}
