using Genix.Diagnostics;
using UnityEditor;

namespace Genix.Editor.Diagnostics
{
    public static class DiagnosticsStore
    {
        public static GenerationDiagnostics LastDiagnostics { get; private set; }

        public static bool ShowCandidateSeeds { get; set; }
        public static bool ShowAcceptedCandidates { get; set; }
        public static bool ShowRejectedCandidates { get; set; }
        public static bool ShowTargetBounds { get; set; }
        public static bool ShowClusters { get; set; }
        public static bool ShowGrid { get; set; }

        public static void SetLast(GenerationDiagnostics diagnostics)
        {
            LastDiagnostics = diagnostics;
            SceneView.RepaintAll();
        }

        public static void Clear()
        {
            LastDiagnostics = null;
            SceneView.RepaintAll();
        }
    }
}