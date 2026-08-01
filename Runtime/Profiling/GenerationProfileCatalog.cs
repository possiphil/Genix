using System.Collections.Generic;
using UnityEngine;

namespace Genix.Profiling
{
    public sealed class GenerationProfileCatalog : ScriptableObject
    {
        [SerializeField] private List<GenerationProfileReport> reports = new();

        public IReadOnlyList<GenerationProfileReport> Reports => reports;

        public void SetReports(IEnumerable<GenerationProfileReport> reports)
        {
            this.reports.Clear();

            foreach (GenerationProfileReport report in reports)
            {
                if (report && !this.reports.Contains(report))
                    this.reports.Add(report);
            }
        }

        public void AddReport(GenerationProfileReport report)
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
