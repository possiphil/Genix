using System.IO;
using Genix.Editor.Drawers;
using Genix.Editor.Infrastructure;
using Genix.Editor.State;
using Genix.Editor.Utilities;
using Genix.Extensions;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    [CustomEditor(typeof(StylePreset))]
    public sealed class StylePresetEditor : UnityEditor.Editor
    {
        private StylePreset _preset;

        private readonly StyleEditState _state = new();
        private readonly StyleEditor _settingsDrawer = new();

        private readonly TimedMessage _feedbackMessage = new();
        private readonly TimedMessage _validationMessage = new();

        private static string _pendingFeedbackTargetId;
        private static string _pendingFeedbackMessage;
        private static MessageType _pendingFeedbackType;

        private void OnEnable()
        {
            _preset = (StylePreset)target;
            LoadFromPreset();

            if (_pendingFeedbackTargetId != _preset.GetLocalObjectId())
                return;

            ShowFeedback(_pendingFeedbackMessage, _pendingFeedbackType);

            _pendingFeedbackTargetId = null;
            _pendingFeedbackMessage = null;
        }

        public override void OnInspectorGUI()
        {
            bool changed = false;
            changed |= DrawStyleSettings();
            if (changed)
            {
                StyleSettingsUtility.ClearUnusedSettings(ref _state.EditingSettings);
                _state.UpdatePendingChanges();

                if (_state.HasPendingChanges)
                    _feedbackMessage.Clear();
            }

            EditorGUILayout.Space(10);
            DrawActionButtons();

            EditorGUILayout.Space(6);
            DrawDefaultButtons();

            EditorGUILayout.Space(10);
            DrawFooterStatus();
        }

        private bool DrawStyleSettings()
        {
            return _settingsDrawer.Draw(_state, ShowValidationError);
        }

        private void DrawActionButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_state.HasPendingChanges))
                {
                    if (GUILayout.Button("Save Changes"))
                        SavePresetChanges();

                    if (GUILayout.Button("Discard Changes"))
                        DiscardPresetChanges();
                }

                if (GUILayout.Button("Save As New Preset"))
                    SaveAsNewPreset();
            }
        }

        private void DrawDefaultButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set Current As Defaults"))
                    SetCurrentAsDefaults();

                if (GUILayout.Button("Restore Defaults"))
                    RestoreDefaults();
            }
        }

        private void DrawFooterStatus()
        {
            UpdateTimedMessages();

            if (_state.HasPendingChanges)
                EditorGui.DrawHelpBox("Unsaved changes. Fields marked with * differ from the saved preset.", MessageType.Warning);

            if (_feedbackMessage.IsVisible)
                EditorGui.DrawHelpBox(_feedbackMessage.Text, _feedbackMessage.Type);

            if (_validationMessage.IsVisible)
                EditorGui.DrawHelpBox(_validationMessage.Text, _validationMessage.Type);
        }

        private void UpdateTimedMessages()
        {
            bool shouldKeepRepainting = false;

            shouldKeepRepainting |= _feedbackMessage.Update();
            shouldKeepRepainting |= _validationMessage.Update();

            if (shouldKeepRepainting)
                Repaint();
        }

        private void ShowFeedback(string message, MessageType type = MessageType.Info, double durationSeconds = 4.0)
        {
            _feedbackMessage.Show(message, type, durationSeconds);
            Repaint();
        }

        private void ShowValidationError(string fieldName, string rule, string restoredValue)
        {
            _validationMessage.Show($"{fieldName} {rule}. Restored saved value: {restoredValue}.", MessageType.Error, 5.0);
            Repaint();
        }

        private void SavePresetChanges()
        {
            EditorGui.ClearTextFieldFocus();
            StyleSettingsUtility.ClearUnusedSettings(ref _state.EditingSettings);

            Undo.RecordObject(_preset, "Save Genix Style Preset");

            _preset.Apply(_state.EditingSettings);

            EditorUtility.SetDirty(_preset);
            AssetDatabase.SaveAssets();

            LoadFromPreset();
            ShowFeedback("Changes saved.");
        }

        private void DiscardPresetChanges()
        {
            EditorGui.ClearTextFieldFocus();

            _state.DiscardChanges();

            ShowFeedback("Changes discarded.");
            Repaint();
        }

        private void SaveAsNewPreset()
        {
            EditorGui.ClearTextFieldFocus();
            AssetFileService.EnsureFolder(ProjectContentPaths.StylePresets);

            StyleSettingsUtility.ClearUnusedSettings(ref _state.EditingSettings);

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Genix Style Preset",
                _preset.name,
                "asset",
                "Choose where to save the new Genix style preset.",
                ProjectContentPaths.StylePresets);
            if (string.IsNullOrEmpty(path))
                return;

            StylePreset newPreset = CreateInstance<StylePreset>();
            newPreset.Initialize(_state.EditingSettings);

            AssetDatabase.CreateAsset(newPreset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _pendingFeedbackTargetId = newPreset.GetLocalObjectId();
            _pendingFeedbackMessage = $"Created new preset: {Path.GetFileNameWithoutExtension(path)}";
            _pendingFeedbackType = MessageType.Info;

            Selection.activeObject = newPreset;
            EditorGUIUtility.PingObject(newPreset);
        }

        private void SetCurrentAsDefaults()
        {
            StyleSettingsUtility.ClearUnusedSettings(ref _state.EditingSettings);

            Undo.RecordObject(_preset, "Set Genix Style Defaults");

            _preset.Apply(_state.EditingSettings);
            _preset.SetCurrentSettingsAsDefaults();

            EditorUtility.SetDirty(_preset);
            AssetDatabase.SaveAssets();

            LoadFromPreset();
            ShowFeedback("Current settings saved as defaults.");
        }

        private void RestoreDefaults()
        {
            EditorGui.ClearTextFieldFocus();

            Undo.RecordObject(_preset, "Restore Genix Style Defaults");

            _preset.RestoreDefaults();

            EditorUtility.SetDirty(_preset);
            AssetDatabase.SaveAssets();

            LoadFromPreset();
            ShowFeedback("Defaults restored.");
        }

        private void LoadFromPreset()
        {
            _state.LoadFromPreset(_preset);
        }
    }
}
