using Genix.Diagnostics;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Styles;
using Genix.Core;
using UnityEngine;

namespace Genix.Sampling
{
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
            IDiagnosticsSink diagnostics = null)
        {
            Bounds = bounds;
            Center = center;
            Radius = radius;

            RequestedCount = requestedCount;
            CandidateCount = Mathf.Max(requestedCount, requestedCount * styleSettings.candidates.multiplier, styleSettings.candidates.minimumCount);
            StyleSettings = styleSettings;
            Diagnostics = diagnostics ?? NullDiagnosticsSink.Instance;
            Random = random;
        }
    }
}
