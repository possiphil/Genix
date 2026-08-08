using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Genix.Editor.Benchmarking
{
    /// <summary>Exports raw and aggregated benchmark data outside Unity's Assets folder.</summary>
    internal static class GenerationBenchmarkExporter
    {
        public static string Export(GenerationBenchmarkCampaignResult campaign)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory;
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
            string suiteName = Sanitize(campaign.suiteName);
            string directory = Path.Combine(projectRoot, "BenchmarkResults", $"{timestamp}_{suiteName}");
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(campaign, true));
            File.WriteAllText(Path.Combine(directory, "runs.csv"), CreateRunsCsv(campaign.runs));
            File.WriteAllText(Path.Combine(directory, "summary.csv"), CreateSummaryCsv(campaign.runs));
            CopySuiteAsset(campaign.suiteAssetPath, directory);
            return directory;
        }

        private static string CreateRunsCsv(IEnumerable<GenerationBenchmarkRunRecord> runs)
        {
            StringBuilder csv = new();
            csv.AppendLine("scenario,scene,target_id,cache,measurement,object_count,seed,repetition,elapsed_ms,succeeded,complete,placed_count,result_hash,has_primary_reference,matches_primary,gc_gen0,gc_gen1,gc_gen2,managed_before_bytes,managed_after_bytes,asset_filter_ms,area_build_ms,candidate_generation_ms,planning_ms,sampling_ms,projection_ms,raycast_ms,validation_ms,raw_samples,candidate_seeds,asset_attempts,rejected_attempts,candidate_cache_hit,message");

            foreach (GenerationBenchmarkRunRecord run in runs)
            {
                Append(csv,
                    run.scenario, run.scene, run.targetId, run.cacheCondition, run.measurement,
                    run.objectCount, run.seed, run.repetition, Number(run.elapsedMilliseconds), run.succeeded,
                    run.complete, run.placedCount, run.resultHash, run.hasPrimaryReference, run.resultMatchesPrimary,
                    run.gcGen0, run.gcGen1, run.gcGen2, run.managedMemoryBefore, run.managedMemoryAfter,
                    Number(run.assetFilterMilliseconds), Number(run.areaBuildMilliseconds),
                    Number(run.candidateGenerationMilliseconds), Number(run.planningMilliseconds),
                    Number(run.samplingMilliseconds), Number(run.projectionMilliseconds),
                    Number(run.raycastMilliseconds), Number(run.validationMilliseconds),
                    run.rawSamples, run.candidateSeeds, run.assetAttempts, run.rejectedAttempts,
                    run.candidateCacheHit, run.message);
            }

            return csv.ToString();
        }

        private static string CreateSummaryCsv(IEnumerable<GenerationBenchmarkRunRecord> runs)
        {
            StringBuilder csv = new();
            csv.AppendLine("scenario,scene,target_id,cache,measurement,object_count,n_total,n_valid,median_ms,q1_ms,q3_ms,iqr_ms,p95_ms,mean_ms,stddev_ms,min_ms,max_ms,completion_rate,semantic_consistency_rate");

            foreach (IGrouping<(string Scenario, string Scene, string Target, string Cache, string Measurement, int Count), GenerationBenchmarkRunRecord> group in runs.GroupBy(run =>
                         (run.scenario, run.scene, run.targetId, run.cacheCondition, run.measurement, run.objectCount)))
            {
                List<double> values = group
                    .Where(run => run.succeeded && run.complete)
                    .Select(run => run.elapsedMilliseconds)
                    .ToList();
                double q1 = GenerationBenchmarkStatistics.LowerQuartile(values);
                double q3 = GenerationBenchmarkStatistics.UpperQuartile(values);
                Append(csv,
                    group.Key.Scenario,
                    group.Key.Scene,
                    group.Key.Target,
                    group.Key.Cache,
                    group.Key.Measurement,
                    group.Key.Count,
                    group.Count(),
                    values.Count,
                    Number(GenerationBenchmarkStatistics.Median(values)),
                    Number(q1),
                    Number(q3),
                    Number(q3 - q1),
                    Number(GenerationBenchmarkStatistics.P95(values)),
                    Number(values.Count > 0 ? values.Average() : 0d),
                    Number(GenerationBenchmarkStatistics.StandardDeviation(values)),
                    Number(values.Count > 0 ? values.Min() : 0d),
                    Number(values.Count > 0 ? values.Max() : 0d),
                    Number(group.Count(run => run.succeeded && run.complete) / (double)group.Count()),
                    Number(SemanticConsistency(group)));
            }

            return csv.ToString();
        }

        private static void Append(StringBuilder csv, params object[] values)
        {
            csv.AppendLine(string.Join(",", values.Select(Escape)));
        }

        private static string Escape(object value)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? $"\"{text.Replace("\"", "\"\"")}\""
                : text;
        }

        private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static double SemanticConsistency(IEnumerable<GenerationBenchmarkRunRecord> runs)
        {
            GenerationBenchmarkRunRecord[] comparable = runs
                .Where(run => run.hasPrimaryReference)
                .ToArray();
            return comparable.Length == 0
                ? 0d
                : comparable.Count(run => run.resultMatchesPrimary) / (double)comparable.Length;
        }

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
