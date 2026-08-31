using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Areas;
using Genix.Editor.Layouts;
using Genix.Editor.DevTools;
using Genix.Editor.TargetAreas;
using Genix.Editor.Utilities;
using Genix.Layouts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Evaluation
{
    /// <summary>Runs thesis quality campaigns and supports systematic visual review of every saved result.</summary>
    public sealed class GenerationEvaluationWindow : EditorWindow
    {
        private const string SelectedSuiteKey = "Genix.Evaluations.SelectedSuite";
        private const string SelectedReportKey = "Genix.Evaluations.SelectedReport";

        private GenerationEvaluationSuite _suite;
        private SerializedObject _serializedSuite;
        private GenerationEvaluationReport _report;
        private int _selectedScenario;
        private int _selectedRun;
        private Vector2 _scenarioScroll;
        private Vector2 _detailsScroll;
        private Vector2 _runScroll;
        private Vector2 _reportDetailsScroll;
        private bool _showSuiteSettings;
        private bool _showPlacedAssets = true;
        private bool _showScenarioCoverage = true;
        private EvaluationScenarioKind? _kindFilter;
        private string _validationMessage = string.Empty;

        [MenuItem("Tools/Genix Developer/Evaluation", false, 30)]
        public static void Open()
        {
            GenerationEvaluationWindow window = GenixWindowDocking.Open<GenerationEvaluationWindow>("Genix Evaluation");
            window.minSize = new Vector2(820f, 560f);
        }

        private void OnEnable()
        {
            GenerationEvaluationRunner.Changed += HandleRunnerChanged;
            SetSuite(LoadRemembered<GenerationEvaluationSuite>(SelectedSuiteKey));
            SetReport(LoadRemembered<GenerationEvaluationReport>(SelectedReportKey));
        }

        private void OnDisable() => GenerationEvaluationRunner.Changed -= HandleRunnerChanged;

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

            DrawReport();
            _serializedSuite.ApplyModifiedProperties();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                GenerationEvaluationSuite selected = (GenerationEvaluationSuite)EditorGUILayout.ObjectField(
                    _suite,
                    typeof(GenerationEvaluationSuite),
                    false,
                    GUILayout.MinWidth(210f));
                if (EditorGUI.EndChangeCheck())
                    SetSuite(selected);

                using (new EditorGUI.DisabledScope(GenerationEvaluationRunner.IsRunning))
                {
                    if (GUILayout.Button(
                            new GUIContent("Actions", "Create, refresh, or clean up evaluation content."),
                            EditorStyles.toolbarDropDown,
                            GUILayout.Width(72f)))
                        ShowSuiteActionsMenu();
                }

                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                GenerationEvaluationReport report = (GenerationEvaluationReport)EditorGUILayout.ObjectField(
                    _report,
                    typeof(GenerationEvaluationReport),
                    false,
                    GUILayout.Width(190f));
                if (EditorGUI.EndChangeCheck())
                    SetReport(report);

                using (new EditorGUI.DisabledScope(!_report))
                {
                    if (GUILayout.Button(new GUIContent("Export", "Writes JSON plus run, check, and aggregate CSV files including current visual ratings."), EditorStyles.toolbarButton, GUILayout.Width(54f)))
                        ExportReport();
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(GenerationEvaluationRunner.LastOutputDirectory)))
                {
                    if (GUILayout.Button("Files", EditorStyles.toolbarButton, GUILayout.Width(45f)))
                        EditorUtility.RevealInFinder(GenerationEvaluationRunner.LastOutputDirectory);
                }
            }
        }

        private void ShowSuiteActionsMenu()
        {
            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("Create / Refresh Thesis Suite"),
                false,
                RefreshThesisSuite);

            if (_suite && !GenerationEvaluationRunner.IsRunning)
            {
                menu.AddItem(
                    new GUIContent("Clean Up Evaluation Layouts…"),
                    false,
                    CleanUpEvaluationLayouts);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Clean Up Evaluation Layouts…"));
            }

            menu.ShowAsContext();
        }

        private void DrawRunPanel()
        {
            int ready = _suite.Scenarios.Count(item => item is { Enabled: true, Ready: true });
            int pending = _suite.Scenarios.Count(item => item is { Ready: false });
            int totalRuns = ready * _suite.RunsPerScenario;
            int savedLayouts = _suite.Scenarios.Count(item =>
                                   item is { Enabled: true, Ready: true, SaveLayouts: true }) *
                               _suite.RunsPerScenario;

            if (DeveloperWindowUi.SectionHeader(
                    new GUIContent("Campaign", "Run and validate the configured evaluation campaign."),
                    new GUIContent("Validate", "Check the suite without running it."),
                    !GenerationEvaluationRunner.IsRunning,
                    72f))
            {
                ValidateSuite();
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(GenerationEvaluationRunner.IsRunning))
                    {
                        if (DeveloperWindowUi.CommandButton(
                                new GUIContent(
                                    "Run All",
                                    $"Runs all {ready} enabled and ready scenarios with {_suite.RunsPerScenario} deterministic seeds each ({totalRuns} runs total)."),
                                0,
                                2))
                            StartEvaluation(false);
                        if (DeveloperWindowUi.CommandButton(new GUIContent("Run Selected"), 1, 2))
                            StartEvaluation(true);
                    }

                    using (new EditorGUI.DisabledScope(!GenerationEvaluationRunner.IsRunning))
                    {
                        if (GUILayout.Button("Stop", EditorStyles.miniButton, GUILayout.Height(28f), GUILayout.Width(64f)))
                            GenerationEvaluationRunner.RequestStop();
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        $"Ready {ready}  |  Runs {totalRuns}  |  Layouts {savedLayouts}  |  Pending {pending}",
                        EditorStyles.miniLabel);
                }

                float progress = GenerationEvaluationRunner.TotalRuns > 0
                    ? GenerationEvaluationRunner.CompletedRuns / (float)GenerationEvaluationRunner.TotalRuns
                    : 0f;
                string progressLabel = GenerationEvaluationRunner.IsRunning
                    ? $"{GenerationEvaluationRunner.Status} | elapsed {FormatDuration(GenerationEvaluationRunner.ElapsedSeconds)} | ETA {FormatDuration(GenerationEvaluationRunner.EstimatedRemainingSeconds)}"
                    : GenerationEvaluationRunner.Status;
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 19f), progress, progressLabel);

                if (!string.IsNullOrWhiteSpace(_validationMessage))
                    EditorGUILayout.HelpBox(_validationMessage, MessageType.Error);
                if (!string.IsNullOrWhiteSpace(GenerationEvaluationRunner.LastError))
                    EditorGUILayout.HelpBox(GenerationEvaluationRunner.LastError, MessageType.Error);

                _showSuiteSettings = EditorGUILayout.Foldout(_showSuiteSettings, "Campaign Settings", true);
                if (_showSuiteSettings)
                {
                    using (new EditorGUI.DisabledScope(GenerationEvaluationRunner.IsRunning))
                    {
                        EditorGUILayout.PropertyField(
                            _serializedSuite.FindProperty("runsPerScenario"),
                            new GUIContent("Runs Per Scenario", "Independent fixed-seed observations per target scenario. The thesis suite uses 20."));
                        EditorGUILayout.PropertyField(
                            _serializedSuite.FindProperty("settleFrames"),
                            new GUIContent("Scene Settle Frames", "Editor frames allowed after opening a scene before generation begins."));
                        EditorGUILayout.PropertyField(
                            _serializedSuite.FindProperty("seeds"),
                            new GUIContent("Deterministic Seeds", "Shared seed sample used identically across all scenarios."),
                            true);
                    }
                }
            }
        }

        private void DrawScenarioList()
        {
            SerializedProperty scenarios = _serializedSuite.FindProperty("scenarios");
            float listWidth = DeveloperWindowUi.ResponsiveListWidth(position.width);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(listWidth)))
            {
                EditorGUILayout.LabelField("Scenarios", EditorStyles.boldLabel);
                _scenarioScroll = EditorGUILayout.BeginScrollView(_scenarioScroll, EditorStyles.helpBox);

                for (int i = 0; i < scenarios.arraySize; i++)
                {
                    SerializedProperty scenario = scenarios.GetArrayElementAtIndex(i);
                    string name = scenario.FindPropertyRelative("displayName").stringValue;
                    bool enabled = scenario.FindPropertyRelative("enabled").boolValue;
                    bool ready = scenario.FindPropertyRelative("ready").boolValue;
                    string marker = !ready ? "[!]" : enabled ? "[x]" : "[ ]";
                    GUIContent label = new($"{marker} {name}", name);
                    if (DeveloperWindowUi.SelectableRow(i == _selectedScenario, label, 24f, listWidth - 20f))
                        _selectedScenario = i;
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawScenarioDetails()
        {
            SerializedProperty scenarios = _serializedSuite.FindProperty("scenarios");
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
                _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll, EditorStyles.helpBox);

                if (scenarios.arraySize > 0)
                {
                    _selectedScenario = Mathf.Clamp(_selectedScenario, 0, scenarios.arraySize - 1);
                    SerializedProperty scenario = scenarios.GetArrayElementAtIndex(_selectedScenario);
                    using (new EditorGUI.DisabledScope(GenerationEvaluationRunner.IsRunning))
                    {
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("enabled"));
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("ready"), new GUIContent("Ready", "Only fully authored scenes should contribute final evaluation observations."));
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("displayName"), new GUIContent("Name"));
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("kind"));
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("scene"));
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("areaProviderId"), new GUIContent("Area Provider"));
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("targetId"), new GUIContent("Target ID"));
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("generationPreset"));
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("checks"));
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("minimumCompletionRatio"));
                        EditorGUILayout.PropertyField(
                            scenario.FindPropertyRelative("maximumCompletionRatio"),
                            new GUIContent("Maximum Completion Ratio", "Use a value below 100% only when the scenario intentionally verifies graceful best-effort behavior under insufficient capacity."));
                        EditorGUILayout.PropertyField(scenario.FindPropertyRelative("saveLayouts"), new GUIContent("Save Every Layout", "Persists each seed result for later visual review. Disabled for performance smoke scenes."));
                        DrawOutdoorSetupAction(scenario);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawOutdoorSetupAction(SerializedProperty scenario)
        {
            SerializedProperty sceneProperty = scenario.FindPropertyRelative("scene");
            string scenePath = AssetDatabase.GetAssetPath(sceneProperty.objectReferenceValue);
            if (!string.Equals(
                    scenePath,
                    OutdoorEvaluationSetupUtility.ScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            EditorGUILayout.Space(5f);
            if (!GUILayout.Button(new GUIContent(
                    "Prepare / Reset Outdoor Setup",
                    "Rebuilds the canonical Outdoor semantics, asset rules, pool, preset, and evaluation-suite entry. This is a maintenance action and is not required before normal runs."),
                    GUILayout.Height(24f)))
            {
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Prepare Outdoor Evaluation",
                "This rewrites the canonical Outdoor scene semantics, related asset definitions, " +
                "asset pool, generation preset, and thesis-suite configuration. Continue?",
                "Prepare / Reset",
                "Cancel");
            if (!confirmed || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                int selectedScenario = _selectedScenario;
                string summary = OutdoorEvaluationSetupUtility.Prepare();
                GenerationEvaluationSuite suite = AssetDatabase.LoadAssetAtPath<GenerationEvaluationSuite>(
                    ThesisEvaluationSuiteFactory.SuitePath);
                SetSuite(suite);
                _selectedScenario = Mathf.Clamp(selectedScenario, 0, Mathf.Max(0, suite.Scenarios.Count - 1));
                _validationMessage = string.Empty;
                Debug.Log(summary);
            }
            catch (Exception exception)
            {
                _validationMessage = exception.Message;
                Debug.LogException(exception);
            }

            GUIUtility.ExitGUI();
        }

        private void DrawReport()
        {
            IReadOnlyList<GenerationEvaluationRunRecord> runs = _report
                ? _report.Runs
                : GenerationEvaluationRunner.RunRecords;
            if (runs.Count == 0)
                return;

            EditorGUILayout.Space(5f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Evaluation Report", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                DrawKindFilter();
            }

            IReadOnlyList<(GenerationEvaluationRunRecord Run, int Index)> filtered = runs
                .Select((run, index) => (run, index))
                .Where(item => !_kindFilter.HasValue || item.run.scenarioKind == _kindFilter.Value.ToString())
                .ToArray();
            int automaticFailures = runs.Count(run => run.AutomaticVerdict == EvaluationAutomaticVerdict.Failed);
            int automaticIncomplete = runs.Count(run => run.AutomaticVerdict == EvaluationAutomaticVerdict.Incomplete);
            int reviewable = runs.Count(run => run.HasLayoutReference);
            int reviewed = runs.Count(run => run.VisualReviewCompleted);
            EditorGUILayout.LabelField(
                $"Runs {runs.Count:N0} | Automatic failures {automaticFailures:N0} | Incomplete evidence {automaticIncomplete:N0} | Saved layouts reviewed {reviewed:N0}/{reviewable:N0}",
                EditorStyles.miniLabel);
            if (_report)
            {
                EditorGUILayout.LabelField(
                    $"Campaign scope {_report.RunScope} | Completed runs {runs.Count:N0}/{_report.ExpectedRunCount:N0} | Cancelled {_report.CampaignCancelled}",
                    EditorStyles.miniLabel);
                if (!_report.CampaignCompleted || runs.Count != _report.ExpectedRunCount)
                {
                    EditorGUILayout.HelpBox(
                        "This report does not contain a complete campaign for its recorded invocation scope. Retain it as partial evidence and rerun before using it as a final campaign.",
                        MessageType.Warning);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawRunList(filtered);
                GUILayout.Space(6f);
                DrawRunDetails(runs);
            }
        }

        private void DrawKindFilter()
        {
            string[] labels = { "All", "Isolated", "Integrated", "Performance" };
            int selected = _kindFilter.HasValue ? (int)_kindFilter.Value + 1 : 0;
            int changed = EditorGui.Popup(selected, labels, GUILayout.Width(110f));
            _kindFilter = changed == 0 ? null : (EvaluationScenarioKind?)(changed - 1);
        }

        private void DrawRunList(IReadOnlyList<(GenerationEvaluationRunRecord Run, int Index)> filtered)
        {
            float listWidth = DeveloperWindowUi.ResponsiveListWidth(position.width, 300f, 500f, 0.38f, 430f);
            _runScroll = EditorGUILayout.BeginScrollView(_runScroll, EditorStyles.helpBox, GUILayout.Width(listWidth), GUILayout.Height(220f));
            foreach ((GenerationEvaluationRunRecord run, int index) in filtered)
            {
                string automatic = run.AutomaticVerdict switch
                {
                    EvaluationAutomaticVerdict.Passed => "PASS",
                    EvaluationAutomaticVerdict.Incomplete => "INCOMPLETE",
                    _ => "FAIL"
                };
                string review = !run.HasLayoutReference
                    ? "NO LAYOUT"
                    : run.visualRating == EvaluationVisualRating.NotReviewed
                        ? "-"
                        : run.visualRating.ToString();
                string label = $"{automatic} | {review} | {run.scenario} | {run.seed}";
                if (DeveloperWindowUi.SelectableRow(index == _selectedRun, new GUIContent(label, label), 23f, listWidth - 20f))
                    _selectedRun = index;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRunDetails(IReadOnlyList<GenerationEvaluationRunRecord> runs)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.Height(300f)))
            {
                _selectedRun = Mathf.Clamp(_selectedRun, 0, runs.Count - 1);
                GenerationEvaluationRunRecord run = runs[_selectedRun];
                bool hasLayoutReference = run.HasLayoutReference;
                bool missingLayoutAsset = run.HasMissingLayoutAsset;
                EditorGUILayout.LabelField(run.scenario, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Seed {run.seed} | Placed {run.placedCount}/{run.requestedCount} | {run.scene}", EditorStyles.miniLabel);

                _reportDetailsScroll = EditorGUILayout.BeginScrollView(
                    _reportDetailsScroll,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
                DrawScenarioCoverage(runs, run.scenario);
                EditorGUILayout.Space(2f);
                DrawAssetCounts(run.assetCounts);
                EditorGUILayout.Space(2f);
                foreach (GenerationEvaluationCheckRecord check in run.checks)
                    EditorGUILayout.LabelField(
                        new GUIContent($"{check.status}: {check.name} ({check.violations})", check.message),
                        EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();

                if (_report && !hasLayoutReference)
                {
                    EditorGUILayout.HelpBox(
                        run.visualRating == EvaluationVisualRating.NotReviewed
                            ? "This run has no persisted layout and is excluded from visual review."
                            : "This run has no persisted layout. Its stored visual rating is not valid review evidence.",
                        run.visualRating == EvaluationVisualRating.NotReviewed ? MessageType.Info : MessageType.Warning);
                }
                else if (_report && missingLayoutAsset)
                {
                    EditorGUILayout.HelpBox(
                        $"The referenced layout asset '{run.layoutAssetPath}' is missing. Restore it before assigning or relying on a visual rating.",
                        MessageType.Error);
                }
                else if (_report)
                {
                    EditorGUI.BeginChangeCheck();
                    run.visualRating = (EvaluationVisualRating)EditorGUILayout.EnumPopup(
                        new GUIContent("Visual Rating", "Pass = no evaluation-relevant visible defect under the frozen rubric; Acceptable = one or more minor visible defects, no major defect, and a valid tested configuration; Fail = at least one major visible defect that invalidates the tested condition."),
                        run.visualRating);
                    run.visualNotes = EditorGUILayout.TextField(
                        new GUIContent("Review Notes", "For Acceptable or Fail, name the rubric category, affected object(s), and observable defect."),
                        run.visualNotes ?? string.Empty);
                    if (run.visualRating is EvaluationVisualRating.Acceptable or EvaluationVisualRating.Fail &&
                        string.IsNullOrWhiteSpace(run.visualNotes))
                    {
                        EditorGUILayout.HelpBox(
                            "An observable review note is required for Acceptable and Fail ratings.",
                            MessageType.Warning);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorUtility.SetDirty(_report);
                        AssetDatabase.SaveAssetIfDirty(_report);
                    }
                }

                using (new EditorGUI.DisabledScope(!hasLayoutReference || missingLayoutAsset))
                {
                    bool sceneIsActive = IsRunSceneActive(run);
                    string label = sceneIsActive ? "Apply Layout" : "Open Scene and Apply Layout";
                    string tooltip = sceneIsActive
                        ? "Applies this saved observation to the already active source scene without reloading it."
                        : "Loads the run's source scene and applies this saved observation without saving the scene.";
                    if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(25f)))
                        OpenRun(run);
                }
            }
        }

        private void DrawScenarioCoverage(IReadOnlyList<GenerationEvaluationRunRecord> runs, string scenario)
        {
            GenerationEvaluationRunRecord[] scenarioRuns = runs
                .Where(run => string.Equals(run.scenario, scenario, StringComparison.Ordinal))
                .ToArray();
            _showScenarioCoverage = EditorGUILayout.Foldout(
                _showScenarioCoverage,
                $"Scenario Coverage ({scenarioRuns.Length:N0} runs)",
                true);
            if (!_showScenarioCoverage)
                return;

            DrawCoverageGroup(
                "Assets",
                GenerationEvaluationCoverage.BuildAssetCoverage(scenarioRuns),
                "Occurrence across this scenario's seeds. Eligible assets with zero occurrences remain visible; this is evidence, not an automatic failure.");
            DrawCoverageGroup(
                "Supports",
                GenerationEvaluationCoverage.BuildSupportCoverage(scenarioRuns),
                "Semantic support kinds used across this scenario's seeds. Counts are tag occurrences and do not change the automatic verdict.");
        }

        private static void DrawCoverageGroup(
            string label,
            IReadOnlyList<GenerationEvaluationCoverageRecord> coverage,
            string tooltip)
        {
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), EditorStyles.miniBoldLabel);
            if (coverage.Count == 0)
            {
                EditorGUILayout.LabelField("No coverage data recorded.", EditorStyles.miniLabel);
                return;
            }

            foreach (GenerationEvaluationCoverageRecord item in coverage)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        new GUIContent(item.name, $"{item.totalCount:N0} occurrences across {item.runsPresent:N0} of {item.totalRuns:N0} runs."),
                        EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(
                        $"{item.runsPresent:N0}/{item.totalRuns:N0} | {item.totalCount:N0}",
                        EditorStyles.miniLabel,
                        GUILayout.Width(92f));
                }
            }
        }

        private void DrawAssetCounts(IReadOnlyCollection<GenerationEvaluationCountRecord> counts)
        {
            int total = counts?.Sum(count => count.count) ?? 0;
            int types = counts?.Count ?? 0;
            _showPlacedAssets = EditorGUILayout.Foldout(
                _showPlacedAssets,
                $"Placed Assets ({total:N0} objects, {types:N0} types)",
                true);
            if (!_showPlacedAssets)
                return;

            if (counts == null || counts.Count == 0)
            {
                EditorGUILayout.LabelField("No placed assets were recorded.", EditorStyles.miniLabel);
                return;
            }

            GenerationEvaluationCountRecord[] ordered = counts
                .OrderBy(count => count.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            const int columns = 2;
            for (int index = 0; index < ordered.Length; index += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int itemIndex = index + column;
                        if (itemIndex >= ordered.Length)
                        {
                            GUILayout.FlexibleSpace();
                            continue;
                        }

                        GenerationEvaluationCountRecord count = ordered[itemIndex];
                        using (new EditorGUILayout.HorizontalScope(GUILayout.MinWidth(150f)))
                        {
                            EditorGUILayout.LabelField(
                                new GUIContent(count.name, $"{count.name}: {count.count:N0} placed"),
                                EditorStyles.miniLabel);
                            EditorGUILayout.LabelField(
                                count.count.ToString("N0"),
                                EditorStyles.miniLabel,
                                GUILayout.Width(34f));
                        }
                    }
                }
            }
        }

        private void StartEvaluation(bool selectedOnly)
        {
            _serializedSuite.ApplyModifiedProperties();
            _validationMessage = string.Empty;
            GenerationEvaluationRunner.Start(_suite, selectedOnly ? _selectedScenario : -1);
        }

        private void ValidateSuite()
        {
            _serializedSuite.ApplyModifiedProperties();
            _validationMessage = string.Join("\n", GenerationEvaluationRunner.Validate(_suite));
        }

        private void RefreshThesisSuite()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                SetSuite(ThesisEvaluationSuiteFactory.CreateOrRefresh(out _));
                _validationMessage = string.Empty;
            }
            catch (Exception exception)
            {
                _validationMessage = exception.Message;
            }
        }

        private void OpenRun(GenerationEvaluationRunRecord run)
        {
            SavedLayout layout = run.LoadLayout();
            if (!layout)
            {
                _validationMessage = $"The referenced layout asset '{run.layoutAssetPath}' is missing.";
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!IsRunSceneActive(run))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                scene = EditorSceneManager.OpenScene(run.scene, OpenSceneMode.Single);
            }

            IBenchmarkAreaResolver resolver = BenchmarkAreaResolverRegistry.CreateResolvers()
                .FirstOrDefault(item => item.ProviderId == run.areaProviderId);
            IAreaSource areaSource = resolver?.Resolve(scene, run.targetId);
            string error = string.Empty;
            if (areaSource == null || !LayoutApplyService.Apply(layout, areaSource, out error))
            {
                _validationMessage = string.IsNullOrWhiteSpace(error)
                    ? $"Could not resolve target '{run.targetId}' in '{run.scene}'."
                    : error;
                return;
            }

            Selection.activeObject = areaSource.ParentTransform;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static bool IsRunSceneActive(GenerationEvaluationRunRecord run)
        {
            if (run == null || string.IsNullOrWhiteSpace(run.scene))
                return false;

            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() &&
                   string.Equals(activeScene.path, run.scene, StringComparison.OrdinalIgnoreCase);
        }

        private void ExportReport()
        {
            AssetDatabase.SaveAssetIfDirty(_report);
            string directory = GenerationEvaluationExporter.Export(_report.ToCampaign());
            int invalid = _report.Runs.Count(run => run.HasInvalidVisualReviewEvidence);
            int reviewable = _report.Runs.Count(run => run.HasLayoutReference);
            int valid = _report.Runs.Count(run => run.VisualReviewEvidenceValid);
            _validationMessage = invalid > 0
                ? $"Export retained {invalid:N0} run(s) with invalid visual-review evidence. See runs.csv and summary.csv before using the export as final evidence."
                : valid < reviewable
                    ? $"Export retained {reviewable - valid:N0} saved layout(s) without complete valid visual-review evidence."
                    : string.Empty;
            EditorUtility.RevealInFinder(directory);
        }

        private void CleanUpEvaluationLayouts()
        {
            GenerationEvaluationLayoutCleanupPlan plan =
                GenerationEvaluationLayoutCleanupService.BuildPlan(_suite);
            if (!plan.IsValid)
            {
                EditorUtility.DisplayDialog("Clean Up Evaluation Layouts", plan.Error, "OK");
                return;
            }

            if (plan.MissingProtectedLayouts > 0)
            {
                EditorUtility.DisplayDialog(
                    "Clean Up Evaluation Layouts",
                    $"Cleanup was stopped because {plan.MissingProtectedLayouts:N0} layout(s) referenced by the retained reports are already missing. Restore them or select a valid final campaign before cleanup.",
                    "OK");
                return;
            }

            if (plan.DeletableLayoutPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Clean Up Evaluation Layouts",
                    $"No superseded evaluation layouts were found. {plan.ProtectedLayoutPaths.Count:N0} layout(s) referenced by the current reports remain protected.",
                    "OK");
                return;
            }

            string rerunSummary = plan.ProtectedReports.Count > 1
                ? $" and {plan.ProtectedReports.Count - 1:N0} newer completed scenario rerun(s)"
                : string.Empty;
            bool confirmed = EditorUtility.DisplayDialog(
                "Clean Up Evaluation Layouts",
                $"Suite: {_suite.name}\n\n" +
                $"Keep {plan.ProtectedLayoutPaths.Count:N0} layout(s) referenced by the latest completed full campaign{rerunSummary}.\n" +
                $"Delete {plan.DeletableLayoutPaths.Count:N0} superseded locked evaluation layout(s) and their owned prefabs.\n\n" +
                "Designer layouts and report assets are not deleted. This cannot be undone.",
                $"Delete {plan.DeletableLayoutPaths.Count:N0}",
                "Cancel");
            if (!confirmed)
                return;

            if (!GenerationEvaluationLayoutCleanupService.Execute(plan, out int deletedCount, out string error))
            {
                EditorUtility.DisplayDialog("Cleanup Failed", error, "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "Evaluation Layouts Cleaned Up",
                $"Deleted {deletedCount:N0} superseded evaluation layout(s). " +
                $"Kept {plan.ProtectedLayoutPaths.Count:N0} layout(s) referenced by the current reports.",
                "OK");
        }

        private void HandleRunnerChanged()
        {
            if (!GenerationEvaluationRunner.IsRunning && GenerationEvaluationRunner.LastReport)
                SetReport(GenerationEvaluationRunner.LastReport);
            Repaint();
        }

        private void SetSuite(GenerationEvaluationSuite suite)
        {
            _suite = suite;
            _serializedSuite = suite ? new SerializedObject(suite) : null;
            _selectedScenario = 0;
            Remember(SelectedSuiteKey, suite);
        }

        private void SetReport(GenerationEvaluationReport report)
        {
            _report = report;
            _selectedRun = 0;
            Remember(SelectedReportKey, report);
        }

        private void EnsureSerializedSuite()
        {
            if (_serializedSuite == null || _serializedSuite.targetObject != _suite)
                _serializedSuite = new SerializedObject(_suite);
            else
                _serializedSuite.Update();
        }

        private static void Remember(string key, UnityEngine.Object asset)
        {
            string path = asset ? AssetDatabase.GetAssetPath(asset) : string.Empty;
            if (string.IsNullOrWhiteSpace(path))
                EditorPrefs.DeleteKey(key);
            else
                EditorPrefs.SetString(key, AssetDatabase.AssetPathToGUID(path));
        }

        private static T LoadRemembered<T>(string key) where T : UnityEngine.Object
        {
            string path = AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(key, string.Empty));
            return AssetDatabase.LoadAssetAtPath<T>(path);
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
