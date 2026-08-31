using System;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Profiling;
using Genix.Placement;
using Genix.Profiling;
using Genix.Sampling;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.PerformanceArea)]
    public sealed class GenerationProfilingTests
    {
        [Test]
        public void PhaseTimesAccumulateAndIgnoreNegativeDurations()
        {
            GenerationProfile profile = new();

            profile.AddPhaseTime(GenerationProfilePhase.Planning, 4f);
            profile.AddPhaseTime(GenerationProfilePhase.Planning, 6f);
            profile.AddPhaseTime(GenerationProfilePhase.Planning, -100f);

            Assert.That(profile.GetPhaseTime(GenerationProfilePhase.Planning), Is.EqualTo(10f));
            Assert.That(profile.GetPhaseTime(GenerationProfilePhase.Apply), Is.Zero);
        }

        [Test]
        public void AreaBuildStepsAccumulateDurationAndCalls()
        {
            GenerationProfile profile = new();

            profile.AddAreaBuildStep(AreaBuildProfileStep.VoxelScan, 3f, 2);
            profile.AddAreaBuildStep(AreaBuildProfileStep.VoxelScan, 5f, 4);

            GenerationAreaBuildStepProfile step = profile.AreaBuildSteps.Single();
            Assert.That(step.Milliseconds, Is.EqualTo(8f));
            Assert.That(step.Calls, Is.EqualTo(6));
        }

        [Test]
        public void PlanningStepsAreSortedByDescendingDuration()
        {
            GenerationProfile profile = new();
            profile.AddPlanningStep(PlanningProfileStep.AssetOrder, 2f);
            profile.AddPlanningStep(PlanningProfileStep.CandidateBuild, 8f);

            Assert.That(profile.GetSortedPlanningSteps().First().Step, Is.EqualTo(PlanningProfileStep.CandidateBuild));
        }

        [Test]
        public void TargetProfileTracksProjectionHitsAndMisses()
        {
            GenerationTargetProfile target = new(PlacementType.Floor);

            target.AddProjection(true, 2f);
            target.AddProjection(false, 3f);

            Assert.That(target.ProjectionAttempts, Is.EqualTo(2));
            Assert.That(target.ProjectionHits, Is.EqualTo(1));
            Assert.That(target.ProjectionMisses, Is.EqualTo(1));
            Assert.That(target.ProjectionMilliseconds, Is.EqualTo(5f));
        }

        [Test]
        public void TargetProfileTracksRaycastCallsAndMultipleHits()
        {
            GenerationTargetProfile target = new(PlacementType.Floor);

            target.AddRaycast(3, 1.5f);
            target.AddRaycast(-2, -1f);

            Assert.That(target.RaycastCalls, Is.EqualTo(2));
            Assert.That(target.RaycastHits, Is.EqualTo(3));
            Assert.That(target.RaycastMilliseconds, Is.EqualTo(1.5f));
        }

        [Test]
        public void TargetProfileSeparatesAcceptedAndRejectedAttempts()
        {
            GenerationTargetProfile target = new(PlacementType.InsideSpace);

            target.AddAssetAttempt(true, RejectionReason.None, 2f);
            target.AddAssetAttempt(false, RejectionReason.OverlapsFixed, 3f);
            target.AddAssetAttempt(false, RejectionReason.OverlapsFixed, -1f);

            Assert.That(target.AssetAttempts, Is.EqualTo(3));
            Assert.That(target.AcceptedAttempts, Is.EqualTo(1));
            Assert.That(target.RejectedAttempts, Is.EqualTo(2));
            Assert.That(target.ValidationMilliseconds, Is.EqualTo(5f));
            Assert.That(target.RejectionCounts[RejectionReason.OverlapsFixed], Is.EqualTo(2));
        }

        [Test]
        public void ValidationStepsAccumulateCallsAndClampDurations()
        {
            GenerationTargetProfile target = new(PlacementType.Floor);

            target.AddValidationStep(ValidationProfileStep.SurfaceFit, 4f);
            target.AddValidationStep(ValidationProfileStep.SurfaceFit, -2f);

            ValidationStepProfile step = target.ValidationSteps.Single();
            Assert.That(step.Calls, Is.EqualTo(2));
            Assert.That(step.Milliseconds, Is.EqualTo(4f));
        }

        [Test]
        public void UnattributedPlanningExcludesCandidateGenerationTime()
        {
            GenerationProfile profile = new();
            profile.AddPhaseTime(GenerationProfilePhase.CandidateGeneration, 20f);
            profile.AddPlanningStep(PlanningProfileStep.CandidateIteration, 70f);
            profile.AddPlanningStep(PlanningProfileStep.CandidateBuild, 30f);

            profile.RecordPlanningUnattributedTime(100f);

            Assert.That(profile.PlanningUnattributedMilliseconds, Is.EqualTo(20f));
        }

        [Test]
        public void ManagedRuntimeStatsClampInvalidValues()
        {
            GenerationProfile profile = new();

            profile.RecordManagedRuntimeStats(-1, 2, 3, -10, 50);

            Assert.That(profile.HasManagedRuntimeStats, Is.True);
            Assert.That(profile.GarbageCollectionsGen0, Is.Zero);
            Assert.That(profile.GarbageCollectionsGen1, Is.EqualTo(2));
            Assert.That(profile.ManagedMemoryBeforeBytes, Is.Zero);
            Assert.That(profile.ManagedMemoryAfterBytes, Is.EqualTo(50));
            Assert.That(profile.ManagedMemoryDeltaBytes, Is.EqualTo(50));
        }

        [Test]
        public void ProfileInitializationCopiesGenerationContextMetadata()
        {
            using GenerationTestScene scene = new(sourceName: "Profile Area");
            GenerationContext context = scene.CreateContext(scene.CreateRequest(
                count: 7,
                targets: PlacementTarget.All,
                algorithm: SamplingAlgorithm.Grid,
                seed: 88));
            GenerationProfile profile = new();

            profile.Initialize(context, " ", dryRun: true);

            Assert.That(profile.TargetName, Is.EqualTo("Profile Area"));
            Assert.That(profile.StyleName, Is.EqualTo("Grid Sampling"));
            Assert.That(profile.RunType, Is.EqualTo("Preview Run"));
            Assert.That(profile.PlacementTargets, Is.EqualTo("Any"));
            Assert.That(profile.SamplingAlgorithm, Is.EqualTo("Grid Sampling"));
            Assert.That(profile.RequestedObjectCount, Is.EqualTo(7));
            Assert.That(profile.RandomSeed, Is.EqualTo(88));
            Assert.That(profile.UseFixedSeed, Is.True);
        }

        [Test]
        public void ProfileInitializationIgnoresMissingContext()
        {
            GenerationProfile profile = new();

            Assert.DoesNotThrow(() => profile.Initialize(null, "Natural", dryRun: false));
            Assert.That(profile.TargetName, Is.Empty);
            Assert.That(profile.RequestedObjectCount, Is.Zero);
        }

        [Test]
        public void RecorderForwardsAttemptsToTargetAndPlanningProfiles()
        {
            GenerationProfilerRecorder recorder = new();

            recorder.RecordAssetAttempt(PlacementType.Wall, false, RejectionReason.OutsideTargetArea, 7f);

            GenerationTargetProfile target = recorder.Profile.GetTarget(PlacementType.Wall);
            GenerationPlanningStepProfile planning = recorder.Profile.PlanningSteps.Single();
            Assert.That(target.RejectedAttempts, Is.EqualTo(1));
            Assert.That(target.ValidationMilliseconds, Is.EqualTo(7f));
            Assert.That(planning.Step, Is.EqualTo(PlanningProfileStep.CandidateValidation));
            Assert.That(planning.Milliseconds, Is.EqualTo(7f));
        }

        [Test]
        public void RecorderForwardsEveryMeasurementKind()
        {
            GenerationProfilerRecorder recorder = new();

            recorder.AddPhaseTime(GenerationProfilePhase.Total, 20f);
            recorder.RecordAreaBuildStep(AreaBuildProfileStep.SceneIndex, 2f, 3);
            recorder.RecordPlanningStep(PlanningProfileStep.AssetCatalog, 4f, 5);
            recorder.RecordCandidateCacheHit(true);
            recorder.RecordRawSamples(PlacementType.Ceiling, 11);
            recorder.RecordCandidateSeeds(PlacementType.Ceiling, 9);
            recorder.RecordTestedSeed(PlacementType.Ceiling);
            recorder.AddSeedGenerationTime(PlacementType.Ceiling, 6f);
            recorder.AddSamplingTime(PlacementType.Ceiling, 7f);
            recorder.RecordProjection(PlacementType.Ceiling, true, 8f);
            recorder.RecordRaycast(PlacementType.Ceiling, 2, 9f);
            recorder.RecordValidationStep(PlacementType.Ceiling, ValidationProfileStep.SurfaceFit, 10f);

            GenerationTargetProfile target = recorder.Profile.GetTarget(PlacementType.Ceiling);
            Assert.That(recorder.Profile.GetPhaseTime(GenerationProfilePhase.Total), Is.EqualTo(20f));
            Assert.That(recorder.Profile.AreaBuildSteps.Single().Calls, Is.EqualTo(3));
            Assert.That(recorder.Profile.PlanningSteps.Single().Calls, Is.EqualTo(5));
            Assert.That(recorder.Profile.CandidateCacheHit, Is.True);
            Assert.That(target.RawSamples, Is.EqualTo(11));
            Assert.That(target.CandidateSeeds, Is.EqualTo(9));
            Assert.That(target.TestedSeeds, Is.EqualTo(1));
            Assert.That(target.SeedGenerationMilliseconds, Is.EqualTo(6f));
            Assert.That(target.SamplingMilliseconds, Is.EqualTo(7f));
            Assert.That(target.ProjectionHits, Is.EqualTo(1));
            Assert.That(target.RaycastHits, Is.EqualTo(2));
            Assert.That(target.ValidationSteps.Single().Milliseconds, Is.EqualTo(10f));
        }

        [Test]
        public void NullProfilerRemainsDisabledAndUnchanged()
        {
            NullGenerationProfiler profiler = NullGenerationProfiler.Instance;
            int targetCount = profiler.Profile.Targets.Count;

            profiler.RecordRawSamples(PlacementType.Floor, 10);
            profiler.RecordAssetAttempt(PlacementType.Floor, false, RejectionReason.OutsideTargetArea, 5f);

            Assert.That(profiler.IsEnabled, Is.False);
            Assert.That(profiler.Profile.Targets.Count, Is.EqualTo(targetCount));
        }

        [Test]
        public void NullProfilerIgnoresEveryMeasurementKind()
        {
            NullGenerationProfiler profiler = NullGenerationProfiler.Instance;
            int phaseCount = profiler.Profile.PhaseTimes.Count;
            int areaStepCount = profiler.Profile.AreaBuildSteps.Count;
            int planningStepCount = profiler.Profile.PlanningSteps.Count;
            int targetCount = profiler.Profile.Targets.Count;

            profiler.Initialize(null, "Ignored", true);
            profiler.AddPhaseTime(GenerationProfilePhase.Total, 1f);
            profiler.RecordAreaBuildStep(AreaBuildProfileStep.SceneIndex, 1f);
            profiler.RecordPlanningStep(PlanningProfileStep.AssetCatalog, 1f);
            profiler.RecordCandidateCacheHit(true);
            profiler.RecordRawSamples(PlacementType.Wall, 1);
            profiler.RecordCandidateSeeds(PlacementType.Wall, 1);
            profiler.RecordTestedSeed(PlacementType.Wall);
            profiler.AddSeedGenerationTime(PlacementType.Wall, 1f);
            profiler.AddSamplingTime(PlacementType.Wall, 1f);
            profiler.RecordProjection(PlacementType.Wall, true, 1f);
            profiler.RecordRaycast(PlacementType.Wall, 1, 1f);
            profiler.RecordAssetAttempt(PlacementType.Wall, false, RejectionReason.OverlapsFixed, 1f);
            profiler.RecordValidationStep(PlacementType.Wall, ValidationProfileStep.FixedOverlap, 1f);

            Assert.That(profiler.Profile.PhaseTimes.Count, Is.EqualTo(phaseCount));
            Assert.That(profiler.Profile.AreaBuildSteps.Count, Is.EqualTo(areaStepCount));
            Assert.That(profiler.Profile.PlanningSteps.Count, Is.EqualTo(planningStepCount));
            Assert.That(profiler.Profile.Targets.Count, Is.EqualTo(targetCount));
        }

        [Test]
        public void ProfilerServicePublishesEnableStoreAndClearTransitions()
        {
            GenerationProfilerService.SetProfilingEnabled(false);
            GenerationProfilerService.ClearLastProfile();
            int changes = 0;
            void OnChanged() => changes++;
            GenerationProfilerService.Changed += OnChanged;

            try
            {
                Assert.That(GenerationProfilerService.CreateRecorderIfEnabled(), Is.Null);
                GenerationProfilerService.SetProfilingEnabled(true);
                GenerationProfilerRecorder recorder = GenerationProfilerService.CreateRecorderIfEnabled();
                Assert.That(recorder, Is.Not.Null);

                GenerationProfilerService.SetProfilingEnabled(true);
                GenerationProfilerService.Store(null);
                GenerationProfilerService.Store(recorder);
                Assert.That(GenerationProfilerService.LastProfile, Is.SameAs(recorder.Profile));

                GenerationProfilerService.ClearLastProfile();
                Assert.That(GenerationProfilerService.LastProfile, Is.Null);
                Assert.That(changes, Is.EqualTo(3));
            }
            finally
            {
                GenerationProfilerService.Changed -= OnChanged;
                GenerationProfilerService.SetProfilingEnabled(false);
                GenerationProfilerService.ClearLastProfile();
            }
        }

        [Test]
        public void ProfileReportCopiesPhasesStepsTargetsAndManagedStats()
        {
            GenerationProfile profile = new();
            profile.AddPhaseTime(GenerationProfilePhase.Planning, 12f);
            profile.AddAreaBuildStep(AreaBuildProfileStep.VoxelScan, 4f, 2);
            profile.AddPlanningStep(PlanningProfileStep.CandidateBuild, 7f, 3);
            profile.RecordManagedRuntimeStats(1, 2, 3, 100, 175);
            GenerationTargetProfile target = profile.GetTarget(PlacementType.Floor);
            target.AddRawSamples(8);
            target.AddCandidateSeeds(6);
            target.AddTestedSeed();
            target.AddProjection(true, 2f);
            target.AddRaycast(2, 3f);
            target.AddAssetAttempt(false, RejectionReason.OutsideTargetArea, 5f);
            target.AddValidationStep(ValidationProfileStep.SurfaceFit, 4f);
            GenerationProfileReport report = ScriptableObject.CreateInstance<GenerationProfileReport>();

            try
            {
                report.Initialize(profile, new DateTime(2026, 8, 2, 21, 15, 16));

                Assert.That(report.CreatedAt, Is.EqualTo("2026-08-02 21:15:16"));
                Assert.That(report.GetPhaseTime(GenerationProfilePhase.Planning), Is.EqualTo(12f));
                Assert.That(report.GetPhaseTime(GenerationProfilePhase.Apply), Is.Zero);
                Assert.That(report.AreaBuildSteps.Single().Calls, Is.EqualTo(2));
                Assert.That(report.PlanningSteps.Single().Milliseconds, Is.EqualTo(7f));
                Assert.That(report.HasManagedRuntimeStats, Is.True);
                Assert.That(report.ManagedMemoryDeltaBytes, Is.EqualTo(75));

                GenerationProfileReport.TargetEntry targetEntry = report.Targets.Single();
                Assert.That(targetEntry.PlacementType, Is.EqualTo("Floor"));
                Assert.That(targetEntry.RawSamples, Is.EqualTo(8));
                Assert.That(targetEntry.ProjectionHits, Is.EqualTo(1));
                Assert.That(targetEntry.RaycastHits, Is.EqualTo(2));
                Assert.That(targetEntry.RejectedAttempts, Is.EqualTo(1));
                Assert.That(targetEntry.ValidationSteps.Single().Step, Is.EqualTo("SurfaceFit"));
                Assert.That(targetEntry.Rejections.Single().Reason, Is.EqualTo("Outside Target Surface"));
            }
            finally
            {
                Object.DestroyImmediate(report);
            }
        }

        [TestCase(false, false, "Not reached")]
        [TestCase(true, false, "Generated")]
        [TestCase(true, true, "Cache")]
        public void ProfileReportDescribesCandidateSource(
            bool reachedCandidateGeneration,
            bool cacheHit,
            string expected)
        {
            GenerationProfile profile = new() { CandidateCacheHit = cacheHit };
            if (reachedCandidateGeneration)
                profile.AddPhaseTime(GenerationProfilePhase.CandidateGeneration, 1f);
            GenerationProfileReport report = ScriptableObject.CreateInstance<GenerationProfileReport>();

            try
            {
                report.Initialize(profile, DateTime.UtcNow);
                Assert.That(report.CandidateSource, Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(report);
            }
        }

        [Test]
        public void ProfileReportRejectsNullAndRepeatedInitialization()
        {
            GenerationProfileReport nullReport = ScriptableObject.CreateInstance<GenerationProfileReport>();
            GenerationProfileReport initializedReport = ScriptableObject.CreateInstance<GenerationProfileReport>();

            try
            {
                Assert.Throws<ArgumentNullException>(() => nullReport.Initialize(null, DateTime.UtcNow));

                initializedReport.Initialize(new GenerationProfile(), DateTime.UtcNow);
                Assert.Throws<InvalidOperationException>(() =>
                    initializedReport.Initialize(new GenerationProfile(), DateTime.UtcNow));
            }
            finally
            {
                Object.DestroyImmediate(nullReport);
                Object.DestroyImmediate(initializedReport);
            }
        }

        [Test]
        public void SerializedProfileEntriesClampNegativeMeasurements()
        {
            GenerationProfileReport.AreaBuildStepEntry area = new("VoxelScan", -1f, -2);
            GenerationProfileReport.PlanningStepEntry planning = new("CandidateBuild", -3f, -4);
            GenerationProfileReport.PhaseEntry phase = new(GenerationProfilePhase.Total, -5f);
            GenerationProfileReport.ValidationStepEntry validation = new("SurfaceFit", -6f, -7);
            GenerationProfileReport.RejectionEntry rejection = new("Rejected", -8);

            Assert.That(area.Milliseconds, Is.Zero);
            Assert.That(area.Calls, Is.Zero);
            Assert.That(planning.Milliseconds, Is.Zero);
            Assert.That(planning.Calls, Is.Zero);
            Assert.That(phase.Milliseconds, Is.Zero);
            Assert.That(validation.Milliseconds, Is.Zero);
            Assert.That(validation.Calls, Is.Zero);
            Assert.That(rejection.Count, Is.Zero);
        }

        [Test]
        public void ProfileCatalogKeepsDistinctReportsAndRemovesDestroyedEntries()
        {
            GenerationProfileCatalog catalog = ScriptableObject.CreateInstance<GenerationProfileCatalog>();
            GenerationProfileReport first = ScriptableObject.CreateInstance<GenerationProfileReport>();
            GenerationProfileReport second = ScriptableObject.CreateInstance<GenerationProfileReport>();

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
    }
}
