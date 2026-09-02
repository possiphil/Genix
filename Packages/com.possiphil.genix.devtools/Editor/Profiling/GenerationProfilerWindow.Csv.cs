using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Areas;
using Genix.Extensions;
using Genix.Placement;
using Genix.Profiling;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Profiling
{
    public sealed partial class GenerationProfilerWindow
    {
        private static void ExportCsv(GenerationProfile profile)
        {
            if (profile == null)
                return;

            List<string> lines = CreateCsvLines(
                profile.TargetName,
                profile.RunType,
                profile.StyleName,
                profile.RandomSeed,
                GetCandidateSource(profile),
                profile.GetPhaseTime,
                GetRuntimeProfile(profile),
                profile.GetSortedPlanningSteps().Select(step => new PlanningStepView(
                    FormatPlanningStep(step.Step),
                    step.Milliseconds,
                    step.Calls)),
                profile.GetSortedAreaBuildSteps().Select(step => new AreaBuildStepView(
                    FormatAreaBuildStep(step.Step),
                    step.Milliseconds,
                    step.Calls)),
                profile.GetSortedTargets().Select(target => new TargetCsvData(
                    target.PlacementType.ToString(),
                    target.SeedGenerationMilliseconds,
                    target.SamplingMilliseconds,
                    target.ProjectionMilliseconds,
                    target.RaycastMilliseconds,
                    target.ValidationMilliseconds,
                    target.RawSamples,
                    target.CandidateSeeds,
                    target.TestedSeeds,
                    target.ProjectionAttempts,
                    target.ProjectionHits,
                    target.RaycastCalls,
                    target.RaycastHits,
                    target.AssetAttempts,
                    target.AcceptedAttempts,
                    target.RejectedAttempts,
                    target.ValidationSteps
                        .OrderBy(entry => entry.Step)
                        .Select(entry => new ValidationStepView(
                            FormatValidationStep(entry.Step),
                            entry.Milliseconds,
                            entry.Calls)),
                    target.RejectionCounts
                        .OrderByDescending(entry => entry.Value)
                        .Select(entry => new RejectionView(entry.Key.ToDisplayName(), entry.Value)))));

            ExportCsv(lines, profile.TargetName, profile.RunId);
        }

        private static void ExportCsv(GenerationProfileReport report)
        {
            if (!report)
                return;

            List<string> lines = CreateCsvLines(
                report.TargetName,
                report.RunType,
                report.StyleName,
                report.RandomSeed,
                report.CandidateSource,
                report.GetPhaseTime,
                GetRuntimeProfile(report),
                report.PlanningSteps.Select(step => new PlanningStepView(
                    FormatPlanningStep(step.Step),
                    step.Milliseconds,
                    step.Calls)),
                report.AreaBuildSteps.Select(step => new AreaBuildStepView(
                    FormatAreaBuildStep(step.Step),
                    step.Milliseconds,
                    step.Calls)),
                report.Targets.Select(target => new TargetCsvData(
                    target.PlacementType,
                    target.SeedGenerationMilliseconds,
                    target.SamplingMilliseconds,
                    target.ProjectionMilliseconds,
                    target.RaycastMilliseconds,
                    target.ValidationMilliseconds,
                    target.RawSamples,
                    target.CandidateSeeds,
                    target.TestedSeeds,
                    target.ProjectionAttempts,
                    target.ProjectionHits,
                    target.RaycastCalls,
                    target.RaycastHits,
                    target.AssetAttempts,
                    target.AcceptedAttempts,
                    target.RejectedAttempts,
                    target.ValidationSteps.Select(entry => new ValidationStepView(
                        FormatValidationStep(entry.Step),
                        entry.Milliseconds,
                        entry.Calls)),
                    target.Rejections.Select(entry => new RejectionView(entry.Reason, entry.Count)))));

            ExportCsv(lines, report.TargetName, report.RunId);
        }

        private static void ExportCsv(IReadOnlyCollection<string> lines, string targetName, string runId)
        {
            string projectDirectory = Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
            string path = EditorUtility.SaveFilePanel(
                "Export Profile CSV",
                projectDirectory,
                CreateCsvFileName(targetName, runId),
                "csv");
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                File.WriteAllText(path, string.Join("\n", lines) + "\n");
                Debug.Log($"Exported Genix profile CSV: {path}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Export Profile CSV",
                    $"The profile could not be exported.\n\n{exception.Message}",
                    "OK");
            }
        }

        private static string CreateCsvFileName(string targetName, string runId)
        {
            string target = string.IsNullOrWhiteSpace(targetName) ? "Unknown" : targetName;
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
                target = target.Replace(invalidCharacter, '_');

            target = target.Replace(' ', '_');
            return $"GenixProfile_{target}_{ShortenRunId(runId)}";
        }

        private static List<string> CreateCsvLines(
            string targetName,
            string runType,
            string styleName,
            int randomSeed,
            string candidateSource,
            System.Func<GenerationProfilePhase, float> getPhaseTime,
            ProfileRuntimeView runtime,
            IEnumerable<PlanningStepView> planningSteps,
            IEnumerable<AreaBuildStepView> areaBuildSteps,
            IEnumerable<TargetCsvData> targets)
        {
            List<string> lines = new()
            {
                "section,name,value",
                $"run,target,{Escape(targetName)}",
                $"run,type,{Escape(runType)}",
                $"run,style,{Escape(styleName)}",
                $"run,seed,{randomSeed}",
                $"run,candidate_source,{Escape(candidateSource)}",
                $"phase,total,{getPhaseTime(GenerationProfilePhase.Total):0.###}",
                $"phase,asset_filter,{getPhaseTime(GenerationProfilePhase.AssetFilter):0.###}",
                $"phase,area_build,{getPhaseTime(GenerationProfilePhase.AreaBuild):0.###}",
                $"phase,candidate_generation,{getPhaseTime(GenerationProfilePhase.CandidateGeneration):0.###}",
                $"phase,planning,{getPhaseTime(GenerationProfilePhase.Planning):0.###}",
                $"phase,planning_unattributed,{runtime.PlanningUnattributedMilliseconds:0.###}",
                $"phase,apply,{getPhaseTime(GenerationProfilePhase.Apply):0.###}",
                $"phase,context_setup,{getPhaseTime(GenerationProfilePhase.ContextSetup):0.###}",
                $"phase,preview_plan_handoff,{getPhaseTime(GenerationProfilePhase.PreviewPlanCopy):0.###}",
                $"phase,preview_diagnostics_handoff,{getPhaseTime(GenerationProfilePhase.PreviewDiagnosticsHandoff):0.###}",
                $"phase,preview_cleanup,{getPhaseTime(GenerationProfilePhase.PreviewCleanup):0.###}",
                $"phase,preview_log,{getPhaseTime(GenerationProfilePhase.PreviewLog):0.###}"
            };

            if (runtime.HasManagedRuntimeStats)
            {
                lines.Add($"gc,gen0_collections,{runtime.GarbageCollectionsGen0}");
                lines.Add($"gc,gen1_collections,{runtime.GarbageCollectionsGen1}");
                lines.Add($"gc,gen2_collections,{runtime.GarbageCollectionsGen2}");
                lines.Add($"memory,managed_before_mb,{BytesToMegabytes(runtime.ManagedMemoryBeforeBytes):0.###}");
                lines.Add($"memory,managed_after_mb,{BytesToMegabytes(runtime.ManagedMemoryAfterBytes):0.###}");
                lines.Add($"memory,managed_delta_mb,{BytesToMegabytes(runtime.ManagedMemoryDeltaBytes):0.###}");
            }

            foreach (PlanningStepView step in (planningSteps ?? Enumerable.Empty<PlanningStepView>())
                         .Where(step => step.Calls > 0 || step.Milliseconds > 0f))
            {
                string metric = SanitizeMetricName(step.Step);
                lines.Add($"planning,{metric}_ms,{step.Milliseconds:0.###}");
                lines.Add($"planning,{metric}_calls,{step.Calls}");
            }

            foreach (AreaBuildStepView step in (areaBuildSteps ?? Enumerable.Empty<AreaBuildStepView>())
                         .Where(step => step.Calls > 0 || step.Milliseconds > 0f))
            {
                string metric = SanitizeMetricName(step.Step);
                lines.Add($"area_build,{metric}_ms,{step.Milliseconds:0.###}");
                lines.Add($"area_build,{metric}_calls,{step.Calls}");
            }

            foreach (TargetCsvData target in targets)
            {
                string prefix = $"target:{target.PlacementType}";
                lines.Add($"{prefix},seed_generation_ms,{target.SeedGenerationMilliseconds:0.###}");
                lines.Add($"{prefix},sampling_ms,{target.SamplingMilliseconds:0.###}");
                lines.Add($"{prefix},projection_ms,{target.ProjectionMilliseconds:0.###}");
                lines.Add($"{prefix},raycast_ms,{target.RaycastMilliseconds:0.###}");
                lines.Add($"{prefix},validation_ms,{target.ValidationMilliseconds:0.###}");

                foreach (ValidationStepView step in target.ValidationSteps.Where(step => step.Calls > 0 || step.Milliseconds > 0f))
                {
                    string metric = SanitizeMetricName(step.Step);
                    lines.Add($"{prefix}:validation,{metric}_ms,{step.Milliseconds:0.###}");
                    lines.Add($"{prefix}:validation,{metric}_calls,{step.Calls}");
                }

                lines.Add($"{prefix},raw_samples,{target.RawSamples}");
                lines.Add($"{prefix},candidate_seeds,{target.CandidateSeeds}");
                lines.Add($"{prefix},tested_seeds,{target.TestedSeeds}");
                lines.Add($"{prefix},projection_attempts,{target.ProjectionAttempts}");
                lines.Add($"{prefix},projection_hits,{target.ProjectionHits}");
                lines.Add($"{prefix},raycast_calls,{target.RaycastCalls}");
                lines.Add($"{prefix},raycast_hits,{target.RaycastHits}");
                lines.Add($"{prefix},asset_attempts,{target.AssetAttempts}");
                lines.Add($"{prefix},accepted_attempts,{target.AcceptedAttempts}");
                lines.Add($"{prefix},rejected_attempts,{target.RejectedAttempts}");

                foreach (RejectionView rejection in target.Rejections.Where(rejection => rejection.Count > 0))
                    lines.Add($"{prefix}:rejection,{Escape(rejection.Reason)},{rejection.Count}");
            }

            return lines;
        }

        private static string Escape(string value) =>
            string.IsNullOrEmpty(value)
                ? string.Empty
                : "\"" + value.Replace("\"", "\"\"") + "\"";

        private static string SanitizeMetricName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            List<char> chars = new();
            bool previousWasSeparator = false;

            foreach (char character in value.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    chars.Add(character);
                    previousWasSeparator = false;
                    continue;
                }

                if (previousWasSeparator || chars.Count == 0)
                    continue;

                chars.Add('_');
                previousWasSeparator = true;
            }

            if (chars.Count > 0 && chars[^1] == '_')
                chars.RemoveAt(chars.Count - 1);

            return chars.Count > 0 ? new string(chars.ToArray()) : "unknown";
        }

        private static string GetCandidateSource(GenerationProfile profile)
        {
            bool hasCandidateData =
                profile.GetPhaseTime(GenerationProfilePhase.CandidateGeneration) > 0f ||
                profile.Targets.Count > 0;

            if (!hasCandidateData)
                return "Not reached";

            return profile.CandidateCacheHit ? "Cache" : "Generated";
        }
    }
}
