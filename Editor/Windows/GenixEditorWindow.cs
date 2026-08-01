using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Diagnostics;
using Genix.Editor.Drawers;
using Genix.Editor.Generation;
using Genix.Editor.Layouts;
using Genix.Editor.Profiling;
using Genix.Editor.Infrastructure;
using Genix.Editor.TargetAreas;
using Genix.Editor.Utilities;
using Genix.Editor.Validation;
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
        private const string FloorSurfaceMaskKey = "Genix.SurfaceLayers.Floor";
        private const string WallSurfaceMaskKey = "Genix.SurfaceLayers.Wall";
        private const string CeilingSurfaceMaskKey = "Genix.SurfaceLayers.Ceiling";
        private const string SurfaceDiscoveryModeKey = "Genix.SurfaceDiscoveryMode";
        private const string RelativeSceneLayersKey = "Genix.Relative.SceneLayers";
        private const string FloorSurfaceAngleKey = "Genix.SurfaceClassification.FloorAngleDegrees";
        private const string CeilingSurfaceAngleKey = "Genix.SurfaceClassification.CeilingAngleDegrees";
        private const string LegacyFloorNormalYThresholdKey = "Genix.SurfaceClassification.FloorNormalYThreshold";
        private const string LegacyCeilingNormalYThresholdKey = "Genix.SurfaceClassification.CeilingNormalYThreshold";
        private const string UseGenerationSeedKey = "Genix.Generation.UseSeed";
        private const string GenerationSeedKey = "Genix.Generation.Seed";
        private const string BestEffortKey = "Genix.Generation.BestEffort";
        private const string GenerationPerformanceModeKey = "Genix.Generation.PerformanceMode";
        private const string DetailedDiagnosticsKey = "Genix.Diagnostics.DetailedCapture";
        private const float DefaultSurfaceAngleDegrees = 60f;
        private const SurfaceDiscoveryMode DefaultSurfaceDiscoveryMode = SurfaceDiscoveryMode.AllMatchingSurfacesInVolume;
        private static readonly GUIContent PreviewRunButtonContent = new(
            "Preview Run",
            "Plan placements and diagnostics without writing generated objects into the scene.");
        private static readonly GUIContent ApplyPreviewButtonContent = new(
            "Apply Preview",
            "Generate the accepted placements from the last preview run.");

        private readonly TargetAreaSelectorHost _targetAreaSelector = new();

        private AreaDecompositionMode _areaDecompositionMode = AreaDecompositionMode.Fast;
        private SurfaceDiscoveryMode _surfaceDiscoveryMode = DefaultSurfaceDiscoveryMode;
        private LayerMask _floorSurfaceLayers;
        private LayerMask _wallSurfaceLayers;
        private LayerMask _ceilingSurfaceLayers;
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

        private static readonly GenerationPerformanceMode[] PerformanceModes =
        {
            GenerationPerformanceMode.Accurate,
            GenerationPerformanceMode.Fast
        };

        private static readonly string[] PerformanceModeOptions =
        {
            "Accurate",
            "Fast"
        };

        private static readonly SurfaceDiscoveryMode[] SurfaceDiscoveryModes =
        {
            SurfaceDiscoveryMode.AllMatchingSurfacesInVolume,
            SurfaceDiscoveryMode.NearSfsBoundaries,
            SurfaceDiscoveryMode.SfsBoundaries
        };

        private static readonly string[] SurfaceDiscoveryModeOptions =
        {
            "All Matching Surfaces",
            "Near SFS Boundaries",
            "SFS Boundaries"
        };

        private const GenerationPerformanceMode DefaultPerformanceMode = GenerationPerformanceMode.Accurate;

        private GenerationMode _generationMode = GenerationMode.TargetPlacement;
        private GenerationPerformanceMode _performanceMode = DefaultPerformanceMode;
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
        private bool _detailedDiagnostics;
        private float _relativeRadius = 2f;

        private readonly StylePreview _stylePreviewDrawer = new();

        private const string DefaultStylePresetName = "Natural";
        private const string DefaultAssetPoolName = "Default Pool";

        private StylePreset[] _stylePresets = Array.Empty<StylePreset>();
        private string[] _stylePresetOptions = Array.Empty<string>();

        private AssetPool[] _assetPools = Array.Empty<AssetPool>();
        private string[] _assetPoolOptions = Array.Empty<string>();

        private readonly DiagnosticsPanel _diagnosticsPanelDrawer = new();
        private SceneSetupReport _lastSceneSetupReport;
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
            LoadSurfaceDiscoveryMode();
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
            PlacementSolver.ClearSceneObjectCache();
            RefreshSelectableAssets();
            RefreshGeneratedLayouts();
            AssignDefaultReferencesIfMissing();
            Repaint();
        }

        private void OnHierarchyChange()
        {
            PlacementSolver.ClearCandidateCache();
            PlacementSolver.ClearSceneObjectCache();
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
            DrawAreaCacheControls();

            _areaDecompositionMode = (AreaDecompositionMode)EditorGUILayout.EnumPopup("Division Method", _areaDecompositionMode);

            DrawSurfaceDiscoveryModeField();

            if (_surfaceDiscoveryMode != SurfaceDiscoveryMode.SfsBoundaries)
            {
                DrawSurfaceLayerField("Floor Layers", ref _floorSurfaceLayers, FloorSurfaceMaskKey);
                DrawSurfaceLayerField("Wall Layers", ref _wallSurfaceLayers, WallSurfaceMaskKey);
                DrawSurfaceLayerField("Ceiling Layers", ref _ceilingSurfaceLayers, CeilingSurfaceMaskKey);

                DrawSurfaceClassificationSettings();
            }

            _assetPool = AssetDropdown.DrawAssetPoolDropdownWithEditButton("Asset Pool", _assetPools, _assetPoolOptions, _assetPool);
            _objectCount = EditorGUILayout.IntField("Object Count", _objectCount);
            DrawGenerationWorkflowSettings();
        }

        private void DrawAreaCacheControls()
        {
            if (CreateAreaSource() is not IAreaCacheControl cacheControl)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUIUtility.labelWidth);

                GUIContent content = new(cacheControl.ClearCacheLabel, cacheControl.ClearCacheTooltip);

                if (!GUILayout.Button(content, GUILayout.Width(150f)))
                    return;
            }

            cacheControl.ClearCache();
            PlacementSolver.ClearCandidateCache();
            PlacementSolver.ClearSceneObjectCache();
            Debug.Log($"Genix cache cleared via {cacheControl.ClearCacheLabel}.");
        }

        private void DrawGenerationWorkflowSettings()
        {
            DrawPerformanceModeField();
            DrawProfileRunField();
            DrawDetailedDiagnosticsField();

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

        private void DrawPerformanceModeField()
        {
            int selectedIndex = Array.IndexOf(PerformanceModes, _performanceMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup("Performance Mode", selectedIndex, PerformanceModeOptions);

            if (!EditorGUI.EndChangeCheck())
                return;

            _performanceMode = PerformanceModes[Mathf.Clamp(selectedIndex, 0, PerformanceModes.Length - 1)];
            EditorPrefs.SetInt(GenerationPerformanceModeKey, (int)_performanceMode);
        }

        private void DrawSurfaceDiscoveryModeField()
        {
            int selectedIndex = Array.IndexOf(SurfaceDiscoveryModes, _surfaceDiscoveryMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup("Surface Source", selectedIndex, SurfaceDiscoveryModeOptions);

            if (!EditorGUI.EndChangeCheck())
                return;

            _surfaceDiscoveryMode = SurfaceDiscoveryModes[Mathf.Clamp(selectedIndex, 0, SurfaceDiscoveryModes.Length - 1)];
            EditorPrefs.SetInt(SurfaceDiscoveryModeKey, (int)_surfaceDiscoveryMode);
            PlacementSolver.ClearCandidateCache();
        }

        private static void DrawProfileRunField()
        {
            EditorGUI.BeginChangeCheck();
            bool profileRun = EditorGUILayout.Toggle("Profile Run", GenerationProfilerService.ProfilingEnabled);

            if (EditorGUI.EndChangeCheck())
                GenerationProfilerService.SetProfilingEnabled(profileRun);
        }

        private void DrawDetailedDiagnosticsField()
        {
            EditorGUI.BeginChangeCheck();
            bool detailedDiagnostics = EditorGUILayout.Toggle("Detailed Diagnostics", _detailedDiagnostics);

            if (!EditorGUI.EndChangeCheck())
                return;

            _detailedDiagnostics = detailedDiagnostics;
            EditorPrefs.SetBool(DetailedDiagnosticsKey, _detailedDiagnostics);
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
            }

            EditorGUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(PreviewRunButtonContent))
                {
                    GenerationWorkflow.ClearPreviewPlan();

                    if (CreateRequest(out GenerationRequest previewRequest))
                        GenerationWorkflow.Preview(previewRequest);
                }

                using (new EditorGUI.DisabledScope(!GenerationWorkflow.HasPreviewPlan))
                {
                    if (GUILayout.Button(ApplyPreviewButtonContent))
                        GenerationWorkflow.ApplyPreview();
                }
            }

            EditorGUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Setup"))
                    ValidateSceneSetup();

                if (GUILayout.Button("Save Layout"))
                    SaveCurrentLayout();
            }

            if (GenerationProfilerService.ProfilingEnabled)
                EditorGUILayout.HelpBox("Generate, Re-Generate, and Preview Run will capture performance profiles until Profile is disabled.", MessageType.Info);

            DrawSceneSetupReport();
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
                _surfaceDiscoveryMode != SurfaceDiscoveryMode.SfsBoundaries,
                GetCombinedSurfaceLayers(),
                floorSurfaceLayers: _floorSurfaceLayers,
                wallSurfaceLayers: _wallSurfaceLayers,
                ceilingSurfaceLayers: _ceilingSurfaceLayers,
                floorNormalYThreshold: AngleToPositiveNormalYThreshold(_floorSurfaceAngleDegrees),
                ceilingNormalYThreshold: -AngleToPositiveNormalYThreshold(_ceilingSurfaceAngleDegrees),
                surfaceDiscoveryMode: _surfaceDiscoveryMode);

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
                _bestEffort,
                _performanceMode,
                _detailedDiagnostics);

            return true;
        }

        private IAreaSource CreateAreaSource()
        {
            return _targetAreaSelector.CreateAreaSource();
        }

        private void ValidateSceneSetup()
        {
            _lastSceneSetupReport = null;

            if (!CreateRequest(out GenerationRequest request))
                return;

            _lastSceneSetupReport = SceneSetupValidator.Validate(request);

            foreach (SceneSetupIssue issue in _lastSceneSetupReport.Issues)
            {
                string message = $"Genix setup validation: {issue.Message}";

                switch (issue.Severity)
                {
                    case SceneSetupIssueSeverity.Error:
                        Debug.LogWarning(message);
                        break;
                    case SceneSetupIssueSeverity.Warning:
                        Debug.LogWarning(message);
                        break;
                    default:
                        Debug.Log(message);
                        break;
                }
            }
        }

        private void DrawSceneSetupReport()
        {
            if (_lastSceneSetupReport == null || _lastSceneSetupReport.Issues.Count == 0)
                return;

            EditorGUILayout.Space(4f);

            foreach (SceneSetupIssue issue in _lastSceneSetupReport.Issues)
            {
                MessageType type = issue.Severity switch
                {
                    SceneSetupIssueSeverity.Error => MessageType.Error,
                    SceneSetupIssueSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info
                };
                EditorGUILayout.HelpBox(issue.Message, type);
            }
        }

        private void DrawSurfaceLayerField(string label, ref LayerMask currentMask, string editorPrefsKey)
        {
            LayerMask newMask = DrawLayerMaskField(label, currentMask);

            if (newMask.value == currentMask.value)
                return;

            currentMask = newMask;
            EditorPrefs.SetInt(editorPrefsKey, currentMask.value);
            PlacementSolver.ClearCandidateCache();
        }

        private LayerMask GetCombinedSurfaceLayers()
        {
            LayerMask mask = default;
            mask.value = _floorSurfaceLayers.value |
                         _wallSurfaceLayers.value |
                         _ceilingSurfaceLayers.value;
            return mask;
        }

    }
}
