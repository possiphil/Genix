using System.Collections.Generic;
using UnityEngine;

namespace Genix.Profiling
{
    /// <summary>Stores persisted generation-profile reports for comparison in the Unity editor.</summary>
    public sealed class GenerationProfileCatalog : ScriptableObject
    {
        [SerializeField] private List<GenerationProfileReport> reports = new();

        /// <summary>Gets reports.</summary>
        public IReadOnlyList<GenerationProfileReport> Reports => reports;

        /// <summary>Sets reports.</summary>
        public void SetReports(IEnumerable<GenerationProfileReport> reports)
        {
            this.reports.Clear();

            foreach (GenerationProfileReport report in reports)
            {
                if (report && !this.reports.Contains(report))
                    this.reports.Add(report);
            }
        }

        /// <summary>Adds report.</summary>
        public void AddReport(GenerationProfileReport report)
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
