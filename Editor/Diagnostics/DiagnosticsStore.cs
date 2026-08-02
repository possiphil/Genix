using Genix.Diagnostics;
using UnityEditor;

namespace Genix.Editor.Diagnostics
{
    /// <summary>Maintains the current editor diagnostics state and scene-visualization options.</summary>
    public static class DiagnosticsStore
    {
        /// <summary>Gets last diagnostics.</summary>
        public static GenerationDiagnostics LastDiagnostics { get; private set; }

        /// <summary>Indicates whether show candidate seeds.</summary>
        public static bool ShowCandidateSeeds { get; set; }
        /// <summary>Indicates whether show accepted candidates.</summary>
        public static bool ShowAcceptedCandidates { get; set; }
        /// <summary>Indicates whether show rejected candidates.</summary>
        public static bool ShowRejectedCandidates { get; set; }
        /// <summary>Indicates whether show target bounds.</summary>
        public static bool ShowTargetBounds { get; set; }
        /// <summary>Indicates whether show clusters.</summary>
        public static bool ShowClusters { get; set; }
        /// <summary>Indicates whether show grid.</summary>
        public static bool ShowGrid { get; set; }

        /// <summary>Sets last.</summary>
        public static void SetLast(GenerationDiagnostics diagnostics)
        {
            LastDiagnostics = diagnostics;
            SceneView.RepaintAll();
        }

        /// <summary>Clears the stored state.</summary>
        public static void Clear()
        {
            LastDiagnostics = null;
            SceneView.RepaintAll();
        }
    }
}
