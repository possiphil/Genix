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
        private static string GetReportListTitle(GenerationProfileReport report)
        {
            string createdAt = string.IsNullOrWhiteSpace(report.CreatedAt) ? "Unknown Time" : report.CreatedAt;
            string target = string.IsNullOrWhiteSpace(report.TargetName) ? "Unknown Target" : report.TargetName;
            return $"{createdAt} - {target}";
        }

        private static string GetReportListInfo(GenerationProfileReport report)
        {
            return $"Total: {FormatMilliseconds(report.GetPhaseTime(GenerationProfilePhase.Total))}    Candidates: {FormatMilliseconds(report.GetPhaseTime(GenerationProfilePhase.CandidateGeneration))}    Planning: {FormatMilliseconds(report.GetPhaseTime(GenerationProfilePhase.Planning))}";
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
                ValidationProfileStep.GeneratedSceneSpacing => "Generated Object Spacing",
                ValidationProfileStep.Relative => "Relative Placement",
                ValidationProfileStep.Exclusion => "Exclusion Region",
                ValidationProfileStep.WallRelationship => "Wall Relationship",
                ValidationProfileStep.AssetSpacing => "Asset Spacing",
                ValidationProfileStep.Clearance => "Clearance",
                _ => step.ToString()
            };

        private static string FormatPlanningStep(PlanningProfileStep step) =>
            step switch
            {
                PlanningProfileStep.UsableTargetSelection => "Eligible Targets",
                PlanningProfileStep.TargetSelection => "Target Selection",
                PlanningProfileStep.AssetCatalog => "Asset Catalog",
                PlanningProfileStep.AssetOrder => "Asset Order",
                PlanningProfileStep.AssetPruning => "Asset Pruning",
                PlanningProfileStep.CandidateIteration => "Candidate Iteration",
                PlanningProfileStep.CandidateBuild => "Candidate Build",
                PlanningProfileStep.CandidateValidation => "Candidate Validation",
                PlanningProfileStep.DiagnosticsRecording => "Diagnostics",
                PlanningProfileStep.ObjectNaming => "Object Naming",
                PlanningProfileStep.PlanRecording => "Plan Update",
                PlanningProfileStep.TargetBudgetRecording => "Budget Recording",
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

        private static bool IsPreviewRun(string runType) =>
            !string.IsNullOrWhiteSpace(runType) &&
            runType.IndexOf("Preview", System.StringComparison.OrdinalIgnoreCase) >= 0;

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
