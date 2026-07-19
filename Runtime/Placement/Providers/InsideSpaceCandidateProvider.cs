using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Sampling;
using UnityEngine;

namespace Genix.Placement.Providers
{
    internal sealed class InsideSpaceCandidateProvider : CandidateProviderBase
    {
        private const int RandomOversampling = 4;
        private const int MaxGeneratedSeeds = 50_000;

        public override List<CandidateSeed> CreateCandidateSeeds(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null)
        {
            SamplingContext samplingContext = CreateSamplingContext(
                context,
                context.TargetBounds,
                context.TargetBounds.center,
                0f,
                diagnostics);
            List<CandidateSeed> seeds = new();

            foreach (Vector3 position in CreateVolumePositions(context, samplingContext))
            {
                samplingContext.Diagnostics.RecordRawSamplePosition(position);

                if (!context.Area.ContainsVolumePoint(position))
                    continue;

                AddSeed(
                    seeds,
                    position,
                    Quaternion.identity,
                    surfaceNormal: Vector3.up,
                    placementType: PlacementType.InsideSpace);
            }

            ShuffleIfNeeded(seeds, context);
            return seeds;
        }

        private static IEnumerable<Vector3> CreateVolumePositions(
            GenerationContext context,
            SamplingContext samplingContext)
        {
            return context.StyleSettings.algorithm switch
            {
                SamplingAlgorithm.Grid => CreateGridPositions(context, samplingContext, false),
                SamplingAlgorithm.JitteredGrid => CreateGridPositions(context, samplingContext, true),
                _ => CreateRandomPositions(context, samplingContext)
            };
        }

        private static IEnumerable<Vector3> CreateRandomPositions(
            GenerationContext context,
            SamplingContext samplingContext)
        {
            Bounds bounds = samplingContext.Bounds;
            int count = Mathf.Min(
                MaxGeneratedSeeds,
                Mathf.Max(samplingContext.CandidateCount, samplingContext.CandidateCount * RandomOversampling));

            for (int i = 0; i < count; i++)
            {
                yield return new Vector3(
                    context.Random.Range(bounds.min.x, bounds.max.x),
                    context.Random.Range(bounds.min.y, bounds.max.y),
                    context.Random.Range(bounds.min.z, bounds.max.z));
            }
        }

        private static IEnumerable<Vector3> CreateGridPositions(
            GenerationContext context,
            SamplingContext samplingContext,
            bool jittered)
        {
            Bounds bounds = samplingContext.Bounds;
            float cellSize = Mathf.Max(0.01f, context.StyleSettings.grid.cellSize);
            float jitterRadius = jittered
                ? cellSize * Mathf.Clamp01(context.StyleSettings.grid.jitterAmount)
                : 0f;
            int emitted = 0;
            int maxCount = Mathf.Min(
                MaxGeneratedSeeds,
                Mathf.Max(samplingContext.CandidateCount, samplingContext.CandidateCount * RandomOversampling));

            for (float x = bounds.min.x; x <= bounds.max.x && emitted < maxCount; x += cellSize)
            {
                for (float y = bounds.min.y; y <= bounds.max.y && emitted < maxCount; y += cellSize)
                {
                    for (float z = bounds.min.z; z <= bounds.max.z && emitted < maxCount; z += cellSize)
                    {
                        emitted++;

                        if (!jittered)
                        {
                            yield return new Vector3(x, y, z);
                            continue;
                        }

                        yield return new Vector3(
                            Mathf.Clamp(x + context.Random.Range(-jitterRadius, jitterRadius), bounds.min.x, bounds.max.x),
                            Mathf.Clamp(y + context.Random.Range(-jitterRadius, jitterRadius), bounds.min.y, bounds.max.y),
                            Mathf.Clamp(z + context.Random.Range(-jitterRadius, jitterRadius), bounds.min.z, bounds.max.z));
                    }
                }
            }
        }
    }
}
