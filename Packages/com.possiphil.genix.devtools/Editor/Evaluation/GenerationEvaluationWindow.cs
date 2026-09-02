using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Areas;
using Genix.Core;
using Genix.Editor.Common;
using Genix.Editor.Drawers;
using Genix.Editor.Infrastructure;
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
    /// <summary>Runs quality-evaluation campaigns and supports systematic visual review of saved results.</summary>
    public sealed class GenerationEvaluationWindow : EditorWindow
    {
        private const string SelectedSuiteKey = "Genix.Evaluations.SelectedSuite";
        private const string SelectedReportKey = "Genix.Evaluations.SelectedReport";
        private const string ReviewCaptureSessionKey = "Genix.Evaluations.ReviewCaptureRunning";
        private const float ScenarioConfigurationMinHeight = 255f;
        private const float ScenarioControlsHeight = 20f;
        private const float PaneSpacing = 6f;
        private const float ReportPaneHeight = 300f;
        private const float SectionHorizontalPadding = 5f;
        private const double InfoMessageDurationSeconds = 5d;

        private enum AutomaticResultFilter
        {
            All,
            Passed,
            Failed,
            Incomplete
        }

        private enum VisualReviewFilter
        {
            All,
            NeedsReview,
            Reviewed,
            Pass,
            Acceptable,
            Fail,
            NoLayout
        }

        private GenerationEvaluationSuite _suite;
        private SerializedObject _serializedSuite;
        private GenerationEvaluationReport _report;
        private int _selectedScenario;
        private int _selectedRun;
        private Vector2 _scenarioScroll;
        private Vector2 _detailsScroll;
        private Vector2 _runScroll;
        private Vector2 _reportDetailsScroll;
        private float _reportPaneTop;
        private readonly Dictionary<string, ScenarioCoverageSnapshot> _coverageByScenario =
            new(StringComparer.Ordinal);
        private bool _showSuiteSettings;
        private bool _showEvaluationCriteria;
        private bool _showVisualEvidence;
        private bool _showPlacedAssets = true;
        private bool _showScenarioCoverage = true;
        private EvaluationScenarioKind? _kindFilter;
        private AutomaticResultFilter _resultFilter;
        private VisualReviewFilter _reviewFilter;
        private string _validationMessage = string.Empty;
        private MessageType _validationMessageType = MessageType.Info;
        private double _validationMessageExpiresAt;

        private GenerationEvaluationSuite[] _evaluationSuites = Array.Empty<GenerationEvaluationSuite>();
        private string[] _evaluationSuiteOptions = Array.Empty<string>();
        private GenerationEvaluationReport[] _evaluationReports = Array.Empty<GenerationEvaluationReport>();
        private string[] _evaluationReportOptions = Array.Empty<string>();
        private GenerationPreset[] _generationPresets = Array.Empty<GenerationPreset>();
        private string[] _generationPresetOptions = Array.Empty<string>();

        private sealed class ScenarioCoverageSnapshot
        {
            public int RunCount { get; }
            public IReadOnlyList<GenerationEvaluationCoverageRecord> Assets { get; }
            public IReadOnlyList<GenerationEvaluationCoverageRecord> Supports { get; }

            public ScenarioCoverageSnapshot(
                int runCount,
                IReadOnlyList<GenerationEvaluationCoverageRecord> assets,
                IReadOnlyList<GenerationEvaluationCoverageRecord> supports)
            {
                RunCount = runCount;
                Assets = assets;
                Supports = supports;
            }
        }

        /// <summary>Opens the Genix Evaluation window.</summary>
        [MenuItem("Tools/Genix Developer/Evaluation", false, 30)]
        public static void Open()
        {
            GenerationEvaluationWindow window = GenixWindowDocking.Open<GenerationEvaluationWindow>("Genix Evaluation");
            window.minSize = new Vector2(820f, 560f);
        }

        private void OnEnable()
        {
            GenerationEvaluationRunner.Changed += HandleRunnerChanged;
            EditorApplication.projectChanged += HandleProjectChanged;
            EditorApplication.update += UpdateValidationMessageTimeout;
            RefreshSelectableAssets();

            GenerationEvaluationSuite rememberedSuite = LoadRemembered<GenerationEvaluationSuite>(SelectedSuiteKey);
            GenerationEvaluationReport rememberedReport = LoadRemembered<GenerationEvaluationReport>(SelectedReportKey);
            SetSuite(rememberedSuite ? rememberedSuite : _evaluationSuites.FirstOrDefault());
            SetReport(rememberedReport ? rememberedReport : _evaluationReports.FirstOrDefault());

            if (EditorCampaignSession.ConsumeInterruptedMarker(ReviewCaptureSessionKey))
            {
                SetValidationMessage(
                    "The previous review capture was interrupted. Capture Missing resumes with the remaining layouts.",
                    MessageType.Warning);
            }
        }

        private void OnDisable()
        {
            GenerationEvaluationRunner.Changed -= HandleRunnerChanged;
            EditorApplication.projectChanged -= HandleProjectChanged;
            EditorApplication.update -= UpdateValidationMessageTimeout;
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

            DrawScenarioSection();

            DrawReport();
            _serializedSuite.ApplyModifiedProperties();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawSuiteDropdown();
                DrawSuiteActions();

                GUILayout.FlexibleSpace();

                DrawReportDropdown();
                DrawReportActions();
            }
        }

        private void DrawSuiteActions()
        {
            using (new EditorGUI.DisabledScope(GenerationEvaluationRunner.IsRunning))
            {
                if (GUILayout.Button(
                        new GUIContent("Suite Actions", "Create an evaluation suite or clean up superseded layouts."),
                        EditorStyles.toolbarDropDown,
                        GUILayout.Width(102f)))
                    ShowSuiteActionsMenu();
            }
        }

        private void ShowSuiteActionsMenu()
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Create Suite…"), false, CreateSuite);
            menu.AddSeparator(string.Empty);

            if (_suite)
            {
                menu.AddItem(
                    new GUIContent("Clean Up Layouts…"),
                    false,
                    CleanUpEvaluationLayouts);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Clean Up Layouts…"));
            }

            menu.ShowAsContext();
        }

        private void DrawReportActions()
        {
            using (new EditorGUI.DisabledScope(!_report))
            {
                if (GUILayout.Button(new GUIContent("Export", "Export the selected report as JSON plus run, check, and aggregate CSV files including current visual ratings."), EditorStyles.toolbarButton, GUILayout.Width(48f)))
                    ExportReport();

                if (GUILayout.Button(
                        new GUIContent(
                            "Capture Missing",
                            "Capture only layouts without a complete, valid set of review images. Existing valid captures are retained."),
                        EditorStyles.toolbarButton,
                        GUILayout.Width(98f)))
                    CaptureReportReviewViews(false);

                if (GUILayout.Button(
                        new GUIContent(string.Empty, "Open additional review-capture actions."),
                        EditorStyles.toolbarDropDown,
                        GUILayout.Width(18f)))
                    ShowReviewCaptureMenu();
            }
        }

        private void ShowReviewCaptureMenu()
        {
            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("Create Review PDF"),
                false,
                () => CreateReviewPdf(true));

            string existingPdf = GenerationEvaluationReviewPdfService.GetExistingPdfPath(_report);
            if (string.IsNullOrWhiteSpace(existingPdf))
            {
                menu.AddDisabledItem(new GUIContent("Open Review PDF"));
            }
            else
            {
                menu.AddItem(
                    new GUIContent("Open Review PDF"),
                    false,
                    () => Application.OpenURL(new Uri(existingPdf).AbsoluteUri));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Recapture All…"),
                false,
                () => CaptureReportReviewViews(true));
            menu.ShowAsContext();
        }

        private void DrawSuiteDropdown(float minimumWidth = 80f)
        {
            if (_evaluationSuites.Length == 0)
            {
                DrawEmptyToolbarDropdown("No Evaluation Suites", minimumWidth, 190f);
                return;
            }

            int selectedIndex = Array.IndexOf(_evaluationSuites, _suite);
            if (selectedIndex < 0)
                selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup(
                selectedIndex,
                _evaluationSuiteOptions,
                EditorStyles.toolbarPopup,
                GUILayout.MinWidth(minimumWidth),
                GUILayout.MaxWidth(190f));
            if (EditorGUI.EndChangeCheck())
                SetSuite(_evaluationSuites[Mathf.Clamp(selectedIndex, 0, _evaluationSuites.Length - 1)]);
        }

        private void DrawReportDropdown(float minimumWidth = 260f)
        {
            if (_evaluationReports.Length == 0)
            {
                DrawEmptyToolbarDropdown("No Evaluation Reports", minimumWidth, 230f);
                return;
            }

            int selectedIndex = Array.IndexOf(_evaluationReports, _report);
            if (selectedIndex < 0)
                selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup(
                selectedIndex,
                _evaluationReportOptions,
                EditorStyles.toolbarPopup,
                GUILayout.MinWidth(minimumWidth),
                GUILayout.MaxWidth(300f));
            if (EditorGUI.EndChangeCheck())
                SetReport(_evaluationReports[Mathf.Clamp(selectedIndex, 0, _evaluationReports.Length - 1)]);
        }

        private static void DrawEmptyToolbarDropdown(string label, float minimumWidth, float maximumWidth)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Popup(
                    0,
                    new[] { label },
                    EditorStyles.toolbarPopup,
                    GUILayout.MinWidth(minimumWidth),
                    GUILayout.MaxWidth(maximumWidth));
            }
        }

        private void DrawRunPanel()
        {
            int included = _suite.Scenarios.Count(item => item is { Enabled: true });
            int totalRuns = included * _suite.RunsPerScenario;
            int savedLayouts = _suite.Scenarios.Count(item =>
                                   item is { Enabled: true, SaveLayouts: true }) *
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
                                    "Run Full Suite",
                                    $"Runs all {included} included scenarios with {_suite.RunsPerScenario} deterministic seeds each ({totalRuns} runs total)."),
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
                        $"Scenarios {included}  |  Runs {totalRuns}  |  Layouts {savedLayouts}",
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
                    EditorGUILayout.HelpBox(_validationMessage, _validationMessageType);
                if (!string.IsNullOrWhiteSpace(GenerationEvaluationRunner.LastError))
                    EditorGUILayout.HelpBox(GenerationEvaluationRunner.LastError, MessageType.Error);

                _showSuiteSettings = EditorGUILayout.Foldout(_showSuiteSettings, "Campaign Settings", true);
                if (_showSuiteSettings)
                {
                    using (new EditorGUI.DisabledScope(GenerationEvaluationRunner.IsRunning))
                    {
                        EditorGUILayout.PropertyField(
                            _serializedSuite.FindProperty("runsPerScenario"),
                            new GUIContent("Runs Per Scenario", "Independent fixed-seed observations per target scenario."));
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

        private void DrawScenarioSection()
        {
            SerializedProperty scenarios = _serializedSuite.FindProperty("scenarios");
            float contentWidth = Mathf.Max(1f, position.width - SectionHorizontalPadding * 2f);
            float listWidth = DeveloperWindowUi.ResponsiveListWidth(contentWidth, 240f, 500f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(SectionHorizontalPadding);
                EditorGUILayout.LabelField("Scenarios", EditorStyles.boldLabel, GUILayout.Width(listWidth));
                GUILayout.Space(PaneSpacing);
                EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                GUILayout.Space(SectionHorizontalPadding);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(SectionHorizontalPadding);
                DrawScenarioList(scenarios, listWidth);
                GUILayout.Space(PaneSpacing);
                DrawScenarioDetails(scenarios, contentWidth, listWidth);
                GUILayout.Space(SectionHorizontalPadding);
            }
        }

        private void DrawScenarioList(SerializedProperty scenarios, float listWidth)
        {
            float bodyHeight = ScenarioConfigurationMinHeight + ScenarioControlsHeight +
                               EditorGUIUtility.standardVerticalSpacing;
            using (new EditorGUILayout.VerticalScope(
                       GUILayout.Width(listWidth),
                       GUILayout.Height(bodyHeight)))
            {
                using (DeveloperWindowUi.VerticalScrollViewScope scrollView =
                       DeveloperWindowUi.VerticalScrollView(
                           _scenarioScroll,
                           DeveloperWindowUi.PaneStyle,
                           GUILayout.Height(ScenarioConfigurationMinHeight)))
                {
                    _scenarioScroll = scrollView.ScrollPosition;
                    for (int i = 0; i < scenarios.arraySize; i++)
                    {
                        SerializedProperty scenario = scenarios.GetArrayElementAtIndex(i);
                        string name = scenario.FindPropertyRelative("displayName").stringValue;
                        bool enabled = scenario.FindPropertyRelative("enabled").boolValue;
                        string status = !enabled ? "Excluded" : string.Empty;
                        string labelText = string.IsNullOrWhiteSpace(status) ? name : $"{name} ({status})";
                        string tooltip = enabled
                            ? "Included in the full evaluation suite."
                            : "Excluded from the full evaluation suite.";
                        GUIContent label = new(labelText, tooltip);
                        if (DeveloperWindowUi.SelectableRow(i == _selectedScenario, label, 24f, listWidth - 26f))
                            _selectedScenario = i;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledScope(GenerationEvaluationRunner.IsRunning))
                {
                    GUILayout.Space(1f);
                    if (GUILayout.Button("+", GUILayout.Height(ScenarioControlsHeight)))
                    {
                        _serializedSuite.ApplyModifiedProperties();
                        Undo.RecordObject(_suite, "Added Evaluation Scenario");
                        _suite.AddScenario(
                            "Evaluation Scenario",
                            EvaluationScenarioKind.Isolated,
                            null,
                            _generationPresets.FirstOrDefault());
                        EditorUtility.SetDirty(_suite);
                        _selectedScenario = _suite.Scenarios.Count - 1;
                        EnsureSerializedSuite(force: true);
                    }

                    using (new EditorGUI.DisabledScope(scenarios.arraySize == 0))
                    {
                        if (GUILayout.Button("-", GUILayout.Height(ScenarioControlsHeight)))
                        {
                            _serializedSuite.ApplyModifiedProperties();
                            Undo.RecordObject(_suite, "Removed Evaluation Scenario");
                            _suite.RemoveScenarioAt(_selectedScenario);
                            EditorUtility.SetDirty(_suite);
                            _selectedScenario = Mathf.Clamp(
                                _selectedScenario,
                                0,
                                Mathf.Max(0, _suite.Scenarios.Count - 1));
                            EnsureSerializedSuite(force: true);
                        }
                    }
                }
            }
        }

        private void DrawScenarioDetails(
            SerializedProperty scenarios,
            float contentWidth,
            float listWidth)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                using (DeveloperWindowUi.VerticalScrollViewScope scrollView =
                       DeveloperWindowUi.VerticalScrollView(
                           _detailsScroll,
                           DeveloperWindowUi.PaneStyle,
                           GUILayout.Height(
                               ScenarioConfigurationMinHeight +
                               ScenarioControlsHeight +
                               EditorGUIUtility.standardVerticalSpacing)))
                {
                    _detailsScroll = scrollView.ScrollPosition;
                    if (scenarios.arraySize == 0)
                        return;

                    _selectedScenario = Mathf.Clamp(_selectedScenario, 0, scenarios.arraySize - 1);
                    SerializedProperty scenario = scenarios.GetArrayElementAtIndex(_selectedScenario);

                    float previousLabelWidth = EditorGUIUtility.labelWidth;
                    float detailsWidth = Mathf.Max(
                        390f,
                        contentWidth - listWidth - 30f);
                    EditorGUIUtility.labelWidth = Mathf.Clamp(detailsWidth * 0.38f, 170f, 220f);

                    try
                    {
                        using (new EditorGUI.DisabledScope(GenerationEvaluationRunner.IsRunning))
                            DrawScenarioConfiguration(scenario);
                    }
                    finally
                    {
                        EditorGUIUtility.labelWidth = previousLabelWidth;
                    }
                }
            }
        }

        private void DrawScenarioConfiguration(SerializedProperty scenario)
        {
            EditorGUILayout.PropertyField(
                scenario.FindPropertyRelative("enabled"),
                new GUIContent("Include in Suite", "Run this scenario as part of the full evaluation suite."));
            EditorGUILayout.PropertyField(scenario.FindPropertyRelative("displayName"), new GUIContent("Name"));
            EditorGUILayout.PropertyField(
                scenario.FindPropertyRelative("kind"),
                new GUIContent("Scenario Type", "Classify the methodological role of this evaluation scenario."));
            EditorGUILayout.PropertyField(scenario.FindPropertyRelative("scene"), new GUIContent("Scene"));
            DrawTargetFields(scenario);

            EditorGUILayout.Space(7f);
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            DrawGenerationPreset(scenario.FindPropertyRelative("generationPreset"));

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);
            DrawEvaluationCriteria(scenario);
            DrawVisualEvidenceSettings(scenario);
        }

        private void DrawGenerationPreset(SerializedProperty presetProperty)
        {
            GenerationPreset selectedPreset = presetProperty.objectReferenceValue as GenerationPreset;
            GenerationPreset newPreset = AssetDropdown.DrawGenerationPresetDropdownWithEditButton(
                new GUIContent(
                    "Generation Preset",
                    "Choose the complete generator configuration used for every seed in this scenario."),
                _generationPresets,
                _generationPresetOptions,
                selectedPreset);
            if (newPreset != selectedPreset)
                presetProperty.objectReferenceValue = newPreset;
        }

        private void DrawEvaluationCriteria(SerializedProperty scenario)
        {
            _showEvaluationCriteria = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showEvaluationCriteria,
                new GUIContent("Automatic Checks", "Configure the objective assertions applied to every generated result."));
            if (_showEvaluationCriteria)
            {
                EditorGUI.indentLevel++;
                SerializedProperty checks = scenario.FindPropertyRelative("checks");
                EditorGUILayout.PropertyField(
                    checks,
                    new GUIContent("Checks", "Objective checks evaluated for every seed in this scenario."));

                if (((EvaluationCheckSet)checks.intValue & EvaluationCheckSet.Completion) != 0)
                {
                    EditorGUILayout.PropertyField(
                        scenario.FindPropertyRelative("minimumCompletionRatio"),
                        new GUIContent("Minimum Completion", "Lowest accepted placed-to-requested object ratio."));
                    EditorGUILayout.PropertyField(
                        scenario.FindPropertyRelative("maximumCompletionRatio"),
                        new GUIContent(
                            "Maximum Completion",
                            "Highest accepted placed-to-requested ratio. Reduce this only for intentionally capacity-limited scenarios."));
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawVisualEvidenceSettings(SerializedProperty scenario)
        {
            _showVisualEvidence = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showVisualEvidence,
                new GUIContent("Visual Evidence", "Control whether generated results are retained for systematic visual review."));
            if (_showVisualEvidence)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    scenario.FindPropertyRelative("saveLayouts"),
                    new GUIContent(
                        "Save Layouts for Review",
                        "Save every seed result for later visual review. Disable this only for scenarios that do not require visual evidence."));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawTargetFields(SerializedProperty scenario)
        {
            SerializedProperty sceneProperty = scenario.FindPropertyRelative("scene");
            SerializedProperty providerProperty = scenario.FindPropertyRelative("areaProviderId");
            SerializedProperty targetProperty = scenario.FindPropertyRelative("targetId");
            IReadOnlyList<IBenchmarkAreaResolver> resolvers = BenchmarkAreaResolverRegistry.CreateResolvers();

            if (resolvers.Count == 0)
            {
                EditorGUILayout.HelpBox("No evaluation target provider is installed.", MessageType.Error);
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
                IReadOnlyList<BenchmarkAreaTarget> targets = resolver?.FindTargets(activeScene) ??
                    Array.Empty<BenchmarkAreaTarget>();

                if (targets.Count > 0)
                {
                    int targetIndex = targets.ToList().FindIndex(target => target.Id == targetProperty.stringValue);
                    if (targetIndex < 0)
                        targetIndex = 0;
                    int newTargetIndex = EditorGui.Popup(
                        new GUIContent("Target Area", "Area evaluated by every run of this scenario."),
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
                        "The saved target is resolved automatically after the evaluation opens this scene. " +
                        "Open the scene to change the target."),
                    status);
            }
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
                GUILayout.Space(SectionHorizontalPadding);
                EditorGUILayout.LabelField("Evaluation Report", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                DrawReportFilters();
                GUILayout.Space(SectionHorizontalPadding);
            }

            IReadOnlyList<(GenerationEvaluationRunRecord Run, int Index)> filtered = runs
                .Select((run, index) => (run, index))
                .Where(item => MatchesReportFilters(item.run))
                .ToArray();
            if (filtered.Count > 0 && filtered.All(item => item.Index != _selectedRun))
            {
                _selectedRun = filtered[0].Index;
                _runScroll = Vector2.zero;
            }
            GenerationEvaluationRunRecord[] visibleRuns = filtered
                .Select(item => item.Run)
                .ToArray();
            int automaticFailures = visibleRuns.Count(
                run => run.AutomaticVerdict == EvaluationAutomaticVerdict.Failed);
            int automaticIncomplete = visibleRuns.Count(
                run => run.AutomaticVerdict == EvaluationAutomaticVerdict.Incomplete);
            int reviewable = visibleRuns.Count(run => run.HasLayoutReference);
            int reviewed = visibleRuns.Count(run => run.VisualReviewCompleted);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(SectionHorizontalPadding);
                EditorGUILayout.LabelField(
                    $"Showing {visibleRuns.Length:N0}/{runs.Count:N0} runs | Automatic failures {automaticFailures:N0} | Incomplete evidence {automaticIncomplete:N0} | Saved layouts reviewed {reviewed:N0}/{reviewable:N0}",
                    EditorStyles.miniLabel);
                GUILayout.Space(SectionHorizontalPadding);
            }
            bool hasCampaignMetadata = _report && _report.ExpectedRunCount > 0;
            if (hasCampaignMetadata &&
                (!_report.CampaignCompleted || runs.Count != _report.ExpectedRunCount))
            {
                string reason = _report.CampaignCancelled
                    ? "The campaign was stopped before completion."
                    : "The campaign did not complete all expected runs.";
                EditorGUILayout.HelpBox(
                    $"Partial report: {runs.Count:N0} of {_report.ExpectedRunCount:N0} runs completed. {reason}",
                    MessageType.Warning);
            }

            Rect paneStart = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint &&
                Mathf.Abs(_reportPaneTop - paneStart.y) > 0.5f)
            {
                _reportPaneTop = paneStart.y;
                Repaint();
            }

            float bottomChrome = EditorGUIUtility.standardVerticalSpacing * 3f;
            float availableHeight = _reportPaneTop > 0f
                ? position.height - _reportPaneTop - bottomChrome
                : ReportPaneHeight;
            float paneHeight = Mathf.Max(
                ReportPaneHeight,
                availableHeight);
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(paneHeight)))
            {
                GUILayout.Space(SectionHorizontalPadding);
                float contentWidth = Mathf.Max(1f, position.width - SectionHorizontalPadding * 2f);
                float listWidth = DeveloperWindowUi.ResponsiveListWidth(contentWidth, 240f, 500f);
                DrawRunList(filtered, paneHeight, listWidth);
                GUILayout.Space(PaneSpacing);
                if (filtered.Count > 0)
                    DrawRunDetails(runs, paneHeight);
                else
                    DrawEmptyRunDetails(paneHeight);
                GUILayout.Space(SectionHorizontalPadding);
            }
        }

        private void DrawReportFilters()
        {
            EditorGUI.BeginChangeCheck();

            string[] scenarioLabels = { "All scenarios", "Isolated", "Real World", "Performance" };
            int selectedScenario = _kindFilter.HasValue ? (int)_kindFilter.Value + 1 : 0;
            int changedScenario = EditorGui.Popup(
                selectedScenario,
                scenarioLabels,
                GUILayout.Width(125f));
            _kindFilter = changedScenario == 0
                ? null
                : (EvaluationScenarioKind?)(changedScenario - 1);

            _resultFilter = (AutomaticResultFilter)EditorGui.Popup(
                (int)_resultFilter,
                new[] { "All results", "Passed", "Failed", "Incomplete" },
                GUILayout.Width(115f));

            _reviewFilter = (VisualReviewFilter)EditorGui.Popup(
                (int)_reviewFilter,
                new[]
                {
                    "All review states",
                    "Needs review",
                    "Reviewed",
                    "Rating: Pass",
                    "Rating: Acceptable",
                    "Rating: Fail",
                    "No layout"
                },
                GUILayout.Width(145f));

            if (EditorGUI.EndChangeCheck())
                _runScroll = Vector2.zero;
        }

        private bool MatchesReportFilters(GenerationEvaluationRunRecord run)
        {
            if (_kindFilter.HasValue && run.scenarioKind != _kindFilter.Value.ToString())
                return false;

            bool resultMatches = _resultFilter switch
            {
                AutomaticResultFilter.Passed => run.AutomaticVerdict == EvaluationAutomaticVerdict.Passed,
                AutomaticResultFilter.Failed => run.AutomaticVerdict == EvaluationAutomaticVerdict.Failed,
                AutomaticResultFilter.Incomplete => run.AutomaticVerdict == EvaluationAutomaticVerdict.Incomplete,
                _ => true
            };
            if (!resultMatches)
                return false;

            return _reviewFilter switch
            {
                VisualReviewFilter.NeedsReview =>
                    run.HasLayoutReference && run.visualRating == EvaluationVisualRating.NotReviewed,
                VisualReviewFilter.Reviewed => run.VisualReviewCompleted,
                VisualReviewFilter.Pass =>
                    run.HasLayoutReference && run.visualRating == EvaluationVisualRating.Pass,
                VisualReviewFilter.Acceptable =>
                    run.HasLayoutReference && run.visualRating == EvaluationVisualRating.Acceptable,
                VisualReviewFilter.Fail =>
                    run.HasLayoutReference && run.visualRating == EvaluationVisualRating.Fail,
                VisualReviewFilter.NoLayout => !run.HasLayoutReference,
                _ => true
            };
        }

        private void DrawRunList(
            IReadOnlyList<(GenerationEvaluationRunRecord Run, int Index)> filtered,
            float paneHeight,
            float listWidth)
        {
            int selectedIndex = -1;
            for (int index = 0; index < filtered.Count; index++)
            {
                if (filtered[index].Index == _selectedRun)
                {
                    selectedIndex = index;
                    break;
                }
            }

            int activated = DeveloperWindowUi.VirtualizedSelectableList(
                ref _runScroll,
                filtered.Count,
                selectedIndex,
                index => CreateRunLabel(filtered[index].Run),
                listWidth,
                paneHeight,
                background: DeveloperWindowUi.PaneStyle);
            if (activated >= 0)
                _selectedRun = filtered[activated].Index;
        }

        private static GUIContent CreateRunLabel(GenerationEvaluationRunRecord run)
        {
            string automatic = run.AutomaticVerdict switch
            {
                EvaluationAutomaticVerdict.Passed => "Pass",
                EvaluationAutomaticVerdict.Incomplete => "Incomplete",
                _ => "Fail"
            };
            string review = !run.HasLayoutReference
                ? "No layout"
                : run.visualRating == EvaluationVisualRating.NotReviewed
                    ? "-"
                    : run.visualRating.ToString();
            string label = $"{automatic} | {review} | {run.scenario} | {run.seed}";
            return new GUIContent(label, label);
        }

        private static void DrawEmptyRunDetails(float paneHeight)
        {
            using (new EditorGUILayout.VerticalScope(
                       DeveloperWindowUi.PaneStyle,
                       GUILayout.ExpandWidth(true),
                       GUILayout.Height(paneHeight)))
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    "No runs match the selected filters.",
                    EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawRunDetails(IReadOnlyList<GenerationEvaluationRunRecord> runs, float paneHeight)
        {
            using (new EditorGUILayout.VerticalScope(
                       DeveloperWindowUi.PaneStyle,
                       GUILayout.ExpandWidth(true),
                       GUILayout.Height(paneHeight)))
            {
                _selectedRun = Mathf.Clamp(_selectedRun, 0, runs.Count - 1);
                GenerationEvaluationRunRecord run = runs[_selectedRun];
                bool hasLayoutReference = run.HasLayoutReference;
                bool missingLayoutAsset = run.HasMissingLayoutAsset;
                EditorGUILayout.LabelField(run.scenario, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"Seed {run.seed} | Placed {run.placedCount}/{run.requestedCount}",
                    EditorStyles.miniLabel);

                _reportDetailsScroll = EditorGUILayout.BeginScrollView(
                    _reportDetailsScroll,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
                DrawScenarioCoverage(runs, run.scenario);
                EditorGUILayout.Space(2f);
                DrawAssetCounts(run.assetCounts);
                EditorGUILayout.Space(2f);
                foreach (GenerationEvaluationCheckRecord check in run.checks)
                {
                    string violationCount = check.status == EvaluationCheckStatus.Failed
                        ? $" ({check.violations})"
                        : string.Empty;
                    EditorGUILayout.LabelField(
                        new GUIContent($"{check.status}: {check.name}{violationCount}", check.message),
                        EditorStyles.miniLabel);
                }
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
                        new GUIContent("Visual Rating", "Pass = no evaluation-relevant visible defect; Acceptable = one or more minor visible defects without invalidating the tested configuration; Fail = at least one major visible defect that invalidates the tested condition."),
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

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string contactSheet = GenerationEvaluationReviewCaptureService
                            .GetExistingContactSheetPath(run);
                        bool hasExistingCapture = !string.IsNullOrWhiteSpace(
                            GenerationEvaluationReviewCaptureService.GetExistingManifestPath(run));
                        string captureLabel = hasExistingCapture ? "Recapture Views" : "Capture Views";
                        if (GUILayout.Button(
                                new GUIContent(
                                    captureLabel,
                                    "Apply this layout and render the overview, orthographic top view, and two perpendicular side views. A 2x2 contact sheet and a hashed manifest are retained outside Assets."),
                                GUILayout.Height(25f)))
                            CaptureSelectedReviewViews();

                        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(contactSheet)))
                        {
                            if (GUILayout.Button(
                                    new GUIContent("Reveal Captures", "Reveal this run's retained review images and 2x2 contact sheet."),
                                    GUILayout.Width(120f),
                                    GUILayout.Height(25f)))
                                EditorUtility.RevealInFinder(contactSheet);
                        }
                    }
                }
            }
        }

        private void DrawScenarioCoverage(IReadOnlyList<GenerationEvaluationRunRecord> runs, string scenario)
        {
            if (!_coverageByScenario.TryGetValue(scenario, out ScenarioCoverageSnapshot coverage))
            {
                GenerationEvaluationRunRecord[] scenarioRuns = runs
                    .Where(run => string.Equals(run.scenario, scenario, StringComparison.Ordinal))
                    .ToArray();
                coverage = new ScenarioCoverageSnapshot(
                    scenarioRuns.Length,
                    GenerationEvaluationCoverage.BuildAssetCoverage(scenarioRuns),
                    GenerationEvaluationCoverage.BuildSupportCoverage(scenarioRuns));
                _coverageByScenario[scenario] = coverage;
            }

            _showScenarioCoverage = EditorGUILayout.Foldout(
                _showScenarioCoverage,
                $"Scenario Coverage ({coverage.RunCount:N0} runs)",
                true);
            if (!_showScenarioCoverage)
                return;

            DrawCoverageGroup(
                "Assets",
                coverage.Assets,
                "Occurrence across this scenario's seeds. Eligible assets with zero occurrences remain visible; this is evidence, not an automatic failure.");
            DrawCoverageGroup(
                "Supports",
                coverage.Supports,
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
            SetValidationMessage(string.Empty, MessageType.Info);
            GenerationEvaluationRunner.Start(_suite, selectedOnly ? _selectedScenario : -1);
        }

        private void ValidateSuite()
        {
            _serializedSuite.ApplyModifiedProperties();
            string validationMessage = string.Join("\n", GenerationEvaluationRunner.Validate(_suite));
            SetValidationMessage(validationMessage, MessageType.Error);
        }

        private void SetValidationMessage(string message, MessageType messageType)
        {
            _validationMessage = message ?? string.Empty;
            _validationMessageType = messageType;
            _validationMessageExpiresAt = messageType == MessageType.Info &&
                                          !string.IsNullOrWhiteSpace(_validationMessage)
                ? EditorApplication.timeSinceStartup + InfoMessageDurationSeconds
                : 0d;
        }

        private void UpdateValidationMessageTimeout()
        {
            if (_validationMessageExpiresAt <= 0d ||
                EditorApplication.timeSinceStartup < _validationMessageExpiresAt)
            {
                return;
            }

            _validationMessage = string.Empty;
            _validationMessageExpiresAt = 0d;
            Repaint();
        }

        private void CreateSuite()
        {
            AssetFileService.EnsureFolder(DevToolsContentPaths.EvaluationSuites);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{DevToolsContentPaths.EvaluationSuites}/GenerationEvaluationSuite.asset");
            GenerationEvaluationSuite suite = CreateInstance<GenerationEvaluationSuite>();
            AssetDatabase.CreateAsset(suite, path);
            AssetDatabase.SaveAssets();
            RefreshSelectableAssets();
            SetSuite(suite);
            Selection.activeObject = suite;
        }

        private void OpenRun(GenerationEvaluationRunRecord run)
        {
            string layoutAssetPath = run.ResolvedLayoutAssetPath;
            SavedLayout layout = run.LoadLayout();
            if (!layout)
            {
                SetValidationMessage(
                    $"The referenced layout asset '{run.layoutAssetPath}' is missing.",
                    MessageType.Error);
                return;
            }

            string sourceScenePath = EvaluationSceneWorkspace.ResolveSourceScenePath(
                _report?.SuiteAssetPath,
                run);
            Scene scene = SceneManager.GetActiveScene();
            if (!IsRunSceneActive(run))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                if (!EvaluationSceneWorkspace.TryPrepare(
                        sourceScenePath,
                        out string writableScenePath,
                        out string workspaceError))
                {
                    SetValidationMessage(workspaceError, MessageType.Error);
                    return;
                }

                scene = EditorSceneManager.OpenScene(writableScenePath, OpenSceneMode.Single);
            }

            layout = AssetDatabase.LoadAssetAtPath<SavedLayout>(layoutAssetPath);
            if (!layout)
            {
                SetValidationMessage(
                    $"The referenced layout asset '{layoutAssetPath}' could not be reloaded after opening the scene.",
                    MessageType.Error);
                return;
            }

            IBenchmarkAreaResolver resolver = BenchmarkAreaResolverRegistry.CreateResolvers()
                .FirstOrDefault(item => item.ProviderId == run.areaProviderId);
            IAreaSource areaSource = resolver?.Resolve(scene, run.targetId);
            string error = string.Empty;
            if (areaSource == null || !LayoutApplyService.Apply(layout, areaSource, out error))
            {
                SetValidationMessage(
                    string.IsNullOrWhiteSpace(error)
                        ? $"Could not resolve target '{run.targetId}' in '{sourceScenePath}'."
                        : error,
                    MessageType.Error);
                return;
            }

            Selection.activeObject = areaSource.ParentTransform;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private bool IsRunSceneActive(GenerationEvaluationRunRecord run)
        {
            if (run == null || string.IsNullOrWhiteSpace(run.scene))
                return false;

            string sourceScenePath = EvaluationSceneWorkspace.ResolveSourceScenePath(
                _report?.SuiteAssetPath,
                run);
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() &&
                   EvaluationSceneWorkspace.MatchesSource(activeScene.path, sourceScenePath);
        }

        private void ExportReport()
        {
            AssetDatabase.SaveAssetIfDirty(_report);
            string directory = GenerationEvaluationExporter.Export(_report.ToCampaign());
            int invalid = _report.Runs.Count(run => run.HasInvalidVisualReviewEvidence);
            int reviewable = _report.Runs.Count(run => run.HasLayoutReference);
            int valid = _report.Runs.Count(run => run.VisualReviewEvidenceValid);
            string exportWarning = invalid > 0
                ? $"Export retained {invalid:N0} run(s) with invalid visual-review evidence. Review runs.csv and summary.csv before using the export."
                : valid < reviewable
                    ? $"Export retained {reviewable - valid:N0} saved layout(s) without complete valid visual-review evidence."
                    : string.Empty;
            SetValidationMessage(exportWarning, MessageType.Warning);
            EditorUtility.RevealInFinder(directory);
        }

        private void CaptureSelectedReviewViews()
        {
            if (!_report || _selectedRun < 0 || _selectedRun >= _report.Runs.Count)
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!GenerationEvaluationReviewCaptureService.CaptureRun(
                    _report,
                    _selectedRun,
                    out string contactSheet,
                    out string error))
            {
                SetValidationMessage(error, MessageType.Error);
                return;
            }

            SetValidationMessage(
                "Captured four standardized review views and one contact sheet for the selected run.",
                MessageType.Info);
            EditorUtility.RevealInFinder(contactSheet);
        }

        private void CaptureReportReviewViews(bool recaptureAll)
        {
            if (!_report)
                return;

            int[] availableRunIndices = _report.Runs
                .Select((run, index) => new { Run = run, Index = index })
                .Where(item => item.Run.HasLayoutReference && !item.Run.HasMissingLayoutAsset)
                .Select(item => item.Index)
                .ToArray();
            if (availableRunIndices.Length == 0)
            {
                SetValidationMessage(
                    "The selected report contains no available saved layouts.",
                    MessageType.Warning);
                return;
            }

            List<int> runIndices = new(availableRunIndices.Length);
            int retained = 0;
            if (recaptureAll)
            {
                runIndices.AddRange(availableRunIndices);
            }
            else
            {
                try
                {
                    for (int index = 0; index < availableRunIndices.Length; index++)
                    {
                        int runIndex = availableRunIndices[index];
                        GenerationEvaluationRunRecord run = _report.Runs[runIndex];
                        if (EditorUtility.DisplayCancelableProgressBar(
                                "Checking Review Captures",
                                $"{index + 1:N0}/{availableRunIndices.Length:N0}: {run.scenario}, seed {run.seed}",
                                index / (float)availableRunIndices.Length))
                        {
                            SetValidationMessage("Review-capture validation cancelled.", MessageType.Info);
                            return;
                        }

                        GenerationEvaluationReviewCaptureService.ReviewCaptureStatus status =
                            GenerationEvaluationReviewCaptureService.GetCaptureStatus(
                                _report,
                                runIndex,
                                out _,
                                out _);
                        if (status == GenerationEvaluationReviewCaptureService.ReviewCaptureStatus.Valid)
                            retained++;
                        else
                            runIndices.Add(runIndex);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                if (runIndices.Count == 0)
                {
                    CreateReviewPdf(
                        true,
                        $"All {retained:N0} available layouts already have valid review captures.");
                    return;
                }
            }

            string title = recaptureAll ? "Recapture All Review Views" : "Capture Missing Review Views";
            string message = recaptureAll
                ? $"This will recapture all {runIndices.Count:N0} available layouts. Each existing capture is replaced only after its new image set is complete. Continue?"
                : $"This will capture {runIndices.Count:N0} missing or invalid layout(s) and retain {retained:N0} valid capture(s). Continue?";
            string confirmLabel = recaptureAll ? "Recapture All" : "Capture Missing";
            if (!EditorUtility.DisplayDialog(
                    title,
                    message,
                    confirmLabel,
                    "Cancel") ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            int captured = 0;
            bool completed = false;
            string firstManifest = string.Empty;
            string reportPath = AssetDatabase.GetAssetPath(_report);
            try
            {
                using (EditorCampaignSession.Begin(ReviewCaptureSessionKey))
                {
                    foreach (int runIndex in runIndices)
                    {
                        _report = AssetDatabase.LoadAssetAtPath<GenerationEvaluationReport>(reportPath);
                        GenerationEvaluationRunRecord run = _report.Runs[runIndex];
                        if (EditorUtility.DisplayCancelableProgressBar(
                                "Genix Visual Review Capture",
                                $"{captured + 1:N0}/{runIndices.Count:N0}: {run.scenario}, seed {run.seed}",
                                captured / (float)runIndices.Count))
                        {
                            SetValidationMessage(
                                $"Review capture cancelled after {captured:N0} layout(s). Capture Missing can resume the batch.",
                                MessageType.Warning);
                            break;
                        }

                        if (!GenerationEvaluationReviewCaptureService.CaptureRun(
                                _report,
                                runIndex,
                                out _,
                                out string error))
                        {
                            SetValidationMessage(
                                $"Review capture stopped after {captured:N0} layout(s). {error}",
                                MessageType.Error);
                            break;
                        }

                        captured++;
                        firstManifest = firstManifest.Length > 0
                            ? firstManifest
                            : GenerationEvaluationReviewCaptureService.GetExistingManifestPath(
                                _report.Runs[runIndex]);
                    }
                }

                if (captured == runIndices.Count)
                    completed = true;
            }
            catch (Exception exception)
            {
                SetValidationMessage(
                    $"Review capture stopped after {captured:N0} layout(s). {exception.Message}",
                    MessageType.Error);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _report = AssetDatabase.LoadAssetAtPath<GenerationEvaluationReport>(reportPath);
                if (_report)
                {
                    _selectedRun = Mathf.Clamp(_selectedRun, 0, Mathf.Max(0, _report.Runs.Count - 1));
                    AssetDatabase.SaveAssetIfDirty(_report);
                }
            }

            if (completed)
            {
                CreateReviewPdf(
                    true,
                    $"Captured four standardized views and one contact sheet for {captured:N0} layout(s).");
            }
            else if (!string.IsNullOrWhiteSpace(firstManifest))
            {
                EditorUtility.RevealInFinder(Path.GetDirectoryName(firstManifest));
            }
        }

        private bool CreateReviewPdf(bool revealInFinder, string successPrefix = "")
        {
            if (!_report)
                return false;

            try
            {
                EditorUtility.DisplayProgressBar(
                    "Creating Review PDF",
                    "Validating contact sheets and assembling PDF pages...",
                    0.5f);
                if (!GenerationEvaluationReviewPdfService.Build(
                        _report,
                        out string pdfPath,
                        out int pageCount,
                        out string error))
                {
                    SetValidationMessage(error, MessageType.Error);
                    return false;
                }

                string message = $"Created a {pageCount:N0}-page visual-review PDF.";
                if (!string.IsNullOrWhiteSpace(successPrefix))
                    message = successPrefix + " " + message;
                SetValidationMessage(message, MessageType.Info);

                if (revealInFinder)
                    EditorUtility.RevealInFinder(pdfPath);
                return true;
            }
            catch (Exception exception)
            {
                SetValidationMessage(
                    $"Could not create the review PDF: {exception.Message}",
                    MessageType.Error);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
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
                    $"Cleanup was stopped because {plan.MissingProtectedLayouts:N0} layout(s) referenced by the retained reports are already missing. Restore them or select a valid completed full-suite report before cleanup.",
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
            _coverageByScenario.Clear();
            if (!GenerationEvaluationRunner.IsRunning && GenerationEvaluationRunner.LastReport)
            {
                RefreshSelectableAssets();
                SetReport(GenerationEvaluationRunner.LastReport);
            }
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
            _coverageByScenario.Clear();
            Remember(SelectedReportKey, report);
        }

        private void RefreshSelectableAssets()
        {
            _evaluationSuites = FindProjectAssets<GenerationEvaluationSuite>();
            _evaluationSuiteOptions = EditorAssets.CreateAssetOptions(_evaluationSuites);
            _evaluationReports = FindProjectAssets<GenerationEvaluationReport>();
            _evaluationReportOptions = EditorAssets.CreateAssetOptions(_evaluationReports);
            _generationPresets = EditorAssets.LoadAssetsFromFolder<GenerationPreset>(
                ProjectContentPaths.GenerationPresets,
                (a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            _generationPresetOptions = EditorAssets.CreateAssetOptions(_generationPresets);
            Repaint();
        }

        private static T[] FindProjectAssets<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset)
                .OrderBy(asset => asset.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void HandleProjectChanged()
        {
            RefreshSelectableAssets();

            if (!_suite || !_evaluationSuites.Contains(_suite))
                SetSuite(_evaluationSuites.FirstOrDefault());
            if (!_report || !_evaluationReports.Contains(_report))
                SetReport(_evaluationReports.FirstOrDefault());
        }

        private void EnsureSerializedSuite(bool force = false)
        {
            if (force || _serializedSuite == null || _serializedSuite.targetObject != _suite)
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
