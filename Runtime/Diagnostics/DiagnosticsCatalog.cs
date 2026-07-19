using System.Collections.Generic;
using UnityEngine;

namespace Genix.Diagnostics
{
    public sealed class DiagnosticsCatalog : ScriptableObject
    {
        [SerializeField] private List<DiagnosticsReport> reports = new();

        public IReadOnlyList<DiagnosticsReport> Reports => reports;

        public void SetReports(IEnumerable<DiagnosticsReport> reports)
        {
            this.reports.Clear();

            foreach (DiagnosticsReport report in reports)
            {
                if (report && !this.reports.Contains(report))
                    this.reports.Add(report);
            }
        }

        public void AddReport(DiagnosticsReport report)
        {
            if (!report || reports.Contains(report))
                return;

            reports.Add(report);
        }

        public void RemoveMissingReports()
        {
            reports.RemoveAll(report => !report);
        }
    }
}
