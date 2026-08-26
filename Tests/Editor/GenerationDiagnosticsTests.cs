using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Sampling;
using Genix.Semantics;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class GenerationDiagnosticsTests
    {
        [Test]
        public void ConstructorPreservesRunConfigurationAndCreatesUniqueId()
        {
            GenerationDiagnostics diagnostics = CreateDiagnostics();

            Assert.That(diagnostics.RunId, Is.Not.Empty);
            Assert.That(diagnostics.TargetName, Is.EqualTo("Area"));
            Assert.That(diagnostics.StyleName, Is.EqualTo("Natural"));
            Assert.That(diagnostics.RequestedObjectCount, Is.EqualTo(20));
            Assert.That(diagnostics.CaptureMode, Is.EqualTo(DiagnosticsMode.Summary));
            Assert.That(diagnostics.StopReason, Is.Empty);
        }

        [Test]
        public void CandidateOutcomeCountersStayConsistent()
        {
            GenerationDiagnostics diagnostics = CreateDiagnostics();

            diagnostics.RecordCandidateOutcome(true, RejectionReason.None);
            diagnostics.RecordCandidateOutcome(false, RejectionReason.OutsideTargetArea);
            diagnostics.RecordCandidateOutcome(false, RejectionReason.OutsideTargetArea);

            Assert.That(diagnostics.TestedCandidateCount, Is.EqualTo(3));
            Assert.That(diagnostics.AcceptedCandidateCount, Is.EqualTo(1));
            Assert.That(diagnostics.RejectedCandidateCount, Is.EqualTo(2));
            Assert.That(diagnostics.CandidateRejectionCounts[RejectionReason.OutsideTargetArea], Is.EqualTo(2));
            Assert.That(diagnostics.TopRejectionReason, Does.Contain("2"));
        }

        [Test]
        public void AcceptedOutcomesDoNotCreateRejectionEntries()
        {
            GenerationDiagnostics diagnostics = CreateDiagnostics();

            diagnostics.RecordCandidateOutcome(true, RejectionReason.None);

            Assert.That(diagnostics.CandidateRejectionCounts, Is.Empty);
            Assert.That(diagnostics.TopRejectionReason, Is.Empty);
        }

        [Test]
        public void LegacyCandidateDetailsStillProvideTopRejection()
        {
            GenerationDiagnostics diagnostics = CreateDiagnostics();
            diagnostics.Candidates.Add(new CandidateDiagnostic(
                "asset",
                "object",
                Vector3.zero,
                Quaternion.identity,
                new Bounds(Vector3.zero, Vector3.one),
                PlacementType.Floor,
                false,
                RejectionReason.OverlapsFixed,
                "Obstacle"));

            Assert.That(diagnostics.HasCandidateOutcomeCounts, Is.False);
            Assert.That(diagnostics.TopRejectionReason, Does.Contain("Overlaps Fixed"));
        }

        [Test]
        public void EnsureCapacityClampsNegativeValuesAndGrowsLists()
        {
            GenerationDiagnostics diagnostics = CreateDiagnostics();

            Assert.DoesNotThrow(() => diagnostics.EnsureCapacity(-10, 12, 5));
            Assert.That(diagnostics.Placements.Capacity, Is.GreaterThanOrEqualTo(12));
            Assert.That(diagnostics.TargetBudgets.Capacity, Is.GreaterThanOrEqualTo(5));
        }

        [TestCase(-1, -2, 0, 0)]
        [TestCase(4, 3, 4, 3)]
        public void TargetBudgetDiagnosticClampsNegativeCounts(int target, int placed, int expectedTarget, int expectedPlaced)
        {
            TargetBudgetDiagnostic budget = new(PlacementType.Wall, target, placed);

            Assert.That(budget.TargetCount, Is.EqualTo(expectedTarget));
            Assert.That(budget.PlacedCount, Is.EqualTo(expectedPlaced));
        }

        [Test]
        public void DiagnosticsReportCopiesSummaryCountsAndMetadata()
        {
            GenerationDiagnostics diagnostics = CreateDiagnostics(styleName: string.Empty);
            diagnostics.DryRun = true;
            diagnostics.StopReason = "Completed";
            diagnostics.Sampler.GeneratedCandidates = 8;
            diagnostics.Sampler.TestedCandidateSeeds = 5;
            diagnostics.Sampler.SupportPrefilterSkips = 13;
            SupportCandidateDiagnostic support = new("Desktop");
            GameObject supportObject = new("Desk");
            PlacementSurfaceDescriptor descriptor = supportObject.AddComponent<PlacementSurfaceDescriptor>();
            support.Record(descriptor);
            support.Record(descriptor);
            diagnostics.Sampler.SupportCandidates.Add(support);
            diagnostics.RecordCandidateOutcome(true, RejectionReason.None);
            diagnostics.RecordCandidateOutcome(false, RejectionReason.OverlapsFixed);
            diagnostics.Placements.Add(new PlacementDiagnostic(
                "Rock",
                "Rock 1",
                Vector3.one,
                Quaternion.identity,
                PlacementType.Floor));
            diagnostics.TargetBudgets.Add(new TargetBudgetDiagnostic(PlacementType.Floor, 3, 1));
            diagnostics.SupportBudgets.Add(new SupportBudgetDiagnostic("Desktop", 2, 1));
            DiagnosticsReport report = ScriptableObject.CreateInstance<DiagnosticsReport>();

            try
            {
                report.Initialize(diagnostics, DiagnosticsMode.Summary, new DateTime(2026, 8, 2, 20, 30, 40));

                Assert.That(report.CreatedAt, Is.EqualTo("2026-08-02 20:30:40"));
                Assert.That(report.TargetName, Is.EqualTo("Area"));
                Assert.That(report.PlacementTargets, Is.EqualTo("Floor"));
                Assert.That(report.StyleName, Is.EqualTo("Random Sampling"));
                Assert.That(report.StopReason, Is.EqualTo("Completed"));
                Assert.That(report.DryRun, Is.True);
                Assert.That(report.GeneratedCandidates, Is.EqualTo(8));
                Assert.That(report.TestedCandidateSeeds, Is.EqualTo(5));
                Assert.That(report.AcceptedCandidates, Is.EqualTo(1));
                Assert.That(report.RejectedCandidates, Is.EqualTo(1));
                Assert.That(report.UnusedCandidates, Is.EqualTo(3));
                Assert.That(report.SupportPrefilterSkips, Is.EqualTo(13));
                Assert.That(report.SupportCandidates.Single().CandidateCount, Is.EqualTo(2));
                Assert.That(report.SupportCandidates.Single().SurfaceCount, Is.EqualTo(1));
                Assert.That(report.PlacedObjects.Single().Label, Is.EqualTo("Rock"));
                Assert.That(report.RejectionReasons.Single().Count, Is.EqualTo(1));
                Assert.That(report.TargetBudgets.Single().PlacedCount, Is.EqualTo(1));
                Assert.That(report.SupportBudgets.Single().Label, Is.EqualTo("Desktop"));
                Assert.That(report.SupportBudgets.Single().PlacedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(supportObject);
                Object.DestroyImmediate(report);
            }
        }

        [Test]
        public void DetailedReportCopiesGeometryAndCountsUniquePositionOutcomes()
        {
            GenerationDiagnostics diagnostics = CreateDiagnostics();
            diagnostics.Sampler.GeneratedCandidates = 4;
            diagnostics.Sampler.CandidateSeeds.Add(Vector3.forward);
            diagnostics.Sampler.RawSamplePositions.Add(Vector3.back);
            diagnostics.Sampler.ClusterCenters.Add(Vector3.left);
            diagnostics.Candidates.Add(new CandidateDiagnostic(
                "Rock",
                "Rock Rejected",
                Vector3.zero,
                Quaternion.identity,
                new Bounds(Vector3.zero, Vector3.one),
                PlacementType.Floor,
                false,
                RejectionReason.OverlapsFixed,
                "Wall"));
            diagnostics.Candidates.Add(new CandidateDiagnostic(
                "Rock",
                "Rock Accepted",
                Vector3.zero,
                Quaternion.identity,
                new Bounds(Vector3.zero, Vector3.one),
                PlacementType.Floor,
                true,
                RejectionReason.None,
                string.Empty));
            diagnostics.Candidates.Add(new CandidateDiagnostic(
                "Tree",
                "Tree Rejected",
                Vector3.one,
                Quaternion.identity,
                new Bounds(Vector3.one, Vector3.one),
                PlacementType.Floor,
                false,
                RejectionReason.OutsideTargetArea,
                string.Empty));
            diagnostics.Placements.Add(new PlacementDiagnostic(
                "Rock",
                "Rock Accepted",
                Vector3.zero,
                Quaternion.identity,
                PlacementType.Floor));
            DiagnosticsReport report = ScriptableObject.CreateInstance<DiagnosticsReport>();

            try
            {
                report.Initialize(diagnostics, DiagnosticsMode.Detailed, DateTime.UtcNow);

                Assert.That(report.IsDetailed, Is.True);
                Assert.That(report.AcceptedPositions, Is.EqualTo(1));
                Assert.That(report.RejectedPositions, Is.EqualTo(1));
                Assert.That(report.CandidateSeeds, Is.EqualTo(new[] { Vector3.forward }));
                Assert.That(report.RawSamplePositions, Is.EqualTo(new[] { Vector3.back }));
                Assert.That(report.ClusterCenters, Is.EqualTo(new[] { Vector3.left }));
                Assert.That(report.CandidateDetails, Has.Count.EqualTo(3));
                Assert.That(report.CandidateDetails[0].RelatedObjectName, Is.EqualTo("Wall"));
                Assert.That(report.PlacementDetails.Single().ObjectName, Is.EqualTo("Rock Accepted"));
            }
            finally
            {
                Object.DestroyImmediate(report);
            }
        }

        [TestCase(SamplingAlgorithm.Random, false, false)]
        [TestCase(SamplingAlgorithm.Grid, true, false)]
        [TestCase(SamplingAlgorithm.JitteredGrid, true, false)]
        [TestCase(SamplingAlgorithm.Cluster, false, true)]
        [TestCase(SamplingAlgorithm.BridsonPoissonDisk, false, false)]
        public void DiagnosticsReportExposesAlgorithmSpecificPreviewCapabilities(
            SamplingAlgorithm algorithm,
            bool supportsGrid,
            bool supportsClusters)
        {
            DiagnosticsReport report = ScriptableObject.CreateInstance<DiagnosticsReport>();

            try
            {
                report.Initialize(CreateDiagnostics(algorithm), DiagnosticsMode.Summary, DateTime.UtcNow);
                Assert.That(report.SupportsGrid, Is.EqualTo(supportsGrid));
                Assert.That(report.SupportsClusters, Is.EqualTo(supportsClusters));
            }
            finally
            {
                Object.DestroyImmediate(report);
            }
        }

        [Test]
        public void DiagnosticsReportRejectsInvalidInitialization()
        {
            DiagnosticsReport nullReport = ScriptableObject.CreateInstance<DiagnosticsReport>();
            DiagnosticsReport noneReport = ScriptableObject.CreateInstance<DiagnosticsReport>();
            DiagnosticsReport initializedReport = ScriptableObject.CreateInstance<DiagnosticsReport>();

            try
            {
                Assert.Throws<ArgumentNullException>(() =>
                    nullReport.Initialize(null, DiagnosticsMode.Summary, DateTime.UtcNow));
                Assert.Throws<ArgumentException>(() =>
                    noneReport.Initialize(CreateDiagnostics(), DiagnosticsMode.None, DateTime.UtcNow));

                initializedReport.Initialize(CreateDiagnostics(), DiagnosticsMode.Summary, DateTime.UtcNow);
                Assert.Throws<InvalidOperationException>(() =>
                    initializedReport.Initialize(CreateDiagnostics(), DiagnosticsMode.Summary, DateTime.UtcNow));
            }
            finally
            {
                Object.DestroyImmediate(nullReport);
                Object.DestroyImmediate(noneReport);
                Object.DestroyImmediate(initializedReport);
            }
        }

        [Test]
        public void DiagnosticsCatalogKeepsDistinctReportsAndRemovesDestroyedEntries()
        {
            DiagnosticsCatalog catalog = ScriptableObject.CreateInstance<DiagnosticsCatalog>();
            DiagnosticsReport first = ScriptableObject.CreateInstance<DiagnosticsReport>();
            DiagnosticsReport second = ScriptableObject.CreateInstance<DiagnosticsReport>();

            try
            {
                catalog.SetReports(new[] { first, first, null, second });
                catalog.AddReport(second);
                Assert.That(catalog.Reports, Is.EqualTo(new[] { first, second }));

                Object.DestroyImmediate(first);
                catalog.RemoveMissingReports();
                Assert.That(catalog.Reports, Is.EqualTo(new[] { second }));
            }
            finally
            {
                if (first)
                    Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(catalog);
            }
        }

        private static GenerationDiagnostics CreateDiagnostics(
            SamplingAlgorithm algorithm = SamplingAlgorithm.Random,
            string styleName = "Natural") => new(
            "Area",
            styleName,
            new StyleSettings { algorithm = algorithm },
            PlacementTarget.Floor,
            TargetDistributionMode.Random,
            TargetDistributionWeights.Default,
            algorithm,
            new Bounds(Vector3.zero, Vector3.one * 10f),
            20,
            true,
            42,
            true,
            RelativePlacementSettings.Disabled,
            DiagnosticsMode.Summary);
    }
}
