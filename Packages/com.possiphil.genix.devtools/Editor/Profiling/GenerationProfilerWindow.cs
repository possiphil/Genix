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
    /// <summary>Provides the generation profiler editor window.</summary>
    public sealed class GenerationProfilerWindow : EditorWindow
    {
        private const float SavedListHeight = 180f;

        private GenerationProfileReport _selectedReport;
        private Vector2 _currentScroll;
        private Vector2 _savedListScroll;
        private Vector2 _savedDetailsScroll;

        /// <summary>Opens or focuses the corresponding Genix editor window.</summary>
        [MenuItem("Tools/Genix Developer/Profiler", false, 10)]
        public static void Open()
        {
            GenerationProfilerWindow window = GetWindow<GenerationProfilerWindow>("Genix Profiler");
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            GenerationProfilerService.Changed += Repaint;
            GenerationProfileCatalogService.Refresh();
        }

        private void OnDisable()
        {
            GenerationProfilerService.Changed -= Repaint;
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.Space(6f);
            DrawCurrentProfile();

            EditorGUILayout.Space(8f);
            DrawSavedProfiles();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                bool captureRuns = GUILayout.Toggle(
                    GenerationProfilerService.ProfilingEnabled,
                    new GUIContent(
                        "Capture Runs",
                        "Instrument subsequent Generate, Re-Generate, and Preview Run operations until disabled. This adds measurement overhead."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(92f));
                if (EditorGUI.EndChangeCheck())
                    GenerationProfilerService.SetProfilingEnabled(captureRuns);

                GUILayout.Space(6f);

                using (new EditorGUI.DisabledScope(GenerationProfilerService.LastProfile == null))
                {
                    if (GUILayout.Button("Save Profile", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                    {
                        _selectedReport = GenerationProfileReportSaver.Save(GenerationProfilerService.LastProfile);
                        GenerationProfileCatalogService.Refresh();
                    }

                    if (GUILayout.Button("Copy CSV", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                        CopyCsv(GenerationProfilerService.LastProfile);

                    if (GUILayout.Button("Clear Current", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                        GenerationProfilerService.ClearLastProfile();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    GenerationProfileCatalogService.Refresh();

                if (GUILayout.Button("Clear Saved", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                    ClearSavedProfiles();
            }
        }

        private void DrawCurrentProfile()
        {
            EditorGUILayout.LabelField("Current Profile", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GenerationProfile profile = GenerationProfilerService.LastProfile;

                if (profile == null)
                    return;

                _currentScroll = EditorGUILayout.BeginScrollView(_currentScroll, GUILayout.MaxHeight(280f));
                DrawRunSummary(profile);
                DrawPhaseSummary(profile);
                DrawTargetProfiles(profile);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSavedProfiles()
        {
            GenerationProfileCatalog catalog = GenerationProfileCatalogService.GetOrCreate();
            List<GenerationProfileReport> reports = catalog.Reports
                .Where(report => report)
                .OrderByDescending(report => report.CreatedAt)
                .ToList();

            DrawSavedHeader(reports);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(SavedListHeight)))
            {
                _savedListScroll = EditorGUILayout.BeginScrollView(_savedListScroll);

                if (reports.Count == 0)
                {
                    GUILayout.Space(EditorGUIUtility.singleLineHeight);
                }
                else
                {
                    foreach (GenerationProfileReport report in reports)
                        DrawSavedProfileListItem(report);
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(6f);
            DrawSelectedSavedProfile();
        }

        private void DrawSavedHeader(IReadOnlyList<GenerationProfileReport> reports)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Saved Profiles ({reports.Count})", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!_selectedReport))
                {
                    if (GUILayout.Button("Copy CSV", GUILayout.Width(80f)))
                        CopyCsv(_selectedReport);

                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                        DeleteSelectedReport();
                }
            }
        }

        private void DrawSavedProfileListItem(GenerationProfileReport report)
        {
            bool selected = report == _selectedReport;
            GUIStyle style = selected ? EditorStyles.helpBox : GUIStyle.none;

            using (new EditorGUILayout.VerticalScope(style))
            {
                if (GUILayout.Button(GetReportListTitle(report), EditorStyles.boldLabel))
                    SelectReport(report);

                EditorGUILayout.LabelField(GetReportListInfo(report), EditorStyles.miniLabel);
            }
        }

        private void DrawSelectedSavedProfile()
        {
            if (!_selectedReport)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _savedDetailsScroll = EditorGUILayout.BeginScrollView(_savedDetailsScroll, GUILayout.MaxHeight(360f));
                DrawRunSummary(_selectedReport);
                DrawPhaseSummary(_selectedReport);
                DrawTargetProfiles(_selectedReport);
                EditorGUILayout.EndScrollView();
            }
        }

        private void SelectReport(GenerationProfileReport report)
        {
            _selectedReport = report;
            Selection.activeObject = report;
        }

        private void DeleteSelectedReport()
        {
            if (!_selectedReport)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Profile",
                "Delete the selected profile report?\n\nThis cannot be undone.",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            GenerationProfileReport report = _selectedReport;
            _selectedReport = null;
            GenerationProfileCatalogService.DeleteReport(report);
            Repaint();
        }

        private void ClearSavedProfiles()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Saved Profiles",
                "Delete all saved profile reports?\n\nThis cannot be undone.",
                "Clear",
                "Cancel");

            if (!confirmed)
                return;

            _selectedReport = null;
            GenerationProfileCatalogService.Clear();
            Repaint();
        }

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

        private static void CopyCsv(GenerationProfile profile)
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

            EditorGUIUtility.systemCopyBuffer = string.Join("\n", lines);
        }

        private static void CopyCsv(GenerationProfileReport report)
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

            EditorGUIUtility.systemCopyBuffer = string.Join("\n", lines);
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

        private static string GetReportListTitle(GenerationProfileReport report)
        {
            string createdAt = string.IsNullOrWhiteSpace(report.CreatedAt) ? "Unknown Time" : report.CreatedAt;
            string target = string.IsNullOrWhiteSpace(report.TargetName) ? "Unknown Target" : report.TargetName;
            return $"{createdAt} - {target}";
        }

        private static string GetReportListInfo(GenerationProfileReport report)
        {
            string planning = FormatMilliseconds(report.GetPhaseTime(GenerationProfilePhase.Planning));
            string unattributed = report.PlanningUnattributedMilliseconds > 0f
                ? $"    Unattributed: {FormatMilliseconds(report.PlanningUnattributedMilliseconds)}"
                : string.Empty;

            return $"Total: {FormatMilliseconds(report.GetPhaseTime(GenerationProfilePhase.Total))}    Candidates: {FormatMilliseconds(report.GetPhaseTime(GenerationProfilePhase.CandidateGeneration))}    Planning: {planning}{unattributed}    Placed: {report.PlacedObjectCount}/{report.RequestedObjectCount}    Seed: {report.RandomSeed}    Source: {report.CandidateSource}";
        }

        private static ProfileRuntimeView GetRuntimeProfile(GenerationProfile profile)
        {
            return new ProfileRuntimeView(
                profile.PlanningUnattributedMilliseconds,
                profile.HasManagedRuntimeStats,
                profile.GarbageCollectionsGen0,
                profile.GarbageCollectionsGen1,
                profile.GarbageCollectionsGen2,
                profile.ManagedMemoryBeforeBytes,
                profile.ManagedMemoryAfterBytes);
        }

        private static ProfileRuntimeView GetRuntimeProfile(GenerationProfileReport report)
        {
            return new ProfileRuntimeView(
                report.PlanningUnattributedMilliseconds,
                report.HasManagedRuntimeStats,
                report.GarbageCollectionsGen0,
                report.GarbageCollectionsGen1,
                report.GarbageCollectionsGen2,
                report.ManagedMemoryBeforeBytes,
                report.ManagedMemoryAfterBytes);
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

        private static void DrawStat(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }

        private static string FormatMilliseconds(float milliseconds)
        {
            return milliseconds >= 1000f
                ? $"{milliseconds / 1000f:0.###} s"
                : $"{milliseconds:0.###} ms";
        }

        private static string FormatAverageMilliseconds(float milliseconds, int count) =>
            count > 0 ? FormatMilliseconds(milliseconds / count) : "-";

        private static string FormatBytes(long bytes) =>
            $"{BytesToMegabytes(bytes):0.###} MB";

        private static string FormatByteDelta(long bytes)
        {
            string sign = bytes > 0L ? "+" : string.Empty;
            return $"{sign}{BytesToMegabytes(bytes):0.###} MB";
        }

        private static double BytesToMegabytes(long bytes) =>
            bytes / (1024d * 1024d);

        private static string FormatRatio(int value, int total)
        {
            if (total <= 0)
                return "-";

            return $"{value}/{total} ({value / (float)total:P1})";
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

        private static string FormatValidationStep(ValidationProfileStep step) =>
            step switch
            {
                ValidationProfileStep.Height => "Height",
                ValidationProfileStep.PlannedSpacing => "Planned Spacing",
                ValidationProfileStep.SurfaceFit => "Surface Fit",
                ValidationProfileStep.Footprint => "Footprint",
                ValidationProfileStep.Volume => "Volume",
                ValidationProfileStep.GeneratedOverlap => "Generated Overlap",
                ValidationProfileStep.FixedOverlap => "Fixed Overlap",
                ValidationProfileStep.FixedSpacing => "Fixed Spacing",
                ValidationProfileStep.GeneratedSceneSpacing => "Generated Scene Spacing",
                ValidationProfileStep.Relative => "Relative",
                ValidationProfileStep.Exclusion => "Exclusion Region",
                ValidationProfileStep.WallRelationship => "Wall Relationship",
                ValidationProfileStep.AssetSpacing => "Asset Spacing",
                ValidationProfileStep.Clearance => "Clearance",
                _ => step.ToString()
            };

        private static string FormatPlanningStep(PlanningProfileStep step) =>
            step switch
            {
                PlanningProfileStep.UsableTargetSelection => "Usable Target Selection",
                PlanningProfileStep.TargetSelection => "Target Selection",
                PlanningProfileStep.AssetCatalog => "Asset Catalog",
                PlanningProfileStep.AssetOrder => "Asset Order",
                PlanningProfileStep.AssetPruning => "Asset Pruning",
                PlanningProfileStep.CandidateIteration => "Candidate Iteration",
                PlanningProfileStep.CandidateBuild => "Candidate Build",
                PlanningProfileStep.CandidateValidation => "Candidate Validation",
                PlanningProfileStep.DiagnosticsRecording => "Diagnostics Recording",
                PlanningProfileStep.ObjectNaming => "Object Naming",
                PlanningProfileStep.PlanRecording => "Plan Recording",
                PlanningProfileStep.TargetBudgetRecording => "Target Budget Recording",
                _ => step.ToString()
            };

        private static string FormatAreaBuildStep(AreaBuildProfileStep step) =>
            step switch
            {
                AreaBuildProfileStep.SubspaceResolve => "Subspace Resolve",
                AreaBuildProfileStep.LiveCacheStore => "Live Cache Store",
                AreaBuildProfileStep.VoxelMaskBuild => "Voxel Mask Build",
                AreaBuildProfileStep.VoxelScan => "Voxel Scan",
                AreaBuildProfileStep.SurfaceExtraction => "Surface Extraction",
                AreaBuildProfileStep.SurfaceRegionBuild => "Surface Region Build",
                AreaBuildProfileStep.WallExtraction => "Wall Extraction",
                AreaBuildProfileStep.WallRegionBuild => "Wall Region Build",
                AreaBuildProfileStep.OccupancyBuild => "Occupancy Build",
                AreaBuildProfileStep.SceneIndex => "Scene Index",
                AreaBuildProfileStep.AreaCacheLookup => "Area Cache Lookup",
                AreaBuildProfileStep.AreaCacheStore => "Area Cache Store",
                _ => step.ToString()
            };

        private static string FormatAreaBuildStep(string step)
        {
            if (System.Enum.TryParse(step, out AreaBuildProfileStep parsed))
                return FormatAreaBuildStep(parsed);

            return string.IsNullOrWhiteSpace(step) ? "Unknown" : step;
        }

        private static string FormatPlanningStep(string step)
        {
            if (System.Enum.TryParse(step, out PlanningProfileStep parsed))
                return FormatPlanningStep(parsed);

            return string.IsNullOrWhiteSpace(step) ? "Unknown" : step;
        }

        private static string FormatValidationStep(string step)
        {
            if (System.Enum.TryParse(step, out ValidationProfileStep parsed))
                return FormatValidationStep(parsed);

            return string.IsNullOrWhiteSpace(step) ? "Unknown" : step;
        }

        private static string ShortenRunId(string runId)
        {
            if (string.IsNullOrEmpty(runId))
                return "-";

            return runId.Length <= 8 ? runId : runId.Substring(0, 8);
        }

        private readonly struct RejectionView
        {
            public string Reason { get; }
            public int Count { get; }

            public RejectionView(string reason, int count)
            {
                Reason = reason;
                Count = count;
            }
        }

        private readonly struct ValidationStepView
        {
            public string Step { get; }
            public float Milliseconds { get; }
            public int Calls { get; }

            public ValidationStepView(string step, float milliseconds, int calls)
            {
                Step = step;
                Milliseconds = milliseconds;
                Calls = calls;
            }
        }

        private readonly struct AreaBuildStepView
        {
            public string Step { get; }
            public float Milliseconds { get; }
            public int Calls { get; }

            public AreaBuildStepView(string step, float milliseconds, int calls)
            {
                Step = step;
                Milliseconds = milliseconds;
                Calls = calls;
            }
        }

        private readonly struct PlanningStepView
        {
            public string Step { get; }
            public float Milliseconds { get; }
            public int Calls { get; }

            public PlanningStepView(string step, float milliseconds, int calls)
            {
                Step = step;
                Milliseconds = milliseconds;
                Calls = calls;
            }
        }

        private readonly struct ProfileRuntimeView
        {
            public float PlanningUnattributedMilliseconds { get; }
            public bool HasManagedRuntimeStats { get; }
            public int GarbageCollectionsGen0 { get; }
            public int GarbageCollectionsGen1 { get; }
            public int GarbageCollectionsGen2 { get; }
            public long ManagedMemoryBeforeBytes { get; }
            public long ManagedMemoryAfterBytes { get; }
            public long ManagedMemoryDeltaBytes => ManagedMemoryAfterBytes - ManagedMemoryBeforeBytes;

            public ProfileRuntimeView(
                float planningUnattributedMilliseconds,
                bool hasManagedRuntimeStats,
                int garbageCollectionsGen0,
                int garbageCollectionsGen1,
                int garbageCollectionsGen2,
                long managedMemoryBeforeBytes,
                long managedMemoryAfterBytes)
            {
                PlanningUnattributedMilliseconds = Mathf.Max(0f, planningUnattributedMilliseconds);
                HasManagedRuntimeStats = hasManagedRuntimeStats;
                GarbageCollectionsGen0 = Mathf.Max(0, garbageCollectionsGen0);
                GarbageCollectionsGen1 = Mathf.Max(0, garbageCollectionsGen1);
                GarbageCollectionsGen2 = Mathf.Max(0, garbageCollectionsGen2);
                ManagedMemoryBeforeBytes = managedMemoryBeforeBytes;
                ManagedMemoryAfterBytes = managedMemoryAfterBytes;
            }
        }

        private readonly struct TargetCsvData
        {
            public string PlacementType { get; }
            public float SeedGenerationMilliseconds { get; }
            public float SamplingMilliseconds { get; }
            public float ProjectionMilliseconds { get; }
            public float RaycastMilliseconds { get; }
            public float ValidationMilliseconds { get; }
            public int RawSamples { get; }
            public int CandidateSeeds { get; }
            public int TestedSeeds { get; }
            public int ProjectionAttempts { get; }
            public int ProjectionHits { get; }
            public int RaycastCalls { get; }
            public int RaycastHits { get; }
            public int AssetAttempts { get; }
            public int AcceptedAttempts { get; }
            public int RejectedAttempts { get; }
            public IEnumerable<ValidationStepView> ValidationSteps { get; }
            public IEnumerable<RejectionView> Rejections { get; }

            public TargetCsvData(
                string placementType,
                float seedGenerationMilliseconds,
                float samplingMilliseconds,
                float projectionMilliseconds,
                float raycastMilliseconds,
                float validationMilliseconds,
                int rawSamples,
                int candidateSeeds,
                int testedSeeds,
                int projectionAttempts,
                int projectionHits,
                int raycastCalls,
                int raycastHits,
                int assetAttempts,
                int acceptedAttempts,
                int rejectedAttempts,
                IEnumerable<ValidationStepView> validationSteps,
                IEnumerable<RejectionView> rejections)
            {
                PlacementType = placementType;
                SeedGenerationMilliseconds = seedGenerationMilliseconds;
                SamplingMilliseconds = samplingMilliseconds;
                ProjectionMilliseconds = projectionMilliseconds;
                RaycastMilliseconds = raycastMilliseconds;
                ValidationMilliseconds = validationMilliseconds;
                RawSamples = rawSamples;
                CandidateSeeds = candidateSeeds;
                TestedSeeds = testedSeeds;
                ProjectionAttempts = projectionAttempts;
                ProjectionHits = projectionHits;
                RaycastCalls = raycastCalls;
                RaycastHits = raycastHits;
                AssetAttempts = assetAttempts;
                AcceptedAttempts = acceptedAttempts;
                RejectedAttempts = rejectedAttempts;
                ValidationSteps = validationSteps ?? Enumerable.Empty<ValidationStepView>();
                Rejections = rejections ?? Enumerable.Empty<RejectionView>();
            }
        }
    }
}
