using System;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Drawers;
using Genix.Editor.Infrastructure;
using Genix.Editor.Layouts;
using Genix.Editor.TargetAreas;
using Genix.Editor.UI;
using Genix.Editor.Utilities;
using Genix.Placement;
using Genix.Styles;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixEditorWindow
    {
        private void DrawStylePresetSection()
        {
            AssignDefaultStylePresetIfMissing();

            _selectedStylePreset = AssetDropdown.DrawStylePresetDropdownWithEditButton(
                CreateGenerationStyleLabel(),
                _stylePresets,
                _stylePresetOptions,
                _selectedStylePreset);
            if (!_selectedStylePreset)
            {
                EditorGUILayout.HelpBox("No generation style preset selected. Create or restore a preset named Natural to use it as the default.", MessageType.Warning);
            }
        }

        private GUIContent CreateGenerationStyleLabel()
        {
            const string guidance = "Choose the overall spacing and distribution behavior for this run.";
            string description = _selectedStylePreset
                ? _selectedStylePreset.Settings.description
                : string.Empty;
            string tooltip = string.IsNullOrWhiteSpace(description)
                ? guidance
                : $"{guidance}\n\n{_selectedStylePreset.name}: {description}";

            return new GUIContent("Generation Style", tooltip);
        }

        private void RefreshSelectableAssets()
        {
            _stylePresets = EditorAssets.LoadAssetsFromFolder<StylePreset>(ProjectContentPaths.StylePresets, CompareStylePresets);
            _stylePresetOptions = EditorAssets.CreateAssetOptions(_stylePresets);
            _assetPools = EditorAssets.LoadAssetsFromFolder<AssetPool>(ProjectContentPaths.AssetPools, CompareAssetPools);
            _assetPoolOptions = EditorAssets.CreateAssetOptions(_assetPools);
            RefreshGenerationPresets();
            RefreshTargetAreas();

            ValidateSelectedAssets();
        }

        private void ValidateSelectedAssets()
        {
            if (_selectedStylePreset && !EditorAssets.ContainsAsset(_stylePresets, _selectedStylePreset))
                _selectedStylePreset = null;

            if (_assetPool && !EditorAssets.ContainsAsset(_assetPools, _assetPool))
                _assetPool = null;

            ValidateSelectedGenerationPreset();
        }

        private void AssignDefaultReferencesIfMissing()
        {
            AssignDefaultStylePresetIfMissing();
            AssignDefaultAssetPoolIfMissing();
        }

        private void AssignDefaultStylePresetIfMissing()
        {
            if (!_selectedStylePreset)
                _selectedStylePreset = FindDefaultStylePreset();
        }

        private void AssignDefaultAssetPoolIfMissing()
        {
            if (!_assetPool)
                _assetPool = FindDefaultAssetPool();
        }

        private StylePreset FindDefaultStylePreset()
        {
            return EditorAssets.LoadAssetAtPath<StylePreset>($"{ProjectContentPaths.StylePresets}/{DefaultStylePresetName}.asset");
        }

        private AssetPool FindDefaultAssetPool()
        {
            return EditorAssets.LoadAssetAtPath<AssetPool>($"{ProjectContentPaths.AssetPools}/{DefaultAssetPoolName}.asset");
        }

        private static int CompareStylePresets(StylePreset a, StylePreset b)
        {
            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareAssetPools(AssetPool a, AssetPool b)
        {
            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshTargetAreas()
        {
            _targetAreaSelector.Refresh();
        }

        private void LoadInitialPlacementSurfaceMask()
        {
            if (_placementSurfaceMaskLoaded)
                return;

            LayerMask defaultMask = ResolveDefaultSurfaceMask();
            _floorSurfaceLayers = LoadSurfaceMask(FloorSurfaceMaskKey, defaultMask);
            _wallSurfaceLayers = LoadSurfaceMask(WallSurfaceMaskKey, defaultMask);
            _ceilingSurfaceLayers = LoadSurfaceMask(CeilingSurfaceMaskKey, defaultMask);
            _placementSurfaceMaskLoaded = true;
        }

        private static LayerMask LoadSurfaceMask(string key, LayerMask fallback)
        {
            return EditorPrefs.HasKey(key) ? EditorPrefs.GetInt(key) : fallback;
        }

        private static LayerMask ResolveDefaultSurfaceMask()
        {
            if (EditorPrefs.HasKey(PlacementSurfaceMaskKey))
            {
                return EditorPrefs.GetInt(PlacementSurfaceMaskKey);
            }

            int layer = LayerMask.NameToLayer(DefaultPlacementSurfaceLayerName);

            if (layer < 0 && !EditorPrefs.GetBool(PlacementSurfaceLayerCreatedKey, false))
            {
                layer = EnsureLayerExists(DefaultPlacementSurfaceLayerName);
                EditorPrefs.SetBool(PlacementSurfaceLayerCreatedKey, true);
            }

            LayerMask mask = layer >= 0 ? 1 << layer : 0;
            EditorPrefs.SetInt(PlacementSurfaceMaskKey, mask.value);
            return mask;
        }

        /// <summary>Returns the union of the currently configured floor, wall, and ceiling surface layers.</summary>
        internal static LayerMask GetConfiguredSurfaceLayerMask()
        {
            LayerMask fallback = ResolveDefaultSurfaceMask();
            LayerMask combined = default;
            combined.value = LoadSurfaceMask(FloorSurfaceMaskKey, fallback).value |
                             LoadSurfaceMask(WallSurfaceMaskKey, fallback).value |
                             LoadSurfaceMask(CeilingSurfaceMaskKey, fallback).value;
            return combined;
        }

        private void LoadSurfaceClassificationSettings()
        {
            if (_surfaceClassificationSettingsLoaded)
                return;

            _floorSurfaceAngleDegrees = EditorPrefs.HasKey(FloorSurfaceAngleKey)
                ? EditorPrefs.GetFloat(FloorSurfaceAngleKey, DefaultSurfaceAngleDegrees)
                : LegacyThresholdToAngle(EditorPrefs.GetFloat(LegacyFloorNormalYThresholdKey, 0.5f));

            _ceilingSurfaceAngleDegrees = EditorPrefs.HasKey(CeilingSurfaceAngleKey)
                ? EditorPrefs.GetFloat(CeilingSurfaceAngleKey, DefaultSurfaceAngleDegrees)
                : LegacyThresholdToAngle(EditorPrefs.GetFloat(LegacyCeilingNormalYThresholdKey, -0.5f));

            _floorSurfaceAngleDegrees = Mathf.Clamp(_floorSurfaceAngleDegrees, 0f, 90f);
            _ceilingSurfaceAngleDegrees = Mathf.Clamp(_ceilingSurfaceAngleDegrees, 0f, 90f);
            _surfaceClassificationSettingsLoaded = true;
        }

        private void LoadSurfaceDiscoveryMode()
        {
            int value = EditorPrefs.GetInt(
                SurfaceDiscoveryModeKey,
                (int)DefaultSurfaceDiscoveryMode);

            _surfaceDiscoveryMode = Enum.IsDefined(typeof(SurfaceDiscoveryMode), value)
                ? (SurfaceDiscoveryMode)value
                : DefaultSurfaceDiscoveryMode;
        }

        private void LoadGenerationWorkflowSettings()
        {
            _useGenerationSeed = EditorPrefs.GetBool(UseGenerationSeedKey, false);
            _generationSeed = EditorPrefs.GetInt(GenerationSeedKey, _generationSeed);
            _bestEffort = EditorPrefs.GetBool(BestEffortKey, true);
            _detailedDiagnostics = EditorPrefs.GetBool(DetailedDiagnosticsKey, false);
            _relativeSceneLayers = EditorPrefs.GetInt(RelativeSceneLayersKey, _relativeSceneLayers);
        }

        private static float AngleToPositiveNormalYThreshold(float angleDegrees)
        {
            angleDegrees = Mathf.Clamp(angleDegrees, 0f, 90f);

            if (Mathf.Approximately(angleDegrees, 90f))
                return 0f;

            return Mathf.Max(0f, Mathf.Cos(angleDegrees * Mathf.Deg2Rad));
        }

        private static float LegacyThresholdToAngle(float threshold)
        {
            return Mathf.Acos(Mathf.Clamp01(Mathf.Abs(threshold))) * Mathf.Rad2Deg;
        }

        private static int EnsureLayerExists(string layerName)
        {
            int existingLayer = LayerMask.NameToLayer(layerName);

            if (existingLayer >= 0)
                return existingLayer;

            UnityEngine.Object tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            SerializedObject tagManager = new(tagManagerAsset);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);

                if (!string.IsNullOrEmpty(layer.stringValue))
                    continue;

                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                return i;
            }

            Debug.LogWarning($"Could not create layer '{layerName}'. No free user layer slot found.");
            return -1;
        }

        private static LayerMask DrawLayerMaskField(string label, LayerMask selected) =>
            DrawLayerMaskField(new GUIContent(label), selected);

        private static LayerMask DrawLayerMaskField(GUIContent label, LayerMask selected)
        {
            string[] layers = InternalEditorUtility.layers;
            int editorMask = 0;

            for (int i = 0; i < layers.Length; i++)
            {
                int layer = LayerMask.NameToLayer(layers[i]);
                if ((selected.value & (1 << layer)) != 0)
                    editorMask |= 1 << i;
            }

            editorMask = EditorGUILayout.MaskField(label, editorMask, layers);

            int mask = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                if ((editorMask & (1 << i)) == 0)
                    continue;

                int layer = LayerMask.NameToLayer(layers[i]);
                mask |= 1 << layer;
            }

            selected.value = mask;
            return selected;
        }
    }
}
