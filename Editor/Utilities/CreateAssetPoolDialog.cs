using System;
using Genix.Assets;
using Genix.Extensions;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Utilities
{
    public sealed class CreateAssetPoolDialog : EditorWindow
    {
        private const string NameControlName = "GenixPoolName";

        private string _poolName;
        private AssetPoolMode _mode;
        private Action<string, AssetPoolMode> _onConfirm;
        private bool _focusedInput;

        private static readonly AssetPoolMode[] PoolModes =
        {
            AssetPoolMode.Static,
            AssetPoolMode.Dynamic
        };

        private static readonly string[] PoolModeLabels =
        {
            AssetPoolMode.Static.ToDisplayName(),
            AssetPoolMode.Dynamic.ToDisplayName()
        };

        public static void Open(
            AssetPoolMode defaultMode,
            Action<string, AssetPoolMode> onConfirm)
        {
            CreateAssetPoolDialog window = CreateInstance<CreateAssetPoolDialog>();

            window.titleContent = new GUIContent("Create Asset Pool");
            window._mode = defaultMode;
            window._poolName = "New Asset Pool";
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
            _poolName = EditorGUILayout.TextField("Asset Pool Name", _poolName);

            FocusInputOnce();

            _mode = DrawModeDropdown("Mode", _mode);

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

        private static AssetPoolMode DrawModeDropdown(string label, AssetPoolMode currentMode)
        {
            int currentIndex = Array.IndexOf(PoolModes, currentMode);

            if (currentIndex < 0)
                currentIndex = 0;

            int selectedIndex = EditorGUILayout.Popup(label, currentIndex, PoolModeLabels);

            return PoolModes[selectedIndex];
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
            string result = string.IsNullOrWhiteSpace(_poolName)
                ? "New Asset Pool"
                : _poolName.Trim();

            _onConfirm?.Invoke(result, _mode);
            Close();
        }
    }
}
