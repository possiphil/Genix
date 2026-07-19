using System.Collections.Generic;
using UnityEngine;

namespace Genix.Sampling.RandomSampling
{
    internal sealed class RandomSampler : ISampler
    {
        public List<Vector3> SamplePositions(SamplingContext context)
        {
            List<Vector3> positions = new();

            for (int i = 0; i < context.CandidateCount; i++)
                positions.Add(GetRandomPosition(context));

            return positions;
        }

        private static Vector3 GetRandomPosition(SamplingContext context)
        {
            Bounds bounds = context.Bounds;
            return new Vector3(
                context.Random.Range(bounds.min.x, bounds.max.x),
                bounds.min.y,
                context.Random.Range(bounds.min.z, bounds.max.z));
        }
    }
}
