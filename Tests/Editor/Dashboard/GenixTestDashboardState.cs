using System;
using System.Collections.Generic;
using System.Linq;
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

        public string Name => name;
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
}
