using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Genix.Editor.Evaluation
{
    /// <summary>Exports raw evaluation observations and aggregate rates outside Unity's Assets folder.</summary>
    internal static class GenerationEvaluationExporter
    {
        public static string Export(GenerationEvaluationCampaignResult campaign)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory;
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string directory = Path.Combine(projectRoot, "EvaluationResults", $"{timestamp}_{Sanitize(campaign.suiteName)}");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(campaign, true));
            File.WriteAllText(Path.Combine(directory, "runs.csv"), CreateRunsCsv(campaign.runs));
            File.WriteAllText(Path.Combine(directory, "checks.csv"), CreateChecksCsv(campaign.runs));
            File.WriteAllText(Path.Combine(directory, "assets.csv"), CreateCountCsv(campaign.runs, run => run.assetCounts, "asset"));
            File.WriteAllText(Path.Combine(directory, "supports.csv"), CreateCountCsv(campaign.runs, run => run.supportCounts, "support"));
            File.WriteAllText(Path.Combine(directory, "rejections.csv"), CreateCountCsv(campaign.runs, run => run.rejectionCounts, "rejection"));
            File.WriteAllText(Path.Combine(directory, "coverage.csv"), CreateCoverageCsv(campaign.runs));
            File.WriteAllText(Path.Combine(directory, "summary.csv"), CreateSummaryCsv(campaign.runs));
            CopySuiteAsset(campaign.suiteAssetPath, directory);
            return directory;
        }

        internal static string CreateRunsCsv(IEnumerable<GenerationEvaluationRunRecord> runs)
        {
            StringBuilder csv = new();
            csv.AppendLine("scenario,kind,scene,area_provider,target_id,preset,seed,requested,placed,generation_succeeded,automatic_verdict,automatic_checks_passed,tested_candidates,rejected_candidates,minimum_placement_distance,top_rejection,stop_reason,layout_asset,layout_guid,visual_reviewable,visual_review_completed,visual_note_valid,visual_review_evidence_valid,layout_asset_missing,visual_rating,visual_notes,visual_capture_manifest,visual_capture_manifest_sha256,visual_capture_created_at_utc");
            foreach (GenerationEvaluationRunRecord run in runs)
            {
                Append(csv, run.scenario, run.scenarioKind, run.scene, run.areaProviderId, run.targetId, run.preset, run.seed,
                    run.requestedCount, run.placedCount, run.generationSucceeded, run.AutomaticVerdict, run.AutomaticChecksPassed,
                    run.testedCandidates, run.rejectedCandidates, Number(run.minimumPlacementDistance), run.topRejection, run.stopReason,
                    run.layoutAssetPath, run.layoutGuid, run.HasLayoutReference, run.VisualReviewCompleted,
                    run.VisualReviewNoteValid, run.VisualReviewEvidenceValid, run.HasMissingLayoutAsset,
                    run.visualRating, run.visualNotes, run.visualReviewCaptureManifestPath,
                    run.visualReviewCaptureManifestSha256, run.visualReviewCapturedAtUtc);
            }
            return csv.ToString();
        }

        private static string CreateCountCsv(
            IEnumerable<GenerationEvaluationRunRecord> runs,
            Func<GenerationEvaluationRunRecord, IEnumerable<GenerationEvaluationCountRecord>> selector,
            string countName)
        {
            StringBuilder csv = new();
            csv.AppendLine($"scenario,seed,{countName},count");
            foreach (GenerationEvaluationRunRecord run in runs)
            foreach (GenerationEvaluationCountRecord count in selector(run) ?? Array.Empty<GenerationEvaluationCountRecord>())
                Append(csv, run.scenario, run.seed, count.name, count.count);
            return csv.ToString();
        }

        private static string CreateChecksCsv(IEnumerable<GenerationEvaluationRunRecord> runs)
        {
            StringBuilder csv = new();
            csv.AppendLine("scenario,seed,check,status,violations,message");
            foreach (GenerationEvaluationRunRecord run in runs)
            foreach (GenerationEvaluationCheckRecord check in run.checks)
                Append(csv, run.scenario, run.seed, check.name, check.status, check.violations, check.message);
            return csv.ToString();
        }

        private static string CreateCoverageCsv(IEnumerable<GenerationEvaluationRunRecord> runs)
        {
            StringBuilder csv = new();
            csv.AppendLine("scenario,kind,subject,name,runs_present,total_runs,run_coverage,total_count");
            foreach (IGrouping<(string Scenario, string Kind), GenerationEvaluationRunRecord> group in
                     runs.GroupBy(run => (run.scenario, run.scenarioKind)))
            {
                AppendCoverage(csv, group.Key, "asset", GenerationEvaluationCoverage.BuildAssetCoverage(group));
                AppendCoverage(csv, group.Key, "support", GenerationEvaluationCoverage.BuildSupportCoverage(group));
            }

            return csv.ToString();
        }

        private static void AppendCoverage(
            StringBuilder csv,
            (string Scenario, string Kind) group,
            string subject,
            IEnumerable<GenerationEvaluationCoverageRecord> coverage)
        {
            foreach (GenerationEvaluationCoverageRecord item in coverage)
                Append(csv, group.Scenario, group.Kind, subject, item.name, item.runsPresent, item.totalRuns,
                    Number(item.RunCoverage), item.totalCount);
        }

        internal static string CreateSummaryCsv(IEnumerable<GenerationEvaluationRunRecord> runs)
        {
            StringBuilder csv = new();
            csv.AppendLine("scenario,kind,runs,generation_success_rate,automatic_pass_rate,automatic_incomplete_rate,automatic_fail_rate,mean_completion_rate,visual_reviewable,visual_reviewed,visual_valid,visual_invalid_evidence,visual_missing_required_notes,visual_missing_layout_assets,visual_unbacked_ratings,visual_pass,visual_acceptable,visual_fail");
            foreach (IGrouping<(string Scenario, string Kind), GenerationEvaluationRunRecord> group in
                     runs.GroupBy(run => (run.scenario, run.scenarioKind)))
            {
                GenerationEvaluationRunRecord[] records = group.ToArray();
                int count = records.Length;
                int reviewable = records.Count(run => run.HasLayoutReference);
                int reviewed = records.Count(run => run.VisualReviewCompleted);
                Append(csv, group.Key.Scenario, group.Key.Kind, count,
                    Number(records.Count(run => run.generationSucceeded) / (double)count),
                    Number(records.Count(run => run.AutomaticVerdict == EvaluationAutomaticVerdict.Passed) / (double)count),
                    Number(records.Count(run => run.AutomaticVerdict == EvaluationAutomaticVerdict.Incomplete) / (double)count),
                    Number(records.Count(run => run.AutomaticVerdict == EvaluationAutomaticVerdict.Failed) / (double)count),
                    Number(records.Average(run => run.requestedCount > 0 ? run.placedCount / (double)run.requestedCount : 0d)),
                    reviewable,
                    reviewed,
                    records.Count(run => run.VisualReviewEvidenceValid),
                    records.Count(run => run.HasInvalidVisualReviewEvidence),
                    records.Count(run => !run.VisualReviewNoteValid),
                    records.Count(run => run.HasMissingLayoutAsset),
                    records.Count(run => !run.HasLayoutReference &&
                                         run.visualRating != EvaluationVisualRating.NotReviewed),
                    records.Count(run => run.HasLayoutReference && run.visualRating == EvaluationVisualRating.Pass),
                    records.Count(run => run.HasLayoutReference && run.visualRating == EvaluationVisualRating.Acceptable),
                    records.Count(run => run.HasLayoutReference && run.visualRating == EvaluationVisualRating.Fail));
            }
            return csv.ToString();
        }

        private static void Append(StringBuilder csv, params object[] values) =>
            csv.AppendLine(string.Join(",", values.Select(Escape)));

        private static string Escape(object value)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? $"\"{text.Replace("\"", "\"\"")}\""
                : text;
        }

        private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static void CopySuiteAsset(string suiteAssetPath, string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(suiteAssetPath))
                return;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                return;

            string source = Path.Combine(projectRoot, suiteAssetPath);
            if (File.Exists(source))
                File.Copy(source, Path.Combine(outputDirectory, "suite.asset.yaml"), overwrite: true);
        }

        private static string Sanitize(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Genix" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return result.Replace(' ', '_');
        }
    }
}
