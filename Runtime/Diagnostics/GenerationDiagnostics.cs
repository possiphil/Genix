using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Extensions;
using Genix.Placement;
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
        public GenerationPerformanceMode PerformanceMode { get; }
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
        public DiagnosticsMode CaptureMode { get; }
        public bool DryRun { get; set; }

        public string StopReason { get; set; }

        public int PlacedObjectCount => Placements.Count;
        public int TestedCandidateCount { get; private set; }
        public int AcceptedCandidateCount { get; private set; }
        public int RejectedCandidateCount { get; private set; }
        public bool HasCandidateOutcomeCounts => TestedCandidateCount > 0;

        public SamplingDiagnostics Sampler { get; } = new();
        public List<CandidateDiagnostic> Candidates { get; } = new();
        public List<PlacementDiagnostic> Placements { get; } = new();
        public List<TargetBudgetDiagnostic> TargetBudgets { get; } = new();
        public IReadOnlyDictionary<RejectionReason, int> CandidateRejectionCounts => _candidateRejectionCounts;

        private readonly Dictionary<RejectionReason, int> _candidateRejectionCounts = new();

        public GenerationDiagnostics(string targetName, string styleName, StyleSettings styleSettings, GenerationMode generationMode, PlacementTarget placementTargets,
            TargetDistributionMode targetDistributionMode, TargetDistributionWeights targetDistributionWeights,
            SamplingAlgorithm samplingAlgorithm, Bounds targetBounds, int requestedObjectCount, bool useRandomSeed, int randomSeed, bool bestEffort,
            RelativePlacementSettings relativePlacement,
            GenerationPerformanceMode performanceMode = GenerationPerformanceMode.Accurate,
            DiagnosticsMode captureMode = DiagnosticsMode.Summary)
        {
            RunId = Guid.NewGuid().ToString();
            TargetName = targetName;
            StyleName = styleName;
            StyleSettings = styleSettings;
            GenerationMode = generationMode;
            PerformanceMode = performanceMode;
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
            CaptureMode = captureMode;
            StopReason = string.Empty;
        }

        public void EnsureCapacity(int candidateCapacity, int placementCapacity, int targetBudgetCapacity)
        {
            EnsureListCapacity(Candidates, candidateCapacity);
            EnsureListCapacity(Placements, placementCapacity);
            EnsureListCapacity(TargetBudgets, targetBudgetCapacity);
        }

        public void RecordCandidateOutcome(bool accepted, RejectionReason rejectionReason)
        {
            TestedCandidateCount++;

            if (accepted)
            {
                AcceptedCandidateCount++;
                return;
            }

            RejectedCandidateCount++;
            _candidateRejectionCounts.TryGetValue(rejectionReason, out int count);
            _candidateRejectionCounts[rejectionReason] = count + 1;
        }

        public string TopRejectionReason
        {
            get
            {
                Dictionary<RejectionReason, int> counts = HasCandidateOutcomeCounts
                    ? new Dictionary<RejectionReason, int>(_candidateRejectionCounts)
                    : new Dictionary<RejectionReason, int>();

                if (!HasCandidateOutcomeCounts)
                {
                    foreach (CandidateDiagnostic candidate in Candidates)
                    {
                        if (candidate.Accepted)
                            continue;

                        counts.TryGetValue(candidate.RejectionReason, out int count);
                        counts[candidate.RejectionReason] = count + 1;
                    }
                }

                if (counts.Count == 0)
                    return string.Empty;

                RejectionReason topReason = RejectionReason.None;
                int topCount = 0;

                foreach (KeyValuePair<RejectionReason, int> entry in counts)
                {
                    if (entry.Value <= topCount)
                        continue;

                    topReason = entry.Key;
                    topCount = entry.Value;
                }

                return $"{topReason.ToDisplayName()} ({topCount})";
            }
        }

        private static void EnsureListCapacity<T>(List<T> list, int capacity)
        {
            int safeCapacity = Mathf.Max(0, capacity);

            if (list.Capacity < safeCapacity)
                list.Capacity = safeCapacity;
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
