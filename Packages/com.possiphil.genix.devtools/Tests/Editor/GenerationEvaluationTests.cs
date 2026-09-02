using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Evaluation;
using Genix.Editor.Infrastructure;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Integration)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class GenerationEvaluationTests
    {
        private const string PackageFixtureScenePath =
            "Packages/com.possiphil.genix.devtools/Tests/Fixtures/ReadOnlyEvaluationScene.unity";

        [Test]
        public void PackageEvaluationSceneOpensFromWritableWorkspaceCopy()
        {
            string expectedPath = EvaluationSceneWorkspace.GetWritableScenePath(PackageFixtureScenePath);
            bool copyAlreadyExisted = AssetDatabase.LoadAssetAtPath<SceneAsset>(expectedPath);
            bool workspaceAlreadyExisted =
                AssetDatabase.IsValidFolder(DevToolsContentPaths.EvaluationWorkspace);
            Scene openedScene = default;

            try
            {
                bool prepared = EvaluationSceneWorkspace.TryPrepare(
                    PackageFixtureScenePath,
                    out string writablePath,
                    out string error);

                Assert.That(prepared, Is.True, error);
                Assert.That(writablePath, Is.EqualTo(expectedPath));
                Assert.That(writablePath, Does.StartWith(DevToolsContentPaths.EvaluationWorkspace));
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(writablePath), Is.Not.Null);
                Assert.That(
                    EvaluationSceneWorkspace.MatchesSource(writablePath, PackageFixtureScenePath),
                    Is.True);

                openedScene = EditorSceneManager.OpenScene(writablePath, OpenSceneMode.Additive);
                Assert.That(openedScene.IsValid(), Is.True);
            }
            finally
            {
                if (openedScene.IsValid())
                    EditorSceneManager.CloseScene(openedScene, true);
                if (!copyAlreadyExisted)
                    AssetDatabase.DeleteAsset(expectedPath);
                if (!workspaceAlreadyExisted)
                    AssetDatabase.DeleteAsset(DevToolsContentPaths.EvaluationWorkspace);
            }
        }

        [Test]
        public void ProjectEvaluationSceneDoesNotNeedWorkspaceCopy()
        {
            const string projectScenePath = "Assets/Genix/Tests/Fixtures/ProjectEvaluationScene.unity";

            Assert.That(
                EvaluationSceneWorkspace.GetWritableScenePath(projectScenePath),
                Is.EqualTo(projectScenePath));
            Assert.That(
                EvaluationSceneWorkspace.MatchesSource(projectScenePath, projectScenePath),
                Is.True);
        }

        [Test]
        public void SuiteCreatesStableDistinctTwentySeedSample()
        {
            GenerationEvaluationSuite first = ScriptableObject.CreateInstance<GenerationEvaluationSuite>();
            GenerationEvaluationSuite second = ScriptableObject.CreateInstance<GenerationEvaluationSuite>();

            try
            {
                Assert.That(first.RunsPerScenario, Is.EqualTo(20));
                Assert.That(first.Seeds.Count, Is.GreaterThanOrEqualTo(20));
                Assert.That(first.Seeds.Take(20).Distinct().Count(), Is.EqualTo(20));
                Assert.That(second.Seeds.Take(20), Is.EqualTo(first.Seeds.Take(20)));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void SuiteConfiguresExactDeterministicCampaign()
        {
            GenerationEvaluationSuite suite = ScriptableObject.CreateInstance<GenerationEvaluationSuite>();

            try
            {
                suite.ConfigureCampaign(3, 4, new[] { 17, -29, 43 });

                Assert.That(suite.RunsPerScenario, Is.EqualTo(3));
                Assert.That(suite.SettleFrames, Is.EqualTo(4));
                Assert.That(suite.Seeds, Is.EqualTo(new[] { 17, -29, 43 }));
            }
            finally
            {
                Object.DestroyImmediate(suite);
            }
        }

        [Test]
        public void SuiteRejectsIncompleteOrDuplicateCampaignSeeds()
        {
            GenerationEvaluationSuite suite = ScriptableObject.CreateInstance<GenerationEvaluationSuite>();

            try
            {
                Assert.Throws<System.ArgumentException>(() =>
                    suite.ConfigureCampaign(3, 2, new[] { 1, 2 }));
                Assert.Throws<System.ArgumentException>(() =>
                    suite.ConfigureCampaign(3, 2, new[] { 1, 2, 1 }));
            }
            finally
            {
                Object.DestroyImmediate(suite);
            }
        }

        [Test]
        public void ReportRoundTripRetainsAutomaticAndVisualObservations()
        {
            GenerationEvaluationReport report = ScriptableObject.CreateInstance<GenerationEvaluationReport>();
            GenerationEvaluationRunRecord run = new()
            {
                scenario = "Office",
                seed = 42,
                generationSucceeded = true,
                visualRating = EvaluationVisualRating.Acceptable,
                visualNotes = "Minor visible issue",
                eligibleAssetNames = new List<string> { "Monitor", "Wall Coat Rack", "Chair" },
                expectedSupportNames = new List<string> { "Desktop" },
                assetCounts = new List<GenerationEvaluationCountRecord>
                {
                    new() { name = "Monitor", count = 4 },
                    new() { name = "Wall Coat Rack", count = 1 }
                },
                supportCounts = new List<GenerationEvaluationCountRecord>
                {
                    new() { name = "Desktop", count = 4 }
                },
                checks = new List<GenerationEvaluationCheckRecord>
                {
                    new() { name = "Containment", status = EvaluationCheckStatus.Passed }
                }
            };

            try
            {
                report.Initialize(new GenerationEvaluationCampaignResult
                {
                    suiteName = "ExampleSuite",
                    suiteDependencyHash = "dependency-hash",
                    runScope = "RunAll",
                    selectedScenarioIndex = -1,
                    expectedRunCount = 20,
                    campaignCompleted = true,
                    campaignCancelled = false,
                    runs = new List<GenerationEvaluationRunRecord> { run }
                });

                GenerationEvaluationCampaignResult restored = report.ToCampaign();
                Assert.That(restored.suiteName, Is.EqualTo("ExampleSuite"));
                Assert.That(restored.suiteDependencyHash, Is.EqualTo("dependency-hash"));
                Assert.That(restored.runScope, Is.EqualTo("RunAll"));
                Assert.That(restored.selectedScenarioIndex, Is.EqualTo(-1));
                Assert.That(restored.expectedRunCount, Is.EqualTo(20));
                Assert.That(restored.campaignCompleted, Is.True);
                Assert.That(restored.campaignCancelled, Is.False);
                Assert.That(restored.runs, Has.Count.EqualTo(1));
                Assert.That(restored.runs[0].AutomaticChecksPassed, Is.True);
                Assert.That(restored.runs[0].visualRating, Is.EqualTo(EvaluationVisualRating.Acceptable));
                Assert.That(restored.runs[0].visualNotes, Is.EqualTo("Minor visible issue"));
                Assert.That(restored.runs[0].assetCounts, Has.Count.EqualTo(2));
                Assert.That(restored.runs[0].assetCounts[1].name, Is.EqualTo("Wall Coat Rack"));
                Assert.That(restored.runs[0].assetCounts[1].count, Is.EqualTo(1));
                Assert.That(restored.runs[0].eligibleAssetNames, Does.Contain("Chair"));
                Assert.That(restored.runs[0].supportCounts.Single().name, Is.EqualTo("Desktop"));
            }
            finally
            {
                Object.DestroyImmediate(report);
            }
        }

        [Test]
        public void CleanupKeepsLatestCampaignAndNewerScenarioReruns()
        {
            GenerationEvaluationReport oldFull = CreateReport(
                "ExampleSuite",
                "Assets/ExampleSuite.asset",
                "2026-08-25T10:00:00Z",
                "RunAll",
                -1,
                true,
                "Assets/Old Full.asset");
            GenerationEvaluationReport baseline = CreateReport(
                "ExampleSuite",
                "Assets/ExampleSuite.asset",
                "2026-08-27T18:00:00Z",
                "RunAll",
                -1,
                true,
                "Assets/Baseline Office.asset",
                "Assets/Baseline Outdoor.asset");
            GenerationEvaluationReport olderOfficeRerun = CreateReport(
                "ExampleSuite",
                "Assets/ExampleSuite.asset",
                "2026-08-27T18:05:00Z",
                "SelectedScenario",
                23,
                true,
                "Assets/Older Office.asset");
            GenerationEvaluationReport latestOfficeRerun = CreateReport(
                "ExampleSuite",
                "Assets/ExampleSuite.asset",
                "2026-08-27T18:08:00Z",
                "SelectedScenario",
                23,
                true,
                "Assets/Latest Office.asset");
            GenerationEvaluationReport partialOutdoorRerun = CreateReport(
                "ExampleSuite",
                "Assets/ExampleSuite.asset",
                "2026-08-27T18:09:00Z",
                "SelectedScenario",
                24,
                false,
                "Assets/Partial Outdoor.asset");
            GenerationEvaluationReport latestOutdoorRerun = CreateReport(
                "ExampleSuite",
                "Assets/ExampleSuite.asset",
                "2026-08-27T18:10:00Z",
                "SelectedScenario",
                24,
                true,
                "Assets/Latest Outdoor.asset");
            GenerationEvaluationReport otherSuite = CreateReport(
                "Other",
                "Assets/Other.asset",
                "2026-08-28T18:00:00Z",
                "RunAll",
                -1,
                true,
                "Assets/Other.asset");
            GenerationEvaluationReport[] reports =
            {
                oldFull,
                baseline,
                olderOfficeRerun,
                latestOfficeRerun,
                partialOutdoorRerun,
                latestOutdoorRerun,
                otherSuite
            };

            try
            {
                GenerationEvaluationLayoutCleanupPlan plan =
                    GenerationEvaluationLayoutCleanupService.BuildPlan(
                        reports,
                        "ExampleSuite",
                        "Assets/ExampleSuite.asset",
                        _ => true);

                Assert.That(plan.IsValid, Is.True, plan.Error);
                Assert.That(plan.BaselineReport, Is.SameAs(baseline));
                Assert.That(
                    plan.ProtectedReports,
                    Is.EquivalentTo(new[] { baseline, latestOfficeRerun, latestOutdoorRerun }));
                Assert.That(
                    plan.DeletableLayoutPaths,
                    Is.EquivalentTo(new[]
                    {
                        "Assets/Old Full.asset",
                        "Assets/Older Office.asset",
                        "Assets/Partial Outdoor.asset"
                    }));
                Assert.That(plan.DeletableLayoutPaths, Does.Not.Contain("Assets/Other.asset"));
            }
            finally
            {
                foreach (GenerationEvaluationReport report in reports)
                    Object.DestroyImmediate(report);
            }
        }

        [Test]
        public void LayoutCleanupRequiresCompletedFullCampaign()
        {
            GenerationEvaluationReport partial = CreateReport(
                "ExampleSuite",
                "Assets/ExampleSuite.asset",
                "2026-08-27T18:00:00Z",
                "RunAll",
                -1,
                false,
                "Assets/Partial.asset");

            try
            {
                GenerationEvaluationLayoutCleanupPlan plan =
                    GenerationEvaluationLayoutCleanupService.BuildPlan(
                        new[] { partial },
                        "ExampleSuite",
                        "Assets/ExampleSuite.asset",
                        _ => true);

                Assert.That(plan.IsValid, Is.False);
                Assert.That(plan.Error, Does.Contain("completed full campaign"));
                Assert.That(plan.DeletableLayoutPaths, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(partial);
            }
        }

        [Test]
        public void CleanupFindsPersistedReportsWithoutTypeIndex()
        {
            string suiteGuid = AssetDatabase.FindAssets(
                    "t:GenerationEvaluationSuite",
                    new[] { DevToolsContentPaths.EvaluationSuites })
                .FirstOrDefault();
            GenerationEvaluationSuite suite = string.IsNullOrWhiteSpace(suiteGuid)
                ? null
                : AssetDatabase.LoadAssetAtPath<GenerationEvaluationSuite>(
                    AssetDatabase.GUIDToAssetPath(suiteGuid));
            string[] persistedReports = AssetDatabase.IsValidFolder(DevToolsContentPaths.EvaluationReports)
                ? AssetDatabase.FindAssets(string.Empty, new[] { DevToolsContentPaths.EvaluationReports })
                : System.Array.Empty<string>();
            if (!suite || persistedReports.Length == 0)
                Assert.Ignore("No persisted evaluation campaign is available in this project.");

            GenerationEvaluationLayoutCleanupPlan plan =
                GenerationEvaluationLayoutCleanupService.BuildPlan(suite);

            Assert.That(plan.IsValid, Is.True, plan.Error);
            Assert.That(plan.BaselineReport.RunScope, Is.EqualTo("RunAll"));
            Assert.That(plan.ProtectedLayoutPaths, Is.Not.Empty);
            Assert.That(plan.MissingProtectedLayouts, Is.Zero);
        }

        [Test]
        public void ScenarioCoverageRetainsExpectedItemsThatWereNeverPlaced()
        {
            GenerationEvaluationRunRecord[] runs =
            {
                new()
                {
                    eligibleAssetNames = new List<string> { "Tablet", "Warning Sign" },
                    assetCounts = new List<GenerationEvaluationCountRecord>
                    {
                        new() { name = "Tablet", count = 2 }
                    }
                },
                new()
                {
                    eligibleAssetNames = new List<string> { "Tablet", "Warning Sign" },
                    assetCounts = new List<GenerationEvaluationCountRecord>
                    {
                        new() { name = "Tablet", count = 1 }
                    }
                }
            };

            IReadOnlyList<GenerationEvaluationCoverageRecord> coverage =
                GenerationEvaluationCoverage.BuildAssetCoverage(runs);
            GenerationEvaluationCoverageRecord tablet = coverage.Single(item => item.name == "Tablet");
            GenerationEvaluationCoverageRecord warning = coverage.Single(item => item.name == "Warning Sign");

            Assert.That(tablet.runsPresent, Is.EqualTo(2));
            Assert.That(tablet.totalCount, Is.EqualTo(3));
            Assert.That(warning.runsPresent, Is.Zero);
            Assert.That(warning.totalRuns, Is.EqualTo(2));
        }

        [Test]
        public void AutomaticVerdictDistinguishesPassIncompleteAndFailure()
        {
            GenerationEvaluationRunRecord run = new()
            {
                generationSucceeded = true,
                checks = new List<GenerationEvaluationCheckRecord>
                {
                    new() { status = EvaluationCheckStatus.Passed },
                    new() { status = EvaluationCheckStatus.NotApplicable }
                }
            };

            Assert.That(run.AutomaticVerdict, Is.EqualTo(EvaluationAutomaticVerdict.Incomplete));
            Assert.That(run.AutomaticChecksPassed, Is.False);
            run.checks.RemoveAt(1);
            Assert.That(run.AutomaticVerdict, Is.EqualTo(EvaluationAutomaticVerdict.Passed));
            Assert.That(run.AutomaticChecksPassed, Is.True);
            run.checks.Add(new GenerationEvaluationCheckRecord { status = EvaluationCheckStatus.Failed });
            Assert.That(run.AutomaticVerdict, Is.EqualTo(EvaluationAutomaticVerdict.Failed));
            Assert.That(run.AutomaticChecksPassed, Is.False);
            run.checks.RemoveAt(run.checks.Count - 1);
            run.generationSucceeded = false;
            Assert.That(run.AutomaticVerdict, Is.EqualTo(EvaluationAutomaticVerdict.Failed));
            Assert.That(run.AutomaticChecksPassed, Is.False);
            run.generationSucceeded = true;
            run.checks.Clear();
            Assert.That(run.AutomaticVerdict, Is.EqualTo(EvaluationAutomaticVerdict.Incomplete));
        }

        [Test]
        public void MissingContextEmitsUnavailableSpatialSourceIntegrityRecord()
        {
            using GenerationTestScene scene = new();
            GenerationEvaluationScenario scenario = GenerationEvaluationScenario.Create(
                "Missing context",
                EvaluationScenarioKind.Isolated,
                null,
                null,
                enabledChecks: EvaluationCheckSet.SpatialSourceIntegrity);

            List<GenerationEvaluationCheckRecord> checks = GenerationResultEvaluator.Evaluate(
                scenario,
                scene.CreateRequest(),
                null);

            Assert.That(checks, Has.Count.EqualTo(1));
            Assert.That(checks[0].name, Is.EqualTo("Spatial Source Integrity"));
            Assert.That(checks[0].status, Is.EqualTo(EvaluationCheckStatus.NotApplicable));
        }

        [Test]
        public void CampaignCompletionRejectsPartialCancelledAndErroredRuns()
        {
            Assert.That(GenerationEvaluationRunner.IsCampaignComplete(20, 20, false, string.Empty), Is.True);
            Assert.That(GenerationEvaluationRunner.IsCampaignComplete(19, 20, false, string.Empty), Is.False);
            Assert.That(GenerationEvaluationRunner.IsCampaignComplete(20, 20, true, string.Empty), Is.False);
            Assert.That(GenerationEvaluationRunner.IsCampaignComplete(20, 20, false, "runner error"), Is.False);
            Assert.That(GenerationEvaluationRunner.IsCampaignComplete(0, 0, false, string.Empty), Is.False);
        }

        [Test]
        public void VisualReviewEvidenceRequiresRetainedLayoutAndNonPassNote()
        {
            GenerationEvaluationRunRecord run = new()
            {
                visualRating = EvaluationVisualRating.Acceptable,
                visualNotes = string.Empty
            };

            Assert.That(run.HasLayoutReference, Is.False);
            Assert.That(run.VisualReviewCompleted, Is.False);
            Assert.That(run.VisualReviewNoteValid, Is.False);
            Assert.That(run.HasInvalidVisualReviewEvidence, Is.True);

            run.layoutAssetPath = "Assets/Missing Evaluation Layout.asset";
            run.visualNotes = "Containment: Chair intersects the target boundary.";

            Assert.That(run.HasLayoutReference, Is.True);
            Assert.That(run.VisualReviewCompleted, Is.True);
            Assert.That(run.VisualReviewNoteValid, Is.True);
            Assert.That(run.HasMissingLayoutAsset, Is.True);
            Assert.That(run.VisualReviewEvidenceValid, Is.False);
            Assert.That(run.HasInvalidVisualReviewEvidence, Is.True);
        }

        [Test]
        public void LayoutReferenceResolvesByGuidAndLoadsOnlyOnDemand()
        {
            const string path = "Assets/__GenixEvaluationLayoutReferenceTest.asset";
            SavedLayout layout = ScriptableObject.CreateInstance<SavedLayout>();
            AssetDatabase.CreateAsset(layout, path);

            try
            {
                GenerationEvaluationRunRecord run = new()
                {
                    layoutAssetPath = path,
                    layoutGuid = AssetDatabase.AssetPathToGUID(path)
                };

                Assert.That(run.HasMissingLayoutAsset, Is.False);
                Assert.That(run.ResolvedLayoutAssetPath, Is.EqualTo(path));
                Assert.That(run.LoadLayout(), Is.SameAs(layout));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void SummarySeparatesReviewableReviewedAndInvalidEvidence()
        {
            GenerationEvaluationRunRecord[] runs =
            {
                new()
                {
                    scenario = "Review",
                    scenarioKind = "Isolated",
                    layoutAssetPath = "Assets/Missing Pass Layout.asset",
                    visualRating = EvaluationVisualRating.Pass
                },
                new()
                {
                    scenario = "Review",
                    scenarioKind = "Isolated",
                    layoutAssetPath = "Assets/Missing Acceptable Layout.asset",
                    visualRating = EvaluationVisualRating.Acceptable,
                    visualNotes = string.Empty
                },
                new()
                {
                    scenario = "Review",
                    scenarioKind = "Isolated",
                    visualRating = EvaluationVisualRating.NotReviewed
                }
            };

            string[] lines = GenerationEvaluationExporter.CreateSummaryCsv(runs)
                .Trim()
                .Split('\n');
            string[] headings = lines[0].TrimEnd('\r').Split(',');
            string[] values = lines[1].TrimEnd('\r').Split(',');
            Dictionary<string, string> row = headings
                .Zip(values, (heading, value) => new { heading, value })
                .ToDictionary(item => item.heading, item => item.value);

            Assert.That(row["visual_reviewable"], Is.EqualTo("2"));
            Assert.That(row["visual_reviewed"], Is.EqualTo("2"));
            Assert.That(row["visual_valid"], Is.EqualTo("0"));
            Assert.That(row["visual_invalid_evidence"], Is.EqualTo("2"));
            Assert.That(row["visual_missing_required_notes"], Is.EqualTo("1"));
            Assert.That(row["visual_missing_layout_assets"], Is.EqualTo("2"));
            Assert.That(row["visual_unbacked_ratings"], Is.EqualTo("0"));
            Assert.That(row["visual_pass"], Is.EqualTo("1"));
            Assert.That(row["visual_acceptable"], Is.EqualTo("1"));
            Assert.That(row["visual_fail"], Is.EqualTo("0"));
        }

        [Test]
        public void ScenarioRetainsExpectedBestEffortCompletionInterval()
        {
            GenerationEvaluationScenario scenario = GenerationEvaluationScenario.Create(
                "Capacity limited",
                EvaluationScenarioKind.Isolated,
                null,
                null,
                completionRatio: 0.05f,
                maximumCompletion: 0.25f);

            Assert.That(scenario.MinimumCompletionRatio, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(scenario.MaximumCompletionRatio, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void ScenarioClampsMaximumCompletionToMinimum()
        {
            GenerationEvaluationScenario scenario = GenerationEvaluationScenario.Create(
                "Invalid interval",
                EvaluationScenarioKind.Isolated,
                null,
                null,
                completionRatio: 0.7f,
                maximumCompletion: 0.2f);

            Assert.That(scenario.MaximumCompletionRatio, Is.EqualTo(0.7f).Within(0.0001f));
        }

        private static GenerationEvaluationReport CreateReport(
            string suiteName,
            string suiteAssetPath,
            string createdAtUtc,
            string runScope,
            int selectedScenarioIndex,
            bool completed,
            params string[] layoutPaths)
        {
            GenerationEvaluationReport report = ScriptableObject.CreateInstance<GenerationEvaluationReport>();
            report.Initialize(new GenerationEvaluationCampaignResult
            {
                suiteName = suiteName,
                suiteAssetPath = suiteAssetPath,
                createdAtUtc = createdAtUtc,
                runScope = runScope,
                selectedScenarioIndex = selectedScenarioIndex,
                expectedRunCount = layoutPaths.Length,
                campaignCompleted = completed,
                runs = layoutPaths
                    .Select(path => new GenerationEvaluationRunRecord { layoutAssetPath = path })
                    .ToList()
            });
            report.name = $"{runScope} {createdAtUtc}";
            return report;
        }
    }
}
