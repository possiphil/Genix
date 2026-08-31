using UnityEditor;
using UnityEngine;

namespace Genix.Editor.DevTools
{
    /// <summary>Shared layout and control conventions for Genix developer windows.</summary>
    internal static class DeveloperWindowUi
    {
        private const float HorizontalChrome = 12f;

        private static GUIStyle _selectableRowStyle;

        /// <summary>Calculates a list width that grows with the window while preserving useful detail space.</summary>
        public static float ResponsiveListWidth(
            float windowWidth,
            float minimum = 250f,
            float maximum = 520f,
            float proportion = 0.35f,
            float minimumDetailsWidth = 390f)
        {
            float available = Mathf.Max(minimum, windowWidth - minimumDetailsWidth - HorizontalChrome);
            return Mathf.Clamp(windowWidth * proportion, minimum, Mathf.Min(maximum, available));
        }

        /// <summary>Draws a persistent, left-aligned list selection with ellipsized text.</summary>
        public static bool SelectableRow(bool selected, GUIContent content, float height = 24f, float width = 0f)
        {
            _selectableRowStyle ??= new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Ellipsis,
                padding = new RectOffset(7, 7, 2, 2)
            };

            GUILayoutOption widthOption = width > 0f
                ? GUILayout.Width(width)
                : GUILayout.ExpandWidth(true);
            bool value = GUILayout.Toggle(
                selected,
                content,
                _selectableRowStyle,
                GUILayout.Height(height),
                widthOption);
            return value && !selected;
        }

        /// <summary>Draws a section heading with an optional compact action on the right.</summary>
        public static bool SectionHeader(GUIContent title, GUIContent action = null, bool actionEnabled = true, float actionWidth = 72f)
        {
            bool invoked = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (action != null)
                {
                    using (new EditorGUI.DisabledScope(!actionEnabled))
                        invoked = GUILayout.Button(action, EditorStyles.miniButton, GUILayout.Width(actionWidth));
                }
            }

            return invoked;
        }

        /// <summary>Draws connected command buttons with stable, matching dimensions.</summary>
        public static bool CommandButton(GUIContent content, int index, int count, float width = 108f, float height = 28f)
        {
            GUIStyle style = index switch
            {
                0 when count > 1 => EditorStyles.miniButtonLeft,
                _ when index == count - 1 && count > 1 => EditorStyles.miniButtonRight,
                _ when count > 1 => EditorStyles.miniButtonMid,
                _ => EditorStyles.miniButton
            };

            return GUILayout.Button(content, style, GUILayout.Width(width), GUILayout.Height(height));
        }
    }
}
