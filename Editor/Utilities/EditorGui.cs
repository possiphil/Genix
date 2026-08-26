using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Utilities
{
    /// <summary>Provides shared IMGUI controls and layout helpers for Genix editor interfaces.</summary>
    public static class EditorGui
    {
        private const float FoldoutTextOffset = -4f;

        /// <summary>Draws a shortcut that selects the supplied asset for editing.</summary>
        public static void DrawEditAssetButton(Object asset, float width = 48f)
        {
            using (new EditorGUI.DisabledScope(!asset))
            {
                if (GUILayout.Button("Edit", GUILayout.Width(width)))
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
