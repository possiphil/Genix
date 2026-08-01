using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Profiling;
using Genix.Sampling;
using UnityEngine;

namespace Genix.Placement.Providers
{
    internal abstract class CandidateProviderBase : ICandidateProvider
    {
        protected const float SamplingBoundsHeight = 0.01f;

        private readonly int _requestedCount;
        private readonly int _minimumCandidateCount;
        private readonly int _candidateCount;

        protected CandidateProviderBase(
            int requestedCount = -1,
            int minimumCandidateCount = -1,
            int candidateCount = -1)
        {
            _requestedCount = requestedCount;
            _minimumCandidateCount = minimumCandidateCount;
            _candidateCount = candidateCount;
        }

        public abstract List<CandidateSeed> CreateCandidateSeeds(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null,
            IGenerationProfiler profiler = null);

        protected SamplingContext CreateSamplingContext(GenerationContext context, Bounds bounds, Vector3 center, float radius, IDiagnosticsSink diagnostics = null)
        {
            return new SamplingContext(
                bounds,
                center,
                context.StyleSettings,
                GetRequestedCount(context),
                context.Random,
                radius,
                diagnostics,
                GetMinimumCandidateCount(context),
                GetCandidateCountOverride());
        }

        protected int GetCandidateCount(GenerationContext context)
        {
            if (_candidateCount > 0)
                return _candidateCount;

            int requestedCount = GetRequestedCount(context);
            int minimumCount = GetMinimumCandidateCount(context);
            return Mathf.Max(
                requestedCount,
                requestedCount * context.StyleSettings.candidates.multiplier,
                minimumCount);
        }

        protected int GetRequestedCount(GenerationContext context) =>
            _requestedCount > 0 ? _requestedCount : context.Count;

        protected int GetMinimumCandidateCount(GenerationContext context) =>
            _minimumCandidateCount >= 0 ? _minimumCandidateCount : context.StyleSettings.candidates.minimumCount;

        protected int GetCandidateCountOverride() => _candidateCount;

        protected static List<Vector3> SamplePositions(SamplingContext samplingContext)
        {
            return SamplerFactory.Create(samplingContext.StyleSettings.algorithm).SamplePositions(samplingContext);
        }

        protected static bool TryCreateSamplingBounds(float minX, float maxX, float minZ, float maxZ, float y, out Bounds bounds)
        {
            if (minX > maxX || minZ > maxZ)
            {
                bounds = default;
                return false;
            }

            Vector3 center = new((minX + maxX) / 2f, y + SamplingBoundsHeight / 2f, (minZ + maxZ) / 2f);

            Vector3 size = new(maxX - minX, SamplingBoundsHeight, maxZ - minZ);

            bounds = new Bounds(center, size);
            return true;
        }

        protected static void AddSeed(
            List<CandidateSeed> seeds,
            Vector3 position,
            Quaternion rotation,
            Collider surfaceCollider = null,
            Vector3 surfaceNormal = default,
            int? voxelLayer = null,
            PlacementType placementType = PlacementType.Floor)
        {
            seeds.Add(new CandidateSeed(position, rotation, surfaceCollider, surfaceNormal, voxelLayer, placementType));
        }

        protected static void ShuffleIfNeeded<T>(List<T> list, GenerationContext context)
        {
            if (!context.StyleSettings.candidates.shuffle)
                return;

            context.Random.Shuffle(list);
        }
    }
}
