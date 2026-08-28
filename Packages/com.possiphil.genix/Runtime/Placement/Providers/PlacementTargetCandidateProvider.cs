using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Profiling;
using Genix.Sampling;
using Genix.Sampling.PoissonSampling;
using Genix.Semantics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Placement.Providers
{
    /// <summary>
    /// Generates floor or ceiling seeds from explicit regions, complete-volume projection, or both.
    /// </summary>
    internal sealed class PlacementTargetCandidateProvider : CandidateProviderBase
    {
        private readonly PlacementTarget _targets;
        private readonly IReadOnlyList<AssetDefinition> _assets;
        private HorizontalSurfaceCandidateProvider _floorProvider;
        private WallCandidateProvider _wallProvider;
        private CeilingCandidateProvider _ceilingProvider;
        private InsideSpaceCandidateProvider _insideSpaceProvider;

        public PlacementTargetCandidateProvider(
            PlacementTarget targets,
            int requestedCount = -1,
            int minimumCandidateCount = -1,
            int candidateCount = -1,
            IReadOnlyList<AssetDefinition> assets = null)
            : base(requestedCount, minimumCandidateCount, candidateCount)
        {
            _targets = targets & PlacementTarget.All;
            _assets = assets;
        }

        public override List<CandidateSeed> CreateCandidateSeeds(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null,
            IGenerationProfiler profiler = null)
        {
            List<CandidateSeed> seeds = new();
            List<PlacementType> activeTypes = GetActivePlacementTypes();

            if ((_targets & PlacementTarget.Floor) != 0)
                seeds.AddRange(CreateFloorProvider(context, activeTypes).CreateCandidateSeeds(context, diagnostics, profiler));

            if ((_targets & PlacementTarget.Wall) != 0)
                seeds.AddRange(CreateWallProvider(context, activeTypes).CreateCandidateSeeds(context, diagnostics, profiler));

            if ((_targets & PlacementTarget.Ceiling) != 0)
                seeds.AddRange(CreateCeilingProvider(context, activeTypes).CreateCandidateSeeds(context, diagnostics, profiler));

            if ((_targets & PlacementTarget.InsideSpace) != 0)
                seeds.AddRange(CreateInsideSpaceProvider(context, activeTypes).CreateCandidateSeeds(context, diagnostics, profiler));

            ShuffleIfNeeded(seeds, context);
            return seeds;
        }

        private HorizontalSurfaceCandidateProvider CreateFloorProvider(
            GenerationContext context,
            IReadOnlyList<PlacementType> activeTypes)
        {
            if (_floorProvider != null)
                return _floorProvider;

            CandidateBudget budget = CreateBudget(context, PlacementType.Floor, activeTypes);
            _floorProvider = new HorizontalSurfaceCandidateProvider(
                budget.RequestedCount,
                budget.MinimumCandidateCount,
                budget.CandidateCount,
                _assets);
            return _floorProvider;
        }

        private WallCandidateProvider CreateWallProvider(
            GenerationContext context,
            IReadOnlyList<PlacementType> activeTypes)
        {
            if (_wallProvider != null)
                return _wallProvider;

            CandidateBudget budget = CreateBudget(context, PlacementType.Wall, activeTypes);
            _wallProvider = new WallCandidateProvider(
                budget.RequestedCount,
                budget.MinimumCandidateCount,
                budget.CandidateCount);
            return _wallProvider;
        }

        private CeilingCandidateProvider CreateCeilingProvider(
            GenerationContext context,
            IReadOnlyList<PlacementType> activeTypes)
        {
            if (_ceilingProvider != null)
                return _ceilingProvider;

            CandidateBudget budget = CreateBudget(context, PlacementType.Ceiling, activeTypes);
            _ceilingProvider = new CeilingCandidateProvider(
                budget.RequestedCount,
                budget.MinimumCandidateCount,
                budget.CandidateCount);
            return _ceilingProvider;
        }

        private InsideSpaceCandidateProvider CreateInsideSpaceProvider(
            GenerationContext context,
            IReadOnlyList<PlacementType> activeTypes)
        {
            if (_insideSpaceProvider != null)
                return _insideSpaceProvider;

            CandidateBudget budget = CreateBudget(context, PlacementType.InsideSpace, activeTypes);
            _insideSpaceProvider = new InsideSpaceCandidateProvider(
                budget.RequestedCount,
                budget.MinimumCandidateCount,
                budget.CandidateCount);
            return _insideSpaceProvider;
        }

        private List<PlacementType> GetActivePlacementTypes()
        {
            List<PlacementType> result = new();

            if ((_targets & PlacementTarget.Floor) != 0)
                result.Add(PlacementType.Floor);

            if ((_targets & PlacementTarget.Wall) != 0)
                result.Add(PlacementType.Wall);

            if ((_targets & PlacementTarget.Ceiling) != 0)
                result.Add(PlacementType.Ceiling);

            if ((_targets & PlacementTarget.InsideSpace) != 0)
                result.Add(PlacementType.InsideSpace);

            return result;
        }

        private CandidateBudget CreateBudget(
            GenerationContext context,
            PlacementType placementType,
            IReadOnlyList<PlacementType> activeTypes)
        {
            int rootRequestedCount = Mathf.Max(1, GetRequestedCount(context));
            int requestedCount = GetRequestedObjectCount(context, placementType, activeTypes, rootRequestedCount);
            float requestedRatio = requestedCount / (float)rootRequestedCount;
            int minimumCandidateCount = Mathf.CeilToInt(GetMinimumCandidateCount(context) * requestedRatio);
            int candidateCount = Mathf.CeilToInt(GetCandidateCount(context) * requestedRatio);

            return new CandidateBudget(
                Mathf.Max(1, requestedCount),
                Mathf.Max(1, minimumCandidateCount),
                Mathf.Max(1, candidateCount));
        }

        private static int GetRequestedObjectCount(
            GenerationContext context,
            PlacementType placementType,
            IReadOnlyList<PlacementType> activeTypes,
            int rootRequestedCount)
        {
            if (activeTypes == null || activeTypes.Count <= 1)
                return rootRequestedCount;

            if (context.TargetDistributionMode == TargetDistributionMode.Weighted)
                return GetWeightedRequestedCount(context, placementType, activeTypes, rootRequestedCount);

            return Mathf.CeilToInt(rootRequestedCount / (float)activeTypes.Count);
        }

        private static int GetWeightedRequestedCount(
            GenerationContext context,
            PlacementType placementType,
            IReadOnlyList<PlacementType> activeTypes,
            int rootRequestedCount)
        {
            int targetWeight = GetWeight(context, placementType);
            int totalWeight = 0;

            foreach (PlacementType type in activeTypes)
                totalWeight += GetWeight(context, type);

            if (targetWeight <= 0 || totalWeight <= 0)
                return Mathf.CeilToInt(rootRequestedCount / (float)activeTypes.Count);

            return Mathf.CeilToInt(rootRequestedCount * (targetWeight / (float)totalWeight));
        }

        private static int GetWeight(GenerationContext context, PlacementType placementType)
        {
            PlacementTarget target = placementType switch
            {
                PlacementType.Floor => PlacementTarget.Floor,
                PlacementType.Wall => PlacementTarget.Wall,
                PlacementType.Ceiling => PlacementTarget.Ceiling,
                PlacementType.InsideSpace => PlacementTarget.InsideSpace,
                _ => PlacementTarget.None
            };

            return context.TargetDistributionMode == TargetDistributionMode.Weighted
                ? context.TargetDistributionWeights.GetWeight(target)
                : 1;
        }

        private readonly struct CandidateBudget
        {
            public int RequestedCount { get; }
            public int MinimumCandidateCount { get; }
            public int CandidateCount { get; }

            public CandidateBudget(int requestedCount, int minimumCandidateCount, int candidateCount)
            {
                RequestedCount = requestedCount;
                MinimumCandidateCount = minimumCandidateCount;
                CandidateCount = candidateCount;
            }
        }
    }
}

