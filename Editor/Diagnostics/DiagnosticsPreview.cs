using Genix.Diagnostics;
using UnityEditor;

namespace Genix.Editor.Diagnostics
{
    public static class DiagnosticsPreview
    {
        public static DiagnosticsReport CurrentReport { get; private set; }

        public static bool ShowBounds { get; set; }
        public static bool ShowGrid { get; set; }
        public static bool ShowClusters { get; set; }
        public static bool ShowCandidateSeeds { get; set; }
        public static bool ShowAccepted { get; set; }
        public static bool ShowRejected { get; set; }

        public static bool HasReport => CurrentReport;

        public static void SetReport(DiagnosticsReport report)
        {
            if (CurrentReport == report)
                return;

            CurrentReport = report;
            SceneView.RepaintAll();
        }

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

        public static void ClearIfCurrent(DiagnosticsReport report)
        {
            if (CurrentReport != report)
                return;

            Clear();
        }

        public static void ClearCurrentReport()
        {
            if (!CurrentReport)
                return;

            CurrentReport = null;
            SceneView.RepaintAll();
        }
    }
}