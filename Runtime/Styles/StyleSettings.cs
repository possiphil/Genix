using System;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;

namespace Genix.Styles
{
    [Serializable]
    public struct StyleSettings
    {
        public string description;
        public SamplingAlgorithm algorithm;

        public PlacementSettings placement;
        public CandidateSettings candidates;
        public GridSettings grid;
        public ClusterSettings cluster;
        public PoissonSettings poisson;

        public StyleSettings(string description, SamplingAlgorithm algorithm, PlacementSettings placement,
            CandidateSettings candidates, GridSettings grid, ClusterSettings cluster, PoissonSettings poisson)
        {
            this.description = description;
            this.algorithm = algorithm;

            this.placement = placement;
            this.candidates = candidates;
            this.grid = grid;
            this.cluster = cluster;
            this.poisson = poisson;
        }
    }
}