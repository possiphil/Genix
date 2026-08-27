using System;
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
    public static class DesignerUiPreferences
    {
        private const string ModeKey = "Genix.DesignerUiMode";

        private static readonly GUIContent[] ModeOptions =
        {
            new("Basic", "Show the controls used by the common designer workflow."),
            new("Advanced", "Show detailed constraints, sampling controls, and authoring overrides.")
        };

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

        /// <summary>Draws the shared mode selector inside an existing toolbar.</summary>
        public static void DrawToolbarSelector(float width = 154f)
        {
            int selected = GUILayout.Toolbar(
                (int)Mode,
                ModeOptions,
                EditorStyles.toolbarButton,
                GUILayout.Width(width));
            Mode = (DesignerUiMode)Mathf.Clamp(selected, 0, ModeOptions.Length - 1);
        }

        /// <summary>Draws a complete mode toolbar and optional hidden-state indicator.</summary>
        public static void DrawWindowToolbar(
            string title,
            bool advancedSettingsActive,
            string activeTooltip = null)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(title, EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                DrawAdvancedActiveIndicator(advancedSettingsActive, activeTooltip);
                DrawToolbarSelector();
            }
        }

        /// <summary>Signals that hidden advanced values still affect the edited object.</summary>
        public static void DrawAdvancedActiveIndicator(bool active, string tooltip = null)
        {
            if (IsAdvanced || !active)
                return;

            GUIContent content = new(EditorGUIUtility.IconContent("SettingsIcon"));
            content.tooltip = string.IsNullOrWhiteSpace(tooltip)
                ? "This configuration contains advanced values. Switch to Advanced to inspect or edit them."
                : tooltip;
            GUILayout.Label(content, GUILayout.Width(20f), GUILayout.Height(18f));
            GUILayout.Space(4f);
        }
    }
}
