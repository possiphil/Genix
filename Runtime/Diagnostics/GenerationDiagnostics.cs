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
    /// <summary>Collects run metadata, candidate outcomes, rejections, and sampling statistics.</summary>
    public sealed class GenerationDiagnostics
    {
        /// <summary>Gets run id.</summary>
        public string RunId { get; }
        /// <summary>Gets target name.</summary>
        public string TargetName { get; }
        /// <summary>Gets style name.</summary>
        public string StyleName { get; }
        /// <summary>Gets style settings.</summary>
        public StyleSettings StyleSettings { get; }
        /// <summary>Gets placement targets.</summary>
        public PlacementTarget PlacementTargets { get; }
        /// <summary>Gets target distribution mode.</summary>
        public TargetDistributionMode TargetDistributionMode { get; }
        /// <summary>Gets target distribution weights.</summary>
        public TargetDistributionWeights TargetDistributionWeights { get; }
        /// <summary>Gets sampling algorithm.</summary>
        public SamplingAlgorithm SamplingAlgorithm { get; }
        /// <summary>Gets target bounds.</summary>
        public Bounds TargetBounds { get; }
        /// <summary>Gets the number of requested object items.</summary>
        public int RequestedObjectCount { get; }
        /// <summary>Indicates whether fixed seed.</summary>
        public bool UseFixedSeed { get; }
        /// <summary>Gets random seed.</summary>
        public int RandomSeed { get; }
        /// <summary>Indicates whether best effort.</summary>
        public bool BestEffort { get; }
        /// <summary>Gets relative placement.</summary>
        public RelativePlacementSettings RelativePlacement { get; }
        /// <summary>Gets capture mode.</summary>
        public DiagnosticsMode CaptureMode { get; }
        /// <summary>Indicates whether dry run.</summary>
        public bool DryRun { get; set; }

        /// <summary>Gets stop reason.</summary>
        public string StopReason { get; set; }

        /// <summary>Gets the number of placed object items.</summary>
        public int PlacedObjectCount => Placements.Count;
        /// <summary>Gets the number of tested candidate items.</summary>
        public int TestedCandidateCount { get; private set; }
        /// <summary>Gets the number of accepted candidate items.</summary>
        public int AcceptedCandidateCount { get; private set; }
        /// <summary>Gets the number of rejected candidate items.</summary>
        public int RejectedCandidateCount { get; private set; }
        /// <summary>Indicates whether candidate outcome counts.</summary>
        public bool HasCandidateOutcomeCounts => TestedCandidateCount > 0;

        /// <summary>Gets sampler.</summary>
        public SamplingDiagnostics Sampler { get; } = new();
        /// <summary>Gets candidates.</summary>
        public List<CandidateDiagnostic> Candidates { get; } = new();
        /// <summary>Gets placements.</summary>
        public List<PlacementDiagnostic> Placements { get; } = new();
        /// <summary>Gets target budgets.</summary>
        public List<TargetBudgetDiagnostic> TargetBudgets { get; } = new();
        /// <summary>Gets candidate rejection counts.</summary>
        public IReadOnlyDictionary<RejectionReason, int> CandidateRejectionCounts => _candidateRejectionCounts;

        private readonly Dictionary<RejectionReason, int> _candidateRejectionCounts = new();

        /// <summary>Initializes a new instance of generation diagnostics.</summary>
        public GenerationDiagnostics(string targetName, string styleName, StyleSettings styleSettings, PlacementTarget placementTargets,
            TargetDistributionMode targetDistributionMode, TargetDistributionWeights targetDistributionWeights,
            SamplingAlgorithm samplingAlgorithm, Bounds targetBounds, int requestedObjectCount, bool useFixedSeed, int randomSeed, bool bestEffort,
            RelativePlacementSettings relativePlacement,
            DiagnosticsMode captureMode = DiagnosticsMode.Summary)
        {
            RunId = Guid.NewGuid().ToString();
            TargetName = targetName;
            StyleName = styleName;
            StyleSettings = styleSettings;
            PlacementTargets = placementTargets;
            TargetDistributionMode = targetDistributionMode;
            TargetDistributionWeights = targetDistributionWeights;
            SamplingAlgorithm = samplingAlgorithm;
            TargetBounds = targetBounds;
            RequestedObjectCount = requestedObjectCount;
            UseFixedSeed = useFixedSeed;
            RandomSeed = randomSeed;
            BestEffort = bestEffort;
            RelativePlacement = relativePlacement ?? RelativePlacementSettings.Disabled;
            CaptureMode = captureMode;
            StopReason = string.Empty;
        }

        /// <summary>Preallocates diagnostic storage for the expected number of entries.</summary>
        public void EnsureCapacity(int candidateCapacity, int placementCapacity, int targetBudgetCapacity)
        {
            EnsureListCapacity(Candidates, candidateCapacity);
            EnsureListCapacity(Placements, placementCapacity);
            EnsureListCapacity(TargetBudgets, targetBudgetCapacity);
        }

        /// <summary>Records candidate outcome.</summary>
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

        /// <summary>Stores top rejection reason.</summary>
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

    /// <summary>Stores target budget data.</summary>
    public sealed class TargetBudgetDiagnostic
    {
        /// <summary>Gets placement type.</summary>
        public PlacementType PlacementType { get; }
        /// <summary>Gets the number of target items.</summary>
        public int TargetCount { get; }
        /// <summary>Gets the number of placed items.</summary>
        public int PlacedCount { get; }

        /// <summary>Initializes a new instance of target budget diagnostic.</summary>
        public TargetBudgetDiagnostic(PlacementType placementType, int targetCount, int placedCount)
        {
            PlacementType = placementType;
            TargetCount = Mathf.Max(0, targetCount);
            PlacedCount = Mathf.Max(0, placedCount);
        }
    }
}
