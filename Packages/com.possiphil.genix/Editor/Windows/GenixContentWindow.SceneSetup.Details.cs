using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.SceneConfiguration;
using Genix.Extensions;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixContentWindow
    {
        private void MarkSceneObjectChanged(Component component)
        {
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            Repaint();
        }

        private void DrawSceneSetupSettingsClipboard(UnityEngine.Object selectedObject)
        {
            if (selectedObject is not PlacementSurfaceDescriptor descriptor)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent(
                        "Surface Settings",
                        "Copy semantic tags, accepted-asset rules, total capacity, and asset-specific capacity limits between placement surfaces."),
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(
                           !SupportSurfaceRegionAuthoring.CanCreate(descriptor.gameObject)))
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                "Add Support Surface",
                                "Create an editable support surface under this object for an internal shelf or raised level."),
                            GUILayout.Width(140f)))
                    {
                        SupportSurfaceRegionAuthoring.Create(
                            descriptor.gameObject,
                            GenixEditorWindow.GetConfiguredSurfaceLayerMask());
                        MarkSceneSetupDirty();
                    }
                }

                if (GUILayout.Button(
                        new GUIContent(
                            "Copy",
                            "Copy this placement surface's designer-authored settings."),
                        GUILayout.Width(58f)))
                {
                    _surfaceSettingsClipboard = PlacementSurfaceSettingsSnapshot.Capture(descriptor);
                    ShowNotification(new GUIContent($"Copied surface settings from {descriptor.gameObject.name}."));
                }

                using (new EditorGUI.DisabledScope(_surfaceSettingsClipboard == null))
                {
                    string sourceName = _surfaceSettingsClipboard?.SourceName;
                    if (GUILayout.Button(
                            new GUIContent(
                                "Paste",
                                string.IsNullOrWhiteSpace(sourceName)
                                    ? "Copy surface settings before pasting."
                                    : $"Paste the surface settings copied from {sourceName}."),
                            GUILayout.Width(58f)))
                    {
                        Undo.RecordObject(descriptor, "Paste Placement Surface Settings");
                        _surfaceSettingsClipboard.ApplyTo(descriptor);
                        EditorUtility.SetDirty(descriptor);
                        EditorSceneManager.MarkSceneDirty(descriptor.gameObject.scene);
                        PlacementSolver.ClearCandidateCache();
                        DestroySelectedObjectEditor();
                        MarkSceneSetupDirty();
                        ShowNotification(new GUIContent($"Pasted surface settings to {descriptor.gameObject.name}."));
                    }
                }
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawSelectedSceneSetupValidation()
        {
            EnsureSceneSetupEntries();
            SceneSetupObjectEntry entry = _sceneSetupEntries.FirstOrDefault(candidate =>
                candidate.MatchesDetailTarget(_selectedSceneSetupObject));

            if (entry == null)
                return;

            string status = GetSceneSetupStatus(
                entry,
                GenixEditorWindow.GetConfiguredSurfaceLayerMask(),
                out MessageType messageType);
            if (messageType is MessageType.Warning or MessageType.Error)
            {
                EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(status, messageType);
                EditorGUILayout.Space(4f);
            }
        }
    }
}
