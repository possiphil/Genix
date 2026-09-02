using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Utilities
{
    /// <summary>Provides shared IMGUI controls and layout helpers for Genix editor interfaces.</summary>
    public static class EditorGui
    {
        /// <summary>Identifies the shared display-name text control used by Genix inspectors.</summary>
        public const string DisplayNameControlName = "GenixDisplayName";

        private const float FoldoutTextOffset = -4f;
        private const int PopupTrailingPadding = 4;

        private static GUIStyle _ellipsizedPopupStyle;

        /// <summary>Gets the shared popup style that truncates long selections with an ellipsis.</summary>
        public static GUIStyle EllipsizedPopupStyle
        {
            get
            {
                if (_ellipsizedPopupStyle != null)
                    return _ellipsizedPopupStyle;

                GUIStyle style = new(EditorStyles.popup)
                {
                    clipping = TextClipping.Ellipsis
                };
                RectOffset padding = style.padding;
                style.padding = new RectOffset(
                    padding.left,
                    padding.right + PopupTrailingPadding,
                    padding.top,
                    padding.bottom);
                _ellipsizedPopupStyle = style;
                return _ellipsizedPopupStyle;
            }
        }

        /// <summary>Draws a popup whose selected value ends with an ellipsis when space is limited.</summary>
        public static int Popup(
            GUIContent label,
            int selectedIndex,
            string[] displayedOptions,
            params GUILayoutOption[] options)
        {
            Rect row = EditorGUILayout.GetControlRect(
                true,
                EditorGUIUtility.singleLineHeight,
                options);
            Rect field = DrawIndentedPrefixLabel(row, label);
            return EditorGUI.Popup(field, selectedIndex, displayedOptions, EllipsizedPopupStyle);
        }

        /// <summary>Draws a popup with rich option content and ellipsized selected text.</summary>
        public static int Popup(
            GUIContent label,
            int selectedIndex,
            GUIContent[] displayedOptions,
            params GUILayoutOption[] options)
        {
            Rect row = EditorGUILayout.GetControlRect(
                true,
                EditorGUIUtility.singleLineHeight,
                options);
            Rect field = DrawIndentedPrefixLabel(row, label);
            return EditorGUI.Popup(field, selectedIndex, displayedOptions, EllipsizedPopupStyle);
        }

        /// <summary>Draws an unlabeled popup whose selected value ends with an ellipsis when space is limited.</summary>
        public static int Popup(
            int selectedIndex,
            string[] displayedOptions,
            params GUILayoutOption[] options)
        {
            Rect field = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight,
                options);
            return EditorGUI.Popup(field, selectedIndex, displayedOptions, EllipsizedPopupStyle);
        }

        private static Rect DrawIndentedPrefixLabel(Rect row, GUIContent label)
        {
            Rect indentedRow = EditorGUI.IndentedRect(row);
            float indentWidth = indentedRow.x - row.x;
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            int previousIndentLevel = EditorGUI.indentLevel;
            EditorGUIUtility.labelWidth = Mathf.Max(0f, previousLabelWidth - 2f * indentWidth);
            EditorGUI.indentLevel = 0;

            try
            {
                return EditorGUI.PrefixLabel(indentedRow, label);
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EditorGUI.indentLevel = previousIndentLevel;
            }
        }

        /// <summary>Draws a shortcut that selects the supplied asset for editing.</summary>
        public static void DrawEditAssetButton(Object asset, float width = 48f)
        {
            using (new EditorGUI.DisabledScope(!asset))
            {
                if (GUILayout.Button("Edit", GUILayout.Width(width)))
                    ShowObjectInInspector(asset);
            }
        }

        /// <summary>Draws a styled Edit button for use inside a connected action group.</summary>
        public static void DrawEditAssetButton(Object asset, GUIStyle style, float width, float height)
        {
            using (new EditorGUI.DisabledScope(!asset))
            {
                if (GUILayout.Button("Edit", style, GUILayout.Width(width), GUILayout.Height(height)))
                    ShowObjectInInspector(asset);
            }
        }

        /// <summary>Selects and reveals the object in the Unity Inspector.</summary>
        public static void ShowObjectInInspector(Object obj)
        {
            if (!obj) return;

            ActiveEditorTracker.sharedTracker.isLocked = false;

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);

            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }

        /// <summary>Draws an indented foldout and returns its expanded state.</summary>
        public static bool DrawIndentedFoldout(bool isExpanded, string label)
        {
            int previousIndentLevel = EditorGUI.indentLevel;

            EditorGUI.indentLevel = Mathf.Max(0, previousIndentLevel - 1);

            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.IndentedRect(rect);
            rect.x += FoldoutTextOffset;
            rect.width -= FoldoutTextOffset;

            EditorGUI.indentLevel = previousIndentLevel;

            return EditorGUI.Foldout(
                rect,
                isExpanded,
                label,
                true,
                EditorStyles.foldoutHeader
            );
        }

        /// <summary>Returns a label that marks values differing from their defaults.</summary>
        public static GUIContent ChangedLabel(string label, bool hasChanged)
        {
            return new GUIContent(hasChanged ? $"{label} *" : label);
        }

        /// <summary>Converts a percentage to a whole-item count, rounding halfway values up.</summary>
        public static int RoundPercentageToCount(int totalCount, float percentage)
        {
            if (totalCount <= 0)
                return 0;

            float exactCount = totalCount * Mathf.Clamp(percentage, 0f, 100f) / 100f;
            return Mathf.FloorToInt(exactCount + 0.5f);
        }

        /// <summary>Clears text field focus.</summary>
        public static void ClearTextFieldFocus()
        {
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }

        /// <summary>Draws a help box when the supplied message is not empty.</summary>
        public static void DrawHelpBox(string message, MessageType messageType, float height = 42f)
        {
            if (messageType is not (MessageType.Warning or MessageType.Error))
                return;

            Rect rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.HelpBox(rect, message, messageType);
        }
    }
}
