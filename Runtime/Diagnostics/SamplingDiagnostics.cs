using System.Collections.Generic;
using UnityEngine;

namespace Genix.Diagnostics
{
    public sealed class SamplingDiagnostics
    {
        public int RequestedCandidates { get; set; }
        public int GeneratedCandidates { get; set; }
        public int TestedCandidateSeeds { get; set; }

        public List<Vector3> CandidateSeeds { get; } = new();
        public List<Vector3> TestedCandidateSeedPositions { get; } = new();
        public List<Vector3> ClusterCenters { get; } = new();
        public List<Vector3> RawSamplePositions { get; } = new();
    }
}
