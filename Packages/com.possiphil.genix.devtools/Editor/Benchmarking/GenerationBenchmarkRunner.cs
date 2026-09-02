using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Assets;
using Genix.Editor.Common;
using Genix.Editor.Profiling;
using Genix.Editor.TargetAreas;
using Genix.Placement;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Genix.Editor.Benchmarking
{
    internal sealed class GenerationBenchmarkWorkItem
    {
        public int ScenarioIndex { get; set; }
        public GenerationBenchmarkScenario Scenario { get; set; }
        public BenchmarkCacheCondition CacheCondition { get; set; }
        public BenchmarkMeasurementKind Measurement { get; set; }
        public int ObjectCount { get; set; }
        public int Seed { get; set; }
        public int Repetition { get; set; }

        public string BlockKey => $"{ScenarioIndex}|{CacheCondition}|{Measurement}|{ObjectCount}";
        public string ResultKey => $"{ScenarioIndex}|{CacheCondition}|{ObjectCount}|{Seed}|{Repetition}";
    }

    /// <summary>Runs complete scene benchmark suites one synchronous generation at a time.</summary>
    [InitializeOnLoad]
    internal static class GenerationBenchmarkRunner
    {
        private enum RunnerState
        {
            Idle,
            LoadScene,
            SettleScene,
            Run,
            RestoreScene
        }

        private const string InterruptedSessionKey = "Genix.Benchmarks.Running";

        private static readonly Dictionary<string, string> PrimaryHashes = new();
        private static readonly List<GenerationBenchmarkWorkItem> WorkItems = new();
        private static readonly List<GenerationBenchmarkRunRecord> Records = new();

        private static RunnerState _state;
        private static GenerationBenchmarkSuite _suite;
        private static AssetCatalog _catalog;
        private static GenerationBenchmarkCampaignResult _campaign;
        private static int _workIndex;
        private static int _settleFramesRemaining;
        private static string _loadedScenarioPath = string.Empty;
        private static string _currentBlockKey = string.Empty;
        private static readonly EditorCampaignAreaContext AreaContext = new();
        private static GameObject _generatedParent;
        private static bool _cancelRequested;
        private static EditorCampaignSession _session;
        private static string _status = "Ready";
        private static string _lastError = string.Empty;
        private static string _lastOutputDirectory = string.Empty;

        static GenerationBenchmarkRunner()
        {
            EditorApplication.update += Update;

            if (EditorCampaignSession.ConsumeInterruptedMarker(InterruptedSessionKey))
            {
                _status = "The previous benchmark was interrupted by a domain reload or editor restart.";
            }
        }

        public static event Action Changed;

        public static bool IsRunning => _state != RunnerState.Idle;
        public static int CompletedRuns => Records.Count;
        public static int TotalRuns => WorkItems.Count;
        public static string Status => _status;
        public static string LastError => _lastError;
        public static string LastOutputDirectory => _lastOutputDirectory;
        public static IReadOnlyList<GenerationBenchmarkRunRecord> RunRecords => Records;
        public static double ElapsedSeconds => IsRunning ? _session?.ElapsedSeconds ?? 0d : 0d;
        public static double EstimatedRemainingSeconds => Records.Count == 0
            ? 0d
            : Math.Max(0d, ElapsedSeconds / Records.Count * (WorkItems.Count - Records.Count));

        public static bool Start(GenerationBenchmarkSuite suite, int selectedScenario = -1)
        {
            if (IsRunning)
                return false;

            List<string> errors = Validate(suite, selectedScenario);

            if (errors.Count > 0)
            {
                _lastError = string.Join("\n", errors);
                _status = "Validation failed";
                Changed?.Invoke();
                return false;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                _status = "Benchmark cancelled before scene loading.";
                Changed?.Invoke();
                return false;
            }

            AssetDatabase.SaveAssets();

            try
            {
                _suite = suite;
                _catalog = AssetCatalogService.GetOrCreate();
                BuildWorkItems(suite, selectedScenario);
                Records.Clear();
                PrimaryHashes.Clear();
                _campaign = CreateCampaign(suite);
                _session = EditorCampaignSession.Begin(InterruptedSessionKey);
            }
            catch (Exception exception)
            {
                Exception cleanupError = DisposeSession();
                _lastError = exception.ToString();
                if (cleanupError != null)
                    _lastError += $"\n\nCleanup failed: {cleanupError}";
                _status = "Benchmark could not start.";
                Changed?.Invoke();
                return false;
            }

            _workIndex = 0;
            _loadedScenarioPath = string.Empty;
            _currentBlockKey = string.Empty;
            _cancelRequested = false;
            _lastError = string.Empty;
            _lastOutputDirectory = string.Empty;
            _state = RunnerState.LoadScene;
            _status = $"Starting {WorkItems.Count:N0} measured runs";
            Changed?.Invoke();
            return true;
        }

        public static void RequestStop()
        {
            if (!IsRunning)
                return;

            _cancelRequested = true;
            _status = "Stopping after the current synchronous run";
            Changed?.Invoke();
        }

        public static List<string> Validate(GenerationBenchmarkSuite suite, int selectedScenario = -1)
        {
            List<string> errors = new();

            if (!suite)
            {
                errors.Add("Select or create a Benchmark Suite.");
                return errors;
            }

            if (CompilationPipeline.codeOptimization != CodeOptimization.Release)
                errors.Add("Unity Code Optimization must be set to Release before performance measurement.");
            if (Profiler.enabled)
                errors.Add("Disable the Unity Profiler before measuring runtime.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                errors.Add("Wait until Unity has finished compiling and importing assets.");
            if ((suite.CacheConditions & (BenchmarkCacheCondition.Cold | BenchmarkCacheCondition.Warm)) == 0)
                errors.Add("Select at least one cache condition.");
            if ((suite.Measurements & (BenchmarkMeasurementKind.Primary | BenchmarkMeasurementKind.Diagnostic)) == 0)
                errors.Add("Select at least one measurement kind.");
            if ((suite.Measurements & BenchmarkMeasurementKind.Diagnostic) != 0 &&
                (suite.Measurements & BenchmarkMeasurementKind.Primary) == 0)
            {
                errors.Add("Phase breakdown requires Runtime so the instrumented plan can be checked against the authoritative run.");
            }

            IReadOnlyList<IBenchmarkAreaResolver> resolvers = BenchmarkAreaResolverRegistry.CreateResolvers();
            IEnumerable<(GenerationBenchmarkScenario Scenario, int Index)> scenarios = suite.Scenarios
                .Select((scenario, index) => (scenario, index))
                .Where(item => item.scenario != null && item.scenario.Enabled)
                .Where(item => selectedScenario < 0 || item.index == selectedScenario);
            int scenarioCount = 0;

            foreach ((GenerationBenchmarkScenario scenario, int index) in scenarios)
            {
                scenarioCount++;
                string label = $"Scenario {index + 1} ({scenario.DisplayName})";

                if (!scenario.Scene)
                    errors.Add($"{label}: Scene is missing.");
                if (!scenario.GenerationPreset)
                {
                    errors.Add($"{label}: Generation Preset is missing.");
                }
                else
                {
                    GenerationPresetSettings settings = scenario.GenerationPreset.Settings;
                    if (!settings.AssetPool)
                        errors.Add($"{label}: Generation Preset has no Asset Pool.");
                    if (!settings.StylePreset)
                        errors.Add($"{label}: Generation Preset has no Generation Style.");
                    if (settings.PlacementTargets == PlacementTarget.None)
                        errors.Add($"{label}: Generation Preset has no Placement Target.");
                    if (settings.RelativePlacementSource == RelativePlacementSource.SelectedObjects)
                        errors.Add($"{label}: Selected Objects cannot be restored across automatic scene switches. Use scene-layer anchors instead.");
                }
                if (scenario.ObjectCounts == null || !scenario.ObjectCounts.Any(count => count > 0))
                    errors.Add($"{label}: Add at least one positive Object Count.");
                if (!resolvers.Any(resolver => resolver.ProviderId == scenario.AreaProviderId))
                    errors.Add($"{label}: Target provider '{scenario.AreaProviderId}' is not installed.");
            }

            if (scenarioCount == 0)
                errors.Add(selectedScenario >= 0 ? "The selected scenario is disabled or missing." : "Enable at least one benchmark scenario.");

            int requiredSeeds = Math.Max(suite.ColdSeedCount, suite.WarmSeedCount);
            if (suite.Seeds == null || suite.Seeds.Count < requiredSeeds)
                errors.Add($"The suite needs at least {requiredSeeds} deterministic seeds.");
            else if (suite.Seeds.Take(requiredSeeds).Distinct().Count() != requiredSeeds)
                errors.Add($"The first {requiredSeeds} deterministic seeds must be unique.");

            return errors;
        }

        private static void Update()
        {
            if (_state == RunnerState.Idle)
                return;

            try
            {
                if (_cancelRequested && _state != RunnerState.RestoreScene)
                {
                    _status = "Benchmark cancelled";
                    _state = RunnerState.RestoreScene;
                }

                switch (_state)
                {
                    case RunnerState.LoadScene:
                        LoadCurrentScene();
                        break;
                    case RunnerState.SettleScene:
                        SettleCurrentScene();
                        break;
                    case RunnerState.Run:
                        RunCurrentItem();
                        break;
                    case RunnerState.RestoreScene:
                        Finish();
                        break;
                }
            }
            catch (Exception exception)
            {
                _lastError = exception.ToString();
                _status = "Benchmark stopped because of an unexpected error.";
                _state = RunnerState.RestoreScene;
            }

            Changed?.Invoke();
        }

        private static void LoadCurrentScene()
        {
            if (_workIndex >= WorkItems.Count)
            {
                _status = "Exporting results";
                _state = RunnerState.RestoreScene;
                return;
            }

            GenerationBenchmarkWorkItem item = WorkItems[_workIndex];
            string scenePath = AssetDatabase.GetAssetPath(item.Scenario.Scene);

            if (string.Equals(scenePath, _loadedScenarioPath, StringComparison.Ordinal))
            {
                _state = RunnerState.Run;
                return;
            }

            DestroyTemporaryParent();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            _loadedScenarioPath = scenePath;
            _settleFramesRemaining = _suite.SettleFrames;
            _currentBlockKey = string.Empty;
            AreaContext.BeginScene();
            _status = $"Loading {item.Scenario.DisplayName}";
            _state = RunnerState.SettleScene;
        }

        private static void SettleCurrentScene()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (_settleFramesRemaining-- > 0)
                return;

            GenerationBenchmarkScenario scenario = WorkItems[_workIndex].Scenario;
            Scene scene = SceneManager.GetActiveScene();
            AreaContext.Resolve(
                scene,
                scenario.AreaProviderId,
                scenario.TargetId,
                scenario.DisplayName,
                status => _status = status);

            _generatedParent = new GameObject("__GenixBenchmarkGenerated")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _state = RunnerState.Run;
        }

        private static void RunCurrentItem()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _status = "Waiting for Unity compilation or asset import";
                return;
            }

            if (_workIndex >= WorkItems.Count)
            {
                _state = RunnerState.RestoreScene;
                return;
            }

            GenerationBenchmarkWorkItem item = WorkItems[_workIndex];

            if (!string.Equals(AssetDatabase.GetAssetPath(item.Scenario.Scene), _loadedScenarioPath, StringComparison.Ordinal))
            {
                _state = RunnerState.LoadScene;
                return;
            }

            PrepareCacheState(item);
            GenerationRequest request = CreateRequest(item);

            if (!GenerationPreflight.IsValid(request, out string preflightError))
                throw new InvalidOperationException(preflightError);

            RuntimeSnapshot before = RuntimeSnapshot.Capture();
            GenerationBenchmarkExecutionResult execution = GenerationBenchmarkExecutor.Execute(
                request,
                _catalog,
                _generatedParent.transform,
                item.Measurement);
            RuntimeSnapshot after = RuntimeSnapshot.Capture();
            GenerationBenchmarkRunRecord record = GenerationBenchmarkRunRecord.Create(
                item,
                execution,
                _loadedScenarioPath,
                AreaContext.TargetId,
                before,
                after);

            if (item.Measurement == BenchmarkMeasurementKind.Primary)
            {
                PrimaryHashes[item.ResultKey] = record.resultHash;
            }
            else
            {
                record.hasPrimaryReference = PrimaryHashes.TryGetValue(item.ResultKey, out string primaryHash) &&
                                             !string.IsNullOrEmpty(primaryHash);
                record.resultMatchesPrimary = record.hasPrimaryReference &&
                                              string.Equals(primaryHash, record.resultHash, StringComparison.Ordinal);
            }

            Records.Add(record);
            _campaign.runs.Add(record);
            _workIndex++;
            _status = $"{Records.Count:N0}/{WorkItems.Count:N0} runs, {item.Scenario.DisplayName}, " +
                      $"{item.CacheCondition}, {BenchmarkMeasurementDisplay.Name(item.Measurement)}";

            if (_workIndex >= WorkItems.Count)
                _state = RunnerState.RestoreScene;
            else if (WorkItems[_workIndex].ScenarioIndex != item.ScenarioIndex)
                _state = RunnerState.LoadScene;
        }

        private static void PrepareCacheState(GenerationBenchmarkWorkItem item)
        {
            bool blockChanged = !string.Equals(_currentBlockKey, item.BlockKey, StringComparison.Ordinal);

            if (blockChanged)
            {
                ClearAllCaches();
                _currentBlockKey = item.BlockKey;

                for (int warmup = 0; warmup < _suite.WarmupRuns; warmup++)
                {
                    int seed = _suite.Seeds[warmup % _suite.Seeds.Count];
                    GenerationBenchmarkWorkItem warmupItem = new()
                    {
                        ScenarioIndex = item.ScenarioIndex,
                        Scenario = item.Scenario,
                        CacheCondition = item.CacheCondition,
                        Measurement = item.Measurement,
                        ObjectCount = item.ObjectCount,
                        Seed = seed,
                        Repetition = -1
                    };
                    GenerationBenchmarkExecutor.Execute(
                        CreateRequest(warmupItem),
                        _catalog,
                        _generatedParent.transform,
                        item.Measurement);
                }
            }

            if (item.CacheCondition == BenchmarkCacheCondition.Cold)
            {
                // Warm code paths above, then restore a cold data-cache state for every sample.
                ClearAllCaches();
                return;
            }

            // Repetitions estimate timing noise, not fixed-seed candidate-cache speedups.
            PlacementSolver.ClearCandidateCache();
        }

        private static void ClearAllCaches()
        {
            if (AreaContext.AreaSource is IAreaCacheControl cacheControl)
                cacheControl.ClearCache();

            PlacementSolver.ClearCandidateCache();
            PlacementSolver.ClearSceneObjectCache();
        }

        private static GenerationRequest CreateRequest(GenerationBenchmarkWorkItem item)
        {
            GenerationBenchmarkScenario scenario = item.Scenario;
            GenerationPresetSettings settings = scenario.GenerationPreset.Settings;
            LayerMask combinedLayers = settings.FloorSurfaceLayers |
                                       settings.WallSurfaceLayers |
                                       settings.CeilingSurfaceLayers;
            AreaBuildSettings areaSettings = new(
                settings.AreaDecompositionMode,
                combinedLayers,
                settings.FloorSurfaceLayers,
                settings.WallSurfaceLayers,
                settings.CeilingSurfaceLayers,
                floorNormalYThreshold: Mathf.Cos(settings.FloorSurfaceAngleDegrees * Mathf.Deg2Rad),
                ceilingNormalYThreshold: -Mathf.Cos(settings.CeilingSurfaceAngleDegrees * Mathf.Deg2Rad),
                surfaceDiscoveryMode: settings.SurfaceDiscoveryMode);
            RelativePlacementSettings relative = new(
                settings.RelativePlacementSource,
                settings.RelativeRadius,
                settings.RelativeSceneLayers,
                Array.Empty<Transform>());

            return new GenerationRequest(
                AreaContext.AreaSource,
                settings.AssetPool,
                item.ObjectCount,
                settings.PlacementTargets,
                settings.TargetDistributionMode,
                settings.TargetDistributionWeights,
                settings.StylePreset.Settings,
                areaSettings,
                relative,
                settings.StylePreset.name,
                useFixedSeed: true,
                randomSeed: item.Seed,
                bestEffort: settings.BestEffort,
                detailedDiagnostics: false,
                supportDistribution: settings.SupportDistribution);
        }

        private static void BuildWorkItems(GenerationBenchmarkSuite suite, int selectedScenario)
        {
            WorkItems.Clear();
            BenchmarkMeasurementKind[] measurements = Values(suite.Measurements).ToArray();
            BenchmarkCacheCondition[] caches = Values(suite.CacheConditions).ToArray();

            for (int scenarioIndex = 0; scenarioIndex < suite.Scenarios.Count; scenarioIndex++)
            {
                GenerationBenchmarkScenario scenario = suite.Scenarios[scenarioIndex];

                if (scenario == null || !scenario.Enabled || selectedScenario >= 0 && scenarioIndex != selectedScenario)
                    continue;

                foreach (int count in scenario.ObjectCounts.Where(value => value > 0).Distinct().OrderBy(value => value))
                {
                    foreach (BenchmarkCacheCondition cache in caches)
                    {
                        int seedCount = cache == BenchmarkCacheCondition.Cold ? suite.ColdSeedCount : suite.WarmSeedCount;

                        foreach (BenchmarkMeasurementKind measurement in measurements)
                        {
                            for (int seedIndex = 0; seedIndex < seedCount; seedIndex++)
                            {
                                for (int repetition = 0; repetition < suite.Repetitions; repetition++)
                                {
                                    WorkItems.Add(new GenerationBenchmarkWorkItem
                                    {
                                        ScenarioIndex = scenarioIndex,
                                        Scenario = scenario,
                                        CacheCondition = cache,
                                        Measurement = measurement,
                                        ObjectCount = count,
                                        Seed = suite.Seeds[seedIndex],
                                        Repetition = repetition
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        private static IEnumerable<BenchmarkMeasurementKind> Values(BenchmarkMeasurementKind values)
        {
            if ((values & BenchmarkMeasurementKind.Primary) != 0)
                yield return BenchmarkMeasurementKind.Primary;
            if ((values & BenchmarkMeasurementKind.Diagnostic) != 0)
                yield return BenchmarkMeasurementKind.Diagnostic;
        }

        private static IEnumerable<BenchmarkCacheCondition> Values(BenchmarkCacheCondition values)
        {
            if ((values & BenchmarkCacheCondition.Cold) != 0)
                yield return BenchmarkCacheCondition.Cold;
            if ((values & BenchmarkCacheCondition.Warm) != 0)
                yield return BenchmarkCacheCondition.Warm;
        }

        private static GenerationBenchmarkCampaignResult CreateCampaign(GenerationBenchmarkSuite suite)
        {
            string suitePath = AssetDatabase.GetAssetPath(suite);
            return new GenerationBenchmarkCampaignResult
            {
                suiteName = suite.name,
                suiteAssetPath = suitePath,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                systemMemoryMb = SystemInfo.systemMemorySize,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                suiteDependencyHash = string.IsNullOrWhiteSpace(suitePath)
                    ? string.Empty
                    : AssetDatabase.GetAssetDependencyHash(suitePath).ToString()
            };
        }

        private static void Finish()
        {
            DestroyTemporaryParent();

            try
            {
                if (_campaign != null && _campaign.runs.Count > 0)
                {
                    _lastOutputDirectory = GenerationBenchmarkExporter.Export(_campaign);
                    _status = _cancelRequested
                        ? $"Cancelled after {Records.Count:N0} runs. Partial results exported."
                        : $"Completed {Records.Count:N0} runs.";
                }
                else if (string.IsNullOrEmpty(_lastError))
                {
                    _status = "No measured runs were completed.";
                }

            }
            catch (Exception exception)
            {
                _lastError = exception.ToString();
                _status = "Benchmark cleanup or result export failed.";
            }
            finally
            {
                Exception cleanupError = DisposeSession();
                if (cleanupError != null)
                {
                    _lastError = string.IsNullOrWhiteSpace(_lastError)
                        ? cleanupError.ToString()
                        : $"{_lastError}\n\n{cleanupError}";
                    _status = "Benchmark cleanup or result export failed.";
                }

                _state = RunnerState.Idle;
                _suite = null;
                _catalog = null;
                AreaContext.BeginScene();
                _campaign = null;
                _currentBlockKey = string.Empty;
            }

            Changed?.Invoke();
        }

        private static Exception DisposeSession()
        {
            try
            {
                _session?.Dispose();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
            finally
            {
                _session = null;
            }
        }

        private static void DestroyTemporaryParent()
        {
            if (_generatedParent)
                Object.DestroyImmediate(_generatedParent);

            _generatedParent = null;
        }
    }
}
