using System.Collections.Generic;
using UnityEngine;

namespace Genix.Sampling.JitteredGridSampling
{
    internal sealed class JitteredGridSampler : ISampler
    {
        public List<Vector3> SamplePositions(SamplingContext context)
        {
            List<Vector3> positions = new();

            float cellSize = context.Grid.cellSize;

            if (cellSize <= 0f)
                return positions;

            for (float x = context.Bounds.min.x; x <= context.Bounds.max.x; x += cellSize)
            {
                for (float z = context.Bounds.min.z; z <= context.Bounds.max.z; z += cellSize)
                {
                    Vector3 gridPosition = new Vector3(x, context.Bounds.min.y, z);

                    context.Diagnostics.RecordRawSamplePosition(gridPosition);
                    positions.Add(CreateJitteredPosition(gridPosition, context));
                }

            }


            return positions;
        }

        private static Vector3 CreateJitteredPosition(Vector3 position, SamplingContext context)
        {
            float jitterRadius = context.Grid.cellSize * context.Grid.jitterAmount;

            float jitteredX = position.x + context.Random.Range(-jitterRadius, jitterRadius);
            float jitteredZ = position.z + context.Random.Range(-jitterRadius, jitterRadius);

            jitteredX = Mathf.Clamp(jitteredX, context.Bounds.min.x, context.Bounds.max.x);
            jitteredZ = Mathf.Clamp(jitteredZ, context.Bounds.min.z, context.Bounds.max.z);

            return new Vector3(jitteredX, position.y, jitteredZ);
        }
    }
}
