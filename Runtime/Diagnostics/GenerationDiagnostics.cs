using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Sampling;
using Genix.Styles;
using UnityEngine;

namespace Genix.Diagnostics
{
    public sealed class GenerationDiagnostics
    {
        public string RunId { get; }
        public string TargetName { get; }
        public string StyleName { get; }
        public StyleSettings StyleSettings { get; }
        public GenerationMode GenerationMode { get; }
        public PlacementTarget PlacementTargets { get; }
        public TargetDistributionMode TargetDistributionMode { get; }
        public TargetDistributionWeights TargetDistributionWeights { get; }
        public SamplingAlgorithm SamplingAlgorithm { get; }
        public Bounds TargetBounds { get; }
        public int RequestedObjectCount { get; }
        public bool UseRandomSeed { get; }
        public int RandomSeed { get; }
        public bool BestEffort { get; }
        public RelativePlacementSettings RelativePlacement { get; }

        public string StopReason { get; set; }

        public int PlacedObjectCount => Placements.Count;

        public SamplingDiagnostics Sampler { get; } = new();
        public List<CandidateDiagnostic> Candidates { get; } = new();
        public List<PlacementDiagnostic> Placements { get; } = new();
        public List<TargetBudgetDiagnostic> TargetBudgets { get; } = new();

        public GenerationDiagnostics(string targetName, string styleName, StyleSettings styleSettings, GenerationMode generationMode, PlacementTarget placementTargets,
            TargetDistributionMode targetDistributionMode, TargetDistributionWeights targetDistributionWeights,
            SamplingAlgorithm samplingAlgorithm, Bounds targetBounds, int requestedObjectCount, bool useRandomSeed, int randomSeed, bool bestEffort,
            RelativePlacementSettings relativePlacement)
        {
            RunId = Guid.NewGuid().ToString();
            TargetName = targetName;
            StyleName = styleName;
            StyleSettings = styleSettings;
            GenerationMode = generationMode;
            PlacementTargets = placementTargets;
            TargetDistributionMode = targetDistributionMode;
            TargetDistributionWeights = targetDistributionWeights;
            SamplingAlgorithm = samplingAlgorithm;
            TargetBounds = targetBounds;
            RequestedObjectCount = requestedObjectCount;
            UseRandomSeed = useRandomSeed;
            RandomSeed = randomSeed;
            BestEffort = bestEffort;
            RelativePlacement = relativePlacement ?? RelativePlacementSettings.Disabled;
            StopReason = string.Empty;
        }
    }

    public sealed class TargetBudgetDiagnostic
    {
        public PlacementType PlacementType { get; }
        public int TargetCount { get; }
        public int PlacedCount { get; }

        public TargetBudgetDiagnostic(PlacementType placementType, int targetCount, int placedCount)
        {
            PlacementType = placementType;
            TargetCount = Mathf.Max(0, targetCount);
            PlacedCount = Mathf.Max(0, placedCount);
        }
    }
}
