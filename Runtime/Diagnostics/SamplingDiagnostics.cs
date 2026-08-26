using System.Collections.Generic;
using Genix.Semantics;
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
        /// <summary>Gets asset attempts eliminated by immutable support compatibility before full validation.</summary>
        public int SupportPrefilterSkips { get; set; }
        /// <summary>Gets candidate counts grouped by semantic support surface.</summary>
        public List<SupportCandidateDiagnostic> SupportCandidates { get; } = new();

        /// <summary>Gets candidate seeds.</summary>
        public List<Vector3> CandidateSeeds { get; } = new();
        /// <summary>Gets tested candidate seed positions.</summary>
        public List<Vector3> TestedCandidateSeedPositions { get; } = new();
        /// <summary>Gets cluster centers.</summary>
        public List<Vector3> ClusterCenters { get; } = new();
        /// <summary>Gets raw sample positions.</summary>
        public List<Vector3> RawSamplePositions { get; } = new();
    }

    /// <summary>Aggregates candidate coverage for one semantic support kind.</summary>
    public sealed class SupportCandidateDiagnostic
    {
        private readonly HashSet<PlacementSurfaceDescriptor> _surfaces = new();

        /// <summary>Gets the human-readable support label.</summary>
        public string Label { get; }
        /// <summary>Gets the number of candidate seeds projected onto matching supports.</summary>
        public int CandidateCount { get; private set; }
        /// <summary>Gets the number of distinct physical supports represented by those candidates.</summary>
        public int SurfaceCount => _surfaces.Count;

        /// <summary>Initializes a support candidate aggregate.</summary>
        public SupportCandidateDiagnostic(string label)
        {
            Label = string.IsNullOrWhiteSpace(label) ? "Unspecified Support" : label;
        }

        internal void Record(PlacementSurfaceDescriptor descriptor)
        {
            CandidateCount++;

            if (descriptor)
                _surfaces.Add(descriptor);
        }
    }
}
