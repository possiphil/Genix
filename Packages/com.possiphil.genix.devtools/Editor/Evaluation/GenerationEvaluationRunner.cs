using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Editor.Assets;
using Genix.Editor.Common;
using Genix.Editor.Generation;
using Genix.Editor.Layouts;
using Genix.Editor.TargetAreas;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Evaluation
{
    internal sealed class GenerationEvaluationWorkItem
    {
        public int ScenarioIndex { get; set; }
        public int RunIndex { get; set; }
        public int Seed { get; set; }
        public GenerationEvaluationScenario Scenario { get; set; }
    }

    /// <summary>Runs reproducible quality evaluations through the production generation workflow.</summary>
    [InitializeOnLoad]
    internal static class GenerationEvaluationRunner
    {
        private enum RunnerState
        {
            Idle,
            LoadScene,
            SettleScene,
            Run,
            RestoreScene
        }

        private const string InterruptedSessionKey = "Genix.Evaluations.Running";
        private const string RunAllScope = "RunAll";
        private const string SelectedScenarioScope = "SelectedScenario";
        private static readonly List<GenerationEvaluationWorkItem> WorkItems = new();
        private static readonly List<GenerationEvaluationRunRecord> Records = new();

        private static RunnerState _state;
        private static GenerationEvaluationSuite _suite;
        private static GenerationEvaluationCampaignResult _campaign;
        private static int _workIndex;
        private static int _settleFramesRemaining;
        private static string _loadedScenePath = string.Empty;
        private static readonly EditorCampaignAreaContext AreaContext = new();
        private static bool _cancelRequested;
        private static EditorCampaignSession _session;
        private static string _status = "Ready";
        private static string _lastError = string.Empty;
        private static string _lastOutputDirectory = string.Empty;
        private static GenerationEvaluationReport _lastReport;
        private static int _lastExpectedRunCount;
        private static bool _lastCampaignCompleted;
        private static bool _lastCampaignCancelled;
        private static string _lastRunScope = "Unknown";

        static GenerationEvaluationRunner()
        {
            EditorApplication.update += Update;

            if (EditorCampaignSession.ConsumeInterruptedMarker(InterruptedSessionKey))
            {
                _status = "The previous evaluation was interrupted by a domain reload or editor restart.";
            }
        }

        public static event Action Changed;
        public static bool IsRunning => _state != RunnerState.Idle;
        public static int CompletedRuns => Records.Count;
        public static int TotalRuns => WorkItems.Count;
        public static string Status => _status;
        public static string LastError => _lastError;
        public static string LastOutputDirectory => _lastOutputDirectory;
        public static GenerationEvaluationReport LastReport => _lastReport;
        public static int LastExpectedRunCount => _lastExpectedRunCount;
        public static bool LastCampaignCompleted => _lastCampaignCompleted;
        public static bool LastCampaignCancelled => _lastCampaignCancelled;
        public static string LastRunScope => _lastRunScope;
        public static IReadOnlyList<GenerationEvaluationRunRecord> RunRecords => Records;
        public static double ElapsedSeconds => IsRunning ? _session?.ElapsedSeconds ?? 0d : 0d;
        public static double EstimatedRemainingSeconds => Records.Count == 0
            ? 0d
            : Math.Max(0d, ElapsedSeconds / Records.Count * (WorkItems.Count - Records.Count));

        public static bool Start(GenerationEvaluationSuite suite, int selectedScenario = -1)
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
                _status = "Evaluation cancelled before scene loading.";
                Changed?.Invoke();
                return false;
            }

            AssetDatabase.SaveAssets();

            try
            {
                _suite = suite;
                BuildWorkItems(suite, selectedScenario);
                Records.Clear();
                _campaign = CreateCampaign(suite, selectedScenario, WorkItems.Count);
                _lastExpectedRunCount = WorkItems.Count;
                _lastCampaignCompleted = false;
                _lastCampaignCancelled = false;
                _lastRunScope = _campaign.runScope;
                _session = EditorCampaignSession.Begin(InterruptedSessionKey);
            }
            catch (Exception exception)
            {
                Exception cleanupError = DisposeSession();
                _lastError = exception.ToString();
                if (cleanupError != null)
                    _lastError += $"\n\nCleanup failed: {cleanupError}";
                _status = "Evaluation could not start.";
                Changed?.Invoke();
                return false;
            }

            _workIndex = 0;
            _loadedScenePath = string.Empty;
            AreaContext.BeginScene();
            _cancelRequested = false;
            _lastError = string.Empty;
            _lastOutputDirectory = string.Empty;
            _lastReport = null;
            _state = RunnerState.LoadScene;
            _status = $"Starting {WorkItems.Count:N0} evaluation runs";
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

        public static List<string> Validate(GenerationEvaluationSuite suite, int selectedScenario = -1)
        {
            List<string> errors = new();
            if (!suite)
            {
                errors.Add("Select or create an Evaluation Suite.");
                return errors;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                errors.Add("Wait until Unity has finished compiling and importing assets.");

            IReadOnlyList<IBenchmarkAreaResolver> resolvers = BenchmarkAreaResolverRegistry.CreateResolvers();
            int scenarioCount = 0;

            for (int index = 0; index < suite.Scenarios.Count; index++)
            {
                GenerationEvaluationScenario scenario = suite.Scenarios[index];
                if (scenario == null || !scenario.Enabled || selectedScenario >= 0 && index != selectedScenario)
                    continue;

                scenarioCount++;
                string label = $"Scenario {index + 1} ({scenario.DisplayName})";
                if (!scenario.Scene)
                    errors.Add($"{label}: Scene is missing.");
                if (!scenario.GenerationPreset)
                    errors.Add($"{label}: Generation Preset is missing.");
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
                        errors.Add($"{label}: Selected Objects cannot be restored across automatic scene switches.");
                }

                if (!resolvers.Any(resolver => resolver.ProviderId == scenario.AreaProviderId))
                    errors.Add($"{label}: Target provider '{scenario.AreaProviderId}' is not installed.");
            }

            if (scenarioCount == 0)
                errors.Add(selectedScenario >= 0
                    ? "The selected scenario is disabled or missing."
                    : "Enable at least one evaluation scenario.");

            if (suite.Seeds == null || suite.Seeds.Count < suite.RunsPerScenario)
                errors.Add($"The suite needs at least {suite.RunsPerScenario} deterministic seeds.");
            else if (suite.Seeds.Take(suite.RunsPerScenario).Distinct().Count() != suite.RunsPerScenario)
                errors.Add($"The first {suite.RunsPerScenario} deterministic seeds must be unique.");

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
                    _status = "Evaluation cancelled";
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
                _status = "Evaluation stopped because of an unexpected error.";
                _state = RunnerState.RestoreScene;
            }

            Changed?.Invoke();
        }

        private static void LoadCurrentScene()
        {
            if (_workIndex >= WorkItems.Count)
            {
                _state = RunnerState.RestoreScene;
                return;
            }

            GenerationEvaluationScenario scenario = WorkItems[_workIndex].Scenario;
            string scenePath = AssetDatabase.GetAssetPath(scenario.Scene);
            if (string.Equals(scenePath, _loadedScenePath, StringComparison.Ordinal))
            {
                _settleFramesRemaining = 0;
                AreaContext.ClearTarget();
                _state = RunnerState.SettleScene;
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            _loadedScenePath = scenePath;
            _settleFramesRemaining = _suite.SettleFrames;
            AreaContext.BeginScene();
            _status = $"Loading {scenario.DisplayName}";
            _state = RunnerState.SettleScene;
        }

        private static void SettleCurrentScene()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (_settleFramesRemaining-- > 0)
                return;

            GenerationEvaluationScenario scenario = WorkItems[_workIndex].Scenario;
            Scene scene = SceneManager.GetActiveScene();
            AreaContext.Resolve(
                scene,
                scenario.AreaProviderId,
                scenario.TargetId,
                scenario.DisplayName,
                status => _status = status);

            _state = RunnerState.Run;
        }

        private static void RunCurrentItem()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _status = "Waiting for Unity compilation or asset import";
                return;
            }

            GenerationEvaluationWorkItem item = WorkItems[_workIndex];
            if (!string.Equals(AssetDatabase.GetAssetPath(item.Scenario.Scene), _loadedScenePath, StringComparison.Ordinal))
            {
                _state = RunnerState.LoadScene;
                return;
            }

            GenerationRequest request = CreateRequest(item);
            bool succeeded = GenerationWorkflow.GenerateForEvaluation(request, out GenerationDiagnostics diagnostics);
            GenerationEvaluationRunRecord record = CreateRecord(item, request, diagnostics, succeeded);
            record.checks = GenerationResultEvaluator.Evaluate(item.Scenario, request, diagnostics);

            if (item.Scenario.SaveLayouts && record.placedCount > 0)
                CaptureLayout(item, request, record);

            Records.Add(record);
            _campaign.runs.Add(record);
            _workIndex++;
            _status = $"{Records.Count:N0}/{WorkItems.Count:N0} runs, {item.Scenario.DisplayName}, seed {item.Seed}";

            if (_workIndex >= WorkItems.Count)
            {
                SceneGenerationService.Clear(AreaContext.AreaSource);
                _state = RunnerState.RestoreScene;
            }
            else if (WorkItems[_workIndex].ScenarioIndex != item.ScenarioIndex)
            {
                SceneGenerationService.Clear(AreaContext.AreaSource);
                _state = RunnerState.LoadScene;
            }
        }

        private static GenerationRequest CreateRequest(GenerationEvaluationWorkItem item)
        {
            GenerationPresetSettings settings = item.Scenario.GenerationPreset.Settings;
            LayerMask combinedLayers = settings.FloorSurfaceLayers | settings.WallSurfaceLayers | settings.CeilingSurfaceLayers;
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
                settings.ObjectCount,
                settings.PlacementTargets,
                settings.TargetDistributionMode,
                settings.TargetDistributionWeights,
                settings.StylePreset.Settings,
                areaSettings,
                relative,
                settings.StylePreset.name,
                true,
                item.Seed,
                settings.BestEffort,
                false,
                settings.SupportDistribution);
        }

        private static GenerationEvaluationRunRecord CreateRecord(
            GenerationEvaluationWorkItem item,
            GenerationRequest request,
            GenerationDiagnostics diagnostics,
            bool succeeded)
        {
            GenerationEvaluationRunRecord record = new()
            {
                scenario = item.Scenario.DisplayName,
                scenarioKind = item.Scenario.Kind.ToString(),
                scene = _loadedScenePath,
                areaProviderId = item.Scenario.AreaProviderId,
                targetId = AreaContext.TargetId,
                preset = item.Scenario.GenerationPreset.name,
                seed = item.Seed,
                requestedCount = request.ObjectCount,
                placedCount = diagnostics?.PlacedObjectCount ?? CountGeneratedObjects(request.AreaSource),
                generationSucceeded = succeeded,
                testedCandidates = diagnostics?.TestedCandidateCount ?? 0,
                rejectedCandidates = diagnostics?.RejectedCandidateCount ?? 0,
                topRejection = diagnostics?.TopRejectionReason ?? string.Empty,
                stopReason = diagnostics?.StopReason ?? string.Empty,
                minimumPlacementDistance = CalculateMinimumPlacementDistance(diagnostics),
                eligibleAssetNames = CollectEligibleAssetNames(request),
                expectedSupportNames = CollectExpectedSupportNames(request),
                supportCounts = CollectSupportCounts(request.AreaSource)
            };

            if (diagnostics != null)
            {
                record.assetCounts = diagnostics.Placements
                    .GroupBy(placement => placement.AssetId)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new GenerationEvaluationCountRecord { name = group.Key, count = group.Count() })
                    .ToList();
                record.rejectionCounts = diagnostics.CandidateRejectionCounts
                    .OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                    .Select(entry => new GenerationEvaluationCountRecord
                    {
                        name = entry.Key.ToString(),
                        count = entry.Value
                    })
                    .ToList();
            }

            return record;
        }

        internal static List<string> CollectEligibleAssetNames(
            GenerationRequest request,
            AssetCatalog catalog = null)
        {
            if (!request.AssetPool ||
                !GenerationAssetFilter.TryResolve(
                    request,
                    catalog ? catalog : AssetCatalogService.GetOrCreate(),
                    out List<AssetDefinition> assets,
                    out _))
                return new List<string>();

            return assets
                .Select(asset => asset.AssetName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> CollectExpectedSupportNames(GenerationRequest request)
        {
            IEnumerable<string> configured = request.SupportDistribution?.Rules
                .Where(rule => rule?.IsConfigured == true && rule.SupportTag)
                .Select(rule => rule.SupportTag.DisplayName) ?? Enumerable.Empty<string>();
            Scene activeScene = SceneManager.GetActiveScene();
            IEnumerable<string> authored = UnityEngine.Object
                .FindObjectsByType<PlacementSurfaceDescriptor>(FindObjectsInactive.Include)
                .Where(descriptor => descriptor && descriptor.gameObject.scene == activeScene)
                .SelectMany(descriptor => descriptor.SurfaceTags)
                .Where(tag => tag && tag.SupportsSurfaces)
                .Select(tag => tag.DisplayName);

            return configured.Concat(authored)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<GenerationEvaluationCountRecord> CollectSupportCounts(IAreaSource areaSource)
        {
            if (!GeneratedHierarchy.TryGet(areaSource, out Transform parent))
                return new List<GenerationEvaluationCountRecord>();

            List<string> labels = new();
            foreach (Transform child in parent)
            {
                GeneratedObjectMetadata metadata = child.GetComponent<GeneratedObjectMetadata>();
                if (!metadata)
                    continue;

                string[] semanticLabels = metadata.SupportSurface
                    ? metadata.SupportSurface.SurfaceTags
                        .Where(tag => tag && tag.SupportsSurfaces)
                        .Select(tag => tag.DisplayName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                    : Array.Empty<string>();
                if (semanticLabels.Length > 0)
                    labels.AddRange(semanticLabels);
                else
                    labels.Add(metadata.PlacementTarget.ToString());
            }

            return labels
                .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new GenerationEvaluationCountRecord
                {
                    name = group.Key,
                    count = group.Count()
                })
                .ToList();
        }

        private static float CalculateMinimumPlacementDistance(GenerationDiagnostics diagnostics)
        {
            if (diagnostics == null || diagnostics.Placements.Count < 2)
                return 0f;

            float minimumSquared = float.PositiveInfinity;
            for (int i = 0; i < diagnostics.Placements.Count - 1; i++)
            for (int j = i + 1; j < diagnostics.Placements.Count; j++)
            {
                float distanceSquared = (diagnostics.Placements[i].Position - diagnostics.Placements[j].Position).sqrMagnitude;
                if (distanceSquared < minimumSquared)
                    minimumSquared = distanceSquared;
            }

            return float.IsPositiveInfinity(minimumSquared) ? 0f : Mathf.Sqrt(minimumSquared);
        }

        private static int CountGeneratedObjects(IAreaSource areaSource) =>
            GeneratedHierarchy.TryGet(areaSource, out Transform parent) ? parent.childCount : 0;

        private static void CaptureLayout(
            GenerationEvaluationWorkItem item,
            GenerationRequest request,
            GenerationEvaluationRunRecord record)
        {
            string name = $"Eval {item.Scenario.DisplayName} Run {item.RunIndex + 1:00} Seed {item.Seed}";
            string notes = $"Locked evaluation observation. Suite: {_suite.name}; scenario: {item.Scenario.DisplayName}; " +
                           $"run: {item.RunIndex + 1}/{_suite.RunsPerScenario}; seed: {item.Seed}.";
            if (!LayoutCaptureService.Save(
                    request.AreaSource,
                    request.PlacementTargets,
                    request.TargetDistributionMode,
                    request.TargetDistributionWeights,
                    request.AssetPool,
                    request.StyleName,
                    out SavedLayout layout,
                    out string error,
                    name,
                    notes,
                    lockLayout: true))
            {
                record.checks.Add(new GenerationEvaluationCheckRecord
                {
                    name = "Layout Capture",
                    status = EvaluationCheckStatus.Failed,
                    violations = 1,
                    message = error
                });
                return;
            }

            record.layoutAssetPath = AssetDatabase.GetAssetPath(layout);
            record.layoutGuid = AssetDatabase.AssetPathToGUID(record.layoutAssetPath);
        }

        private static void BuildWorkItems(GenerationEvaluationSuite suite, int selectedScenario)
        {
            WorkItems.Clear();
            for (int scenarioIndex = 0; scenarioIndex < suite.Scenarios.Count; scenarioIndex++)
            {
                GenerationEvaluationScenario scenario = suite.Scenarios[scenarioIndex];
                if (scenario == null || !scenario.Enabled ||
                    selectedScenario >= 0 && scenarioIndex != selectedScenario)
                    continue;

                for (int run = 0; run < suite.RunsPerScenario; run++)
                {
                    WorkItems.Add(new GenerationEvaluationWorkItem
                    {
                        ScenarioIndex = scenarioIndex,
                        RunIndex = run,
                        Seed = suite.Seeds[run],
                        Scenario = scenario
                    });
                }
            }
        }

        private static GenerationEvaluationCampaignResult CreateCampaign(
            GenerationEvaluationSuite suite,
            int selectedScenario,
            int expectedRunCount)
        {
            string suitePath = AssetDatabase.GetAssetPath(suite);
            return new GenerationEvaluationCampaignResult
            {
                suiteName = suite.name,
                suiteAssetPath = suitePath,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                operatingSystem = SystemInfo.operatingSystem,
                suiteDependencyHash = string.IsNullOrWhiteSpace(suitePath)
                    ? string.Empty
                    : AssetDatabase.GetAssetDependencyHash(suitePath).ToString(),
                runScope = selectedScenario >= 0 ? SelectedScenarioScope : RunAllScope,
                selectedScenarioIndex = selectedScenario,
                expectedRunCount = Mathf.Max(0, expectedRunCount),
                campaignCompleted = false,
                campaignCancelled = false
            };
        }

        private static void Finish()
        {
            try
            {
                FinalizeCampaignMetadata();

                if (_campaign != null && _campaign.runs.Count > 0)
                {
                    _lastReport = GenerationEvaluationReportService.Save(_campaign);
                    _lastOutputDirectory = GenerationEvaluationExporter.Export(_campaign);
                    _status = _campaign.campaignCompleted
                        ? $"Completed {Records.Count:N0} runs."
                        : _cancelRequested
                            ? $"Cancelled after {Records.Count:N0} runs. Partial report saved."
                            : $"Incomplete after {Records.Count:N0}/{_campaign.expectedRunCount:N0} runs. Partial report saved.";
                }
                else if (string.IsNullOrEmpty(_lastError))
                {
                    _status = "No evaluation runs were completed.";
                }

            }
            catch (Exception exception)
            {
                _lastError = exception.ToString();
                _status = "Evaluation cleanup or result export failed.";
            }
            finally
            {
                Exception cleanupError = DisposeSession();
                if (cleanupError != null)
                {
                    _lastError = string.IsNullOrWhiteSpace(_lastError)
                        ? cleanupError.ToString()
                        : $"{_lastError}\n\n{cleanupError}";
                    _status = "Evaluation cleanup or result export failed.";
                }

                _state = RunnerState.Idle;
                _suite = null;
                _campaign = null;
                AreaContext.BeginScene();
            }

            Changed?.Invoke();
        }

        private static void FinalizeCampaignMetadata()
        {
            if (_campaign == null)
                return;

            _campaign.campaignCancelled = _cancelRequested;
            _campaign.campaignCompleted = IsCampaignComplete(
                _campaign.runs.Count,
                _campaign.expectedRunCount,
                _cancelRequested,
                _lastError);
            _lastExpectedRunCount = _campaign.expectedRunCount;
            _lastCampaignCompleted = _campaign.campaignCompleted;
            _lastCampaignCancelled = _campaign.campaignCancelled;
            _lastRunScope = _campaign.runScope;
        }

        internal static bool IsCampaignComplete(
            int completedRuns,
            int expectedRuns,
            bool cancelled,
            string error) =>
            !cancelled &&
            string.IsNullOrWhiteSpace(error) &&
            expectedRuns > 0 &&
            completedRuns == expectedRuns;

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
    }
}
