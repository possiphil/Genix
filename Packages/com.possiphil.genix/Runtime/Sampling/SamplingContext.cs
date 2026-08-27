using Genix.Diagnostics;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Styles;
using Genix.Core;
using UnityEngine;

namespace Genix.Sampling
{
    /// <summary>Immutable bounds, count, settings, random stream, and diagnostics supplied to a sampler.</summary>
    internal readonly struct SamplingContext
    {
        public Bounds Bounds { get; }
        public Vector3 Center { get; }
        public float Radius { get; }

        public int RequestedCount { get; }
        public int CandidateCount { get; }

        public StyleSettings StyleSettings { get; }
        public IDiagnosticsSink Diagnostics { get; }
        public GenerationRandom Random { get; }

        public CandidateSettings Candidates => StyleSettings.candidates;
        public GridSettings Grid => StyleSettings.grid;
        public ClusterSettings Cluster => StyleSettings.cluster;
        public PoissonSettings Poisson => StyleSettings.poisson;

        public SamplingContext(
            Bounds bounds,
            Vector3 center,
            StyleSettings styleSettings,
            int requestedCount,
            GenerationRandom random,
            float radius = 0f,
            IDiagnosticsSink diagnostics = null,
            int minimumCandidateCount = -1,
            int candidateCountOverride = -1)
        {
            Bounds = bounds;
            Center = center;
            Radius = radius;

            RequestedCount = requestedCount;
            CandidateCount = candidateCountOverride > 0
                ? candidateCountOverride
                : styleSettings.candidates.GetBudget(requestedCount, minimumCandidateCount);
            StyleSettings = styleSettings;
            Diagnostics = diagnostics ?? NullDiagnosticsSink.Instance;
            Random = random;
        }
    }
}
