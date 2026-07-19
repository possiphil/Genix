using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using UnityEngine;

namespace Genix.Styles
{
    public static class StyleSettingsUtility
    {
        public static void ClearUnusedSettings(ref StyleSettings settings)
        {
            switch (settings.algorithm)
            {
                case SamplingAlgorithm.Grid:
                    settings.candidates = default;
                    settings.grid.jitterAmount = 0f;
                    settings.cluster = default;
                    settings.poisson = default;
                    break;

                case SamplingAlgorithm.JitteredGrid:
                    settings.candidates = default;
                    settings.cluster = default;
                    settings.poisson = default;
                    break;

                case SamplingAlgorithm.Random:
                    settings.grid = default;
                    settings.cluster = default;
                    settings.poisson = default;
                    break;

                case SamplingAlgorithm.Cluster:
                    settings.grid = default;
                    settings.poisson = default;
                    break;

                case SamplingAlgorithm.BridsonPoissonDisk:
                    settings.grid = default;
                    settings.cluster = default;
                    break;

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(settings.algorithm), settings.algorithm, $"Can't clear algorithm: {settings.algorithm}");
            }
        }

        public static bool AreEqual(StyleSettings a, StyleSettings b)
        {
            return a.description == b.description && a.algorithm == b.algorithm && AreEqual(a.placement, b.placement) && AreEqual(a.candidates, b.candidates)
                && AreEqual(a.grid, b.grid) && AreEqual(a.cluster, b.cluster) && AreEqual(a.poisson, b.poisson);
        }

        public static bool AreEqual(PlacementSettings a, PlacementSettings b)
        {
            return a.useFixedObjectClearance == b.useFixedObjectClearance
                   && (!a.useFixedObjectClearance || Approximately(a.fixedObjectDistance, b.fixedObjectDistance));
        }

        private static bool AreEqual(CandidateSettings a, CandidateSettings b)
        {
            return a.multiplier == b.multiplier && a.minimumCount == b.minimumCount && a.shuffle == b.shuffle;
        }

        private static bool AreEqual(GridSettings a, GridSettings b)
        {
            return Approximately(a.cellSize, b.cellSize) && Approximately(a.jitterAmount, b.jitterAmount);
        }

        private static bool AreEqual(ClusterSettings a, ClusterSettings b)
        {
            return a.count == b.count && Approximately(a.radius, b.radius) && a.useMinCenterDistance == b.useMinCenterDistance
                && (!a.useMinCenterDistance || Approximately(a.minCenterDistance, b.minCenterDistance));
        }

        private static bool AreEqual(PoissonSettings a, PoissonSettings b)
        {
            return Approximately(a.minDistance, b.minDistance) && a.attempts == b.attempts;
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.0001f;
        }
    }
}