using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Editor.TargetAreas;
using Genix.Editor.DevTools;
using Genix.Editor.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Benchmarking
{
    /// <summary>Configures, runs, and inspects reproducible Genix performance benchmark campaigns.</summary>
    public sealed class GenerationBenchmarkWindow : EditorWindow
    {
        private const string SelectedSuiteKey = "Genix.Benchmarks.SelectedSuite";

        private GenerationBenchmarkSuite _suite;
        private SerializedObject _serializedSuite;
        private int _selectedScenario;
        private Vector2 _scenarioScroll;
        private Vector2 _detailsScroll;
        private Vector2 _resultsScroll;
        private bool _showCampaignSettings;
        private string _validationMessage = string.Empty;
        private MessageType _validationMessageType = MessageType.None;

        [MenuItem("Tools/Genix Developer/Benchmarks", false, 20)]
        public static void Open()
        {
            GenerationBenchmarkWindow window = GenixWindowDocking.Open<GenerationBenchmarkWindow>("Genix Benchmarks");
            window.minSize = new Vector2(760f, 520f);
        }

        private void OnEnable()
        {
            GenerationBenchmarkRunner.Changed += Repaint;
            string suiteGuid = EditorPrefs.GetString(SelectedSuiteKey, string.Empty);
            string path = AssetDatabase.GUIDToAssetPath(suiteGuid);
            SetSuite(AssetDatabase.LoadAssetAtPath<GenerationBenchmarkSuite>(path));
        }

        private void OnDisable()
        {
            GenerationBenchmarkRunner.Changed -= Repaint;
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
                EditorGUI.BeginChangeCheck();
                GenerationBenchmarkSuite selected = (GenerationBenchmarkSuite)EditorGUILayout.ObjectField(
                    _suite,
                    typeof(GenerationBenchmarkSuite),
                    false,
                    GUILayout.MinWidth(220f));

                if (EditorGUI.EndChangeCheck())
                    SetSuite(selected);

                if (GUILayout.Button(
                        new GUIContent("Actions", "Create or populate benchmark suites."),
                        EditorStyles.toolbarDropDown,
                        GUILayout.Width(72f)))
                    ShowSuiteActionsMenu();

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(GenerationBenchmarkRunner.LastOutputDirectory)))
                {
                    if (GUILayout.Button("Results", EditorStyles.toolbarButton, GUILayout.Width(62f)))
                        EditorUtility.RevealInFinder(GenerationBenchmarkRunner.LastOutputDirectory);
                }
            }
        }

        private void ShowSuiteActionsMenu()
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Create Suite"), false, CreateSuite);
            if (_suite && !GenerationBenchmarkRunner.IsRunning)
                menu.AddItem(new GUIContent("Add Evaluation Scenes"), false, AddEvaluationScenes);
            else
                menu.AddDisabledItem(new GUIContent("Add Evaluation Scenes"));
            menu.ShowAsContext();
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
            EditorGUILayout.PropertyField(cache, GUIContent.none, GUILayout.Width(115f));
            EditorGUILayout.PropertyField(measurements, GUIContent.none, GUILayout.Width(150f));
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
                _scenarioScroll = EditorGUILayout.BeginScrollView(_scenarioScroll, EditorStyles.helpBox);

                for (int i = 0; i < scenarios.arraySize; i++)
                {
                    SerializedProperty scenario = scenarios.GetArrayElementAtIndex(i);
                    string name = scenario.FindPropertyRelative("displayName").stringValue;
                    bool enabled = scenario.FindPropertyRelative("enabled").boolValue;
                    string label = $"{(enabled ? "[x]" : "[ ]")} {name}";

                    if (DeveloperWindowUi.SelectableRow(
                            i == _selectedScenario,
                            new GUIContent(label, name),
                            24f,
                            listWidth - 20f))
                        _selectedScenario = i;
                }

                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(GenerationBenchmarkRunner.IsRunning))
                    {
                        if (GUILayout.Button("+"))
                        {
                            _serializedSuite.ApplyModifiedProperties();
                            Undo.RecordObject(_suite, "Added Benchmark Scenario");
                            _suite.AddScenario("Benchmark Scenario", null);
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

                using (new EditorGUI.DisabledScope(GenerationBenchmarkRunner.IsRunning))
                {
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("enabled"));
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("displayName"), new GUIContent("Name"));
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("scene"));
                    DrawTargetFields(scenario);
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("assetPool"));
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("stylePreset"));
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("placementTargets"));
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("targetDistributionMode"));
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("targetDistributionWeights"), true);
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("areaBuildSettings"), true);
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("relativePlacement"), true);
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("bestEffort"));
                    EditorGUILayout.PropertyField(scenario.FindPropertyRelative("objectCounts"), true);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTargetFields(SerializedProperty scenario)
        {
            SerializedProperty sceneProperty = scenario.FindPropertyRelative("scene");
            SerializedProperty providerProperty = scenario.FindPropertyRelative("areaProviderId");
            SerializedProperty targetProperty = scenario.FindPropertyRelative("targetId");
            IReadOnlyList<IBenchmarkAreaResolver> resolvers = BenchmarkAreaResolverRegistry.CreateResolvers();
            string[] providerNames = resolvers.Select(resolver => resolver.DisplayName).ToArray();
            int providerIndex = Mathf.Max(0, resolvers.ToList().FindIndex(resolver => resolver.ProviderId == providerProperty.stringValue));

            if (providerNames.Length > 0)
            {
                int newIndex = EditorGui.Popup(
                    new GUIContent("Area Provider", "Spatial integration used to resolve the target after each scene switch."),
                    providerIndex,
                    providerNames);
                providerProperty.stringValue = resolvers[Mathf.Clamp(newIndex, 0, resolvers.Count - 1)].ProviderId;
            }
            else
            {
                EditorGUILayout.HelpBox("No benchmark target provider is installed.", MessageType.Error);
            }

            SceneAsset sceneAsset = sceneProperty.objectReferenceValue as SceneAsset;
            string scenePath = sceneAsset ? AssetDatabase.GetAssetPath(sceneAsset) : string.Empty;
            Scene activeScene = SceneManager.GetActiveScene();

            if (sceneAsset && string.Equals(activeScene.path, scenePath, StringComparison.Ordinal))
            {
                IBenchmarkAreaResolver resolver = resolvers.FirstOrDefault(item => item.ProviderId == providerProperty.stringValue);
                IReadOnlyList<BenchmarkAreaTarget> targets = resolver?.FindTargets(activeScene) ?? Array.Empty<BenchmarkAreaTarget>();

                if (targets.Count > 0)
                {
                    int targetIndex = Mathf.Max(0, targets.ToList().FindIndex(target => target.Id == targetProperty.stringValue));
                    int newTargetIndex = EditorGui.Popup(
                        new GUIContent("Target Area", "Stable scene target resolved for every automated run."),
                        targetIndex,
                        targets.Select(target => target.DisplayName).ToArray());
                    targetProperty.stringValue = targets[Mathf.Clamp(newTargetIndex, 0, targets.Count - 1)].Id;
                    return;
                }
            }

            EditorGUILayout.PropertyField(targetProperty, new GUIContent("Target ID"));

            using (new EditorGUI.DisabledScope(!sceneAsset))
            {
                if (GUILayout.Button("Open Scene to Select Target", GUILayout.Width(190f)) &&
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
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
                $"Runs {records.Count:N0} | Failures {failures:N0} | Primary/Diagnostic result mismatches {mismatches:N0}",
                EditorStyles.miniLabel);

            _resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll, EditorStyles.helpBox, GUILayout.Height(105f));

            foreach (GenerationBenchmarkRunRecord run in records
                         .Skip(Math.Max(0, records.Count - 20))
                         .Reverse())
            {
                EditorGUILayout.LabelField(
                    $"{run.scenario} | {run.cacheCondition} | {run.measurement} | {run.objectCount:N0} | seed {run.seed} | {run.elapsedMilliseconds:0.###} ms | {run.placedCount:N0} placed");
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
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/ThesisPerformanceSuite.asset");
            GenerationBenchmarkSuite suite = CreateInstance<GenerationBenchmarkSuite>();
            AssetDatabase.CreateAsset(suite, path);
            AssetDatabase.SaveAssets();
            SetSuite(suite);
            Selection.activeObject = suite;
        }

        private void AddEvaluationScenes()
        {
            _serializedSuite.ApplyModifiedProperties();
            HashSet<SceneAsset> existing = _suite.Scenarios
                .Where(scenario => scenario?.Scene)
                .Select(scenario => scenario.Scene)
                .Aggregate(new HashSet<SceneAsset>(), (set, scene) =>
                {
                    set.Add(scene);
                    return set;
                });
            string[] guids = AssetDatabase.FindAssets("t:SceneAsset");
            List<SceneAsset> scenes = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.IndexOf("/Evaluation/Scenes/Performance/", StringComparison.Ordinal) >= 0 ||
                               path.IndexOf("/Evaluation/Scenes/RealWorld/", StringComparison.Ordinal) >= 0)
                .Select(AssetDatabase.LoadAssetAtPath<SceneAsset>)
                .Where(scene => scene && !existing.Contains(scene))
                .OrderBy(scene => AssetDatabase.GetAssetPath(scene), StringComparer.OrdinalIgnoreCase)
                .ToList();

            Undo.RecordObject(_suite, "Added Evaluation Benchmark Scenes");

            foreach (SceneAsset scene in scenes)
                _suite.AddScenario(ObjectNames.NicifyVariableName(scene.name), scene);

            EditorUtility.SetDirty(_suite);
            EnsureSerializedSuite(force: true);
            _validationMessage = string.Empty;
            _validationMessageType = MessageType.None;
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
