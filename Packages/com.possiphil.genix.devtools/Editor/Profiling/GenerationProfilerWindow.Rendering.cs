using System.Collections.Generic;
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
        private static void DrawRunSummary(GenerationProfile profile)
        {
            DrawRunSummary(
                profile.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                profile.RunId,
                profile.TargetName,
                profile.RunType,
                profile.PlacementTargets,
                profile.DistributionMode,
                profile.StyleName,
                profile.SamplingAlgorithm,
                profile.RequestedObjectCount,
                profile.PlacedObjectCount,
                profile.RandomSeed,
                GetCandidateSource(profile),
                profile.StopReason);
        }

        private static void DrawRunSummary(GenerationProfileReport report)
        {
            DrawRunSummary(
                report.CreatedAt,
                report.RunId,
                report.TargetName,
                report.RunType,
                report.PlacementTargets,
                report.DistributionMode,
                report.StyleName,
                report.SamplingAlgorithm,
                report.RequestedObjectCount,
                report.PlacedObjectCount,
                report.RandomSeed,
                report.CandidateSource,
                report.StopReason);
        }

        private static void DrawRunSummary(
            string createdAt,
            string runId,
            string targetName,
            string runType,
            string placementTargets,
            string distributionMode,
            string styleName,
            string samplingAlgorithm,
            int requestedObjectCount,
            int placedObjectCount,
            int randomSeed,
            string candidateSource,
            string stopReason)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Run", EditorStyles.boldLabel);
            DrawStat("Created", createdAt);
            DrawStat("Run ID", ShortenRunId(runId));
            DrawStat("Target", targetName);
            DrawStat("Run Type", runType);
            DrawStat("Targets", placementTargets);
            DrawStat("Distribution", distributionMode);
            DrawStat("Style", styleName);
            DrawStat("Sampling", samplingAlgorithm);
            DrawStat("Requested", requestedObjectCount.ToString());
            DrawStat("Placed/Planned", placedObjectCount.ToString());
            DrawStat("Seed", randomSeed.ToString());
            DrawStat("Candidate Source", candidateSource);

            if (!string.IsNullOrWhiteSpace(stopReason))
                EditorGUILayout.HelpBox(stopReason, MessageType.Warning);
        }

        private static void DrawPhaseSummary(GenerationProfile profile)
        {
            ProfileRuntimeView runtime = GetRuntimeProfile(profile);
            DrawPhaseSummary(phase => profile.GetPhaseTime(phase), runtime);
            DrawRuntimeSummary(runtime);
            DrawPlanningBreakdown(profile.GetSortedPlanningSteps()
                .Select(step => new PlanningStepView(
                    FormatPlanningStep(step.Step),
                    step.Milliseconds,
                    step.Calls)));
            DrawAreaBuildBreakdown(profile.GetSortedAreaBuildSteps()
                .Select(step => new AreaBuildStepView(
                    FormatAreaBuildStep(step.Step),
                    step.Milliseconds,
                    step.Calls)));
        }

        private static void DrawPhaseSummary(GenerationProfileReport report)
        {
            ProfileRuntimeView runtime = GetRuntimeProfile(report);
            DrawPhaseSummary(report.GetPhaseTime, runtime);
            DrawRuntimeSummary(runtime);
            DrawPlanningBreakdown(report.PlanningSteps.Select(step => new PlanningStepView(
                FormatPlanningStep(step.Step),
                step.Milliseconds,
                step.Calls)));
            DrawAreaBuildBreakdown(report.AreaBuildSteps.Select(step => new AreaBuildStepView(
                FormatAreaBuildStep(step.Step),
                step.Milliseconds,
                step.Calls)));
        }

        private static void DrawPhaseSummary(System.Func<GenerationProfilePhase, float> getPhaseTime, ProfileRuntimeView runtime)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Pipeline", EditorStyles.boldLabel);
            DrawPhase(getPhaseTime, GenerationProfilePhase.Total, "Total");
            DrawPhase(getPhaseTime, GenerationProfilePhase.AssetFilter, "Asset Filter");
            DrawPhase(getPhaseTime, GenerationProfilePhase.AreaBuild, "Area Build");
            DrawPhase(getPhaseTime, GenerationProfilePhase.CandidateGeneration, "Candidate Generation");
            DrawPhase(getPhaseTime, GenerationProfilePhase.Planning, "Planning Solver");
            DrawPlanningUnattributed(runtime);
            DrawPhase(getPhaseTime, GenerationProfilePhase.Apply, "Apply");
            DrawPhase(getPhaseTime, GenerationProfilePhase.ContextSetup, "Context Setup");
            DrawPhase(getPhaseTime, GenerationProfilePhase.PreviewPlanCopy, "Preview Plan Handoff");
            DrawPhase(getPhaseTime, GenerationProfilePhase.PreviewDiagnosticsHandoff, "Preview Diagnostics Handoff");
            DrawPhase(getPhaseTime, GenerationProfilePhase.PreviewCleanup, "Preview Cleanup");
            DrawPhase(getPhaseTime, GenerationProfilePhase.PreviewLog, "Preview Log");
        }

        private static void DrawPhase(System.Func<GenerationProfilePhase, float> getPhaseTime, GenerationProfilePhase phase, string label)
        {
            float milliseconds = getPhaseTime(phase);

            if (milliseconds <= 0f && phase != GenerationProfilePhase.Apply)
                return;

            DrawStat(label, FormatMilliseconds(milliseconds));
        }

        private static void DrawPlanningUnattributed(ProfileRuntimeView runtime)
        {
            if (runtime.PlanningUnattributedMilliseconds <= 0f)
                return;

            DrawStat("Planning Unattributed", FormatMilliseconds(runtime.PlanningUnattributedMilliseconds));
        }

        private static void DrawRuntimeSummary(ProfileRuntimeView runtime)
        {
            if (!runtime.HasManagedRuntimeStats)
                return;

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Managed Runtime", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawStat(
                    "GC Collections",
                    $"Gen0 {runtime.GarbageCollectionsGen0}, Gen1 {runtime.GarbageCollectionsGen1}, Gen2 {runtime.GarbageCollectionsGen2}");
                DrawStat(
                    "Memory Delta",
                    $"{FormatByteDelta(runtime.ManagedMemoryDeltaBytes)} ({FormatBytes(runtime.ManagedMemoryBeforeBytes)} -> {FormatBytes(runtime.ManagedMemoryAfterBytes)})");
            }
        }

        private static void DrawPlanningBreakdown(IEnumerable<PlanningStepView> steps)
        {
            List<PlanningStepView> entries = (steps ?? Enumerable.Empty<PlanningStepView>())
                .Where(entry => entry.Calls > 0 || entry.Milliseconds > 0f)
                .OrderByDescending(entry => entry.Milliseconds)
                .ToList();

            if (entries.Count == 0)
                return;

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Planning Breakdown", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (PlanningStepView entry in entries)
                {
                    string value = $"{FormatMilliseconds(entry.Milliseconds)} ({entry.Calls} calls, avg {FormatAverageMilliseconds(entry.Milliseconds, entry.Calls)})";
                    DrawStat(entry.Step, value);
                }
            }
        }

        private static void DrawAreaBuildBreakdown(IEnumerable<AreaBuildStepView> steps)
        {
            List<AreaBuildStepView> entries = (steps ?? Enumerable.Empty<AreaBuildStepView>())
                .Where(entry => entry.Calls > 0 || entry.Milliseconds > 0f)
                .OrderByDescending(entry => entry.Milliseconds)
                .ToList();

            if (entries.Count == 0)
                return;

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Area Build Breakdown", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (AreaBuildStepView entry in entries)
                {
                    string value = $"{FormatMilliseconds(entry.Milliseconds)} ({entry.Calls} calls, avg {FormatAverageMilliseconds(entry.Milliseconds, entry.Calls)})";
                    DrawStat(entry.Step, value);
                }
            }
        }

        private static void DrawTargetProfiles(GenerationProfile profile)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);

            if (profile.Targets.Count == 0)
            {
                EditorGUILayout.LabelField("No target-level profile data captured.");
                return;
            }

            foreach (GenerationTargetProfile target in profile.GetSortedTargets())
                DrawTargetProfile(target);
        }

        private static void DrawTargetProfiles(GenerationProfileReport report)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);

            if (report.Targets.Count == 0)
            {
                EditorGUILayout.LabelField("No target-level profile data captured.");
                return;
            }

            foreach (GenerationProfileReport.TargetEntry target in report.Targets)
                DrawTargetProfile(target);
        }

        private static void DrawTargetProfile(GenerationTargetProfile target)
        {
            DrawTargetProfile(
                target.PlacementType.ToDisplayName(),
                target.SeedGenerationMilliseconds,
                target.SamplingMilliseconds,
                target.ProjectionMilliseconds,
                target.RaycastMilliseconds,
                target.ValidationMilliseconds,
                target.RawSamples,
                target.CandidateSeeds,
                target.TestedSeeds,
                target.ProjectionHits,
                target.ProjectionAttempts,
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
                    .Select(entry => new RejectionView(entry.Key.ToDisplayName(), entry.Value)));
        }

        private static void DrawTargetProfile(GenerationProfileReport.TargetEntry target)
        {
            DrawTargetProfile(
                target.PlacementType,
                target.SeedGenerationMilliseconds,
                target.SamplingMilliseconds,
                target.ProjectionMilliseconds,
                target.RaycastMilliseconds,
                target.ValidationMilliseconds,
                target.RawSamples,
                target.CandidateSeeds,
                target.TestedSeeds,
                target.ProjectionHits,
                target.ProjectionAttempts,
                target.RaycastCalls,
                target.RaycastHits,
                target.AssetAttempts,
                target.AcceptedAttempts,
                target.RejectedAttempts,
                target.ValidationSteps.Select(entry => new ValidationStepView(
                    FormatValidationStep(entry.Step),
                    entry.Milliseconds,
                    entry.Calls)),
                target.Rejections.Select(entry => new RejectionView(entry.Reason, entry.Count)));
        }

        private static void DrawTargetProfile(
            string placementType,
            float seedGenerationMilliseconds,
            float samplingMilliseconds,
            float projectionMilliseconds,
            float raycastMilliseconds,
            float validationMilliseconds,
            int rawSamples,
            int candidateSeeds,
            int testedSeeds,
            int projectionHits,
            int projectionAttempts,
            int raycastCalls,
            int raycastHits,
            int assetAttempts,
            int acceptedAttempts,
            int rejectedAttempts,
            IEnumerable<ValidationStepView> validationSteps,
            IEnumerable<RejectionView> rejections)
        {
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(placementType, EditorStyles.boldLabel);
                DrawStat("Seed Generation", FormatMilliseconds(seedGenerationMilliseconds));
                DrawStat("Sampling", FormatMilliseconds(samplingMilliseconds));
                DrawStat("Projection", FormatMilliseconds(projectionMilliseconds));
                DrawStat("Raycast", $"{FormatMilliseconds(raycastMilliseconds)} ({raycastCalls} calls, {raycastHits} hits)");
                DrawStat("Validation", FormatMilliseconds(validationMilliseconds));
                DrawValidationSteps(validationSteps);
                DrawStat("Raw Samples", rawSamples.ToString());
                DrawStat("Candidate Seeds", candidateSeeds.ToString());
                DrawStat("Tested Seeds", testedSeeds.ToString());
                DrawStat("Projection Hits", FormatRatio(projectionHits, projectionAttempts));
                DrawStat("Accepted Ratio", FormatRatio(acceptedAttempts, assetAttempts));
                DrawStat("Avg Projection", FormatAverageMilliseconds(projectionMilliseconds, projectionAttempts));
                DrawStat("Avg Raycast", FormatAverageMilliseconds(raycastMilliseconds, raycastCalls));
                DrawStat("Avg Validation", FormatAverageMilliseconds(validationMilliseconds, assetAttempts));
                DrawStat("Asset Attempts", assetAttempts.ToString());
                DrawStat("Accepted Attempts", acceptedAttempts.ToString());
                DrawStat("Rejected Attempts", rejectedAttempts.ToString());

                DrawRejectionCounts(rejections);
            }
        }

        private static void DrawValidationSteps(IEnumerable<ValidationStepView> validationSteps)
        {
            List<ValidationStepView> entries = (validationSteps ?? Enumerable.Empty<ValidationStepView>())
                .Where(entry => entry.Calls > 0 || entry.Milliseconds > 0f)
                .OrderByDescending(entry => entry.Milliseconds)
                .ToList();

            if (entries.Count == 0)
                return;

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Validation Breakdown", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (ValidationStepView entry in entries)
                {
                    string value = $"{FormatMilliseconds(entry.Milliseconds)} ({entry.Calls} calls, avg {FormatAverageMilliseconds(entry.Milliseconds, entry.Calls)})";
                    DrawStat(entry.Step, value);
                }
            }
        }

        private static void DrawRejectionCounts(IEnumerable<RejectionView> rejections)
        {
            List<RejectionView> entries = (rejections ?? Enumerable.Empty<RejectionView>())
                .Where(entry => entry.Count > 0)
                .ToList();

            if (entries.Count == 0)
                return;

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Rejections", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (RejectionView entry in entries)
                    DrawStat(entry.Reason, entry.Count.ToString());
            }
        }

        private static void DrawStat(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }
    }
}

