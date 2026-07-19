using System.Collections.Generic;
using UnityEngine;

namespace Genix.Sampling
{
    internal interface ISampler
    {
        List<Vector3> SamplePositions(SamplingContext context);
    }
}