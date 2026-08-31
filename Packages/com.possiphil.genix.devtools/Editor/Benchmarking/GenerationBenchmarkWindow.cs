using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Core;
using Genix.Editor.Drawers;
using Genix.Editor.Infrastructure;
using Genix.Editor.TargetAreas;
using Genix.Editor.DevTools;
using Genix.Editor.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Benchmarking
{
    /// <summary>Configures, runs, and inspects reproducible Genix performance benchmark campaigns.</summary>
    public sealed class GenerationBenchmarkWindow : EditorWindow
    {
        private const string SelectedSuiteKey = "Genix.Benchmarks.SelectedSuite";

        private static readonly string[] CacheConditionOptions =
        {
            "All cache states",
            "Cold cache",
            "Warm cache"
        };

        private static readonly string[] MeasurementOptions =
        {
            "Runtime only",
            "Phase breakdown"
        };

        private GenerationBenchmarkSuite _suite;
        private SerializedObject _serializedSuite;
        private int _selectedScenario;
        private Vector2 _scenarioScroll;
        private Vector2 _detailsScroll;
        private Vector2 _resultsScroll;
        private bool _showCampaignSettings;
        private string _validationMessage = string.Empty;
        private MessageType _validationMessageType = MessageType.None;

        private GenerationPreset[] _generationPresets = Array.Empty<GenerationPreset>();
        private string[] _generationPresetOptions = Array.Empty<string>();
        private GenerationBenchmarkSuite[] _benchmarkSuites = Array.Empty<GenerationBenchmarkSuite>();
        private string[] _benchmarkSuiteOptions = Array.Empty<string>();

        [MenuItem("Tools/Genix Developer/Benchmarks", false, 20)]
        public static void Open()
        {
            GenerationBenchmarkWindow window = GenixWindowDocking.Open<GenerationBenchmarkWindow>("Genix Benchmarks");
            window.minSize = new Vector2(760f, 520f);
        }

        private void OnEnable()
        {
            GenerationBenchmarkRunner.Changed += Repaint;
            EditorApplication.projectChanged += HandleProjectChanged;
            string suiteGuid = EditorPrefs.GetString(SelectedSuiteKey, string.Empty);
            string path = AssetDatabase.GUIDToAssetPath(suiteGuid);
            GenerationBenchmarkSuite rememberedSuite = AssetDatabase.LoadAssetAtPath<GenerationBenchmarkSuite>(path);
            RefreshSelectableAssets();
            SetSuite(rememberedSuite ? rememberedSuite : _benchmarkSuites.FirstOrDefault());
        }

        private void OnDisable()
        {
            GenerationBenchmarkRunner.Changed -= Repaint;
            EditorApplication.projectChanged -= HandleProjectChanged;
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4f);

            if (!_suite)
                return;

            EnsureSerializedSuite();
            DrawRunPanel();
            EditorGUILayout.Space(5f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawScenarioList();
                GUILayout.Space(6f);
                DrawScenarioDetails();
            }

            DrawResults();
            _serializedSuite.ApplyModifiedProperties();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawSuiteDropdown();

                if (GUILayout.Button(
                        new GUIContent("Create", "Create and select a new benchmark suite."),
                        EditorStyles.toolbarButton,
                        GUILayout.Width(56f)))
                {
                    CreateSuite();
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(GenerationBenchmarkRunner.LastOutputDirectory)))
                {
                    if (GUILayout.Button("Results", EditorStyles.toolbarButton, GUILayout.Width(62f)))
                        EditorUtility.RevealInFinder(GenerationBenchmarkRunner.LastOutputDirectory);
                }
            }
        }

        private void DrawSuiteDropdown()
        {
            if (_benchmarkSuites.Length == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Popup(
                        0,
                        new[] { "No Benchmark Suites" },
                        EditorStyles.toolbarPopup,
                        GUILayout.MinWidth(220f),
                        GUILayout.MaxWidth(420f));
                }
                return;
            }

            int selectedIndex = Array.IndexOf(_benchmarkSuites, _suite);
            if (selectedIndex < 0)
                selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup(
                selectedIndex,
                _benchmarkSuiteOptions,
                EditorStyles.toolbarPopup,
                GUILayout.MinWidth(220f),
                GUILayout.MaxWidth(420f));
            if (EditorGUI.EndChangeCheck())
                SetSuite(_benchmarkSuites[Mathf.Clamp(selectedIndex, 0, _benchmarkSuites.Length - 1)]);
        }

        private void DrawRunPanel()
        {
            if (DeveloperWindowUi.SectionHeader(
                    new GUIContent("Campaign", "Run and validate the configured benchmark campaign."),
                    new GUIContent("Validate", "Check the suite without running it."),
                    !GenerationBenchmarkRunner.IsRunning,
                    72f))
            {
                ValidateSuite();
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(GenerationBenchmarkRunner.IsRunning))
                    {
                        if (DeveloperWindowUi.CommandButton(new GUIContent("Run Full Suite"), 0, 2, 120f))
                            StartBenchmark(selectedOnly: false);

                        if (DeveloperWindowUi.CommandButton(new GUIContent("Run Selected"), 1, 2, 120f))
                            StartBenchmark(selectedOnly: true);
                    }

                    using (new EditorGUI.DisabledScope(!GenerationBenchmarkRunner.IsRunning))
                    {
                        if (GUILayout.Button("Stop", EditorStyles.miniButton, GUILayout.Height(28f), GUILayout.Width(70f)))
                            GenerationBenchmarkRunner.RequestStop();
                    }

                    GUILayout.FlexibleSpace();
                    DrawGlobalSettings();
                }

                float progress = GenerationBenchmarkRunner.TotalRuns > 0
                    ? GenerationBenchmarkRunner.CompletedRuns / (float)GenerationBenchmarkRunner.TotalRuns
                    : 0f;
                string progressLabel = GenerationBenchmarkRunner.IsRunning
                    ? $"{GenerationBenchmarkRunner.Status} | elapsed {FormatDuration(GenerationBenchmarkRunner.ElapsedSeconds)} | ETA {FormatDuration(GenerationBenchmarkRunner.EstimatedRemainingSeconds)}"
                    : GenerationBenchmarkRunner.Status;
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 19f), progress, progressLabel);

                if (!string.IsNullOrWhiteSpace(_validationMessage) &&
                    _validationMessageType is MessageType.Warning or MessageType.Error)
                    EditorGUILayout.HelpBox(_validationMessage, _validationMessageType);
                if (!string.IsNullOrWhiteSpace(GenerationBenchmarkRunner.LastError))
                    EditorGUILayout.HelpBox(GenerationBenchmarkRunner.LastError, MessageType.Error);

                _showCampaignSettings = EditorGUILayout.Foldout(
                    _showCampaignSettings,
                    "Campaign Settings",
                    true);

                if (_showCampaignSettings)
                    DrawCampaignSettings();
            }
        }

        private void DrawGlobalSettings()
        {
            SerializedProperty cache = _serializedSuite.FindProperty("cacheConditions");
            SerializedProperty measurements = _serializedSuite.FindProperty("measurements");
            using (new EditorGUI.DisabledScope(GenerationBenchmarkRunner.IsRunning))
            {
                BenchmarkCacheCondition cacheValue =
                    (BenchmarkCacheCondition)cache.intValue;
                int cacheIndex = cacheValue switch
                {
                    BenchmarkCacheCondition.Cold => 1,
                    BenchmarkCacheCondition.Warm => 2,
                    _ => 0
                };
                int changedCacheIndex = EditorGui.Popup(
                    cacheIndex,
                    CacheConditionOptions,
                    GUILayout.Width(135f));
                cache.intValue = changedCacheIndex switch
                {
                    1 => (int)BenchmarkCacheCondition.Cold,
                    2 => (int)BenchmarkCacheCondition.Warm,
                    _ => (int)(BenchmarkCacheCondition.Cold | BenchmarkCacheCondition.Warm)
                };

                BenchmarkMeasurementKind measurementValue =
                    (BenchmarkMeasurementKind)measurements.intValue;
                int measurementIndex = measurementValue == BenchmarkMeasurementKind.Primary
                    ? 0
                    : 1;
                int changedMeasurementIndex = EditorGui.Popup(
                    measurementIndex,
                    MeasurementOptions,
                    GUILayout.Width(215f));
                measurements.intValue = changedMeasurementIndex == 0
                    ? (int)BenchmarkMeasurementKind.Primary
                    : (int)(BenchmarkMeasurementKind.Primary | BenchmarkMeasurementKind.Diagnostic);
            }
        }

        private void DrawCampaignSettings()
        {
            using (new EditorGUI.DisabledScope(GenerationBenchmarkRunner.IsRunning))
            {
                EditorGUILayout.PropertyField(
                    _serializedSuite.FindProperty("coldSeedCount"),
                    new GUIContent("Cold Seeds", "Measured seeds per object count with all Genix data caches cleared before every sample."));
                EditorGUILayout.PropertyField(
                    _serializedSuite.FindProperty("warmSeedCount"),
                    new GUIContent("Warm Seeds", "Measured seeds per object count after an unmeasured cache warm-up."));
                EditorGUILayout.PropertyField(
                    _serializedSuite.FindProperty("warmupRuns"),
                    new GUIContent("Code Warm-ups", "Unmeasured executions per block. Cold caches are cleared again after these runs."));
                EditorGUILayout.PropertyField(
                    _serializedSuite.FindProperty("repetitions"),
                    new GUIContent("Repetitions", "Repeated measurements per seed. Keep at one when many independent seeds are used."));
                EditorGUILayout.PropertyField(
                    _serializedSuite.FindProperty("settleFrames"),
                    new GUIContent("Scene Settle Frames", "Editor frames excluded from timing after opening each scene."));
                EditorGUILayout.PropertyField(
                    _serializedSuite.FindProperty("seeds"),
                    new GUIContent("Deterministic Seeds"),
                    true);
            }
        }

        private void DrawScenarioList()
        {
            SerializedProperty scenarios = _serializedSuite.FindProperty("scenarios");

            float listWidth = DeveloperWindowUi.ResponsiveListWidth(position.width, 240f, 500f);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(listWidth)))
            {
                EditorGUILayout.LabelField("Scenarios", EditorStyles.boldLabel);
                using (DeveloperWindowUi.VerticalScrollViewScope scrollView =
                       DeveloperWindowUi.VerticalScrollView(_scenarioScroll, EditorStyles.helpBox))
                {
                    _scenarioScroll = scrollView.ScrollPosition;
                    for (int i = 0; i < scenarios.arraySize; i++)
                    {
                        SerializedProperty scenario = scenarios.GetArrayElementAtIndex(i);
                        string name = scenario.FindPropertyRelative("displayName").stringValue;
                        bool enabled = scenario.FindPropertyRelative("enabled").boolValue;
                        string label = enabled ? name : $"{name} (Excluded)";

                        if (DeveloperWindowUi.SelectableRow(
                                i == _selectedScenario,
                                new GUIContent(label, enabled ? "Included in the benchmark suite." : "Excluded from the benchmark suite."),
                                24f,
                                listWidth - 26f))
                            _selectedScenario = i;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(GenerationBenchmarkRunner.IsRunning))
                    {
                        if (GUILayout.Button("+"))
                        {
                            _serializedSuite.ApplyModifiedProperties();
                            Undo.RecordObject(_suite, "Added Benchmark Scenario");
                            _suite.AddScenario("Benchmark Scenario", null, GetDefaultGenerationPreset());
                            EditorUtility.SetDirty(_suite);
                            _selectedScenario = _suite.Scenarios.Count - 1;
                            EnsureSerializedSuite(force: true);
                        }

                        using (new EditorGUI.DisabledScope(scenarios.arraySize == 0))
                        {
                            if (GUILayout.Button("-"))
                            {
                                _serializedSuite.ApplyModifiedProperties();
                                Undo.RecordObject(_suite, "Removed Benchmark Scenario");
                                _suite.RemoveScenarioAt(_selectedScenario);
                                EditorUtility.SetDirty(_suite);
                                _selectedScenario = Mathf.Clamp(_selectedScenario, 0, Mathf.Max(0, _suite.Scenarios.Count - 1));
                                EnsureSerializedSuite(force: true);
                            }
                        }
                    }
                }
            }
        }

        private void DrawScenarioDetails()
        {
            SerializedProperty scenarios = _serializedSuite.FindProperty("scenarios");

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
                _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll, EditorStyles.helpBox);

                if (scenarios.arraySize == 0)
                {
                    EditorGUILayout.EndScrollView();
                    return;
                }

                _selectedScenario = Mathf.Clamp(_selectedScenario, 0, scenarios.arraySize - 1);
                SerializedProperty scenario = scenarios.GetArrayElementAtIndex(_selectedScenario);

                float previousLabelWidth = EditorGUIUtility.labelWidth;
                float detailsWidth = Mathf.Max(
                    390f,
                    position.width - DeveloperWindowUi.ResponsiveListWidth(position.width, 240f, 500f) - 30f);
                EditorGUIUtility.labelWidth = Mathf.Clamp(detailsWidth * 0.38f, 170f, 220f);

                try
                {
                    using (new EditorGUI.DisabledScope(GenerationBenchmarkRunner.IsRunning))
                        DrawScenarioConfiguration(scenario);
                }
                finally
                {
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawScenarioConfiguration(SerializedProperty scenario)
        {
            EditorGUILayout.PropertyField(
                scenario.FindPropertyRelative("enabled"),
                new GUIContent("Include in Suite", "Run this scenario as part of the full benchmark suite."));
            EditorGUILayout.PropertyField(scenario.FindPropertyRelative("displayName"), new GUIContent("Name"));
            EditorGUILayout.PropertyField(scenario.FindPropertyRelative("scene"), new GUIContent("Scene"));
            DrawTargetFields(scenario);

            EditorGUILayout.Space(7f);
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            DrawGenerationPreset(scenario.FindPropertyRelative("generationPreset"));
            EditorGUILayout.PropertyField(
                scenario.FindPropertyRelative("objectCounts"),
                new GUIContent(
                    "Object Counts",
                    "Requested counts measured as separate benchmark cases. These override the preset's Object Count."),
                true);
        }

        private void DrawGenerationPreset(SerializedProperty presetProperty)
        {
            GenerationPreset selectedPreset = presetProperty.objectReferenceValue as GenerationPreset;
            GenerationPreset newPreset = AssetDropdown.DrawGenerationPresetDropdownWithEditButton(
                new GUIContent(
                    "Generation Preset",
                    "Choose the complete designer configuration measured by this scenario."),
                _generationPresets,
                _generationPresetOptions,
                selectedPreset);
            if (newPreset != selectedPreset)
                presetProperty.objectReferenceValue = newPreset;
        }

        private void DrawTargetFields(SerializedProperty scenario)
        {
            SerializedProperty sceneProperty = scenario.FindPropertyRelative("scene");
            SerializedProperty providerProperty = scenario.FindPropertyRelative("areaProviderId");
            SerializedProperty targetProperty = scenario.FindPropertyRelative("targetId");
            IReadOnlyList<IBenchmarkAreaResolver> resolvers = BenchmarkAreaResolverRegistry.CreateResolvers();

            if (resolvers.Count == 0)
            {
                EditorGUILayout.HelpBox("No benchmark target provider is installed.", MessageType.Error);
                return;
            }

            int providerIndex = resolvers.ToList().FindIndex(resolver => resolver.ProviderId == providerProperty.stringValue);
            if (providerIndex < 0)
                providerIndex = 0;

            if (resolvers.Count > 1)
            {
                int newIndex = EditorGui.Popup(
                    new GUIContent("Area Provider", "Spatial integration used to resolve the target after each scene switch."),
                    providerIndex,
                    resolvers.Select(resolver => resolver.DisplayName).ToArray());
                providerIndex = Mathf.Clamp(newIndex, 0, resolvers.Count - 1);
            }

            providerProperty.stringValue = resolvers[providerIndex].ProviderId;

            SceneAsset sceneAsset = sceneProperty.objectReferenceValue as SceneAsset;
            string scenePath = sceneAsset ? AssetDatabase.GetAssetPath(sceneAsset) : string.Empty;
            Scene activeScene = SceneManager.GetActiveScene();

            if (sceneAsset && string.Equals(activeScene.path, scenePath, StringComparison.Ordinal))
            {
                IBenchmarkAreaResolver resolver = resolvers[providerIndex];
                IReadOnlyList<BenchmarkAreaTarget> targets = resolver?.FindTargets(activeScene) ?? Array.Empty<BenchmarkAreaTarget>();

                if (targets.Count > 0)
                {
                    int targetIndex = targets.ToList().FindIndex(target => target.Id == targetProperty.stringValue);
                    if (targetIndex < 0)
                        targetIndex = 0;
                    int newTargetIndex = EditorGui.Popup(
                        new GUIContent("Target Area", "Area used for every automated run of this scenario."),
                        targetIndex,
                        targets.Select(target => target.DisplayName).ToArray());
                    targetProperty.stringValue = targets[Mathf.Clamp(newTargetIndex, 0, targets.Count - 1)].Id;
                    return;
                }

                EditorGUILayout.HelpBox("No target areas were found in the selected scene.", MessageType.Warning);
                return;
            }

            string status = !sceneAsset
                ? "Select a scene first"
                : string.IsNullOrWhiteSpace(targetProperty.stringValue)
                    ? "Open scene to select"
                    : "Saved in scene";
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    new GUIContent(
                        "Target Area",
                        "The saved target is resolved automatically after the benchmark opens this scene. " +
                        "Open the scene to change the target."),
                    status);
            }
        }

        private void DrawResults()
        {
            IReadOnlyList<GenerationBenchmarkRunRecord> records = GenerationBenchmarkRunner.RunRecords;

            if (records.Count == 0)
                return;

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Latest Campaign", EditorStyles.boldLabel);
            int mismatches = records.Count(record =>
                record.measurement == BenchmarkMeasurementKind.Diagnostic.ToString() &&
                record.hasPrimaryReference &&
                !record.resultMatchesPrimary);
            int failures = records.Count(record => !record.succeeded);
            EditorGUILayout.LabelField(
                $"Runs {records.Count:N0} | Failures {failures:N0} | Runtime vs phase breakdown mismatches {mismatches:N0}",
                EditorStyles.miniLabel);

            _resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll, EditorStyles.helpBox, GUILayout.Height(105f));

            foreach (GenerationBenchmarkRunRecord run in records
                         .Skip(Math.Max(0, records.Count - 20))
                         .Reverse())
            {
                EditorGUILayout.LabelField(
                    $"{run.scenario} | {run.cacheCondition} | {BenchmarkMeasurementDisplay.Name(run.measurement)} | " +
                    $"{run.objectCount:N0} | seed {run.seed} | {run.elapsedMilliseconds:0.###} ms | " +
                    $"{run.placedCount:N0} placed");
            }

            EditorGUILayout.EndScrollView();
        }

        private void StartBenchmark(bool selectedOnly)
        {
            _serializedSuite.ApplyModifiedProperties();
            _validationMessage = string.Empty;
            GenerationBenchmarkRunner.Start(_suite, selectedOnly ? _selectedScenario : -1);
        }

        private void ValidateSuite()
        {
            _serializedSuite.ApplyModifiedProperties();
            List<string> errors = GenerationBenchmarkRunner.Validate(_suite);
            _validationMessage = errors.Count == 0 ? string.Empty : string.Join("\n", errors);
            _validationMessageType = errors.Count == 0 ? MessageType.None : MessageType.Error;
        }

        private void CreateSuite()
        {
            const string root = "Assets/Genix";
            const string folder = "Assets/Genix/Benchmarks";
            EnsureFolder("Assets", "Genix");
            EnsureFolder(root, "Benchmarks");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/GenerationBenchmarkSuite.asset");
            GenerationBenchmarkSuite suite = CreateInstance<GenerationBenchmarkSuite>();
            AssetDatabase.CreateAsset(suite, path);
            AssetDatabase.SaveAssets();
            RefreshSelectableAssets();
            SetSuite(suite);
            Selection.activeObject = suite;
        }

        private void SetSuite(GenerationBenchmarkSuite suite)
        {
            _suite = suite;
            _serializedSuite = suite ? new SerializedObject(suite) : null;
            _selectedScenario = 0;

            if (suite)
            {
                string path = AssetDatabase.GetAssetPath(suite);
                EditorPrefs.SetString(SelectedSuiteKey, AssetDatabase.AssetPathToGUID(path));
            }
            else
            {
                EditorPrefs.DeleteKey(SelectedSuiteKey);
            }
        }

        private void RefreshSelectableAssets()
        {
            _benchmarkSuites = AssetDatabase.FindAssets("t:GenerationBenchmarkSuite")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GenerationBenchmarkSuite>)
                .Where(suite => suite)
                .OrderBy(suite => suite.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _benchmarkSuiteOptions = EditorAssets.CreateAssetOptions(_benchmarkSuites);
            _generationPresets = EditorAssets.LoadAssetsFromFolder<GenerationPreset>(
                ProjectContentPaths.GenerationPresets,
                (a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            _generationPresetOptions = EditorAssets.CreateAssetOptions(_generationPresets);
            Repaint();
        }

        private GenerationPreset GetDefaultGenerationPreset()
        {
            return _generationPresets.FirstOrDefault(preset =>
                       string.Equals(preset.name, "Performance Benchmark", StringComparison.OrdinalIgnoreCase)) ??
                   _generationPresets.FirstOrDefault();
        }

        private void HandleProjectChanged()
        {
            RefreshSelectableAssets();

            if (!_suite || !_benchmarkSuites.Contains(_suite))
                SetSuite(_benchmarkSuites.FirstOrDefault());
        }

        private void EnsureSerializedSuite(bool force = false)
        {
            if (force || _serializedSuite == null || _serializedSuite.targetObject != _suite)
                _serializedSuite = new SerializedObject(_suite);
            else
                _serializedSuite.Update();
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static string FormatDuration(double seconds)
        {
            if (seconds <= 0d)
                return "--";

            TimeSpan duration = TimeSpan.FromSeconds(seconds);
            return duration.TotalHours >= 1d
                ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
                : $"{duration.Minutes:00}:{duration.Seconds:00}";
        }
    }
}
