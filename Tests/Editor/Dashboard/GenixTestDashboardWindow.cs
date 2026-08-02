using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static readonly string[] KnownTestAssemblies =
        {
            "Genix.Tests.Editor",
            "Genix.Tests.SpaceFoundation.Editor"
        };

        private readonly Dictionary<string, bool> _foldouts = new();
        private Vector2 _scroll;
        private string _search = string.Empty;
        private string _areaFilter = "All";
        private ResultTypeFilter _typeFilter;
        private ResultStatusFilter _statusFilter;
        private GenixTestPreset _preset;
        private GenixTestResultRecord _selected;
        private TestRunnerApi _runner;

        [MenuItem("Tools/Genix/Test Dashboard")]
        private static void Open() => GetWindow<GenixTestDashboardWindow>("Genix Tests");

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
            DrawCategories();
            DrawSelectedResult();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                _preset = (GenixTestPreset)EditorGUILayout.EnumPopup(
                    new GUIContent("Preset", "Quick runs the fast smoke suite. Full adds property, workflow, and snapshot tests. Stress adds high-volume robustness runs. Performance runs repeatable benchmarks only."),
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

        private static void DrawSummary()
        {
            GenixTestDashboardState state = GenixTestDashboardState.instance;
            int passed = state.Results.Count(result => result.Passed);
            int failed = state.Results.Count(result => result.Failed);
            int skipped = state.Results.Count(result => result.Skipped);
            int nunitPassed = state.Results.Count(result => !result.IsProperty && result.Passed);
            int propertyPassed = state.Results.Count(result => result.IsProperty && result.Passed);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStatusDot(GetAggregateColor(state.Results));
                    GUILayout.Label(state.Running ? "Running" : state.Results.Count == 0 ? "Not run" : "Last run", EditorStyles.boldLabel, GUILayout.Width(70f));
                    GUILayout.Label($"Preset: {state.Preset}", GUILayout.Width(120f));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{state.DurationSeconds * 1000d:0.0} ms", GUILayout.Width(90f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(22f);
                    GUILayout.Label($"Total: {passed:N0}/{state.Results.Count:N0}", GUILayout.Width(112f));
                    GUILayout.Label($"NUnit: {nunitPassed:N0}/{state.NUnitTestCount:N0}", GUILayout.Width(120f));
                    GUILayout.Label($"Properties: {propertyPassed:N0}/{state.PropertyTestCount:N0}", GUILayout.Width(132f));
                    GUILayout.Label(
                        $"Property Cases: {state.PropertyCases:N0}/{state.ExpectedPropertyCases:N0}",
                        EditorStyles.boldLabel,
                        GUILayout.Width(205f));
                    GUILayout.Label($"Failed: {failed:N0}", GUILayout.Width(82f));
                    GUILayout.Label($"Skipped: {skipped:N0}", GUILayout.Width(88f));
                    GUILayout.FlexibleSpace();
                }
            }

            if (!string.IsNullOrWhiteSpace(state.RunMessage))
                EditorGUILayout.HelpBox(state.RunMessage, state.Running ? MessageType.Info : MessageType.Warning);
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

            foreach (IGrouping<string, GenixTestResultRecord> group in filteredResults
                         .OrderBy(result => result.Area, StringComparer.Ordinal)
                         .ThenBy(result => result.Name, StringComparer.Ordinal)
                         .GroupBy(result => result.Area))
            {
                List<GenixTestResultRecord> categoryResults = group.ToList();
                int passed = categoryResults.Count(result => result.Passed);
                int failed = categoryResults.Count(result => result.Failed);
                int propertyCases = categoryResults.Sum(result => result.PropertyCasesExecuted);
                int propertyTarget = categoryResults.Sum(result => result.PropertyCasesExpected);
                bool expanded = _foldouts.TryGetValue(group.Key, out bool value) && value;

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStatusDot(GetAggregateColor(categoryResults));
                    expanded = EditorGUILayout.Foldout(
                        expanded,
                        $"{group.Key}  {passed}/{categoryResults.Count} passed" +
                        (propertyTarget > 0 ? $", {propertyCases:N0}/{propertyTarget:N0} property cases" : string.Empty) +
                        (failed > 0 ? $", {failed} failed" : string.Empty),
                        true,
                        EditorStyles.foldoutHeader);

                    using (new EditorGUI.DisabledScope(GenixTestDashboardState.instance.Running))
                    {
                        if (GUILayout.Button(new GUIContent("Run", $"Repeat the {group.Key} tests shown in this result set."), GUILayout.Width(48f)))
                            RunTests(categoryResults.Select(result => result.FullName).ToArray(), GenixTestPresetContext.Current);
                    }
                }

                _foldouts[group.Key] = expanded;

                if (!expanded)
                    continue;

                foreach (GenixTestResultRecord result in categoryResults)
                    DrawTestRow(result);
            }
        }

        private void DrawTestRow(GenixTestResultRecord result)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(22f);
                DrawStatusDot(GetResultColor(result));
                GUIStyle style = _selected == result ? EditorStyles.boldLabel : EditorStyles.label;

                if (GUILayout.Button(result.Name, style))
                    _selected = result;

                GUILayout.FlexibleSpace();
                GUILayout.Label(result.IsProperty ? "Property" : "NUnit", EditorStyles.miniLabel, GUILayout.Width(52f));

                if (result.IsProperty)
                {
                    GUILayout.Label(
                        $"{result.PropertyCasesExecuted:N0}/{result.PropertyCasesExpected:N0} cases",
                        GUILayout.Width(112f));
                }

                GUILayout.Label($"{result.DurationSeconds * 1000d:0.###} ms", GUILayout.Width(82f));
            }
        }

        private void DrawSelectedResult()
        {
            if (_selected == null)
                return;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Test Details", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_selected.FullName, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.LabelField("Type", _selected.IsProperty ? "Property" : "NUnit");

            if (_selected.IsProperty)
            {
                EditorGUILayout.LabelField(
                    "Generated Cases",
                    $"{_selected.PropertyCasesExecuted:N0} of {_selected.PropertyCasesExpected:N0}");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(GenixTestDashboardState.instance.Running))
                {
                    if (GUILayout.Button("Run Test", GUILayout.Width(86f)))
                        RunTests(new[] { _selected.FullName }, GenixTestPresetContext.Current);
                }

                if (GUILayout.Button("Open Source", GUILayout.Width(96f)))
                    OpenSource(_selected.FullName);
            }

            if (!string.IsNullOrWhiteSpace(_selected.Message))
                EditorGUILayout.HelpBox(_selected.Message, _selected.Failed ? MessageType.Error : MessageType.Info);

            if (!string.IsNullOrWhiteSpace(_selected.Output))
            {
                EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_selected.Output, GUILayout.MinHeight(60f));
            }

            if (!string.IsNullOrWhiteSpace(_selected.StackTrace))
            {
                EditorGUILayout.LabelField("Stack Trace", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_selected.StackTrace, GUILayout.MinHeight(90f));
            }
        }

        private void RunPreset(GenixTestPreset preset)
        {
            string[] categories = preset switch
            {
                GenixTestPreset.Quick => new[] { GenixTestCategories.Quick },
                GenixTestPreset.Full => new[] { GenixTestCategories.Full },
                GenixTestPreset.Stress => new[] { GenixTestCategories.Full, GenixTestCategories.Stress },
                GenixTestPreset.Performance => new[] { GenixTestCategories.Performance },
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

        private static string[] GetAvailableTestAssemblies()
        {
            HashSet<string> loadedAssemblies = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetName().Name)
                .ToHashSet(StringComparer.Ordinal);

            return KnownTestAssemblies
                .Where(loadedAssemblies.Contains)
                .ToArray();
        }

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
                 result.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 result.FullName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private void Run(Filter filter, GenixTestPreset preset)
        {
            _selected = null;
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
