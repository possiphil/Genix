using System.Collections.Generic;
using UnityEngine;

namespace Genix.Diagnostics
{
    /// <summary>Stores persisted diagnostics reports for inspection in the Unity editor.</summary>
    public sealed class DiagnosticsCatalog : ScriptableObject
    {
        [SerializeField] private List<DiagnosticsReport> reports = new();

        /// <summary>Gets reports.</summary>
        public IReadOnlyList<DiagnosticsReport> Reports => reports;

        /// <summary>Sets reports.</summary>
        public void SetReports(IEnumerable<DiagnosticsReport> reports)
        {
            this.reports.Clear();

            foreach (DiagnosticsReport report in reports)
            {
                if (report && !this.reports.Contains(report))
                    this.reports.Add(report);
            }
        }

        /// <summary>Adds report.</summary>
        public void AddReport(DiagnosticsReport report)
        {
            if (!report || reports.Contains(report))
                return;

            reports.Add(report);
        }

        /// <summary>Removes missing reports.</summary>
        public void RemoveMissingReports()
        {
            reports.RemoveAll(report => !report);
        }
    }
}
