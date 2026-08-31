using System;
using Genix.Authoring;
using Genix.Assets;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Genix.Editor.UI
{
    /// <summary>Available levels of detail for designer-facing Genix editors.</summary>
    public enum DesignerUiMode
    {
        /// <summary>Shows the controls needed by the common generation workflow.</summary>
        Basic,
        /// <summary>Shows every authored constraint and low-level generation control.</summary>
        Advanced
    }

    /// <summary>Stores and draws the shared Basic/Advanced interface mode.</summary>
    [InitializeOnLoad]
    public static class DesignerUiPreferences
    {
        private const string ModeKey = "Genix.DesignerUiMode";
        private const string ShowAuthoringGuidesKey = "Genix.ShowAuthoringGuides";

        private static readonly GUIContent AdvancedToggle = new(
            "Advanced",
            "Show additional constraints, sampling controls, and authoring overrides.");

        static DesignerUiPreferences()
        {
            AuthoringVisualization.ShowSceneGuides = EditorPrefs.GetBool(ShowAuthoringGuidesKey, false);
        }

        /// <summary>Gets or sets the shared interface mode.</summary>
        public static DesignerUiMode Mode
        {
            get
            {
                int stored = EditorPrefs.GetInt(ModeKey, (int)DesignerUiMode.Basic);
                return Enum.IsDefined(typeof(DesignerUiMode), stored)
                    ? (DesignerUiMode)stored
                    : DesignerUiMode.Basic;
            }
            set
            {
                if (Mode == value)
                    return;

                EditorPrefs.SetInt(ModeKey, (int)value);
                InternalEditorUtility.RepaintAllViews();
            }
        }

        /// <summary>Indicates whether advanced designer controls should be shown.</summary>
        public static bool IsAdvanced => Mode == DesignerUiMode.Advanced;

        /// <summary>Gets or sets global Scene view visibility for Genix authoring guides.</summary>
        public static bool ShowAuthoringGuides
        {
            get => AuthoringVisualization.ShowSceneGuides;
            set
            {
                if (AuthoringVisualization.ShowSceneGuides == value)
                    return;

                AuthoringVisualization.ShowSceneGuides = value;
                EditorPrefs.SetBool(ShowAuthoringGuidesKey, value);
                SceneView.RepaintAll();
            }
        }

        /// <summary>Draws the shared mode selector inside an existing toolbar.</summary>
        public static void DrawToolbarSelector(float width = 82f)
        {
            bool showAdvanced = GUILayout.Toggle(
                IsAdvanced,
                AdvancedToggle,
                EditorStyles.toolbarButton,
                GUILayout.Width(width));
            Mode = showAdvanced ? DesignerUiMode.Advanced : DesignerUiMode.Basic;
        }

        /// <summary>Draws the shared mode selector in a right-aligned toolbar.</summary>
        public static void DrawWindowToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.FlexibleSpace();
                DrawToolbarSelector();
            }
        }
    }

    /// <summary>Maps implementation terminology to consistent designer-facing language.</summary>
    public static class DesignerTerminology
    {
        /// <summary>Returns the label used for an asset-pool mode throughout designer UI.</summary>
        public static string AssetPoolMode(AssetPoolMode mode) => mode switch
        {
            Genix.Assets.AssetPoolMode.Static => "Manual List",
            Genix.Assets.AssetPoolMode.Dynamic => "Rule-Based",
            _ => mode.ToString()
        };

        /// <summary>Returns the concise explanation used for an asset-pool mode.</summary>
        public static string AssetPoolModeTooltip(AssetPoolMode mode) => mode switch
        {
            Genix.Assets.AssetPoolMode.Static =>
                "Use only the asset definitions explicitly added to this pool.",
            Genix.Assets.AssetPoolMode.Dynamic =>
                "Automatically include catalog assets that match this pool's filters.",
            _ => string.Empty
        };

        /// <summary>Draws a quiet empty state without presenting instructions as a warning.</summary>
        public static void DrawEmptyState(string message, float height = 34f)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.LabelField(rect, message, EditorStyles.centeredGreyMiniLabel);
        }
    }
}
