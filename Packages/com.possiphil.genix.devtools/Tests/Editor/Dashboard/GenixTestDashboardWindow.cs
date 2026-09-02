using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Editor.Utilities;
using Genix.Tests.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Genix.Tests.Dashboard
{
    /// <summary>Developer-facing test overview backed by Unity's standard TestRunnerApi.</summary>
    internal sealed class GenixTestDashboardWindow : EditorWindow
    {
        private enum ResultTypeFilter
        {
            All,
            NUnit,
            Property
        }

        private enum ResultStatusFilter
        {
            All,
            Passed,
            Failed,
            Skipped
        }

        private const string PresetPreference = "Genix.Tests.DashboardPreset.v2";
        private const float MinimumWindowWidth = 480f;
        private const float MinimumWindowHeight = 420f;
        private const float MinimumTestListWidth = 480f;
        private const float TestListHorizontalPadding = 6f;
        private const float MinimumSummaryWidth = 480f;
        private const float SummaryHorizontalPadding = 6f;
        private const float SummaryStatsIndent = 22f;
        private const float SummaryStatsSpacing = 8f;
        private const float SummaryInnerPadding = 12f;
        private const float CategoryLeftPadding = 4f;
        private const float CategoryGroupSpacing = 2f;
        private const float TestRowHeight = 18f;
        private const float TestActionWidth = 48f;
        private const float TestRowRightPadding = 4f;
        private const float TestDurationWidth = 82f;
        private const float TestCasesWidth = 104f;
        private const float TestTypeWidth = 54f;
        private const float TestColumnSpacing = 4f;
        private readonly Dictionary<string, bool> _foldouts = new();
        private Vector2 _scroll;
        private string _search = string.Empty;
        private string _areaFilter = "All";
        private ResultTypeFilter _typeFilter;
        private ResultStatusFilter _statusFilter;
        private GenixTestPreset _preset;
        private TestRunnerApi _runner;
        private GUIStyle _testNameStyle;

        [MenuItem("Tools/Genix Developer/Tests", false, 40)]
        private static void Open()
        {
            GenixTestDashboardWindow window = GenixWindowDocking.Open<GenixTestDashboardWindow>("Genix Tests");
            window.minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
        }

        private void OnEnable()
        {
            int savedPreset = EditorPrefs.GetInt(PresetPreference, (int)GenixTestPreset.Quick);
            _preset = Enum.IsDefined(typeof(GenixTestPreset), savedPreset)
                ? (GenixTestPreset)savedPreset
                : GenixTestPreset.Quick;

            GenixTestDashboardState state = GenixTestDashboardState.instance;

            if (state.Running && string.IsNullOrWhiteSpace(state.ActiveRunGuid))
                state.FailToStart("Recovered an interrupted test start. No Unity test run is active.");

            GenixTestDashboardState.Changed += Repaint;
        }

        private void OnDisable()
        {
            GenixTestDashboardState.Changed -= Repaint;

            if (_runner)
                DestroyImmediate(_runner);

            _runner = null;
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSummary();
            DrawFilters();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            float testListWidth = Mathf.Max(
                MinimumTestListWidth,
                position.width - TestListHorizontalPadding);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(testListWidth)))
                DrawCategories();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                _preset = (GenixTestPreset)EditorGUILayout.EnumPopup(
                    new GUIContent("Preset", "Quick runs the fast smoke suite. Full adds property, workflow, and snapshot tests. Stress adds high-volume robustness runs. Scene performance is measured separately in Genix Benchmarks."),
                    _preset,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(380f));

                if (EditorGUI.EndChangeCheck())
                    EditorPrefs.SetInt(PresetPreference, (int)_preset);

                GenixTestDashboardState state = GenixTestDashboardState.instance;

                using (new EditorGUI.DisabledScope(state.Running))
                {
                    if (GUILayout.Button(new GUIContent("Run", "Run the selected preset through Unity Test Framework."), EditorStyles.toolbarButton, GUILayout.Width(52f)))
                        RunPreset(_preset);
                }

                using (new EditorGUI.DisabledScope(!state.Running))
                {
                    if (GUILayout.Button(new GUIContent("Stop", "Cancel the active Unity test run."), EditorStyles.toolbarButton, GUILayout.Width(52f)))
                        StopRun();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Unity Runner", "Open Unity's built-in Test Runner for its native hierarchy and controls."), EditorStyles.toolbarButton, GUILayout.Width(86f)))
                    EditorApplication.ExecuteMenuItem("Window/General/Test Runner");

                if (GUILayout.Button(new GUIContent("Coverage", "Open Unity's Code Coverage window. Enable recording there, then run Quick or Full from this dashboard."), EditorStyles.toolbarButton, GUILayout.Width(68f)))
                    EditorApplication.ExecuteMenuItem("Window/Analysis/Code Coverage");

                using (new EditorGUI.DisabledScope(state.Results.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Export", "Export the latest detailed result set as JSON."), EditorStyles.toolbarButton, GUILayout.Width(58f)))
                        ExportResults();
                }
            }
        }

        private void DrawSummary()
        {
            GenixTestDashboardState state = GenixTestDashboardState.instance;
            int passed = state.Results.Count(result => result.Passed);
            int failed = state.Results.Count(result => result.Failed);
            int skipped = state.Results.Count(result => result.Skipped);
            int nunitPassed = state.Results.Count(result => !result.IsProperty && result.Passed);
            int propertyPassed = state.Results.Count(result => result.IsProperty && result.Passed);

            float availableWidth = position.width - SummaryHorizontalPadding;
            float summaryWidth = Mathf.Max(MinimumSummaryWidth, availableWidth);
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.Width(summaryWidth)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStatusDot(GetAggregateColor(state.Results));
                    GUILayout.Label(state.Running ? "Running" : state.Results.Count == 0 ? "Not run" : "Last run", EditorStyles.boldLabel, GUILayout.Width(70f));
                    GUILayout.Label($"Preset: {state.Preset}", GUILayout.Width(120f));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{state.DurationSeconds * 1000d:0.0} ms", GUILayout.Width(90f));
                }

                string[] stats =
                {
                    $"Total: {passed:N0}/{state.Results.Count:N0}",
                    $"NUnit: {nunitPassed:N0}/{state.NUnitTestCount:N0}",
                    $"Properties: {propertyPassed:N0}/{state.PropertyTestCount:N0}",
                    $"Property Cases: {state.PropertyCases:N0}/{state.ExpectedPropertyCases:N0}",
                    $"Failed: {failed:N0}",
                    $"Skipped: {skipped:N0}"
                };
                float[] widths = { 110f, 110f, 130f, 200f, 82f, 88f };
                float statsWidth = summaryWidth - SummaryStatsIndent - SummaryInnerPadding;
                DrawWrappedSummaryStats(stats, widths, statsWidth);
            }

            if (!string.IsNullOrWhiteSpace(state.RunMessage))
                EditorGUILayout.HelpBox(state.RunMessage, state.Running ? MessageType.Info : MessageType.Warning);
        }

        private static void DrawWrappedSummaryStats(
            IReadOnlyList<string> stats,
            IReadOnlyList<float> widths,
            float availableWidth)
        {
            int start = 0;

            while (start < stats.Count)
            {
                int end = start;
                float usedWidth = 0f;

                while (end < stats.Count)
                {
                    float nextWidth = widths[end] + (end > start ? SummaryStatsSpacing : 0f);
                    if (end > start && usedWidth + nextWidth > availableWidth)
                        break;

                    usedWidth += nextWidth;
                    end++;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(SummaryStatsIndent);

                    for (int index = start; index < end; index++)
                    {
                        if (index > start)
                            GUILayout.Space(SummaryStatsSpacing);

                        GUIStyle style = index == 3 ? EditorStyles.boldLabel : EditorStyles.label;
                        GUILayout.Label(stats[index], style, GUILayout.Width(widths[index]));
                    }

                    GUILayout.FlexibleSpace();
                }

                start = end;
            }
        }

        private void DrawFilters()
        {
            IReadOnlyList<GenixTestResultRecord> results = GenixTestDashboardState.instance.Results;
            string[] areas = new[] { "All" }
                .Concat(results.Select(result => result.Area).Distinct().OrderBy(value => value, StringComparer.Ordinal))
                .ToArray();
            int areaIndex = Math.Max(0, Array.IndexOf(areas, _areaFilter));
            int propertyCount = results.Count(result => result.IsProperty);
            int nunitCount = results.Count - propertyCount;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Show", GUILayout.Width(34f));
                _typeFilter = (ResultTypeFilter)GUILayout.Toolbar(
                    (int)_typeFilter,
                    new[]
                    {
                        $"All ({results.Count:N0})",
                        $"NUnit ({nunitCount:N0})",
                        $"Property ({propertyCount:N0})"
                    },
                    EditorStyles.toolbarButton,
                    GUILayout.Width(300f));
                _statusFilter = (ResultStatusFilter)EditorGUILayout.EnumPopup(
                    _statusFilter,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(82f));
                areaIndex = EditorGUILayout.Popup(areaIndex, areas, EditorStyles.toolbarPopup, GUILayout.Width(120f));
                _areaFilter = areas[areaIndex];
                GUILayout.FlexibleSpace();
                _search = GUILayout.TextField(
                    _search,
                    EditorStyles.toolbarSearchField,
                    GUILayout.MinWidth(160f),
                    GUILayout.MaxWidth(280f));
            }

            List<GenixTestResultRecord> filtered = GetFilteredResults().ToList();
            int propertyCases = filtered.Sum(result => result.PropertyCasesExecuted);
            int propertyTarget = filtered.Sum(result => result.PropertyCasesExpected);
            string caseText = propertyTarget > 0
                ? $", {propertyCases:N0}/{propertyTarget:N0} property cases"
                : string.Empty;

            EditorGUILayout.LabelField(
                $"Showing {filtered.Count:N0} of {results.Count:N0} tests{caseText}",
                EditorStyles.miniLabel);
        }

        private void DrawCategories()
        {
            IReadOnlyList<GenixTestResultRecord> results = GenixTestDashboardState.instance.Results;

            if (results.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    GenixTestDashboardState.instance.Running
                        ? "Waiting for the first test result..."
                        : "Choose a preset and run it. Quick is selected by default for fast feedback; tests never start automatically.",
                    MessageType.Info);
                return;
            }

            List<GenixTestResultRecord> filteredResults = GetFilteredResults().ToList();

            if (filteredResults.Count == 0)
            {
                EditorGUILayout.HelpBox("No results match the active filters.", MessageType.Info);
                return;
            }

            bool firstCategory = true;
            foreach (IGrouping<string, GenixTestResultRecord> group in filteredResults
                         .OrderBy(result => result.Area, StringComparer.Ordinal)
                         .ThenBy(result => result.DisplayName, StringComparer.Ordinal)
                         .GroupBy(result => result.Area))
            {
                if (!firstCategory)
                    GUILayout.Space(CategoryGroupSpacing);

                firstCategory = false;
                List<GenixTestResultRecord> categoryResults = group.ToList();
                int passed = categoryResults.Count(result => result.Passed);
                int failed = categoryResults.Count(result => result.Failed);
                int propertyCases = categoryResults.Sum(result => result.PropertyCasesExecuted);
                int propertyTarget = categoryResults.Sum(result => result.PropertyCasesExpected);
                bool expanded = _foldouts.TryGetValue(group.Key, out bool value) && value;

                expanded = DrawCategoryRow(
                    group.Key,
                    passed,
                    categoryResults,
                    propertyCases,
                    propertyTarget,
                    failed,
                    expanded);

                _foldouts[group.Key] = expanded;

                if (!expanded)
                    continue;

                GUILayout.Space(CategoryGroupSpacing);

                foreach (GenixTestResultRecord result in categoryResults)
                    DrawTestRow(result);
            }
        }

        private bool DrawCategoryRow(
            string categoryName,
            int passed,
            IReadOnlyList<GenixTestResultRecord> results,
            int propertyCases,
            int propertyTarget,
            int failed,
            bool expanded)
        {
            Rect rowRect = GUILayoutUtility.GetRect(
                0f,
                TestRowHeight,
                GUIStyle.none,
                GUILayout.ExpandWidth(true));
            float left = rowRect.x + CategoryLeftPadding;
            EditorGUI.DrawRect(new Rect(left, rowRect.y + 4f, 10f, 10f), GetAggregateColor(results));
            left += 16f;

            float right = rowRect.xMax - TestRowRightPadding;
            Rect runRect = new(right - TestActionWidth, rowRect.y, TestActionWidth, rowRect.height);
            Rect foldoutRect = new(
                left,
                rowRect.y,
                Mathf.Max(0f, runRect.x - TestColumnSpacing - left),
                rowRect.height);
            string label = $"{categoryName}  {passed}/{results.Count} passed" +
                           (propertyTarget > 0 ? $", {propertyCases:N0}/{propertyTarget:N0} property cases" : string.Empty) +
                           (failed > 0 ? $", {failed} failed" : string.Empty);
            expanded = EditorGUI.Foldout(
                foldoutRect,
                expanded,
                label,
                true,
                EditorStyles.foldoutHeader);

            using (new EditorGUI.DisabledScope(GenixTestDashboardState.instance.Running))
            {
                if (GUI.Button(
                        runRect,
                        new GUIContent("Run", $"Repeat the {categoryName} tests shown in this result set."),
                        EditorStyles.miniButton))
                {
                    RunTests(results.Select(result => result.FullName).ToArray(), GenixTestPresetContext.Current);
                }
            }

            return expanded;
        }

        private void DrawTestRow(GenixTestResultRecord result)
        {
            Rect rowRect = GUILayoutUtility.GetRect(
                0f,
                TestRowHeight,
                GUIStyle.none,
                GUILayout.ExpandWidth(true));
            float left = rowRect.x + 22f;
            Rect statusRect = new(left, rowRect.y + 4f, 10f, 10f);
            EditorGUI.DrawRect(statusRect, GetResultColor(result));
            left += 16f;

            float right = rowRect.xMax - TestRowRightPadding;
            Rect openRect = TakeRightColumn(ref right, rowRect, TestActionWidth);
            Rect runRect = TakeRightColumn(ref right, rowRect, TestActionWidth);
            Rect durationRect = TakeRightColumn(ref right, rowRect, TestDurationWidth);
            Rect casesRect = TakeRightColumn(ref right, rowRect, TestCasesWidth);
            Rect typeRect = TakeRightColumn(ref right, rowRect, TestTypeWidth);
            Rect nameRect = new(left, rowRect.y, Mathf.Max(0f, right - left), rowRect.height);

            _testNameStyle ??= new GUIStyle(EditorStyles.label)
            {
                clipping = TextClipping.Ellipsis
            };
            EditorGUI.LabelField(
                nameRect,
                new GUIContent(result.DisplayName, result.FullName),
                _testNameStyle);
            EditorGUI.LabelField(
                typeRect,
                result.IsProperty ? "Property" : "NUnit",
                EditorStyles.centeredGreyMiniLabel);
            if (result.IsProperty)
            {
                EditorGUI.LabelField(
                    casesRect,
                    $"{result.PropertyCasesExecuted:N0}/{result.PropertyCasesExpected:N0} cases",
                    EditorStyles.miniLabel);
            }

            EditorGUI.LabelField(
                durationRect,
                $"{result.DurationSeconds * 1000d:0.###} ms",
                EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(GenixTestDashboardState.instance.Running))
            {
                if (GUI.Button(
                        runRect,
                        new GUIContent("Run", $"Run only {result.FullName}."),
                        EditorStyles.miniButton))
                {
                    RunTests(new[] { result.FullName }, GenixTestPresetContext.Current);
                }
            }

            if (GUI.Button(
                    openRect,
                    new GUIContent("Open", $"Open the source file for {result.FullName}."),
                    EditorStyles.miniButton))
                OpenSource(result.FullName);
        }

        private static Rect TakeRightColumn(ref float right, Rect rowRect, float width)
        {
            right -= width;
            Rect column = new(right, rowRect.y, width, rowRect.height);
            right -= TestColumnSpacing;
            return column;
        }

        private void RunPreset(GenixTestPreset preset)
        {
            string[] categories = preset switch
            {
                GenixTestPreset.Quick => new[] { GenixTestCategories.Quick },
                GenixTestPreset.Full => new[] { GenixTestCategories.Full },
                GenixTestPreset.Stress => new[] { GenixTestCategories.Full, GenixTestCategories.Stress },
                _ => Array.Empty<string>()
            };

            if (categories.Length == 0)
                return;

            GenixTestPresetContext.Current = preset;
            Run(new Filter
            {
                testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
                assemblyNames = GetAvailableTestAssemblies(),
                categoryNames = categories
            }, preset);
        }

        private void RunTests(string[] testNames, GenixTestPreset preset)
        {
            Run(new Filter
            {
                testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
                assemblyNames = GetAvailableTestAssemblies(),
                testNames = testNames
            }, preset);
        }

        private static string[] GetAvailableTestAssemblies() =>
            GenixTestAssemblyDiscovery.GetLoadedAssemblyNames();

        private IEnumerable<GenixTestResultRecord> GetFilteredResults()
        {
            return GenixTestDashboardState.instance.Results.Where(result =>
                (_typeFilter == ResultTypeFilter.All ||
                 _typeFilter == ResultTypeFilter.Property && result.IsProperty ||
                 _typeFilter == ResultTypeFilter.NUnit && !result.IsProperty) &&
                (_statusFilter == ResultStatusFilter.All ||
                 _statusFilter == ResultStatusFilter.Passed && result.Passed ||
                 _statusFilter == ResultStatusFilter.Failed && result.Failed ||
                 _statusFilter == ResultStatusFilter.Skipped && result.Skipped) &&
                (_areaFilter == "All" || result.Area == _areaFilter) &&
                (string.IsNullOrWhiteSpace(_search) ||
                 result.DisplayName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 result.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 result.FullName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private void Run(Filter filter, GenixTestPreset preset)
        {
            GenixTestPresetContext.Current = preset;
            GenixTestDashboardState state = GenixTestDashboardState.instance;
            state.Begin(preset);

            if (!_runner)
                _runner = CreateInstance<TestRunnerApi>();

            try
            {
                string guid = _runner.Execute(new ExecutionSettings(filter));

                if (string.IsNullOrWhiteSpace(guid))
                {
                    state.FailToStart("Unity returned no run identifier, so the test run was not started.");
                    return;
                }

                state.Started(guid);
            }
            catch (Exception exception)
            {
                state.FailToStart($"Unity could not start the test run: {exception.Message}");
                Debug.LogException(exception);
            }
        }

        private static void StopRun()
        {
            GenixTestDashboardState state = GenixTestDashboardState.instance;
            string guid = state.ActiveRunGuid;
            bool cancellationRequested = false;

            if (!string.IsNullOrWhiteSpace(guid))
                cancellationRequested = TestRunnerApi.CancelTestRun(guid);

            if (cancellationRequested)
                state.MarkCancellationRequested();
            else
                state.FailToStart("No active Unity test run was found. The stale dashboard state has been cleared.");
        }

        private static void ExportResults()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Genix Test Results",
                string.Empty,
                $"genix-tests-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                "json");

            if (string.IsNullOrWhiteSpace(path))
                return;

            File.WriteAllText(path, GenixTestDashboardState.instance.ToExportJson());
            EditorUtility.RevealInFinder(path);
        }

        private static void OpenSource(string fullName)
        {
            int methodSeparator = fullName.LastIndexOf('.');

            if (methodSeparator <= 0)
                return;

            string fixtureName = fullName.Substring(0, methodSeparator);
            int fixtureSeparator = fixtureName.LastIndexOf('.');
            string shortName = fixtureSeparator >= 0 ? fixtureName.Substring(fixtureSeparator + 1) : fixtureName;

            foreach (string guid in AssetDatabase.FindAssets($"{shortName} t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script && script.GetClass()?.FullName == fixtureName)
                {
                    AssetDatabase.OpenAsset(script);
                    return;
                }
            }
        }

        private static Color GetAggregateColor(IEnumerable<GenixTestResultRecord> results)
        {
            List<GenixTestResultRecord> values = results.ToList();

            if (values.Count == 0)
                return new Color(0.5f, 0.5f, 0.5f);

            if (values.Any(result => result.Failed))
                return new Color(0.85f, 0.22f, 0.2f);

            if (values.All(result => result.Passed))
                return new Color(0.25f, 0.72f, 0.34f);

            return new Color(0.95f, 0.68f, 0.18f);
        }

        private static Color GetResultColor(GenixTestResultRecord result) =>
            result.Failed
                ? new Color(0.85f, 0.22f, 0.2f)
                : result.Passed
                    ? new Color(0.25f, 0.72f, 0.34f)
                    : new Color(0.95f, 0.68f, 0.18f);

        private static void DrawStatusDot(Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.Width(12f), GUILayout.Height(18f));
            rect.y += 4f;
            rect.height = 10f;
            rect.width = 10f;
            EditorGUI.DrawRect(rect, color);
        }
    }
}
