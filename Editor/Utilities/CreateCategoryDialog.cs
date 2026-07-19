using System;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Utilities
{
    public sealed class CreateCategoryDialog : EditorWindow
    {
        private const string NameControlName = "GenixCategoryName";

        private string _categoryName = "New Category";
        private bool _allowMultipleTags = true;
        private Action<string, bool> _onConfirm;
        private bool _focusedInput;

        public static void Open(Action<string, bool> onConfirm)
        {
            CreateCategoryDialog window = CreateInstance<CreateCategoryDialog>();

            window.titleContent = new GUIContent("Create Category");
            window._onConfirm = onConfirm;

            window.minSize = new Vector2(380f, 122f);
            window.maxSize = new Vector2(380f, 122f);

            window.ShowUtility();
            window.Focus();
        }

        private void OnGUI()
        {
            HandleKeyboardInput();

            EditorGUILayout.Space(8f);

            GUI.SetNextControlName(NameControlName);
            _categoryName = EditorGUILayout.TextField("Category Name", _categoryName);

            FocusInputOnce();

            _allowMultipleTags = EditorGUILayout.Toggle("Allow Multiple Tags", _allowMultipleTags);

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(90f)))
                    Close();

                if (GUILayout.Button("Create", GUILayout.Width(90f)))
                    Confirm();
            }
        }

        private void HandleKeyboardInput()
        {
            Event currentEvent = Event.current;

            if (currentEvent.type != EventType.KeyDown)
                return;

            if (currentEvent.keyCode == KeyCode.Return ||
                currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                Confirm();
                currentEvent.Use();
            }

            if (currentEvent.keyCode == KeyCode.Escape)
            {
                Close();
                currentEvent.Use();
            }
        }

        private void FocusInputOnce()
        {
            if (_focusedInput)
                return;

            EditorGUI.FocusTextInControl(NameControlName);
            _focusedInput = true;
        }

        private void Confirm()
        {
            string result = string.IsNullOrWhiteSpace(_categoryName)
                ? "New Category"
                : _categoryName.Trim();

            _onConfirm?.Invoke(result, _allowMultipleTags);
            Close();
        }
    }
}