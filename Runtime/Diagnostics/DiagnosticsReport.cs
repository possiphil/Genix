using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Genix.Core;
using Genix.Extensions;
using Genix.Sampling;
using Genix.Styles;
using UnityEngine;

namespace Genix.Diagnostics
{
    public sealed class DiagnosticsReport : ScriptableObject
    {
        [SerializeField, HideInInspector] private bool _initialized;

        [SerializeField, HideInInspector] private DiagnosticsMode _mode;
        [SerializeField, HideInInspector] private string _createdAt;

        [SerializeField, HideInInspector] private string _runId;
        [SerializeField, HideInInspector] private string _targetName;
        [SerializeField, HideInInspector] private string _generationMode;
        [SerializeField, HideInInspector] private string _placementTargets;
        [SerializeField, HideInInspector] private string _targetDistributionMode;
        [SerializeField, HideInInspector] private string _targetDistributionWeights;
        [SerializeField, HideInInspector] private string _relativeSource;
        [SerializeField, HideInInspector] private float _relativeRadius;
        [SerializeField, HideInInspector] private string _styleName;
        [SerializeField, HideInInspector] private string _stopReason;

        [SerializeField, HideInInspector] private int _requestedObjectCount;
        [SerializeField, HideInInspector] private int _placedObjectCount;
        [SerializeField, HideInInspector] private bool _useRandomSeed;
        [SerializeField, HideInInspector] private int _randomSeed;
        [SerializeField, HideInInspector] private bool _bestEffort;

        [SerializeField, HideInInspector] private int _generatedCandidates;
        [SerializeField, HideInInspector] private int _testedCandidateSeeds;
        [SerializeField, HideInInspector] private int _acceptedPositions;
        [SerializeField, HideInInspector] private int _rejectedPositions;
        [SerializeField, HideInInspector] private int _testedCandidates;
        [SerializeField, HideInInspector] private int _acceptedCandidates;
        [SerializeField, HideInInspector] private int _rejectedCandidates;
        [SerializeField, HideInInspector] private int _unusedCandidates;

        [SerializeField, HideInInspector] private Bounds _targetBounds;
        [SerializeField, HideInInspector] private SamplingAlgorithm _samplingAlgorithm;
        [SerializeField, HideInInspector] private StyleSettings _styleSettings;

        [SerializeField, HideInInspector] private List<CountEntry> _placedObjects = new();
        [SerializeField, HideInInspector] private List<CountEntry> _rejectionReasons = new();
        [SerializeField, HideInInspector] private List<TargetBudgetEntry> _targetBudgets = new();

        [SerializeField, HideInInspector] private List<Vector3> _candidateSeeds = new();
        [SerializeField, HideInInspector] private List<Vector3> _rawSamplePositions = new();
        [SerializeField, HideInInspector] private List<Vector3> _clusterCenters = new();
        [SerializeField, HideInInspector] private List<CandidateEntry> _candidateDetails = new();
        [SerializeField, HideInInspector] private List<PlacementEntry> _placementDetails = new();

        public DiagnosticsMode Mode => _mode;
        public string CreatedAt => _createdAt;

        public string RunId => _runId;
        public string TargetName => _targetName;
        public string GenerationMode => _generationMode;
        public string PlacementTargets => _placementTargets;
        public string TargetDistributionMode => _targetDistributionMode;
        public string TargetDistributionWeights => _targetDistributionWeights;
        public string RelativeSource => _relativeSource;
        public float RelativeRadius => _relativeRadius;
        public string StyleName => _styleName;
        public string StopReason => _stopReason;

        public int RequestedObjectCount => _requestedObjectCount;
        public int PlacedObjectCount => _placedObjectCount;
        public bool UseRandomSeed => _useRandomSeed;
        public int RandomSeed => _randomSeed;
        public bool BestEffort => _bestEffort;

        public int GeneratedCandidates => _generatedCandidates;
        public int TestedCandidateSeeds => _testedCandidateSeeds > 0
            ? _testedCandidateSeeds
            : Mathf.Min(_testedCandidates, _generatedCandidates);
        public int AcceptedPositions => _acceptedPositions > 0 || _candidateDetails.Count == 0
            ? _acceptedPositions
            : CountPositionOutcomes(_candidateDetails).AcceptedPositions;
        public int RejectedPositions => _rejectedPositions > 0 || _candidateDetails.Count == 0
            ? _rejectedPositions
            : CountPositionOutcomes(_candidateDetails).RejectedPositions;
        public int TestedCandidates => _testedCandidates;
        public int CandidateAttempts => _testedCandidates;
        public int AcceptedCandidates => _acceptedCandidates;
        public int RejectedCandidates => _rejectedCandidates;
        public int UnusedCandidates => _unusedCandidates;

        public Bounds TargetBounds => _targetBounds;
        public SamplingAlgorithm SamplingAlgorithm => _samplingAlgorithm;
        public StyleSettings StyleSettings => _styleSettings;

        public IReadOnlyList<CountEntry> PlacedObjects => _placedObjects;
        public IReadOnlyList<CountEntry> RejectionReasons => _rejectionReasons;
        public IReadOnlyList<TargetBudgetEntry> TargetBudgets => _targetBudgets;

        public IReadOnlyList<Vector3> CandidateSeeds => _candidateSeeds;
        public IReadOnlyList<Vector3> RawSamplePositions => _rawSamplePositions;
        public IReadOnlyList<Vector3> ClusterCenters => _clusterCenters;
        public IReadOnlyList<CandidateEntry> CandidateDetails => _candidateDetails;
        public IReadOnlyList<PlacementEntry> PlacementDetails => _placementDetails;

        public bool IsDetailed => _mode == DiagnosticsMode.Detailed;
        public bool SupportsGrid => _samplingAlgorithm is SamplingAlgorithm.Grid or SamplingAlgorithm.JitteredGrid;
        public bool SupportsClusters => _samplingAlgorithm == SamplingAlgorithm.Cluster;

        public void Initialize(
            GenerationDiagnostics diagnostics,
            DiagnosticsMode mode,
            DateTime createdAt)
        {
            if (_initialized)
                throw new InvalidOperationException("This diagnostics report has already been initialized.");

            if (diagnostics == null)
                throw new ArgumentNullException(nameof(diagnostics));

            if (mode == DiagnosticsMode.None)
                throw new ArgumentException("Diagnostics report mode must not be NONE.", nameof(mode));

            _initialized = true;

            _mode = mode;
            _createdAt = createdAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            _runId = diagnostics.RunId;
            _targetName = diagnostics.TargetName;
            _generationMode = diagnostics.GenerationMode.ToDisplayName();
            _placementTargets = FormatPlacementTargets(diagnostics.PlacementTargets);
            _targetDistributionMode = diagnostics.TargetDistributionMode.ToDisplayName();
            _targetDistributionWeights = FormatTargetWeights(diagnostics.TargetDistributionWeights);
            _relativeSource = diagnostics.RelativePlacement.IsEnabled
                ? diagnostics.RelativePlacement.Source.ToDisplayName()
                : string.Empty;
            _relativeRadius = diagnostics.RelativePlacement.IsEnabled ? diagnostics.RelativePlacement.Radius : 0f;
            _styleName = GetStyleDisplayName(diagnostics);
            _stopReason = diagnostics.StopReason;

            _requestedObjectCount = diagnostics.RequestedObjectCount;
            _placedObjectCount = diagnostics.PlacedObjectCount;
            _useRandomSeed = diagnostics.UseRandomSeed;
            _randomSeed = diagnostics.RandomSeed;
            _bestEffort = diagnostics.BestEffort;

            _generatedCandidates = diagnostics.Sampler.GeneratedCandidates;
            _testedCandidateSeeds = diagnostics.Sampler.TestedCandidateSeeds;
            PositionOutcomeCounts positionOutcomes = CountPositionOutcomes(diagnostics.Candidates);
            _acceptedPositions = positionOutcomes.AcceptedPositions;
            _rejectedPositions = positionOutcomes.RejectedPositions;
            _testedCandidates = diagnostics.Candidates.Count;
            _acceptedCandidates = diagnostics.Candidates.Count(candidate => candidate.Accepted);
            _rejectedCandidates = diagnostics.Candidates.Count(candidate => !candidate.Accepted);
            _unusedCandidates = Mathf.Max(0, _generatedCandidates - _testedCandidateSeeds);

            _targetBounds = diagnostics.TargetBounds;
            _samplingAlgorithm = diagnostics.SamplingAlgorithm;
            _styleSettings = diagnostics.StyleSettings;

            _placedObjects = CreatePlacedObjectCounts(diagnostics);
            _rejectionReasons = CreateRejectionReasonCounts(diagnostics);
            _targetBudgets = CreateTargetBudgetEntries(diagnostics);

            CopyScenePreviewData(diagnostics);
        }

        private void CopyScenePreviewData(GenerationDiagnostics diagnostics)
        {
            _candidateSeeds.Clear();
            _rawSamplePositions.Clear();
            _clusterCenters.Clear();
            _candidateDetails.Clear();
            _placementDetails.Clear();

            _candidateSeeds.AddRange(diagnostics.Sampler.CandidateSeeds);
            _rawSamplePositions.AddRange(diagnostics.Sampler.RawSamplePositions);
            _clusterCenters.AddRange(diagnostics.Sampler.ClusterCenters);

            foreach (CandidateDiagnostic candidate in diagnostics.Candidates)
            {
                _candidateDetails.Add(new CandidateEntry(
                    candidate.AssetId,
                    candidate.ObjectName,
                    candidate.Position,
                    candidate.Rotation,
                    candidate.Bounds,
                    candidate.PlacementType.ToDisplayName(),
                    candidate.Accepted,
                    candidate.RejectionReason.ToDisplayName(),
                    candidate.RelatedObjectName));
            }

            foreach (PlacementDiagnostic placement in diagnostics.Placements)
            {
                _placementDetails.Add(new PlacementEntry(
                    placement.AssetId,
                    placement.ObjectName,
                    placement.Position,
                    placement.Rotation,
                    placement.PlacementType.ToDisplayName()));
            }
        }

        private static string GetStyleDisplayName(GenerationDiagnostics diagnostics)
        {
            return string.IsNullOrWhiteSpace(diagnostics.StyleName)
                ? diagnostics.SamplingAlgorithm.ToAlgorithmName()
                : diagnostics.StyleName;
        }

        private static List<CountEntry> CreatePlacedObjectCounts(GenerationDiagnostics diagnostics)
        {
            return diagnostics.Placements
                .GroupBy(placement => placement.AssetId)
                .OrderByDescending(group => group.Count())
                .Select(group => new CountEntry(group.Key, group.Count()))
                .ToList();
        }

        private static List<CountEntry> CreateRejectionReasonCounts(GenerationDiagnostics diagnostics)
        {
            return diagnostics.Candidates
                .Where(candidate => !candidate.Accepted)
                .GroupBy(candidate => candidate.RejectionReason)
                .OrderByDescending(group => group.Count())
                .Select(group => new CountEntry(group.Key.ToDisplayName(), group.Count()))
                .ToList();
        }

        private static List<TargetBudgetEntry> CreateTargetBudgetEntries(GenerationDiagnostics diagnostics)
        {
            return diagnostics.TargetBudgets
                .OrderBy(entry => entry.PlacementType)
                .Select(entry => new TargetBudgetEntry(entry.PlacementType.ToDisplayName(), entry.TargetCount, entry.PlacedCount))
                .ToList();
        }

        private static string FormatTargetWeights(TargetDistributionWeights weights)
        {
            return $"Floor {weights.Floor}, Wall {weights.Wall}, Ceiling {weights.Ceiling}, Inside Space {weights.InsideSpace}";
        }

        private static string FormatPlacementTargets(PlacementTarget targets)
        {
            targets &= PlacementTarget.All;

            if (targets == PlacementTarget.All)
                return "Any";

            if (targets == PlacementTarget.None)
                return "None";

            List<string> labels = new();

            if ((targets & PlacementTarget.Floor) != 0)
                labels.Add("Floor");

            if ((targets & PlacementTarget.Wall) != 0)
                labels.Add("Wall");

            if ((targets & PlacementTarget.Ceiling) != 0)
                labels.Add("Ceiling");

            if ((targets & PlacementTarget.InsideSpace) != 0)
                labels.Add("Inside Space");

            return string.Join(", ", labels);
        }

        private static PositionOutcomeCounts CountPositionOutcomes(IEnumerable<CandidateDiagnostic> candidates)
        {
            return DiagnosticPositionCounter.Count(
                candidates,
                candidate => candidate.Position,
                candidate => candidate.Accepted);
        }

        private static PositionOutcomeCounts CountPositionOutcomes(IEnumerable<CandidateEntry> candidates)
        {
            return DiagnosticPositionCounter.Count(
                candidates,
                candidate => candidate.Position,
                candidate => candidate.Accepted);
        }

        [Serializable]
        public struct CountEntry
        {
            [SerializeField, HideInInspector] private string _label;
            [SerializeField, HideInInspector] private int _count;

            public string Label => _label;
            public int Count => _count;

            public CountEntry(string label, int count)
            {
                _label = label;
                _count = count;
            }
        }

        [Serializable]
        public struct TargetBudgetEntry
        {
            [SerializeField, HideInInspector] private string _target;
            [SerializeField, HideInInspector] private int _targetCount;
            [SerializeField, HideInInspector] private int _placedCount;

            public string Target => _target;
            public int TargetCount => _targetCount;
            public int PlacedCount => _placedCount;

            public TargetBudgetEntry(string target, int targetCount, int placedCount)
            {
                _target = target;
                _targetCount = targetCount;
                _placedCount = placedCount;
            }
        }

        [Serializable]
        public struct CandidateEntry
        {
            [SerializeField, HideInInspector] private string _assetId;
            [SerializeField, HideInInspector] private Vector3 _position;
            [SerializeField, HideInInspector] private Quaternion _rotation;
            [SerializeField, HideInInspector] private Bounds _bounds;
            [SerializeField, HideInInspector] private string _placementType;
            [SerializeField, HideInInspector] private bool _accepted;
            [SerializeField, HideInInspector] private string _rejectionReason;

            public string AssetId => _assetId;
            public Vector3 Position => _position;
            public Quaternion Rotation => _rotation;
            public Bounds Bounds => _bounds;
            public string PlacementType => _placementType;
            public bool Accepted => _accepted;
            public string RejectionReason => _rejectionReason;

            [SerializeField, HideInInspector] private string _relatedObjectName;
            [SerializeField, HideInInspector] private string _objectName;

            public string ObjectName => _objectName;
            public string RelatedObjectName => _relatedObjectName;

            public CandidateEntry(
                string assetId,
                string objectName,
                Vector3 position,
                Quaternion rotation,
                Bounds bounds,
                string placementType,
                bool accepted,
                string rejectionReason,
                string relatedObjectName)
            {
                _assetId = assetId;
                _objectName = objectName;
                _position = position;
                _rotation = rotation;
                _bounds = bounds;
                _placementType = placementType;
                _accepted = accepted;
                _rejectionReason = rejectionReason;
                _relatedObjectName = relatedObjectName;
            }
        }

        [Serializable]
        public struct PlacementEntry
        {
            [SerializeField, HideInInspector] private string _assetId;
            [SerializeField, HideInInspector] private Vector3 _position;
            [SerializeField, HideInInspector] private Quaternion _rotation;
            [SerializeField, HideInInspector] private string _objectName;
            [SerializeField, HideInInspector] private string _placementType;

            public string AssetId => _assetId;
            public Vector3 Position => _position;
            public Quaternion Rotation => _rotation;
            public string ObjectName => _objectName;
            public string PlacementType => _placementType;

            public PlacementEntry(
                string assetId,
                string objectName,
                Vector3 position,
                Quaternion rotation,
                string placementType)
            {
                _assetId = assetId;
                _objectName = objectName;
                _position = position;
                _rotation = rotation;
                _placementType = placementType;
            }
        }
    }
}
