using FsCheck;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using System;

namespace Genix.Tests.Framework
{
    /// <summary>Runs FsCheck properties with the active Genix preset and replayable failure output.</summary>
    internal static class GenixProperty
    {
        public static void Check(string name, Property property)
        {
            int expectedCases = GenixTestPresetContext.PropertyTestCount;
            Config configuration = Config.QuickThrowOnFailure
                .WithName(name)
                .WithMaxTest(expectedCases);
            CountingRunner runner = new(configuration.Runner, expectedCases);

            property.Check(configuration.WithRunner(runner));
        }

        private sealed class CountingRunner : IRunner
        {
            private readonly IRunner _inner;
            private readonly int _expectedCases;
            private int _caseCount;

            public CountingRunner(IRunner inner, int expectedCases)
            {
                _inner = inner;
                _expectedCases = expectedCases;
            }

            public void OnStartFixture(Type fixtureType) => _inner.OnStartFixture(fixtureType);

            public void OnArguments(
                int testNumber,
                FSharpList<object> arguments,
                FSharpFunc<int, FSharpFunc<FSharpList<object>, string>> formatter)
            {
                _caseCount = Math.Max(_caseCount, testNumber + 1);
                _inner.OnArguments(testNumber, arguments, formatter);
            }

            public void OnShrink(
                FSharpList<object> arguments,
                FSharpFunc<FSharpList<object>, string> formatter) =>
                _inner.OnShrink(arguments, formatter);

            public void OnFinished(string name, TestResult result)
            {
                GenixTestExecutionMetrics.RecordPropertyCases(name, _caseCount, _expectedCases);
                _inner.OnFinished(name, result);
            }
        }
    }
}
