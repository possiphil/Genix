using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Sampling;
using UnityEngine;

namespace Genix.Placement.Providers
{
    internal sealed class PlacementTargetCandidateProvider : CandidateProviderBase
    {
        private readonly PlacementTarget _targets;

        public PlacementTargetCandidateProvider(PlacementTarget targets)
        {
            _targets = targets & PlacementTarget.All;
        }

        public override List<CandidateSeed> CreateCandidateSeeds(GenerationContext context, IDiagnosticsSink diagnostics = null)
        {
            List<CandidateSeed> seeds = new();

            if ((_targets & PlacementTarget.Floor) != 0)
                seeds.AddRange(new HorizontalSurfaceCandidateProvider().CreateCandidateSeeds(context, diagnostics));

            if ((_targets & PlacementTarget.Wall) != 0)
                seeds.AddRange(new WallCandidateProvider().CreateCandidateSeeds(context, diagnostics));

            if ((_targets & PlacementTarget.Ceiling) != 0)
                seeds.AddRange(new CeilingCandidateProvider().CreateCandidateSeeds(context, diagnostics));

            if ((_targets & PlacementTarget.InsideSpace) != 0)
                seeds.AddRange(new InsideSpaceCandidateProvider().CreateCandidateSeeds(context, diagnostics));

            ShuffleIfNeeded(seeds, context);
            return seeds;
        }
    }

    internal sealed class HorizontalSurfaceCandidateProvider : CandidateProviderBase
    {
        public override List<CandidateSeed> CreateCandidateSeeds(GenerationContext context, IDiagnosticsSink diagnostics = null)
        {
            List<CandidateSeed> seeds = new();

            foreach (SurfaceRegion region in context.Area.FloorRegions)
            {
                SamplingContext samplingContext = CreateSamplingContext(context, region.Bounds, region.Bounds.center, 0f, diagnostics);
                List<Vector3> positions = SamplePositions(samplingContext);

                foreach (Vector3 rawPosition in positions)
                {
                    if (!context.Area.TryProjectToFloor(rawPosition, region, out SurfacePoint surfacePoint))
                        continue;

                    AddSeed(
                        seeds,
                        surfacePoint.Position,
                        Quaternion.identity,
                        surfacePoint.SurfaceCollider,
                        surfacePoint.Normal,
                        surfacePoint.VoxelLayer,
                        PlacementType.Floor);
                }
            }

            ShuffleIfNeeded(seeds, context);
            return seeds;
        }

    }

    internal sealed class CeilingCandidateProvider : CandidateProviderBase
    {
        public override List<CandidateSeed> CreateCandidateSeeds(GenerationContext context, IDiagnosticsSink diagnostics = null)
        {
            List<CandidateSeed> seeds = new();

            foreach (SurfaceRegion region in context.Area.CeilingRegions)
            {
                SamplingContext samplingContext = CreateSamplingContext(context, region.Bounds, region.Bounds.center, 0f, diagnostics);
                List<Vector3> positions = SamplePositions(samplingContext);

                foreach (Vector3 rawPosition in positions)
                {
                    if (!context.Area.TryProjectToCeiling(rawPosition, region, out SurfacePoint surfacePoint))
                        continue;

                    AddSeed(
                        seeds,
                        surfacePoint.Position,
                        Quaternion.identity,
                        surfacePoint.SurfaceCollider,
                        surfacePoint.Normal,
                        surfacePoint.VoxelLayer,
                        PlacementType.Ceiling);
                }
            }

            ShuffleIfNeeded(seeds, context);
            return seeds;
        }
    }
}
