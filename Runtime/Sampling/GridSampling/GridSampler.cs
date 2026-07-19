using System.Collections.Generic;
using UnityEngine;

namespace Genix.Sampling.GridSampling
{
    internal sealed class GridSampler : ISampler
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
                    Vector3 position = new Vector3(x, context.Bounds.min.y, z);

                    context.Diagnostics.RecordRawSamplePosition(position);
                    positions.Add(position);
                }
            }


            return positions;
        }
    }
}
