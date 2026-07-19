using System;

namespace Genix.Sampling.GridSampling
{
    [Serializable]
    public struct GridSettings
    {
        public float cellSize;
        public float jitterAmount;

        public GridSettings(float cellSize, float jitterAmount)
        {
            this.cellSize = cellSize;
            this.jitterAmount = jitterAmount;
        }
    }
}