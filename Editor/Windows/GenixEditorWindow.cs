using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Diagnostics;
using Genix.Editor.Drawers;
using Genix.Editor.Generation;
using Genix.Editor.Layouts;
using Genix.Editor.Infrastructure;
using Genix.Editor.TargetAreas;
using Genix.Editor.Utilities;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Extensions;
using Genix.Layouts;
using Genix.Placement;
using Genix.Styles;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixEditorWindow : EditorWindow
    {
        private const string DefaultPlacementSurfaceLayerName = "Placement Surface";
        private const string PlacementSurfaceLayerCreatedKey = "Genix.PlacementSurfaceLayerCreated";
        private const string PlacementSurfaceMaskKey = "Genix.PlacementSurfaceLayerMask";
        private const string RelativeSceneLayersKey = "Genix.Relative.SceneLayers";
        private const string FloorSurfaceAngleKey = "Genix.SurfaceClassification.FloorAngleDegrees";
        private const string CeilingSurfaceAngleKey = "Genix.SurfaceClassification.CeilingAngleDegrees";
        private const string LegacyFloorNormalYThresholdKey = "Genix.SurfaceClassification.FloorNormalYThreshold";
        private const string LegacyCeilingNormalYThresholdKey = "Genix.SurfaceClassification.CeilingNormalYThreshold";
        private const string UseGenerationSeedKey = "Genix.Generation.UseSeed";
        private const string GenerationSeedKey = "Genix.Generation.Seed";
        private const string BestEffortKey = "Genix.Generation.BestEffort";
        private const float DefaultSurfaceAngleDegrees = 60f;

        private readonly TargetAreaSelectorHost _targetAreaSelector = new();

        private AreaDecompositionMode _areaDecompositionMode = AreaDecompositionMode.Fast;
        private bool _usePlacementSurfaceCheck = true;
        private LayerMask _placementSurfaceLayers;
        private bool _placementSurfaceMaskLoaded;
        private float _floorSurfaceAngleDegrees = DefaultSurfaceAngleDegrees;
        private float _ceilingSurfaceAngleDegrees = DefaultSurfaceAngleDegrees;
        private bool _surfaceClassificationSettingsLoaded;

        private AssetPool _assetPool;

        private static readonly TargetDistributionMode[] TargetDistributionModes =
        {
            TargetDistributionMode.Random,
            TargetDistributionMode.Balanced,
            TargetDistributionMode.Weighted
        };

        private static readonly string[] TargetDistributionOptions =
        {
            "Random",
            "Balanced",
            "Weighted"
        };

        private static readonly RelativePlacementSource[] RelativeSources =
        {
            RelativePlacementSource.None,
            RelativePlacementSource.Any,
            RelativePlacementSource.GeneratedObjects,
            RelativePlacementSource.SceneObjects,
            RelativePlacementSource.SelectedObjects
        };

        private static readonly string[] RelativeSourceOptions =
        {
            "None",
            "Any",
            "Generated Objects",
            "Scene Objects",
            "Selected Objects"
        };

        private GenerationMode _generationMode = GenerationMode.TargetPlacement;
        private PlacementTarget _placementTargets = PlacementTarget.All;
        private TargetDistributionMode _targetDistributionMode = TargetDistributionMode.Random;
        private TargetDistributionWeights _targetDistributionWeights = TargetDistributionWeights.Default;
        private RelativePlacementSource _relativeSource = RelativePlacementSource.None;
        private LayerMask _relativeSceneLayers = ~0;

        private StylePreset _selectedStylePreset;

        private int _objectCount = 5;
        private bool _useGenerationSeed;
        private int _generationSeed = 12345;
        private bool _bestEffort = true;
        private float _relativeRadius = 2f;

        private readonly StylePreview _stylePreviewDrawer = new();

        private const string DefaultStylePresetName = "Natural";
        private const string DefaultAssetPoolName = "Default Pool";

        private StylePreset[] _stylePresets = Array.Empty<StylePreset>();
        private string[] _stylePresetOptions = Array.Empty<string>();

        private AssetPool[] _assetPools = Array.Empty<AssetPool>();
        private string[] _assetPoolOptions = Array.Empty<string>();

        private readonly DiagnosticsPanel _diagnosticsPanelDrawer = new();
        private SavedLayout[] _generatedLayouts = Array.Empty<SavedLayout>();
        private bool _showGeneratedLayouts = true;
        private static Texture2D _lockedLayoutIcon;
        private static Texture2D _unlockedLayoutIcon;

        private Vector2 _scrollPosition;

        [MenuItem("Tools/Genix/Generator")]
        public static void Open()
        {
            GetWindow<GenixEditorWindow>("Genix Generator");
        }

        private void OnEnable()
        {
            LoadInitialPlacementSurfaceMask();
            LoadSurfaceClassificationSettings();
            LoadGenerationWorkflowSettings();
            RefreshSelectableAssets();
            RefreshGeneratedLayouts();
            AssignDefaultReferencesIfMissing();
        }

        private void OnFocus()
        {
            DiagnosticsPreview.ClearCurrentReport();

            RefreshSelectableAssets();
            RefreshGeneratedLayouts();
            AssignDefaultReferencesIfMissing();
            Repaint();
        }

        private void OnProjectChange()
        {
            PlacementSolver.ClearCandidateCache();
            RefreshSelectableAssets();
            RefreshGeneratedLayouts();
            AssignDefaultReferencesIfMissing();
            Repaint();
        }

        private void OnHierarchyChange()
        {
            PlacementSolver.ClearCandidateCache();
            RefreshSelectableAssets();
            RefreshGeneratedLayouts();
            AssignDefaultReferencesIfMissing();
            Repaint();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawInputSection();
            EditorGUILayout.Space(8);

            DrawGenerationModeSection();

            DrawStylePresetSection();
            EditorGUILayout.Space(8);

            DrawGenerationButtons();

            DrawGeneratedLayoutsSection();

            _diagnosticsPanelDrawer.Draw();

            EditorGUILayout.EndScrollView();
        }

        private void DrawInputSection()
        {
            AssignDefaultAssetPoolIfMissing();

            _targetAreaSelector.Draw("Target Area");

            _areaDecompositionMode = (AreaDecompositionMode)EditorGUILayout.EnumPopup("Division Method", _areaDecompositionMode);

            _usePlacementSurfaceCheck = EditorGUILayout.Toggle("Surface Check", _usePlacementSurfaceCheck);

            if (_usePlacementSurfaceCheck)
            {
                LayerMask newMask = DrawLayerMaskField("Surface Layers", _placementSurfaceLayers);

                if (newMask.value != _placementSurfaceLayers.value)
                {
                    _placementSurfaceLayers = newMask;
                    EditorPrefs.SetInt(PlacementSurfaceMaskKey, _placementSurfaceLayers.value);
                }

                DrawSurfaceClassificationSettings();
            }

            _assetPool = AssetDropdown.DrawAssetPoolDropdownWithEditButton("Asset Pool", _assetPools, _assetPoolOptions, _assetPool);
            _objectCount = EditorGUILayout.IntField("Object Count", _objectCount);
            DrawGenerationWorkflowSettings();
        }

        private void DrawGenerationWorkflowSettings()
        {
            EditorGUI.BeginChangeCheck();
            bool bestEffort = EditorGUILayout.Toggle("Best Effort", _bestEffort);

            if (EditorGUI.EndChangeCheck())
            {
                _bestEffort = bestEffort;
                EditorPrefs.SetBool(BestEffortKey, _bestEffort);
            }

            EditorGUI.BeginChangeCheck();
            bool useSeed = EditorGUILayout.Toggle("Use Seed", _useGenerationSeed);

            if (EditorGUI.EndChangeCheck())
            {
                _useGenerationSeed = useSeed;
                EditorPrefs.SetBool(UseGenerationSeedKey, _useGenerationSeed);
            }

            if (!_useGenerationSeed)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                int seed = EditorGUILayout.IntField("Seed", _generationSeed);

                if (EditorGUI.EndChangeCheck())
                {
                    _generationSeed = seed;
                    EditorPrefs.SetInt(GenerationSeedKey, _generationSeed);
                }

                if (GUILayout.Button("Randomize", GUILayout.Width(90f)))
                {
                    _generationSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    EditorPrefs.SetInt(GenerationSeedKey, _generationSeed);
                    GUI.FocusControl(null);
                }
            }
        }

        private void DrawSurfaceClassificationSettings()
        {
            EditorGUI.BeginChangeCheck();

            float floorAngle = EditorGUILayout.Slider("Floor Angle", _floorSurfaceAngleDegrees, 0f, 90f);
            float ceilingAngle = EditorGUILayout.Slider("Ceiling Angle", _ceilingSurfaceAngleDegrees, 0f, 90f);

            if (!EditorGUI.EndChangeCheck())
                return;

            _floorSurfaceAngleDegrees = Mathf.Clamp(floorAngle, 0f, 90f);
            _ceilingSurfaceAngleDegrees = Mathf.Clamp(ceilingAngle, 0f, 90f);

            EditorPrefs.SetFloat(FloorSurfaceAngleKey, _floorSurfaceAngleDegrees);
            EditorPrefs.SetFloat(CeilingSurfaceAngleKey, _ceilingSurfaceAngleDegrees);
        }

        private void DrawGenerationButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate") && CreateRequest(out GenerationRequest generateRequest))
                    GenerationWorkflow.Generate(generateRequest);

                if (GUILayout.Button("Clear"))
                {
                    IAreaSource areaSource = CreateAreaSource();

                    if (areaSource != null)
                        GenerationWorkflow.Clear(areaSource);
                    else
                        Debug.LogWarning("No target area selected. Choose a target area/location in the Genix window before clearing generated objects.");
                }

                if (GUILayout.Button("Re-Generate") && CreateRequest(out GenerationRequest regenerateRequest))
                    GenerationWorkflow.Regenerate(regenerateRequest);

                if (GUILayout.Button("Save Layout"))
                    SaveCurrentLayout();
            }
        }

        private bool CreateRequest(out GenerationRequest request)
        {
            request = null;

            if (!_selectedStylePreset)
            {
                Debug.LogWarning("Generation could not start because no generation style preset is selected.");
                return false;
            }

            IAreaSource areaSource = CreateAreaSource();

            if (areaSource == null)
            {
                Debug.LogWarning("Generation could not start because no target area/location is selected.");
                return false;
            }

            AreaBuildSettings areaSettings = new(
                _areaDecompositionMode,
                _usePlacementSurfaceCheck,
                _placementSurfaceLayers,
                floorNormalYThreshold: AngleToPositiveNormalYThreshold(_floorSurfaceAngleDegrees),
                ceilingNormalYThreshold: -AngleToPositiveNormalYThreshold(_ceilingSurfaceAngleDegrees));

            request = new GenerationRequest(
                areaSource,
                _assetPool,
                _objectCount,
                _generationMode,
                GetEffectivePlacementTargets(),
                GetEffectiveTargetDistributionMode(),
                GetEffectiveTargetDistributionWeights(),
                _selectedStylePreset.Settings,
                areaSettings,
                CreateRelativePlacementSettings(),
                _selectedStylePreset.name,
                _useGenerationSeed,
                _generationSeed,
                _bestEffort);

            return true;
        }

        private IAreaSource CreateAreaSource()
        {
            return _targetAreaSelector.CreateAreaSource();
        }

    }
}
