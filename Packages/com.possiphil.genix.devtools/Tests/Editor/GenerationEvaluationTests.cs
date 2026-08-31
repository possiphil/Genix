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
        public void ThesisSuiteUsesFrozenSummativeSeedBlock()
        {
            int[] expected =
            {
                -1851488837, 594494322, -1423066958, -1689967793, -625522695,
                2068292989, 1927287721, 1647605635, 1950899649, -2114452199,
                -1439182028, 1518213276, -1260957334, -2118255369, -96239834,
                559500117, 239994572, 476828007, -1364768060, -1207775653
            };

            Assert.That(ThesisEvaluationSuiteFactory.FinalRunsPerScenario, Is.EqualTo(20));
            Assert.That(ThesisEvaluationSuiteFactory.FinalSettleFrames, Is.EqualTo(2));
            Assert.That(ThesisEvaluationSuiteFactory.FinalSeeds, Is.EqualTo(expected));
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
                    suiteName = "Thesis",
                    suiteDependencyHash = "dependency-hash",
                    runScope = "RunAll",
                    selectedScenarioIndex = -1,
                    expectedRunCount = 20,
                    campaignCompleted = true,
                    campaignCancelled = false,
                    runs = new List<GenerationEvaluationRunRecord> { run }
                });

                GenerationEvaluationCampaignResult restored = report.ToCampaign();
                Assert.That(restored.suiteName, Is.EqualTo("Thesis"));
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
        public void LayoutCleanupKeepsLatestFullCampaignAndLatestNewerRerunPerScenario()
        {
            GenerationEvaluationReport oldFull = CreateReport(
                "Thesis",
                "Assets/Thesis.asset",
                "2026-08-25T10:00:00Z",
                "RunAll",
                -1,
                true,
                "Assets/Old Full.asset");
            GenerationEvaluationReport baseline = CreateReport(
                "Thesis",
                "Assets/Thesis.asset",
                "2026-08-27T18:00:00Z",
                "RunAll",
                -1,
                true,
                "Assets/Baseline Office.asset",
                "Assets/Baseline Outdoor.asset");
            GenerationEvaluationReport olderOfficeRerun = CreateReport(
                "Thesis",
                "Assets/Thesis.asset",
                "2026-08-27T18:05:00Z",
                "SelectedScenario",
                23,
                true,
                "Assets/Older Office.asset");
            GenerationEvaluationReport latestOfficeRerun = CreateReport(
                "Thesis",
                "Assets/Thesis.asset",
                "2026-08-27T18:08:00Z",
                "SelectedScenario",
                23,
                true,
                "Assets/Latest Office.asset");
            GenerationEvaluationReport partialOutdoorRerun = CreateReport(
                "Thesis",
                "Assets/Thesis.asset",
                "2026-08-27T18:09:00Z",
                "SelectedScenario",
                24,
                false,
                "Assets/Partial Outdoor.asset");
            GenerationEvaluationReport latestOutdoorRerun = CreateReport(
                "Thesis",
                "Assets/Thesis.asset",
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
                        "Thesis",
                        "Assets/Thesis.asset",
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
                "Thesis",
                "Assets/Thesis.asset",
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
                        "Thesis",
                        "Assets/Thesis.asset",
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
        public void LayoutCleanupDiscoversPersistedReportsWithoutTypeIndexEntries()
        {
            GenerationEvaluationSuite suite = AssetDatabase.LoadAssetAtPath<GenerationEvaluationSuite>(
                ThesisEvaluationSuiteFactory.SuitePath);
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
        public void ReportMigrationRepairsOnlyLegacyMissingScriptHeader()
        {
            const string guid = "82f4641c484e4fe09c56e3baf063380f";
            const string legacy =
                "MonoBehaviour:\n" +
                "  m_Script: {fileID: 0}\n" +
                "  m_EditorClassIdentifier: Genix.DevTools.Editor:Genix.Editor.Evaluation:GenerationEvaluationReport\n" +
                "  suiteName: Thesis\n";

            bool changed = GenerationEvaluationReportMigration.TryRewriteLegacyYaml(
                legacy,
                guid,
                out string rewritten);

            Assert.That(changed, Is.True);
            Assert.That(rewritten, Does.Contain($"m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}"));
            Assert.That(
                rewritten,
                Does.Contain("Genix.DevTools.Editor::Genix.Editor.Evaluation.GenerationEvaluationReport"));
            Assert.That(rewritten, Does.Contain("suiteName: Thesis"));
            Assert.That(
                GenerationEvaluationReportMigration.TryRewriteLegacyYaml(
                    rewritten,
                    guid,
                    out string unchanged),
                Is.False);
            Assert.That(unchanged, Is.EqualTo(rewritten));

            string preSplit = legacy.Replace(
                "Genix.DevTools.Editor:Genix.Editor.Evaluation:GenerationEvaluationReport",
                "Genix.Editor:Genix.Editor.Evaluation:GenerationEvaluationReport");
            Assert.That(
                GenerationEvaluationReportMigration.TryRewriteLegacyYaml(
                    preSplit,
                    guid,
                    out string migratedPreSplit),
                Is.True);
            Assert.That(migratedPreSplit, Does.Contain($"guid: {guid}"));
            Assert.That(migratedPreSplit, Does.Contain("Genix.DevTools.Editor::"));
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

        [Test]
        public void OutdoorSceneUsesBroadSemanticRegionsAndReusablePathSources()
        {
            Scene scene = SceneManager.GetSceneByPath(OutdoorEvaluationSetupUtility.ScenePath);
            bool closeAfterTest = !scene.IsValid() || !scene.isLoaded;
            if (closeAfterTest)
            {
                scene = EditorSceneManager.OpenScene(
                    OutdoorEvaluationSetupUtility.ScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                Transform[] transforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .ToArray();
                Transform semanticRoot = transforms.Single(item => item.name == "Genix Outdoor Semantics");
                Transform[] semanticObjects = semanticRoot.GetComponentsInChildren<Transform>(true);

                Assert.That(
                    semanticObjects.Any(item => item.name.StartsWith("Trail Direction Anchor")),
                    Is.False);
                Assert.That(
                    semanticObjects.Any(item => item.name.StartsWith("Trail Bollard Anchor")),
                    Is.False);
                Assert.That(
                    semanticObjects.Any(item => item.name.StartsWith("Trail Marker Support")),
                    Is.False);
                Assert.That(
                    semanticObjects.Any(item => item.name.StartsWith("Path Side Support")),
                    Is.False);
                Assert.That(
                    semanticObjects.Any(item => item.name.StartsWith("Water Support")),
                    Is.False);

                Transform restArea = semanticObjects.Single(item => item.name == "Rest Area Region");
                AssetRelationAnchor restAreaAnchor = restArea.GetComponent<AssetRelationAnchor>();
                Assert.That(restArea.GetComponent<Collider>(), Is.Null);
                Assert.That(restArea.GetComponent<PlacementSurfaceDescriptor>(), Is.Null);
                Assert.That(restAreaAnchor, Is.Not.Null);
                Assert.That(restAreaAnchor.TryGetBounds(out Bounds restBounds), Is.True);
                Assert.That(restBounds.size.x, Is.GreaterThanOrEqualTo(6f));
                Assert.That(restBounds.size.y, Is.GreaterThanOrEqualTo(15f));
                Assert.That(restBounds.size.z, Is.GreaterThanOrEqualTo(2f));
                Assert.That(
                    semanticObjects.Count(item => item.name.StartsWith("Rest Bench Anchor")),
                    Is.Zero);

                Transform bridgeExclusion = semanticObjects.Single(item => item.name == "Bridge Exclusion Region");
                PlacementExclusionRegion bridgeRegion = bridgeExclusion.GetComponent<PlacementExclusionRegion>();
                Assert.That(bridgeRegion, Is.Not.Null);
                Assert.That(bridgeRegion.Shape, Is.EqualTo(ExclusionRegionShape.Box));
                Assert.That(bridgeRegion.AffectedTargets & (PlacementTarget.Floor | PlacementTarget.Wall),
                    Is.EqualTo(PlacementTarget.Floor | PlacementTarget.Wall));
                Assert.That(bridgeRegion.Size.x, Is.GreaterThan(1f));
                Assert.That(bridgeRegion.Size.z, Is.GreaterThan(1f));

                Transform waterRegion = semanticObjects.Single(item => item.name == "Water Placement Region");
                MeshCollider waterCollider = waterRegion.GetComponent<MeshCollider>();
                PlacementSurfaceDescriptor waterDescriptor =
                    waterRegion.GetComponent<PlacementSurfaceDescriptor>();
                Assert.That(waterCollider, Is.Not.Null);
                Assert.That(waterCollider.sharedMesh, Is.Not.Null);
                Assert.That(waterCollider.sharedMesh.triangles.Length, Is.GreaterThan(6));
                Assert.That(waterDescriptor.LimitCapacity, Is.False);

                Transform originalWater = transforms.Single(item => item.name == "Water");
                Assert.That(originalWater.GetComponent<PlacementSurfaceDescriptor>(), Is.Null);

                Transform parking = semanticObjects.Single(item => item.name == "Parking Region");
                Assert.That(parking.GetComponent<Collider>(), Is.Null);
                Assert.That(parking.GetComponent<PlacementSurfaceDescriptor>(), Is.Null);
                AssetRelationAnchor parkingAnchor = parking.GetComponent<AssetRelationAnchor>();
                Assert.That(parkingAnchor, Is.Not.Null);
                Assert.That(parkingAnchor.TryGetBounds(out Bounds parkingBounds), Is.True);
                Assert.That(parkingBounds.size.y, Is.GreaterThanOrEqualTo(15f));

                Transform path = transforms.Single(item => item.name == "Path");
                Assert.That(path.GetComponent<PlacementSurfaceDescriptor>(), Is.Null);
                Assert.That(path.GetComponent<PlacementExclusionRegion>(), Is.Null);
                Transform[] pathSegments = path.Cast<Transform>()
                    .Where(item => item.name.StartsWith("Spline"))
                    .ToArray();
                Assert.That(pathSegments, Has.Length.EqualTo(2));
                Assert.That(pathSegments.All(item => item.GetComponent<PlacementSurfaceDescriptor>()), Is.True);
                Assert.That(pathSegments.All(item =>
                {
                    PathPlacementSource source = item.GetComponent<PathPlacementSource>();
                    return source && source.IsConfigured && source.PointCount > 2 &&
                           source.PathTags.Any(tag => tag.DisplayName == "Path");
                }), Is.True);
                Assert.That(pathSegments.All(item =>
                {
                    PlacementExclusionRegion exclusion = item.GetComponent<PlacementExclusionRegion>();
                    return exclusion &&
                           exclusion.Shape == ExclusionRegionShape.ChildColliders &&
                           exclusion.ExemptAssetTags.Any(tag => tag.DisplayName == "Path");
                }), Is.True);
            }
            finally
            {
                if (closeAfterTest)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void OutdoorPresetUsesGlobalMarkerCountsAndRegionalDistribution()
        {
            AssetDefinition trailSign = AssetDatabase.LoadAssetAtPath<AssetDefinition>(
                "Assets/Genix/Assets/Definitions/Trail Sign.asset");
            AssetDefinition cliffRock = AssetDatabase.LoadAssetAtPath<AssetDefinition>(
                "Assets/Genix/Assets/Definitions/Cliff Rock.asset");
            AssetDefinition bollard = AssetDatabase.LoadAssetAtPath<AssetDefinition>(
                "Assets/Genix/Assets/Definitions/Bollard.asset");
            AssetDefinition bench = AssetDatabase.LoadAssetAtPath<AssetDefinition>(
                "Assets/Genix/Assets/Definitions/Bench.asset");
            AssetDefinition car = AssetDatabase.LoadAssetAtPath<AssetDefinition>(
                "Assets/Genix/Assets/Definitions/Peugeot.asset");
            SemanticTag terrain = AssetDatabase.LoadAssetAtPath<SemanticTag>(
                "Assets/Genix/Assets/Tags/Values/Support Type/Terrain.asset");
            SemanticTag path = AssetDatabase.LoadAssetAtPath<SemanticTag>(
                "Assets/Genix/Assets/Tags/Values/Function/Path.asset");
            SemanticTag restArea = AssetDatabase.LoadAssetAtPath<SemanticTag>(
                "Assets/Genix/Assets/Tags/Values/Function/Rest Area.asset");
            SemanticTag signage = AssetDatabase.LoadAssetAtPath<SemanticTag>(
                "Assets/Genix/Assets/Tags/Values/Role/Signage.asset");
            AssetPool pool = AssetDatabase.LoadAssetAtPath<AssetPool>(OutdoorEvaluationSetupUtility.PoolPath);
            GenerationPreset preset = AssetDatabase.LoadAssetAtPath<GenerationPreset>(
                OutdoorEvaluationSetupUtility.PresetPath);

            Assert.That(trailSign, Is.Not.Null);
            Assert.That(cliffRock, Is.Not.Null);
            Assert.That(bollard, Is.Not.Null);
            Assert.That(bench, Is.Not.Null);
            Assert.That(car, Is.Not.Null);
            Assert.That(pool, Is.Not.Null);
            Assert.That(preset, Is.Not.Null);
            Assert.That(trailSign.RequiredSupportTags, Is.EquivalentTo(new[] { terrain }));
            Assert.That(bollard.RequiredSupportTags, Is.EquivalentTo(new[] { terrain }));

            Assert.That(trailSign.AssetRelativePlacement.IsConfigured, Is.False);
            Assert.That(trailSign.PathPlacement.PathTag, Is.SameAs(path));
            Assert.That(trailSign.PathPlacement.Side, Is.EqualTo(PathPlacementSide.Right));
            Assert.That(trailSign.PathPlacement.Facing, Is.EqualTo(PathPlacementFacing.AlongPath));
            Assert.That(trailSign.PathPlacement.MinimumDistance, Is.EqualTo(1.5f));
            Assert.That(trailSign.PathPlacement.MaximumDistance, Is.EqualTo(3f));
            Assert.That(trailSign.GetMinimumSpacingTo(trailSign), Is.EqualTo(5f));
            Assert.That(trailSign.LimitPlacements, Is.True);
            Assert.That(trailSign.MaxPlacements, Is.EqualTo(3));
            Assert.That(cliffRock.SurfaceHeightMode, Is.EqualTo(SurfaceHeightMode.Lowest));
            Assert.That(bollard.AssetRelativePlacement.Source,
                Is.EqualTo(AssetRelativeAnchorSource.SceneAnchors));
            Assert.That(bollard.AssetRelativePlacement.TargetTag, Is.SameAs(path));
            Assert.That(bollard.AssetRelativePlacement.UsesPathStations, Is.True);
            Assert.That(bollard.AssetRelativePlacement.PathStationSides,
                Is.EqualTo(PathPlacementSide.BothSides));
            Assert.That(bollard.AssetRelativePlacement.MaximumPerAnchor, Is.EqualTo(1));

            Assert.That(bollard.AssetRelativePlacement.CardinalityMode,
                Is.EqualTo(AssetRelativeCardinalityMode.Exactly));
            Assert.That(bench.RequiredSupportTags, Is.EquivalentTo(new[] { terrain }));
            Assert.That(car.RequiredSupportTags, Is.EquivalentTo(new[] { terrain }));
            Assert.That(bench.AssetRelativePlacement.TargetTag, Is.SameAs(restArea));
            Assert.That(bench.AssetRelativePlacement.CardinalityMode,
                Is.EqualTo(AssetRelativeCardinalityMode.AtMost));
            Assert.That(bench.AssetRelativePlacement.RequireInsideAnchorBounds, Is.True);
            Assert.That(bench.PathPlacement.PathTag, Is.SameAs(path));
            Assert.That(bench.PathPlacement.MaximumDistance, Is.EqualTo(5f));
            Assert.That(bench.PathPlacement.Facing, Is.EqualTo(PathPlacementFacing.TowardPath));
            Assert.That(car.AssetRelativePlacement.RequireInsideAnchorBounds, Is.True);

            Assert.That(
                bollard.AssetRelativePlacement.Facing,
                Is.EqualTo(AssetRelativeFacing.Any));

            AssetPoolTagLimit signLimit = pool.TagPlacementLimits.Single(limit =>
                limit.AssetTag == signage);
            Assert.That((signLimit.MinPlacements, signLimit.MaxPlacements), Is.EqualTo((3, 3)));

            SupportDistributionSettings distribution = preset.Settings.SupportDistribution;
            Dictionary<string, SupportDistributionRule> rules = distribution.Rules.ToDictionary(
                rule => rule.SupportTag.DisplayName);
            Assert.That(distribution.IsEnabled, Is.True);
            Assert.That(rules.Keys, Is.EquivalentTo(new[] { "Water" }));
            Assert.That(rules["Water"].Value, Is.EqualTo(8));
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
