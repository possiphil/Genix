using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Profiling;

namespace Genix.Editor.Benchmarking
{
    /// <summary>Lets local automation validate and run the benchmark runner used by the Editor window.</summary>
    [InitializeOnLoad]
    internal static class GenerationBenchmarkCommandBridge
    {
        private const string RequestPath = "Library/Genix/BenchmarkCommandRequest.json";
        private const string ResponsePath = "Library/Genix/BenchmarkCommandResponse.json";
        private const string RefreshSessionKey = "Genix.OpenEditorBenchmarkBridge.RefreshPending";
        private const double PollIntervalSeconds = 0.25d;

        private static double _nextPoll;
        private static bool _ownsActiveRun;

        static GenerationBenchmarkCommandBridge()
        {
            EditorApplication.update += Poll;
            GenerationBenchmarkRunner.Changed += HandleRunnerChanged;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll ||
                GenerationBenchmarkRunner.IsRunning ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            _nextPoll = EditorApplication.timeSinceStartup + PollIntervalSeconds;
            if (!File.Exists(RequestPath))
            {
                SessionState.SetBool(RefreshSessionKey, false);
                return;
            }

            if (!SessionState.GetBool(RefreshSessionKey, false))
            {
                SessionState.SetBool(RefreshSessionKey, true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return;
            }

            SessionState.SetBool(RefreshSessionKey, false);

            BenchmarkCommandRequest request;
            try
            {
                request = JsonUtility.FromJson<BenchmarkCommandRequest>(File.ReadAllText(RequestPath));
                File.Delete(RequestPath);
            }
            catch (Exception exception)
            {
                WriteResponse(new BenchmarkCommandResponse { error = exception.Message });
                return;
            }

            try
            {
                if (request.prepareEnvironment)
                {
                    Profiler.enabled = false;

                    if (CompilationPipeline.codeOptimization != CodeOptimization.Release)
                    {
                        CompilationPipeline.codeOptimization = CodeOptimization.Release;
                        request.prepareEnvironment = false;
                        File.WriteAllText(RequestPath, JsonUtility.ToJson(request, true));
                        return;
                    }
                }

                GenerationBenchmarkSuite suite = AssetDatabase.LoadAssetAtPath<GenerationBenchmarkSuite>(
                    request.suiteAssetPath);
                if (!suite)
                {
                    throw new InvalidOperationException(
                        $"Benchmark suite was not found at '{request.suiteAssetPath}'.");
                }

                List<string> errors = GenerationBenchmarkRunner.Validate(suite, request.scenarioIndex);
                if (request.validateOnly || errors.Count > 0)
                {
                    WriteResponse(new BenchmarkCommandResponse
                    {
                        status = errors.Count == 0 ? "Benchmark suite validation passed." : "Benchmark suite validation failed.",
                        error = string.Join("\n", errors)
                    });
                    return;
                }

                if (!GenerationBenchmarkRunner.Start(suite, request.scenarioIndex))
                {
                    WriteResponse(new BenchmarkCommandResponse
                    {
                        status = GenerationBenchmarkRunner.Status,
                        error = GenerationBenchmarkRunner.LastError
                    });
                    return;
                }

                _ownsActiveRun = true;
            }
            catch (Exception exception)
            {
                WriteResponse(new BenchmarkCommandResponse { error = exception.ToString() });
            }
        }

        private static void HandleRunnerChanged()
        {
            if (!_ownsActiveRun || GenerationBenchmarkRunner.IsRunning)
                return;

            _ownsActiveRun = false;
            IReadOnlyCollection<GenerationBenchmarkRunRecord> runs = GenerationBenchmarkRunner.RunRecords;
            WriteResponse(new BenchmarkCommandResponse
            {
                status = GenerationBenchmarkRunner.Status,
                error = GenerationBenchmarkRunner.LastError,
                completedRuns = runs.Count,
                failedRuns = runs.Count(run => !run.succeeded),
                incompleteRuns = runs.Count(run => run.succeeded && !run.complete),
                semanticMismatches = runs.Count(run =>
                    run.measurement == BenchmarkMeasurementKind.Diagnostic.ToString() &&
                    run.hasPrimaryReference &&
                    !run.resultMatchesPrimary),
                outputDirectory = GenerationBenchmarkRunner.LastOutputDirectory
            });
        }

        private static void WriteResponse(BenchmarkCommandResponse response)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResponsePath) ?? "Library/Genix");
            string temporaryPath = ResponsePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(response, true));
            if (File.Exists(ResponsePath))
                File.Delete(ResponsePath);
            File.Move(temporaryPath, ResponsePath);
        }

        [Serializable]
        private sealed class BenchmarkCommandRequest
        {
            public string suiteAssetPath = "Assets/Genix/Benchmarks/ThesisPerformanceSuite.asset";
            public int scenarioIndex = -1;
            public bool validateOnly = false;
            public bool prepareEnvironment;
        }

        [Serializable]
        private sealed class BenchmarkCommandResponse
        {
            public string status = string.Empty;
            public string error = string.Empty;
            public int completedRuns;
            public int failedRuns;
            public int incompleteRuns;
            public int semanticMismatches;
            public string outputDirectory = string.Empty;
        }
    }
}
