using System;
using System.Collections.Generic;
using System.IO;
using Genix.Core;
using Genix.Editor.Generation;
using Genix.Editor.Infrastructure;
using Genix.Editor.UI;
using Genix.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixEditorWindow
    {
        private static readonly GUIContent GenerationPresetLabel = new(
            "Generation Preset",
            "Reuse generator settings across scenes. The target area and detailed diagnostic capture are not stored.");

        private GenerationPreset _selectedGenerationPreset;
        private GenerationPreset[] _generationPresets = Array.Empty<GenerationPreset>();
        private string[] _generationPresetOptions = { "Custom" };

        private void DrawGenerationPresetSection()
        {
            int selectedIndex = GetGenerationPresetIndex(_selectedGenerationPreset);
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float rowSpacing = EditorGUIUtility.standardVerticalSpacing;
            bool showActions = DesignerUiPreferences.IsAdvanced;
            float rowStep = lineHeight + rowSpacing;
            Rect blockRect = EditorGUILayout.GetControlRect(false, lineHeight * 3f + rowSpacing * 2f);
            Rect presetRect = new(blockRect.x, blockRect.y, blockRect.width, lineHeight);
            Rect contentHeaderRect = new(
                blockRect.x,
                blockRect.y + rowStep * 2f,
                blockRect.width,
                lineHeight);

            const float editWidth = 48f;
            Rect editRect = new(
                presetRect.xMax - editWidth,
                presetRect.y,
                editWidth,
                presetRect.height);
            Rect popupRowRect = new(
                presetRect.x,
                presetRect.y,
                Mathf.Max(0f, editRect.x - RunButtonGap - presetRect.x),
                presetRect.height);
            Rect popupRect = EditorGUI.PrefixLabel(popupRowRect, GenerationPresetLabel);

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUI.Popup(
                popupRect,
                selectedIndex,
                _generationPresetOptions,
                EditorGui.EllipsizedPopupStyle);

            if (EditorGUI.EndChangeCheck())
                SelectGenerationPreset(selectedIndex);

            using (new EditorGUI.DisabledScope(!_selectedGenerationPreset))
            {
                if (GUI.Button(
                        editRect,
                        new GUIContent("Edit", "Open the selected generation preset in the Inspector.")))
                {
                    EditorGui.ShowObjectInInspector(_selectedGenerationPreset);
                }
            }

            if (showActions)
            {
                Rect actionRect = new(
                    blockRect.x,
                    blockRect.y + rowStep,
                    blockRect.width,
                    lineHeight);
                DrawGenerationPresetActions(actionRect);
            }

            EditorGUI.LabelField(contentHeaderRect, "Content", EditorStyles.boldLabel);
        }

        private void DrawGenerationPresetActions(Rect rowRect)
        {
            const float saveWidth = 96f;
            const float actionWidth = 64f;
            const float actionGap = 2f;
            float groupWidth = saveWidth + actionWidth * 2f + actionGap * 2f;
            Rect saveRect = new(
                rowRect.xMax - groupWidth,
                rowRect.y,
                saveWidth,
                rowRect.height);
            Rect updateRect = new(
                saveRect.xMax + actionGap,
                rowRect.y,
                actionWidth,
                rowRect.height);
            Rect revertRect = new(
                updateRect.xMax + actionGap,
                rowRect.y,
                actionWidth,
                rowRect.height);

            if (GUI.Button(
                    saveRect,
                    new GUIContent("Save as New", "Create a preset from the current settings."),
                    EditorStyles.miniButton))
            {
                SaveGenerationPresetAsNew();
            }

            using (new EditorGUI.DisabledScope(!_selectedGenerationPreset))
            {
                if (GUI.Button(
                        updateRect,
                        new GUIContent("Update", "Save the current settings to the selected preset."),
                        EditorStyles.miniButton))
                {
                    UpdateSelectedGenerationPreset();
                }

                if (GUI.Button(
                        revertRect,
                        new GUIContent("Revert", "Discard current changes and reload the selected preset."),
                        EditorStyles.miniButton))
                {
                    ApplyGenerationPreset(_selectedGenerationPreset);
                }
            }
        }

        private void RefreshGenerationPresets()
        {
            List<GenerationPreset> presets = AssetFileService.FindAssets<GenerationPreset>("Assets");
            presets.Sort(CompareGenerationPresets);
            _generationPresets = presets.ToArray();
            _generationPresetOptions = new string[_generationPresets.Length + 1];
            _generationPresetOptions[0] = "Custom";

            for (int i = 0; i < _generationPresets.Length; i++)
                _generationPresetOptions[i + 1] = _generationPresets[i].name;

        }

        private void ValidateSelectedGenerationPreset()
        {
            if (_selectedGenerationPreset &&
                !EditorAssets.ContainsAsset(_generationPresets, _selectedGenerationPreset))
            {
                _selectedGenerationPreset = null;
            }

        }

        private void LoadDefaultGenerationPreset()
        {
            GenerationPreset lastPreset = GenerationPresetPreferences.GetDefault();

            if (!lastPreset)
                return;

            _selectedGenerationPreset = lastPreset;
            ApplyGenerationPreset(_selectedGenerationPreset);
        }

        private void SelectGenerationPreset(int selectedIndex)
        {
            _selectedGenerationPreset = selectedIndex > 0 && selectedIndex <= _generationPresets.Length
                ? _generationPresets[selectedIndex - 1]
                : null;

            if (_selectedGenerationPreset)
            {
                GenerationPresetPreferences.SetDefault(_selectedGenerationPreset);
                ApplyGenerationPreset(_selectedGenerationPreset);
            }
            else
            {
                GenerationPresetPreferences.ClearDefault();
            }
        }

        private int GetGenerationPresetIndex(GenerationPreset preset)
        {
            if (!preset)
                return 0;

            for (int i = 0; i < _generationPresets.Length; i++)
            {
                if (_generationPresets[i] == preset)
                    return i + 1;
            }

            return 0;
        }

        private GenerationPresetSettings CaptureGenerationPresetSettings()
        {
            return new GenerationPresetSettings(
                _assetPool,
                _selectedStylePreset,
                _objectCount,
                _placementTargets,
                _targetDistributionMode,
                _targetDistributionWeights,
                _areaDecompositionMode,
                _surfaceDiscoveryMode,
                _floorSurfaceLayers,
                _wallSurfaceLayers,
                _ceilingSurfaceLayers,
                _floorSurfaceAngleDegrees,
                _ceilingSurfaceAngleDegrees,
                _relativeSource,
                _relativeRadius,
                _relativeSceneLayers,
                _useGenerationSeed,
                _generationSeed,
                _bestEffort,
                CreateSupportDistributionSettings());
        }

        private void ApplyGenerationPreset(GenerationPreset preset)
        {
            if (!preset)
                return;

            GenerationPresetSettings settings = preset.Settings;
            _assetPool = settings.AssetPool;
            _selectedStylePreset = settings.StylePreset;
            _objectCount = settings.ObjectCount;
            _placementTargets = settings.PlacementTargets;
            _targetDistributionMode = settings.TargetDistributionMode;
            _targetDistributionWeights = settings.TargetDistributionWeights;
            ApplySupportDistributionSettings(settings.SupportDistribution);
            _areaDecompositionMode = settings.AreaDecompositionMode;
            _surfaceDiscoveryMode = settings.SurfaceDiscoveryMode;
            _floorSurfaceLayers = settings.FloorSurfaceLayers;
            _wallSurfaceLayers = settings.WallSurfaceLayers;
            _ceilingSurfaceLayers = settings.CeilingSurfaceLayers;
            _floorSurfaceAngleDegrees = settings.FloorSurfaceAngleDegrees;
            _ceilingSurfaceAngleDegrees = settings.CeilingSurfaceAngleDegrees;
            _relativeSource = settings.RelativePlacementSource;
            _relativeRadius = settings.RelativeRadius;
            _relativeSceneLayers = settings.RelativeSceneLayers;
            _useGenerationSeed = settings.UseFixedSeed;
            _generationSeed = settings.RandomSeed;
            _bestEffort = settings.BestEffort;

            PersistGenerationPresetEditorPreferences();
            GenerationWorkflow.ClearPreviewPlan();
            _lastSceneSetupReport = null;
            Repaint();
        }

        private void PersistGenerationPresetEditorPreferences()
        {
            EditorPrefs.SetInt(SurfaceDiscoveryModeKey, (int)_surfaceDiscoveryMode);
            EditorPrefs.SetInt(FloorSurfaceMaskKey, _floorSurfaceLayers.value);
            EditorPrefs.SetInt(WallSurfaceMaskKey, _wallSurfaceLayers.value);
            EditorPrefs.SetInt(CeilingSurfaceMaskKey, _ceilingSurfaceLayers.value);
            EditorPrefs.SetFloat(FloorSurfaceAngleKey, _floorSurfaceAngleDegrees);
            EditorPrefs.SetFloat(CeilingSurfaceAngleKey, _ceilingSurfaceAngleDegrees);
            EditorPrefs.SetInt(RelativeSceneLayersKey, _relativeSceneLayers.value);
            EditorPrefs.SetBool(UseGenerationSeedKey, _useGenerationSeed);
            EditorPrefs.SetInt(GenerationSeedKey, _generationSeed);
            EditorPrefs.SetBool(BestEffortKey, _bestEffort);
        }

        private void SaveGenerationPresetAsNew()
        {
            EditorGui.ClearTextFieldFocus();
            AssetFileService.EnsureFolder(ProjectContentPaths.GenerationPresets);

            string suggestedName = _selectedGenerationPreset
                ? _selectedGenerationPreset.name
                : "Generation Preset";
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Genix Generation Preset",
                suggestedName,
                "asset",
                "Choose where to save the reusable Genix generation settings.",
                ProjectContentPaths.GenerationPresets);

            if (string.IsNullOrEmpty(path))
                return;

            GenerationPreset preset = CreateInstance<GenerationPreset>();
            preset.Apply(CaptureGenerationPresetSettings());
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshGenerationPresets();
            _selectedGenerationPreset = preset;
            GenerationPresetPreferences.SetDefault(preset);
            Debug.Log($"Created Genix generation preset '{Path.GetFileNameWithoutExtension(path)}'.");
        }

        private void UpdateSelectedGenerationPreset()
        {
            if (!_selectedGenerationPreset)
                return;

            EditorGui.ClearTextFieldFocus();
            Undo.RecordObject(_selectedGenerationPreset, "Update Genix Generation Preset");
            _selectedGenerationPreset.Apply(CaptureGenerationPresetSettings());
            EditorUtility.SetDirty(_selectedGenerationPreset);
            AssetDatabase.SaveAssets();
            Debug.Log($"Updated Genix generation preset '{_selectedGenerationPreset.name}'.");
        }

        private static int CompareGenerationPresets(GenerationPreset a, GenerationPreset b) =>
            string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
    }
}
