using System.Collections.Generic;
using UnityEngine;

namespace Genix.Diagnostics
{
    /// <summary>Collects candidate-generation and projection counts for one generation run.</summary>
    public sealed class SamplingDiagnostics
    {
        /// <summary>Gets requested candidates.</summary>
        public int RequestedCandidates { get; set; }
        /// <summary>Gets generated candidates.</summary>
        public int GeneratedCandidates { get; set; }
        /// <summary>Gets tested candidate seeds.</summary>
        public int TestedCandidateSeeds { get; set; }

        /// <summary>Gets candidate seeds.</summary>
        public List<Vector3> CandidateSeeds { get; } = new();
        /// <summary>Gets tested candidate seed positions.</summary>
        public List<Vector3> TestedCandidateSeedPositions { get; } = new();
        /// <summary>Gets cluster centers.</summary>
        public List<Vector3> ClusterCenters { get; } = new();
        /// <summary>Gets raw sample positions.</summary>
        public List<Vector3> RawSamplePositions { get; } = new();
    }
}
