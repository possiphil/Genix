using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Utilities
{
    /// <summary>Opens Genix workflow windows beside an existing Unity Inspector when possible.</summary>
    internal static class GenixWindowDocking
    {
        private const string InspectorWindowTypeName = "UnityEditor.InspectorWindow";

        /// <summary>
        /// Focuses an existing window without moving it, or creates it beside an open Inspector.
        /// </summary>
        public static T Open<T>(string title) where T : EditorWindow
        {
            T existing = FindOpenWindow<T>();

            if (existing)
            {
                ShowAndFocus(existing, title);
                return existing;
            }

            return Create<T>(title);
        }

        private static T Create<T>(string title) where T : EditorWindow
        {
            Type inspectorType = GetOpenInspectorType();
            T window = inspectorType != null
                ? EditorWindow.GetWindow<T>(title, true, inspectorType)
                : EditorWindow.GetWindow<T>(title);
            ShowAndFocus(window, title);
            return window;
        }

        private static T FindOpenWindow<T>() where T : EditorWindow =>
            Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(window => window);

        private static Type GetOpenInspectorType()
        {
            Type inspectorType = typeof(UnityEditor.Editor).Assembly.GetType(InspectorWindowTypeName);
            return inspectorType != null && Resources.FindObjectsOfTypeAll(inspectorType).Length > 0
                ? inspectorType
                : null;
        }

        private static void ShowAndFocus(EditorWindow window, string title)
        {
            window.titleContent = new GUIContent(title);
            window.Show();
            window.Focus();
        }
    }
}
