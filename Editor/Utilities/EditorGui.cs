using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Utilities
{
    public static class EditorGui
    {
        private const float FoldoutTextOffset = -4f;

        public static void DrawEditAssetButton(Object asset, float width = 48f)
        {
            using (new EditorGUI.DisabledScope(!asset))
            {
                if (GUILayout.Button("Edit", GUILayout.Width(width)))
                    ShowObjectInInspector(asset);
            }
        }

        public static void ShowObjectInInspector(Object obj)
        {
            if (!obj) return;

            ActiveEditorTracker.sharedTracker.isLocked = false;

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);

            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }

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

        public static GUIContent ChangedLabel(string label, bool hasChanged)
        {
            return new GUIContent(hasChanged ? $"{label} *" : label);
        }

        public static void ClearTextFieldFocus()
        {
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }

        public static void DrawHelpBox(string message, MessageType messageType, float height = 42f)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.HelpBox(rect, message, messageType);
        }
    }
}