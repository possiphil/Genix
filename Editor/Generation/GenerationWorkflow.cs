using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Editor.Diagnostics;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.Genix.Editor.Common;
using Genix.Editor.Profiling;
using Genix.Editor.Utilities;
using Genix.Extensions;
using Genix.Placement;
using Genix.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Editor.Generation
{
    /// <summary>Coordinates preview, generation, regeneration, and cleanup operations in the Unity editor.</summary>
    public static class GenerationWorkflow
    {
        private const string GenerateUndoName = "Generated Genix Objects";
        private const string RegenerateUndoName = "Regenerated Genix Objects";
        private const string ClearUndoName = "Cleared Genix Objects";
        private const string ApplyPreviewUndoName = "Applied Genix Preview";

        private static PreviewPlan _lastPreviewPlan;

        /// <summary>Indicates whether preview plan.</summary>
        public static bool HasPreviewPlan => _lastPreviewPlan is { Count: > 0 };

        /// <summary>Plans and applies a generation request to the scene.</summary>
        public static void Generate(GenerationRequest request)
        {
            if (!Validate(request))
                return;

            ClearPreviewPlan();
            ClearPreviewDiagnostics();
            UndoStep.ExecuteAsSingleStep(GenerateUndoName, () => GenerateInternal(request));
        }

        /// <summary>Clears the previous result and generates a replacement from the request.</summary>
        public static void Regenerate(GenerationRequest request)
        {
            if (!Validate(request))
                return;

            ClearPreviewPlan();
            ClearPreviewDiagnostics();
            UndoStep.ExecuteAsSingleStep(RegenerateUndoName, () => RegenerateInternal(request));
        }

        /// <summary>Builds and retains a generation plan without instantiating scene objects.</summary>
        public static void Preview(GenerationRequest request)
        {
            ClearPreviewPlan();
            ClearPreviewDiagnostics();

            if (!Validate(request))
                return;

            PreviewInternal(request);
        }

        /// <summary>Applies the retained preview plan without generating a new plan.</summary>
        public static bool ApplyPreview()
        {
            if (!HasPreviewPlan)
            {
                Debug.LogWarning("No Genix preview run is available to apply. Run Preview Run first.");
                return false;
            }

            PreviewPlan preview = _lastPreviewPlan;

            if (preview.AreaSource == null || !preview.AreaSource.ParentTransform)
            {
                ClearPreviewPlan();
                Debug.LogWarning("The last Genix preview can no longer be applied because its target area is no longer available.");
                return false;
            }

            bool applied = false;
            string applyError = string.Empty;

            UndoStep.ExecuteAsSingleStep(ApplyPreviewUndoName, () =>
            {
                bool parentExisted = GeneratedHierarchy.TryGet(preview.AreaSource, out _);
                Transform generatedParent = GeneratedHierarchy.GetOrCreate(preview.AreaSource);

                if (!SceneGenerationService.Apply(preview.Plan, generatedParent, out applyError))
                {
                    SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);
                    return;
                }

                applied = true;
            });

            if (!applied)
            {
                if (!string.IsNullOrWhiteSpace(applyError))
                    Debug.LogWarning(applyError);

                return false;
            }

            ClearPreviewPlan();
            Debug.Log($"Applied Genix preview run: generated {preview.Count} objects for '{preview.TargetName}'.");
            return true;
        }

        /// <summary>Clears the stored state.</summary>
        public static void Clear(IAreaSource areaSource)
        {
            if (areaSource == null || !areaSource.ParentTransform)
            {
                Debug.LogWarning("No location is selected. Choose a Target Area before clearing generated objects.");
                return;
            }

            ClearPreviewPlan();
            ClearPreviewDiagnostics();
            UndoStep.ExecuteAsSingleStep(ClearUndoName, () =>
            {
                if (!SceneGenerationService.Clear(areaSource))
                    Debug.Log("No generated Genix objects were found for the selected location.");
            });
        }

        /// <summary>Clears preview plan.</summary>
        public static void ClearPreviewPlan()
        {
            _lastPreviewPlan = null;
        }

        private static bool GenerateInternal(GenerationRequest request)
        {
            bool shouldProfile = GenerationProfilerService.ProfilingEnabled;
            Stopwatch totalStopwatch = shouldProfile ? Stopwatch.StartNew() : null;
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            Stopwatch assetFilterStopwatch = shouldProfile ? Stopwatch.StartNew() : null;
            bool assetsResolved = GenerationAssetFilter.TryResolve(
                request,
                catalog,
                out List<AssetDefinition> assets,
                out string assetError);
            float assetFilterMilliseconds = StopAndReadMilliseconds(assetFilterStopwatch);

            if (!assetsResolved)
            {
                Debug.LogWarning(assetError);
                return false;
            }

            foreach (string warning in GenerationAssetFilter.GetUnavailableTargetWarnings(request, assets))
                Debug.LogWarning(warning);

            bool parentExisted = GeneratedHierarchy.TryGet(request.AreaSource, out _);
            Transform generatedParent = GeneratedHierarchy.GetOrCreate(request.AreaSource);

            if (!TryCreateContext(request, generatedParent, assets, out GenerationContext context))
            {
                SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);
                return false;
            }

            GenerationProfilerRecorder profileRecorder = shouldProfile
                ? GenerationProfilerService.CreateRecorderIfEnabled()
                : null;
            IGenerationProfiler profiler = InitializeProfiler(
                profileRecorder,
                context,
                request.StyleName,
                dryRun: false,
                assetFilterMilliseconds);

            try
            {
                if (!RelativeAnchorProvider.HasAnyAnchor(context))
                {
                    string message =
                        $"Relative placement could not start because '{request.RelativePlacement.Source.ToDisplayName()}' has no usable anchor objects. " +
                        "Choose another Relative To source, select scene objects, generate anchor objects first, or adjust the relative scene layers.";
                    RecordProfileStopReason(profileRecorder, message);
                    SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);
                    Debug.LogWarning(message);
                    return false;
                }

                DiagnosticsRecorder recorder = CreateDiagnosticsRecorder(request, context, recordAcceptedCandidates: false);
                ManagedRuntimeSnapshot runtimeBefore = profileRecorder != null
                    ? CaptureManagedRuntimeSnapshot()
                    : default;
                Stopwatch planningStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                GenerationOutcome outcome = GenerationEngine.BuildPlan(context, assets, recorder, profiler);
                ManagedRuntimeSnapshot runtimeAfter = profileRecorder != null
                    ? CaptureManagedRuntimeSnapshot()
                    : default;
                float planningMilliseconds = StopAndReadMilliseconds(planningStopwatch);
                RecordPlanningTime(profiler, planningMilliseconds);
                RecordManagedRuntimeStats(profileRecorder, runtimeBefore, runtimeAfter);
                RecordProfilePlacedCount(profileRecorder, outcome.PlacedCount);

                if (!outcome.ShouldApply)
                {
                    context.Plan.Clear();
                    recorder.Diagnostics.Placements.Clear();
                    recorder.RecordStopReason(outcome.Message);
                    RecordProfileStopReason(profileRecorder, outcome.Message);
                    DiagnosticsStore.SetLast(recorder.Diagnostics);
                    SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);

                    string rollbackText = context.BestEffort
                        ? string.Empty
                        : " Best Effort is disabled, so the complete plan was discarded and nothing was placed.";
                    Debug.LogWarning(
                        $"Genix found {outcome.PlacedCount} of {context.Count} requested placements. " +
                        $"{outcome.Message}{rollbackText} Open Genix Diagnostics for rejection details.");
                    return false;
                }

                Stopwatch applyStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                if (!SceneGenerationService.Apply(context.Plan, generatedParent, out string applyError))
                {
                    float applyMilliseconds = StopAndReadMilliseconds(applyStopwatch);
                    profiler.AddPhaseTime(GenerationProfilePhase.Apply, applyMilliseconds);
                    recorder.Diagnostics.Placements.Clear();
                    recorder.RecordStopReason(applyError);
                    RecordProfileStopReason(profileRecorder, applyError);
                    DiagnosticsStore.SetLast(recorder.Diagnostics);
                    SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);
                    Debug.LogWarning(applyError);
                    return false;
                }

                profiler.AddPhaseTime(GenerationProfilePhase.Apply, StopAndReadMilliseconds(applyStopwatch));

                if (!outcome.IsComplete)
                {
                    recorder.RecordStopReason(outcome.Message);
                    RecordProfileStopReason(profileRecorder, outcome.Message);
                    Debug.LogWarning(outcome.Message);
                }

                DiagnosticsStore.SetLast(recorder.Diagnostics);
                return true;
            }
            finally
            {
                FinishProfile(profileRecorder, profiler, totalStopwatch);
            }
        }

        private static bool PreviewInternal(GenerationRequest request)
        {
            bool shouldProfile = GenerationProfilerService.ProfilingEnabled;
            Stopwatch totalStopwatch = shouldProfile ? Stopwatch.StartNew() : null;
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            Stopwatch assetFilterStopwatch = shouldProfile ? Stopwatch.StartNew() : null;
            bool assetsResolved = GenerationAssetFilter.TryResolve(
                request,
                catalog,
                out List<AssetDefinition> assets,
                out string assetError);
            float assetFilterMilliseconds = StopAndReadMilliseconds(assetFilterStopwatch);

            if (!assetsResolved)
            {
                Debug.LogWarning(assetError);
                return false;
            }

            foreach (string warning in GenerationAssetFilter.GetUnavailableTargetWarnings(request, assets))
                Debug.LogWarning(warning);

            Stopwatch contextSetupStopwatch = shouldProfile ? Stopwatch.StartNew() : null;
            bool parentExisted = GeneratedHierarchy.TryGet(request.AreaSource, out _);
            Transform generatedParent = GeneratedHierarchy.GetOrCreate(request.AreaSource);

            if (!TryCreateContext(request, generatedParent, assets, out GenerationContext context))
            {
                SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);
                return false;
            }

            GenerationProfilerRecorder profileRecorder = shouldProfile
                ? GenerationProfilerService.CreateRecorderIfEnabled()
                : null;
            IGenerationProfiler profiler = InitializeProfiler(
                profileRecorder,
                context,
                request.StyleName,
                dryRun: true,
                assetFilterMilliseconds);
            float contextSetupMilliseconds = StopAndReadMilliseconds(contextSetupStopwatch);
            profiler.AddPhaseTime(
                GenerationProfilePhase.ContextSetup,
                Mathf.Max(0f, contextSetupMilliseconds - context.AreaBuildMilliseconds));

            try
            {
                if (!RelativeAnchorProvider.HasAnyAnchor(context))
                {
                    string message =
                        $"Relative placement preview could not start because '{request.RelativePlacement.Source.ToDisplayName()}' has no usable anchor objects.";
                    RecordProfileStopReason(profileRecorder, message);
                    SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);
                    Debug.LogWarning(message);
                    return false;
                }

                DiagnosticsRecorder recorder = CreateDiagnosticsRecorder(request, context, recordAcceptedCandidates: true);
                recorder.Diagnostics.DryRun = true;
                ManagedRuntimeSnapshot runtimeBefore = profileRecorder != null
                    ? CaptureManagedRuntimeSnapshot()
                    : default;
                Stopwatch planningStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                GenerationOutcome outcome = GenerationEngine.BuildPlan(context, assets, recorder, profiler);
                ManagedRuntimeSnapshot runtimeAfter = profileRecorder != null
                    ? CaptureManagedRuntimeSnapshot()
                    : default;
                float planningMilliseconds = StopAndReadMilliseconds(planningStopwatch);
                RecordPlanningTime(profiler, planningMilliseconds);
                RecordManagedRuntimeStats(profileRecorder, runtimeBefore, runtimeAfter);
                RecordProfilePlacedCount(profileRecorder, outcome.PlacedCount);

                if (!outcome.IsComplete)
                {
                    recorder.RecordStopReason(outcome.Message);
                    RecordProfileStopReason(profileRecorder, outcome.Message);
                }

                Stopwatch previewPlanStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                bool previewPlanRetained = StorePreviewPlan(
                    outcome.ShouldApply ? request.AreaSource : null,
                    context.Plan);
                profiler.AddPhaseTime(
                    GenerationProfilePhase.PreviewPlanCopy,
                    StopAndReadMilliseconds(previewPlanStopwatch));

                Stopwatch diagnosticsStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                DiagnosticsStore.ShowAcceptedCandidates = true;
                DiagnosticsStore.SetLast(recorder.Diagnostics);
                profiler.AddPhaseTime(
                    GenerationProfilePhase.PreviewDiagnosticsHandoff,
                    StopAndReadMilliseconds(diagnosticsStopwatch));

                Stopwatch cleanupStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                if (!previewPlanRetained)
                    context.Plan.Clear();
                SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);
                profiler.AddPhaseTime(
                    GenerationProfilePhase.PreviewCleanup,
                    StopAndReadMilliseconds(cleanupStopwatch));

                Stopwatch logStopwatch = profiler.IsEnabled ? Stopwatch.StartNew() : null;
                Debug.Log(
                    outcome.ShouldApply
                        ? $"Genix preview run planned {outcome.PlacedCount} of {context.Count} requested placements. Open Genix Diagnostics for the scene preview."
                        : $"Genix preview run found {outcome.PlacedCount} of {context.Count} requested placements. {outcome.Message} Open Genix Diagnostics for rejection details.");
                profiler.AddPhaseTime(
                    GenerationProfilePhase.PreviewLog,
                    StopAndReadMilliseconds(logStopwatch));
                return outcome.ShouldApply;
            }
            finally
            {
                FinishProfile(profileRecorder, profiler, totalStopwatch);
            }
        }

        private static IGenerationProfiler InitializeProfiler(
            GenerationProfilerRecorder recorder,
            GenerationContext context,
            string styleName,
            bool dryRun,
            float assetFilterMilliseconds)
        {
            IGenerationProfiler profiler = recorder != null
                ? recorder
                : NullGenerationProfiler.Instance;

            if (!profiler.IsEnabled)
                return profiler;

            profiler.Initialize(context, styleName, dryRun);
            profiler.AddPhaseTime(GenerationProfilePhase.AssetFilter, assetFilterMilliseconds);
            profiler.AddPhaseTime(GenerationProfilePhase.AreaBuild, context.AreaBuildMilliseconds);
            RecordAreaBuildSteps(profiler, context.AreaBuildProfile);
            return profiler;
        }

        private static void RecordAreaBuildSteps(
            IGenerationProfiler profiler,
            AreaBuildProfile areaBuildProfile)
        {
            if (profiler is not { IsEnabled: true } || areaBuildProfile == null)
                return;

            foreach (AreaBuildStepProfile step in areaBuildProfile.Steps)
                profiler.RecordAreaBuildStep(step.Step, step.Milliseconds, step.Calls);
        }

        private static void RecordPlanningTime(IGenerationProfiler profiler, float totalPlanningMilliseconds)
        {
            if (profiler is not { IsEnabled: true })
                return;

            float candidateMilliseconds = profiler.Profile.GetPhaseTime(GenerationProfilePhase.CandidateGeneration);
            profiler.AddPhaseTime(
                GenerationProfilePhase.Planning,
                Mathf.Max(0f, totalPlanningMilliseconds - candidateMilliseconds));
            profiler.Profile.RecordPlanningUnattributedTime(
                profiler.Profile.GetPhaseTime(GenerationProfilePhase.Planning));
        }

        private static void RecordManagedRuntimeStats(
            GenerationProfilerRecorder recorder,
            ManagedRuntimeSnapshot before,
            ManagedRuntimeSnapshot after)
        {
            if (recorder == null)
                return;

            recorder.Profile.RecordManagedRuntimeStats(
                after.GarbageCollectionsGen0 - before.GarbageCollectionsGen0,
                after.GarbageCollectionsGen1 - before.GarbageCollectionsGen1,
                after.GarbageCollectionsGen2 - before.GarbageCollectionsGen2,
                before.ManagedMemoryBytes,
                after.ManagedMemoryBytes);
        }

        private static void RecordProfilePlacedCount(GenerationProfilerRecorder recorder, int count)
        {
            if (recorder != null)
                recorder.Profile.PlacedObjectCount = Mathf.Max(0, count);
        }

        private static void RecordProfileStopReason(GenerationProfilerRecorder recorder, string stopReason)
        {
            if (recorder != null)
                recorder.Profile.StopReason = stopReason ?? string.Empty;
        }

        private static void FinishProfile(
            GenerationProfilerRecorder recorder,
            IGenerationProfiler profiler,
            Stopwatch totalStopwatch)
        {
            if (recorder == null)
                return;

            profiler.AddPhaseTime(GenerationProfilePhase.Total, StopAndReadMilliseconds(totalStopwatch));
            GenerationProfilerService.Store(recorder);
        }

        private static float StopAndReadMilliseconds(Stopwatch stopwatch)
        {
            if (stopwatch == null)
                return 0f;

            stopwatch.Stop();
            return (float)stopwatch.Elapsed.TotalMilliseconds;
        }

        private static void ClearPreviewDiagnostics()
        {
            if (DiagnosticsStore.LastDiagnostics?.DryRun == true)
                DiagnosticsStore.Clear();
        }

        private static ManagedRuntimeSnapshot CaptureManagedRuntimeSnapshot()
        {
            return new ManagedRuntimeSnapshot(
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2),
                GC.GetTotalMemory(false));
        }

        private static bool StorePreviewPlan(IAreaSource areaSource, GenerationPlan plan)
        {
            if (areaSource == null || plan == null || plan.Count == 0)
            {
                ClearPreviewPlan();
                return false;
            }

            _lastPreviewPlan = new PreviewPlan(areaSource, plan);
            return true;
        }

        private static void RegenerateInternal(GenerationRequest request)
        {
            GameObject snapshot = SceneGenerationService.CreateSnapshot(request.AreaSource);

            try
            {
                SceneGenerationService.Clear(request.AreaSource);

                if (!GenerateInternal(request))
                    SceneGenerationService.RestoreSnapshot(request.AreaSource, snapshot);
            }
            finally
            {
                if (snapshot)
                    UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        private static bool TryCreateContext(
            GenerationRequest request,
            Transform generatedParent,
            IReadOnlyList<AssetDefinition> assets,
            out GenerationContext context)
        {
            try
            {
                context = GenerationContextFactory.Create(request, generatedParent, assets);
                return true;
            }
            catch (Exception exception)
            {
                context = null;
                Debug.LogWarning(exception.Message);
                return false;
            }
        }

        private static DiagnosticsRecorder CreateDiagnosticsRecorder(
            GenerationRequest request,
            GenerationContext context,
            bool recordAcceptedCandidates)
        {
            DiagnosticsMode diagnosticsMode = request.DetailedDiagnostics
                ? DiagnosticsMode.Detailed
                : DiagnosticsMode.Summary;

            return new DiagnosticsRecorder(
                context,
                diagnosticsMode,
                request.StyleName,
                recordAcceptedCandidates);
        }

        private static bool Validate(GenerationRequest request)
        {
            if (GenerationPreflight.IsValid(request, out string error))
                return true;

            Debug.LogWarning(error);
            return false;
        }

        private sealed class PreviewPlan
        {
            public IAreaSource AreaSource { get; }
            public GenerationPlan Plan { get; }
            public string TargetName { get; }
            public int Count => Plan.Count;

            public PreviewPlan(IAreaSource areaSource, GenerationPlan plan)
            {
                AreaSource = areaSource;
                Plan = plan;
                TargetName = areaSource.SourceInfo.SourceName;
            }
        }

        private readonly struct ManagedRuntimeSnapshot
        {
            public int GarbageCollectionsGen0 { get; }
            public int GarbageCollectionsGen1 { get; }
            public int GarbageCollectionsGen2 { get; }
            public long ManagedMemoryBytes { get; }

            public ManagedRuntimeSnapshot(
                int garbageCollectionsGen0,
                int garbageCollectionsGen1,
                int garbageCollectionsGen2,
                long managedMemoryBytes)
            {
                GarbageCollectionsGen0 = garbageCollectionsGen0;
                GarbageCollectionsGen1 = garbageCollectionsGen1;
                GarbageCollectionsGen2 = garbageCollectionsGen2;
                ManagedMemoryBytes = managedMemoryBytes;
            }
        }
    }
}
