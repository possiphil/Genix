using System;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;

namespace Genix.Styles
{
    /// <summary>Configures style behavior.</summary>
    [Serializable]
    public struct StyleSettings
    {
        /// <summary>Stores description.</summary>
        public string description;
        /// <summary>Stores algorithm.</summary>
        public SamplingAlgorithm algorithm;

        /// <summary>Stores placement.</summary>
        public PlacementSettings placement;
        /// <summary>Stores candidates.</summary>
        public CandidateSettings candidates;
        /// <summary>Stores grid.</summary>
        public GridSettings grid;
        /// <summary>Stores cluster.</summary>
        public ClusterSettings cluster;
        /// <summary>Stores poisson.</summary>
        public PoissonSettings poisson;

        /// <summary>Initializes a new instance of style settings.</summary>
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