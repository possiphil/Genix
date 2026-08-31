using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Semantics;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class DiagnosticsRecorderTests
    {
        private readonly List<UnityEngine.Object> _objects = new();
        private GenerationContext _context;
        private AssetDefinition _asset;

        [SetUp]
        public void SetUp()
        {
            GameObject areaRoot = CreateGameObject("Area");
            GameObject generatedRoot = CreateGameObject("Generated");
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            pool.Initialize("Pool", AssetPoolMode.Static);
            _objects.Add(pool);

            GameObject prefab = CreateGameObject("Prefab");
            _asset = ScriptableObject.CreateInstance<AssetDefinition>();
            _asset.Initialize(prefab, Vector3.one);
            _objects.Add(_asset);
            pool.AddStaticAsset(_asset);

            PlacementArea area = new(
                new SpatialSourceInfo("Test", "Area", "diagnostics-tests"),
                new Bounds(Vector3.zero, Vector3.one * 10f),
                new[] { SurfaceRegion.CreateFloor("Floor", -5f, 5f, -5f, 5f, -5f) },
                Array.Empty<SurfaceRegion>());
            StyleSettings style = new(
                string.Empty,
                SamplingAlgorithm.Random,
                new PlacementSettings(),
                new CandidateSettings(2, 1, false),
                new GridSettings(1f, 0f),
                new ClusterSettings(2, 1f),
                new PoissonSettings(1f, 30));
            GenerationRequest request = new(
                new StubAreaSource(areaRoot.transform),
                pool,
                4,
                PlacementTarget.Floor,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                style,
                default,
                useFixedSeed: true,
                randomSeed: 7);
            _context = new GenerationContext(request, generatedRoot.transform, area);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object value in _objects)
            {
                if (value)
                    UnityEngine.Object.DestroyImmediate(value);
            }

            _objects.Clear();
        }

        [Test]
        public void NoneModeIgnoresAllRecordedEvents()
        {
            DiagnosticsRecorder recorder = new(_context, DiagnosticsMode.None);

            recorder.RecordCandidatePool(3, Seeds());
            recorder.RecordTestedCandidateSeed(Vector3.one);
            recorder.RecordStopReason("Stopped");

            Assert.That(recorder.Diagnostics.Sampler.GeneratedCandidates, Is.Zero);
            Assert.That(recorder.Diagnostics.Sampler.TestedCandidateSeeds, Is.Zero);
            Assert.That(recorder.Diagnostics.StopReason, Is.Empty);
        }

        [Test]
        public void SummaryModeCountsPoolWithoutRetainingSeedPositions()
        {
            DiagnosticsRecorder recorder = new(_context, DiagnosticsMode.Summary);

            recorder.RecordCandidatePool(3, Seeds());

            Assert.That(recorder.Diagnostics.Sampler.RequestedCandidates, Is.EqualTo(3));
            Assert.That(recorder.Diagnostics.Sampler.GeneratedCandidates, Is.EqualTo(2));
            Assert.That(recorder.Diagnostics.Sampler.CandidateSeeds, Is.Empty);
        }

        [Test]
        public void DetailedModeRetainsPoolAndTestedSeedPositions()
        {
            DiagnosticsRecorder recorder = new(_context, DiagnosticsMode.Detailed);

            recorder.RecordCandidatePool(3, Seeds());
            recorder.RecordTestedCandidateSeed(new Vector3(4f, 5f, 6f));

            Assert.That(recorder.Diagnostics.Sampler.CandidateSeeds, Has.Count.EqualTo(2));
            Assert.That(recorder.Diagnostics.Sampler.TestedCandidateSeeds, Is.EqualTo(1));
            Assert.That(recorder.Diagnostics.Sampler.TestedCandidateSeedPositions[0], Is.EqualTo(new Vector3(4f, 5f, 6f)));
        }

        [Test]
        public void SummaryAggregatesSupportCoverageAndPrefilterSkips()
        {
            SemanticTag desktop = CreateSurfaceTag("Desktop");
            CandidateSeed first = CreateSupportSeed("Desk A", desktop, Vector3.left);
            CandidateSeed second = CreateSupportSeed("Desk B", desktop, Vector3.right);
            DiagnosticsRecorder recorder = new(_context, DiagnosticsMode.Summary);

            recorder.RecordCandidatePool(2, new[] { first, second });
            recorder.RecordSupportPrefilterSkips(7);

            SupportCandidateDiagnostic aggregate = recorder.Diagnostics.Sampler.SupportCandidates.Single();
            Assert.That(aggregate.Label, Is.EqualTo("Desktop"));
            Assert.That(aggregate.CandidateCount, Is.EqualTo(2));
            Assert.That(aggregate.SurfaceCount, Is.EqualTo(2));
            Assert.That(recorder.Diagnostics.Sampler.SupportPrefilterSkips, Is.EqualTo(7));
        }

        [Test]
        public void SummaryModeCountsRejectedCandidateWithoutRetainingGeometry()
        {
            DiagnosticsRecorder recorder = new(_context, DiagnosticsMode.Summary);

            recorder.RecordCandidate(
                "asset",
                "object",
                Candidate(),
                new Bounds(Vector3.zero, Vector3.one),
                false,
                RejectionReason.OverlapsFixed);

            Assert.That(recorder.Diagnostics.TestedCandidateCount, Is.EqualTo(1));
            Assert.That(recorder.Diagnostics.RejectedCandidateCount, Is.EqualTo(1));
            Assert.That(recorder.Diagnostics.Candidates, Is.Empty);
        }

        [Test]
        public void SummaryModeCanRetainAcceptedCandidatesForPreview()
        {
            DiagnosticsRecorder recorder = new(
                _context,
                DiagnosticsMode.Summary,
                recordAcceptedCandidates: true);

            recorder.RecordCandidate(
                "asset",
                "object",
                Candidate(),
                new Bounds(Vector3.zero, Vector3.one),
                true,
                RejectionReason.None);

            Assert.That(recorder.Diagnostics.Candidates, Has.Count.EqualTo(1));
            Assert.That(recorder.Diagnostics.Candidates[0].Accepted, Is.True);
        }

        [Test]
        public void TargetBudgetsMergeKeysAndRemainSorted()
        {
            DiagnosticsRecorder recorder = new(_context, DiagnosticsMode.Summary);
            Dictionary<PlacementType, int> targets = new() { [PlacementType.Wall] = 4 };
            Dictionary<PlacementType, int> placed = new() { [PlacementType.Floor] = 2 };

            recorder.RecordTargetBudgets(targets, placed);

            Assert.That(recorder.Diagnostics.TargetBudgets, Has.Count.EqualTo(2));
            Assert.That(recorder.Diagnostics.TargetBudgets[0].PlacementType, Is.EqualTo(PlacementType.Floor));
            Assert.That(recorder.Diagnostics.TargetBudgets[0].PlacedCount, Is.EqualTo(2));
            Assert.That(recorder.Diagnostics.TargetBudgets[1].TargetCount, Is.EqualTo(4));
        }

        [Test]
        public void SupportBudgetsAreCopiedIntoDiagnostics()
        {
            DiagnosticsRecorder recorder = new(_context, DiagnosticsMode.Summary);

            recorder.RecordSupportBudgets(new[]
            {
                new SupportBudgetDiagnostic("Shelf", 2, 2),
                new SupportBudgetDiagnostic("Default / Other Surfaces", 8, 6)
            });

            Assert.That(recorder.Diagnostics.SupportBudgets, Has.Count.EqualTo(2));
            Assert.That(recorder.Diagnostics.SupportBudgets[0].Label, Is.EqualTo("Shelf"));
            Assert.That(recorder.Diagnostics.SupportBudgets[1].PlacedCount, Is.EqualTo(6));
        }

        [Test]
        public void DetailedModeRecordsSamplingPreviewGeometry()
        {
            DiagnosticsRecorder recorder = new(_context, DiagnosticsMode.Detailed);

            recorder.RecordRawSamplePosition(Vector3.one);
            recorder.RecordClusterCenter(Vector3.right);
            recorder.RecordClusterCenters(new[] { Vector3.forward, Vector3.back });

            Assert.That(recorder.Diagnostics.Sampler.RawSamplePositions, Has.Count.EqualTo(1));
            Assert.That(recorder.Diagnostics.Sampler.ClusterCenters, Has.Count.EqualTo(3));
        }

        [Test]
        public void PlacementRecordsAssetAndObjectIdentity()
        {
            DiagnosticsRecorder recorder = new(_context, DiagnosticsMode.Summary);

            recorder.RecordPlacement(_asset, "Generated 1", Candidate());

            Assert.That(recorder.Diagnostics.Placements, Has.Count.EqualTo(1));
            Assert.That(recorder.Diagnostics.Placements[0].AssetId, Is.EqualTo(_asset.AssetName));
            Assert.That(recorder.Diagnostics.Placements[0].ObjectName, Is.EqualTo("Generated 1"));
        }

        private static List<CandidateSeed> Seeds() => new()
        {
            new CandidateSeed(Vector3.zero, Quaternion.identity),
            new CandidateSeed(Vector3.one, Quaternion.identity)
        };

        private static PlacementCandidate Candidate() => new(
            Vector3.zero,
            Quaternion.identity,
            surfaceNormal: Vector3.up,
            placementType: PlacementType.Floor);

        private GameObject CreateGameObject(string name)
        {
            GameObject value = new(name);
            _objects.Add(value);
            return value;
        }

        private SemanticTag CreateSurfaceTag(string name)
        {
            TagCategory category = ScriptableObject.CreateInstance<TagCategory>();
            category.name = name + " Category";
            category.Initialize(true, TagCategoryUsage.Surface);
            SemanticTag tag = ScriptableObject.CreateInstance<SemanticTag>();
            tag.name = name;
            tag.Initialize(category);
            _objects.Add(category);
            _objects.Add(tag);
            return tag;
        }

        private CandidateSeed CreateSupportSeed(string name, SemanticTag tag, Vector3 position)
        {
            GameObject support = CreateGameObject(name);
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetSurfaceTags(new[] { tag });
            return new CandidateSeed(
                position,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
        }

        private sealed class StubAreaSource : IAreaSource
        {
            public SpatialSourceInfo SourceInfo { get; } = new("Test", "Area", "diagnostics-tests");
            public Transform ParentTransform { get; }
            public IReadOnlyList<SemanticTag> SemanticTags => Array.Empty<SemanticTag>();
            public IReadOnlyList<TagCategory> AnyTagCategories => Array.Empty<TagCategory>();

            public StubAreaSource(Transform parentTransform) => ParentTransform = parentTransform;
            public bool IsSourceCollider(Collider collider) => false;

            public bool TryBuildArea(AreaBuildSettings settings, out PlacementArea area, out string error)
            {
                area = null;
                error = "Not used.";
                return false;
            }
        }
    }
}
