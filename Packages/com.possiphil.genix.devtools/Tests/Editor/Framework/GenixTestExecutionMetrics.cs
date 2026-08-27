using System;
using System.Collections.Generic;

namespace Genix.Tests.Framework
{
    internal readonly struct PropertyCaseMetrics
    {
        public int Executed { get; }
        public int Expected { get; }

        public PropertyCaseMetrics(int executed, int expected)
        {
            Executed = Math.Max(0, executed);
            Expected = Math.Max(0, expected);
        }
    }

    /// <summary>Collects per-property cases that Unity Test Framework otherwise reports as one leaf test.</summary>
    internal static class GenixTestExecutionMetrics
    {
        private static readonly object Gate = new();
        private static readonly Dictionary<string, PropertyCaseMetrics> PropertyRuns = new(StringComparer.Ordinal);

        public static int PropertyCases
        {
            get
            {
                lock (Gate)
                {
                    int total = 0;

                    foreach (PropertyCaseMetrics metrics in PropertyRuns.Values)
                        total += metrics.Executed;

                    return total;
                }
            }
        }

        public static void Reset()
        {
            lock (Gate)
                PropertyRuns.Clear();
        }

        public static void RecordPropertyCases(string name, int executed, int expected)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            lock (Gate)
                PropertyRuns[name] = new PropertyCaseMetrics(executed, expected);
        }

        public static bool TryGetPropertyCases(string name, out PropertyCaseMetrics metrics)
        {
            lock (Gate)
                return PropertyRuns.TryGetValue(name ?? string.Empty, out metrics);
        }
    }
}
