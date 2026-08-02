using System;

namespace Genix.Sampling.GridSampling
{
    /// <summary>Parameters shared by regular and jittered grid sampling.</summary>
    [Serializable]
    public struct GridSettings
    {
        /// <summary>Distance between neighboring grid coordinates in world units.</summary>
        public float cellSize;
        /// <summary>Maximum normalized displacement within a grid cell for jittered sampling.</summary>
        public float jitterAmount;

        /// <summary>Creates grid-sampling settings.</summary>
        /// <param name="cellSize">Grid spacing in world units.</param>
        /// <param name="jitterAmount">Normalized random displacement.</param>
        public GridSettings(float cellSize, float jitterAmount)
        {
            this.cellSize = cellSize;
            this.jitterAmount = jitterAmount;
        }
    }
}
