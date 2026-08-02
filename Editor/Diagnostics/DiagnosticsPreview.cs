using Genix.Diagnostics;
using UnityEditor;

namespace Genix.Editor.Diagnostics
{
    /// <summary>Owns the currently visualized diagnostics report and Scene-view display filters.</summary>
    public static class DiagnosticsPreview
    {
        /// <summary>Gets current report.</summary>
        public static DiagnosticsReport CurrentReport { get; private set; }

        /// <summary>Indicates whether show bounds.</summary>
        public static bool ShowBounds { get; set; }
        /// <summary>Indicates whether show grid.</summary>
        public static bool ShowGrid { get; set; }
        /// <summary>Indicates whether show clusters.</summary>
        public static bool ShowClusters { get; set; }
        /// <summary>Indicates whether show candidate seeds.</summary>
        public static bool ShowCandidateSeeds { get; set; }
        /// <summary>Indicates whether show accepted.</summary>
        public static bool ShowAccepted { get; set; }
        /// <summary>Indicates whether show rejected.</summary>
        public static bool ShowRejected { get; set; }

        /// <summary>Indicates whether a diagnostics report is available for visualization.</summary>
        public static bool HasReport => CurrentReport;

        /// <summary>Sets report.</summary>
        public static void SetReport(DiagnosticsReport report)
        {
            if (CurrentReport == report)
                return;

            CurrentReport = report;
            SceneView.RepaintAll();
        }

        /// <summary>Clears the report and resets all visualization filters.</summary>
        public static void Clear()
        {
            CurrentReport = null;

            ShowBounds = false;
            ShowGrid = false;
            ShowClusters = false;
            ShowCandidateSeeds = false;
            ShowAccepted = false;
            ShowRejected = false;

            SceneView.RepaintAll();
        }

        /// <summary>Clears if current.</summary>
        public static void ClearIfCurrent(DiagnosticsReport report)
        {
            if (CurrentReport != report)
                return;

            Clear();
        }

        /// <summary>Clears current report.</summary>
        public static void ClearCurrentReport()
        {
            if (!CurrentReport)
                return;

            CurrentReport = null;
            SceneView.RepaintAll();
        }
    }
}
