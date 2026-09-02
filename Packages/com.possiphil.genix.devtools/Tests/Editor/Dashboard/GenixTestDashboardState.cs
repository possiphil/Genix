using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Genix.Tests.Framework;
using NUnit.Framework.Interfaces;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Genix.Tests.Dashboard
{
    [Serializable]
    internal sealed class GenixTestResultRecord
    {
        [SerializeField] private string name;
        [SerializeField] private string fullName;
        [SerializeField] private string area;
        [SerializeField] private string resultState;
        [SerializeField] private string message;
        [SerializeField] private string stackTrace;
        [SerializeField] private string output;
        [SerializeField] private double durationSeconds;
        [SerializeField] private bool property;
        [SerializeField] private int propertyCasesExecuted;
        [SerializeField] private int propertyCasesExpected;
        [NonSerialized] private string cachedDisplayName;

        public string Name => name;
        public string DisplayName => cachedDisplayName ??= GenixTestDisplayName.Format(name);
        public string FullName => fullName;
        public string Area => area;
        public string ResultState => resultState;
        public string Message => message;
        public string StackTrace => stackTrace;
        public string Output => output;
        public double DurationSeconds => durationSeconds;
        public bool IsProperty => property;
        public int PropertyCasesExecuted => propertyCasesExecuted;
        public int PropertyCasesExpected => propertyCasesExpected;
        public bool Passed => resultState.StartsWith("Passed", StringComparison.Ordinal);
        public bool Failed => resultState.StartsWith("Failed", StringComparison.Ordinal);
        public bool Skipped => resultState.StartsWith("Skipped", StringComparison.Ordinal) ||
                               resultState.StartsWith("Inconclusive", StringComparison.Ordinal);

        public GenixTestResultRecord(ITestResultAdaptor result)
        {
            name = result.Name;
            fullName = result.FullName;
            area = ResolveArea(result.Test.Categories);
            resultState = result.ResultState;
            message = result.Message ?? string.Empty;
            stackTrace = result.StackTrace ?? string.Empty;
            output = result.Output ?? string.Empty;
            durationSeconds = result.Duration;
            property = result.Test.Categories?.Contains(GenixTestCategories.Property) == true;

            if (property && GenixTestExecutionMetrics.TryGetPropertyCases(result.Name, out PropertyCaseMetrics metrics))
            {
                propertyCasesExecuted = metrics.Executed;
                propertyCasesExpected = metrics.Expected;
            }
        }

        private static string ResolveArea(IEnumerable<string> categories)
        {
            const string prefix = "Genix.Area.";
            string category = categories?.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
            return string.IsNullOrWhiteSpace(category) ? "Other" : category.Substring(prefix.Length);
        }
    }

    internal static class GenixTestDisplayName
    {
        private static readonly Regex WordBoundary = new(
            @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[A-Za-z])(?=[0-9])|(?<=[0-9])(?=[A-Za-z])|_+",
            RegexOptions.Compiled);

        private static readonly Dictionary<string, string> PreferredTerms = new(
            StringComparer.OrdinalIgnoreCase)
        {
            ["api"] = "API",
            ["fscheck"] = "FsCheck",
            ["genix"] = "Genix",
            ["id"] = "ID",
            ["json"] = "JSON",
            ["nunit"] = "NUnit",
            ["obb"] = "oriented bounds",
            ["pcg"] = "PCG",
            ["poisson"] = "Poisson",
            ["sfs"] = "SFS",
            ["ui"] = "UI",
            ["unity"] = "Unity",
            ["xz"] = "XZ"
        };

        public static string Format(string testName)
        {
            if (string.IsNullOrWhiteSpace(testName))
                return string.Empty;

            int argumentsStart = testName.IndexOf('(');
            string identifier = argumentsStart >= 0 ? testName.Substring(0, argumentsStart) : testName;
            string arguments = argumentsStart >= 0 ? testName.Substring(argumentsStart) : string.Empty;
            string[] words = WordBoundary
                .Split(identifier)
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .ToArray();

            List<string> displayWords = new(words.Length);
            for (int index = 0; index < words.Length; index++)
            {
                if (index + 1 < words.Length && words[index] == "3" && words[index + 1] == "D")
                {
                    displayWords.Add("3D");
                    index++;
                    continue;
                }

                if (index + 1 < words.Length && words[index] == "N" && words[index + 1] == "Unit")
                {
                    displayWords.Add("NUnit");
                    index++;
                    continue;
                }

                string word = words[index];
                if (PreferredTerms.TryGetValue(word, out string preferred))
                    displayWords.Add(preferred);
                else
                    displayWords.Add(index == 0 ? word : word.ToLowerInvariant());
            }

            return string.Join(" ", displayWords) +
                   (string.IsNullOrEmpty(arguments) ? string.Empty : " " + arguments);
        }
    }

    [FilePath("Library/Genix/TestDashboardState.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class GenixTestDashboardState : ScriptableSingleton<GenixTestDashboardState>
    {
        [SerializeField] private bool running;
        [SerializeField] private string preset;
        [SerializeField] private string startedAt;
        [SerializeField] private string finishedAt;
        [SerializeField] private string runMessage;
        [SerializeField] private string activeRunGuid;
        [SerializeField] private double durationSeconds;
        [SerializeField] private int propertyCases;
        [SerializeField] private List<GenixTestResultRecord> results = new();

        public static event Action Changed;

        public bool Running => running;
        public string Preset => preset;
        public string StartedAt => startedAt;
        public string FinishedAt => finishedAt;
        public string RunMessage => runMessage;
        public string ActiveRunGuid => activeRunGuid;
        public double DurationSeconds => durationSeconds;
        public int PropertyCases => running ? GenixTestExecutionMetrics.PropertyCases : propertyCases;
        public int ExpectedPropertyCases => results.Sum(result => result.PropertyCasesExpected);
        public int NUnitTestCount => results.Count(result => !result.IsProperty);
        public int PropertyTestCount => results.Count(result => result.IsProperty);
        public IReadOnlyList<GenixTestResultRecord> Results => results;

        public void Begin(GenixTestPreset activePreset)
        {
            running = true;
            preset = activePreset.ToString();
            startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            finishedAt = string.Empty;
            runMessage = string.Empty;
            activeRunGuid = string.Empty;
            durationSeconds = 0d;
            propertyCases = 0;
            results.Clear();
            GenixTestExecutionMetrics.Reset();
            Persist();
        }

        public void Started(string guid)
        {
            if (!running)
                return;

            activeRunGuid = guid ?? string.Empty;
            Persist();
        }

        public void Record(ITestResultAdaptor result)
        {
            if (result == null || result.HasChildren)
                return;

            int existing = results.FindIndex(item => item.FullName == result.FullName);
            GenixTestResultRecord record = new(result);

            if (existing >= 0)
                results[existing] = record;
            else
                results.Add(record);

            propertyCases = GenixTestExecutionMetrics.PropertyCases;
            Persist();
        }

        public void Finish(ITestResultAdaptor result)
        {
            running = false;
            finishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            durationSeconds = result?.Duration ?? results.Sum(item => item.DurationSeconds);
            propertyCases = GenixTestExecutionMetrics.PropertyCases;
            runMessage = string.Empty;
            activeRunGuid = string.Empty;
            Persist();
        }

        public void FailToStart(string message)
        {
            running = false;
            finishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            durationSeconds = 0d;
            propertyCases = GenixTestExecutionMetrics.PropertyCases;
            activeRunGuid = string.Empty;
            runMessage = string.IsNullOrWhiteSpace(message)
                ? "Unity did not start the requested test run."
                : message;
            Persist();
        }

        public void MarkCancellationRequested()
        {
            runMessage = "Cancellation requested. Unity is finishing the active test cleanup.";
            Persist();
        }

        public string ToExportJson()
        {
            ExportData export = new(
                preset,
                startedAt,
                finishedAt,
                durationSeconds,
                propertyCases,
                results);
            return JsonUtility.ToJson(export, true);
        }

        private void Persist()
        {
            Save(true);
            Changed?.Invoke();
        }

        [Serializable]
        private sealed class ExportData
        {
            [SerializeField] private string schemaVersion = "2";
            [SerializeField] private string runPreset;
            [SerializeField] private string runStartedAt;
            [SerializeField] private string runFinishedAt;
            [SerializeField] private double runDurationSeconds;
            [SerializeField] private int executedPropertyCases;
            [SerializeField] private int expectedPropertyCases;
            [SerializeField] private List<GenixTestResultRecord> testResults;

            public string SchemaVersion => schemaVersion;

            public ExportData(
                string preset,
                string startedAt,
                string finishedAt,
                double durationSeconds,
                int propertyCases,
                IEnumerable<GenixTestResultRecord> results)
            {
                runPreset = preset;
                runStartedAt = startedAt;
                runFinishedAt = finishedAt;
                runDurationSeconds = durationSeconds;
                executedPropertyCases = propertyCases;
                testResults = new List<GenixTestResultRecord>(results);
                expectedPropertyCases = testResults.Sum(result => result.PropertyCasesExpected);
            }
        }
    }

    [InitializeOnLoad]
    internal static class GenixTestResultCollector
    {
        private static readonly Callbacks CallbackInstance;

        static GenixTestResultCollector()
        {
            CallbackInstance = new Callbacks();
            TestRunnerApi.RegisterTestCallback(CallbackInstance, 10);
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                if (!GenixTestDashboardState.instance.Running)
                    GenixTestDashboardState.instance.Begin(GenixTestPresetContext.Current);
            }

            public void RunFinished(ITestResultAdaptor result) =>
                GenixTestDashboardState.instance.Finish(result);

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result) =>
                GenixTestDashboardState.instance.Record(result);
        }
    }

    /// <summary>
    /// Accepts file-based test requests so local automation can run the same presets while this
    /// Unity project remains open in the editor.
    /// </summary>
    [InitializeOnLoad]
    internal static class GenixOpenEditorTestBridge
    {
        private const string RequestPath = "Library/Genix/TestCommandRequest.json";
        private const string ResponsePath = "Library/Genix/TestCommandResponse.json";
        private const string RefreshSessionKey = "Genix.OpenEditorTestBridge.RefreshPending";
        private const string ActiveRunSessionKey = "Genix.OpenEditorTestBridge.ActiveRun";
        private const double PollIntervalSeconds = 0.25d;

        private static TestRunnerApi _runner;
        private static bool _ownsActiveRun;
        private static double _nextPollTime;

        static GenixOpenEditorTestBridge()
        {
            bool interruptedByReload = SessionState.GetBool(ActiveRunSessionKey, false);
            _ownsActiveRun = false;
            SessionState.SetBool(ActiveRunSessionKey, false);

            if (GenixTestDashboardState.instance.Running)
            {
                GenixTestDashboardState.instance.FailToStart(
                    interruptedByReload
                        ? "The previous test run was interrupted by a Unity domain reload."
                        : "The previous test run ended without a completion callback.");
            }

            GenixTestDashboardState.Changed += OnDashboardStateChanged;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPollTime)
                return;

            _nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSeconds;

            if (_ownsActiveRun ||
                GenixTestDashboardState.instance.Running ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            if (!File.Exists(RequestPath))
            {
                SessionState.SetBool(RefreshSessionKey, false);
                return;
            }

            // Local packages are not always auto-refreshed while Unity is in the background. Keep
            // the request across the possible domain reload and consume it on the following poll.
            if (!SessionState.GetBool(RefreshSessionKey, false))
            {
                SessionState.SetBool(RefreshSessionKey, true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return;
            }

            SessionState.SetBool(RefreshSessionKey, false);

            TestCommandRequest request;

            try
            {
                request = JsonUtility.FromJson<TestCommandRequest>(File.ReadAllText(RequestPath));
                File.Delete(RequestPath);
            }
            catch (Exception exception)
            {
                WriteErrorResponse($"Could not read the test request: {exception.Message}");
                return;
            }

            if (request == null ||
                !Enum.TryParse(request.preset, true, out GenixTestPreset preset))
            {
                WriteErrorResponse("Preset must be Quick, Full, or Stress.");
                return;
            }

            Start(preset);
        }

        private static void Start(GenixTestPreset preset)
        {
            string[] categories = preset switch
            {
                GenixTestPreset.Quick => new[] { GenixTestCategories.Quick },
                GenixTestPreset.Full => new[] { GenixTestCategories.Full },
                GenixTestPreset.Stress => new[] { GenixTestCategories.Full, GenixTestCategories.Stress },
                _ => Array.Empty<string>()
            };
            string[] assemblies = GenixTestAssemblyDiscovery.GetLoadedAssemblyNames();
            GenixTestPresetContext.Current = preset;
            GenixTestDashboardState state = GenixTestDashboardState.instance;
            state.Begin(preset);
            _ownsActiveRun = true;
            SessionState.SetBool(ActiveRunSessionKey, true);
            _runner = ScriptableObject.CreateInstance<TestRunnerApi>();

            try
            {
                string guid = _runner.Execute(new ExecutionSettings(new Filter
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = assemblies,
                    categoryNames = categories
                }));

                if (string.IsNullOrWhiteSpace(guid))
                    state.FailToStart("Unity returned no run identifier for the file-based test request.");
                else
                    state.Started(guid);
            }
            catch (Exception exception)
            {
                state.FailToStart($"Unity could not start the file-based test run: {exception.Message}");
            }
        }

        private static void OnDashboardStateChanged()
        {
            if (!_ownsActiveRun || GenixTestDashboardState.instance.Running)
                return;

            WriteResponse(GenixTestDashboardState.instance.ToExportJson());
            _ownsActiveRun = false;
            SessionState.SetBool(ActiveRunSessionKey, false);

            if (_runner)
                UnityEngine.Object.DestroyImmediate(_runner);

            _runner = null;
        }

        private static void WriteErrorResponse(string message)
        {
            WriteResponse(JsonUtility.ToJson(new TestCommandError(message), true));
        }

        private static void WriteResponse(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResponsePath) ?? "Library/Genix");
            string temporaryPath = ResponsePath + ".tmp";
            File.WriteAllText(temporaryPath, json);

            if (File.Exists(ResponsePath))
                File.Delete(ResponsePath);

            File.Move(temporaryPath, ResponsePath);
        }

        [Serializable]
        private sealed class TestCommandRequest
        {
            public string preset = string.Empty;
        }

        [Serializable]
        private sealed class TestCommandError
        {
            [SerializeField] private string error;

            public TestCommandError(string value) => error = value;
        }
    }
}
