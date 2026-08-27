using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Editor.Generation;
using Genix.Placement;
using Genix.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Editor.Benchmarking
{
    /// <summary>Result of one headless generation-core measurement.</summary>
    internal sealed class GenerationBenchmarkExecutionResult
    {
        public bool Succeeded { get; set; }
        public bool Complete { get; set; }
        public int PlacedCount { get; set; }
        public string Message { get; set; } = string.Empty;
        public double ElapsedMilliseconds { get; set; }
        public string ResultHash { get; set; } = string.Empty;
        public GenerationProfile Profile { get; set; }
    }

    /// <summary>
    /// Executes the production generation core without preview UI, logging, scene application, or result I/O.
    /// </summary>
    internal static class GenerationBenchmarkExecutor
    {
        public static GenerationBenchmarkExecutionResult Execute(
            GenerationRequest request,
            AssetCatalog catalog,
            Transform generatedParent,
            BenchmarkMeasurementKind measurement)
        {
            bool diagnostic = measurement == BenchmarkMeasurementKind.Diagnostic;
            GenerationProfilerRecorder recorder = diagnostic ? new GenerationProfilerRecorder() : null;
            IGenerationProfiler profiler = diagnostic
                ? recorder
                : NullGenerationProfiler.Instance;
            Stopwatch assetFilterStopwatch = diagnostic ? Stopwatch.StartNew() : null;
            long totalStart = Stopwatch.GetTimestamp();

            if (!GenerationAssetFilter.TryResolve(request, catalog, out List<AssetDefinition> assets, out string assetError))
            {
                double failedElapsed = ElapsedMilliseconds(totalStart);
                return new GenerationBenchmarkExecutionResult
                {
                    Succeeded = false,
                    Message = assetError,
                    ElapsedMilliseconds = failedElapsed
                };
            }

            assetFilterStopwatch?.Stop();

            try
            {
                GenerationContext context = diagnostic
                    ? GenerationContextFactory.Create(request, generatedParent, assets)
                    : GenerationContextFactory.CreateUninstrumented(request, generatedParent, assets);

                if (diagnostic)
                {
                    recorder.Initialize(context, request.StyleName, dryRun: true);
                    recorder.AddPhaseTime(
                        GenerationProfilePhase.AssetFilter,
                        (float)assetFilterStopwatch.Elapsed.TotalMilliseconds);
                    recorder.AddPhaseTime(GenerationProfilePhase.AreaBuild, context.AreaBuildMilliseconds);

                    if (context.AreaBuildProfile != null)
                    {
                        foreach (AreaBuildStepProfile step in context.AreaBuildProfile.Steps)
                            recorder.RecordAreaBuildStep(step.Step, step.Milliseconds, step.Calls);
                    }
                }

                if (!RelativeAnchorProvider.HasAnyAnchor(context))
                {
                    double noAnchorElapsed = ElapsedMilliseconds(totalStart);
                    string message = $"Relative placement source '{request.RelativePlacement.Source}' has no usable anchors.";

                    if (recorder != null)
                    {
                        recorder.Profile.StopReason = message;
                        recorder.AddPhaseTime(GenerationProfilePhase.Total, (float)noAnchorElapsed);
                    }

                    return new GenerationBenchmarkExecutionResult
                    {
                        Succeeded = false,
                        Message = message,
                        ElapsedMilliseconds = noAnchorElapsed,
                        Profile = recorder?.Profile
                    };
                }

                Stopwatch planningStopwatch = diagnostic ? Stopwatch.StartNew() : null;
                GenerationOutcome outcome = GenerationEngine.BuildPlan(
                    context,
                    assets,
                    NullDiagnosticsSink.Instance,
                    profiler);
                planningStopwatch?.Stop();
                double elapsed = ElapsedMilliseconds(totalStart);
                string resultHash = CalculatePlanHash(context.Plan);

                if (recorder != null)
                {
                    float candidateMilliseconds = recorder.Profile.GetPhaseTime(GenerationProfilePhase.CandidateGeneration);
                    float planningMilliseconds = (float)planningStopwatch.Elapsed.TotalMilliseconds;
                    recorder.AddPhaseTime(
                        GenerationProfilePhase.Planning,
                        Mathf.Max(0f, planningMilliseconds - candidateMilliseconds));
                    recorder.Profile.RecordPlanningUnattributedTime(
                        recorder.Profile.GetPhaseTime(GenerationProfilePhase.Planning));
                    recorder.Profile.PlacedObjectCount = outcome.PlacedCount;
                    recorder.Profile.StopReason = outcome.Message ?? string.Empty;
                    recorder.AddPhaseTime(GenerationProfilePhase.Total, (float)elapsed);
                }

                context.Plan.Clear();
                return new GenerationBenchmarkExecutionResult
                {
                    Succeeded = outcome.ShouldApply,
                    Complete = outcome.IsComplete,
                    PlacedCount = outcome.PlacedCount,
                    Message = outcome.Message ?? string.Empty,
                    ElapsedMilliseconds = elapsed,
                    ResultHash = resultHash,
                    Profile = recorder?.Profile
                };
            }
            catch (Exception exception)
            {
                double elapsed = ElapsedMilliseconds(totalStart);
                return new GenerationBenchmarkExecutionResult
                {
                    Succeeded = false,
                    Message = exception.Message,
                    ElapsedMilliseconds = elapsed,
                    Profile = recorder?.Profile
                };
            }
        }

        private static double ElapsedMilliseconds(long startTimestamp) =>
            (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

        private static string CalculatePlanHash(GenerationPlan plan)
        {
            ulong hash = 14695981039346656037UL;

            foreach (PlannedObject planned in plan.Objects)
            {
                AddString(ref hash, planned.Asset ? planned.Asset.name : string.Empty);
                AddInt(ref hash, (int)planned.Candidate.PlacementType);
                AddVector(ref hash, planned.Candidate.Position);
                AddQuaternion(ref hash, planned.Candidate.Rotation);
            }

            return hash.ToString("X16");
        }

        private static void AddVector(ref ulong hash, Vector3 value)
        {
            AddInt(ref hash, BitConverter.SingleToInt32Bits(value.x));
            AddInt(ref hash, BitConverter.SingleToInt32Bits(value.y));
            AddInt(ref hash, BitConverter.SingleToInt32Bits(value.z));
        }

        private static void AddQuaternion(ref ulong hash, Quaternion value)
        {
            AddInt(ref hash, BitConverter.SingleToInt32Bits(value.x));
            AddInt(ref hash, BitConverter.SingleToInt32Bits(value.y));
            AddInt(ref hash, BitConverter.SingleToInt32Bits(value.z));
            AddInt(ref hash, BitConverter.SingleToInt32Bits(value.w));
        }

        private static void AddString(ref ulong hash, string value)
        {
            foreach (char character in value ?? string.Empty)
                AddInt(ref hash, character);
        }

        private static void AddInt(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211UL;
            }
        }
    }
}
