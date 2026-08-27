using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Diagnostics;
using Genix.Editor.Drawers;
using Genix.Editor.Generation;
using Genix.Editor.Layouts;
using Genix.Editor.Infrastructure;
using Genix.Editor.TargetAreas;
using Genix.Editor.UI;
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
    /// <summary>Provides the main designer workflow for configuring, previewing, applying, and diagnosing Genix generation.</summary>
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
        private const string DetailedDiagnosticsKey = "Genix.Diagnostics.DetailedCapture";
        private const float DefaultSurfaceAngleDegrees = 60f;
        private const SurfaceDiscoveryMode DefaultSurfaceDiscoveryMode = SurfaceDiscoveryMode.AllMatchingSurfacesInVolume;
        private static readonly GUIContent PreviewRunButtonContent = new(
            "Preview Run",
            "Plan placements and diagnostics without writing generated objects into the scene.");
        private static readonly GUIContent ApplyPreviewButtonContent = new(
            "Apply Preview",
            "Generate the accepted placements from the last preview run.");
        private static readonly GUIContent GenerateButtonContent = new(
            "Generate",
            "Plan placements and immediately instantiate the accepted objects in the scene.");
        private static readonly GUIContent ClearGeneratedButtonContent = new(
            "Clear",
            "Remove Genix-generated objects belonging to the selected target area.");
        private static readonly GUIContent RegenerateButtonContent = new(
            "Re-Generate",
            "Replace the current generated result with a newly planned result using the current settings.");
        private static readonly GUIContent RandomizeSeedButtonContent = new(
            "Randomize",
            "Choose a new fixed seed. The value remains reproducible until randomized again.");
        private static readonly GUIContent ValidateSetupButtonContent = new(
            "Validate Setup",
            "Check the selected target, assets, layers, and generation settings without generating objects.");
        private static readonly GUIContent SaveLayoutButtonContent = new(
            "Save Layout",
            "Capture the current Genix-generated hierarchy as a reusable layout asset.");

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

        private static readonly GUIContent[] TargetDistributionOptions =
        {
            new("Random", "Choose placement targets freely from the available candidates. Best when no fixed target ratio is required."),
            new("Balanced", "Aim for an equal object count on every selected placement target."),
            new("Weighted", "Distribute objects according to the relative weights shown below.")
        };

        private static readonly RelativePlacementSource[] RelativeSources =
        {
            RelativePlacementSource.None,
            RelativePlacementSource.Any,
            RelativePlacementSource.GeneratedObjects,
            RelativePlacementSource.SceneObjects,
            RelativePlacementSource.SelectedObjects
        };

        private static readonly GUIContent[] RelativeSourceOptions =
        {
            new("None", "Do not constrain placements relative to other objects."),
            new("Any", "Keep placements near generated objects or matching scene objects."),
            new("Generated Objects", "Keep new placements near objects already planned or generated by Genix."),
            new("Scene Objects", "Keep placements near existing scene objects on the selected layers."),
            new("Selected Objects", "Keep placements near the transforms currently selected in the Hierarchy or Scene view.")
        };

        private static readonly SurfaceDiscoveryMode[] SurfaceDiscoveryModes =
        {
            SurfaceDiscoveryMode.AllMatchingSurfacesInVolume,
            SurfaceDiscoveryMode.NearSfsBoundaries,
            SurfaceDiscoveryMode.SfsBoundaries
        };

        private static readonly GUIContent[] SurfaceDiscoveryModeOptions =
        {
            new("All Matching Surfaces", "Search all colliders on the configured layers throughout the SFS volume. Recommended for most scenes and for interior floors at arbitrary heights."),
            new("Near SFS Boundaries", "Project onto matching colliders only near SFS boundary regions. Use when interior surfaces should be ignored and a smaller search area is preferable."),
            new("SFS Boundaries", "Use only voxel-derived SFS boundary regions without physics surface projection. Best for fully voxel-defined spaces.")
        };

        private static readonly AreaDecompositionMode[] AreaDecompositionModes =
        {
            AreaDecompositionMode.Fast,
            AreaDecompositionMode.Precise
        };

        private static readonly GUIContent[] AreaDecompositionOptions =
        {
            new("Layer Bounds", "Merge each voxel layer into broad rectangular regions. Faster, but holes and irregular outlines may be approximated."),
            new("Cell-Preserving", "Decompose occupied cells into tighter rectangles. Use for irregular SFS boundaries where preserving holes and outlines matters.")
        };

        private PlacementTarget _placementTargets = PlacementTarget.All;
        private TargetDistributionMode _targetDistributionMode = TargetDistributionMode.Random;
        private TargetDistributionWeights _targetDistributionWeights = TargetDistributionWeights.Default;
        private bool _supportDistributionEnabled;
        private int _defaultSupportWeight = 1;
        private readonly List<SupportDistributionRule> _supportDistributionRules = new();
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

        /// <summary>Opens or focuses the corresponding Genix editor window.</summary>
        [MenuItem("Tools/Genix/Generator")]
        public static void Open()
        {
            GenixWindowDocking.Open<GenixEditorWindow>("Genix Generator");
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Genix Generator");
            LoadInitialPlacementSurfaceMask();
            LoadSurfaceDiscoveryMode();
            LoadSurfaceClassificationSettings();
            LoadGenerationWorkflowSettings();
            RefreshSelectableAssets();
            LoadDefaultGenerationPreset();
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
            DesignerUiPreferences.DrawWindowToolbar(
                "Generation",
                HasAdvancedGeneratorSettings(),
                "This generation configuration contains hidden surface, distribution, relation, seed, or diagnostics settings. They remain active in Basic mode.");

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            StarterContentInstaller.DrawSetupButton();

            DrawGenerationPresetSection();
            EditorGUILayout.Space(8);

            DrawInputSection();
            EditorGUILayout.Space(8);

            DrawPlacementSettingsSection();

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

            _targetAreaSelector.Draw(new GUIContent(
                "Target Area",
                "Spatial area in which Genix may search for valid placement positions. The available entries come from the selected area-provider integration."));

            if (DesignerUiPreferences.IsAdvanced)
            {
                DrawAreaCacheControls();
                DrawSurfaceDiscoveryModeField();

                if (ShouldShowAreaDecomposition())
                    DrawAreaDecompositionModeField();

                if (_surfaceDiscoveryMode != SurfaceDiscoveryMode.SfsBoundaries)
                {
                    DrawSurfaceLayerField(new GUIContent("Floor Layers", "Layers that may provide upward-facing placement surfaces."), ref _floorSurfaceLayers, FloorSurfaceMaskKey);
                    DrawSurfaceLayerField(new GUIContent("Wall Layers", "Layers that may provide near-vertical placement surfaces."), ref _wallSurfaceLayers, WallSurfaceMaskKey);
                    DrawSurfaceLayerField(new GUIContent("Ceiling Layers", "Layers that may provide downward-facing placement surfaces."), ref _ceilingSurfaceLayers, CeilingSurfaceMaskKey);

                    DrawSurfaceClassificationSettings();
                }
            }

            _assetPool = AssetDropdown.DrawAssetPoolDropdownWithEditButton(
                new GUIContent(
                    "Asset Pool",
                    "Collection of prefabs eligible for this run. Static pools list assets explicitly; dynamic pools resolve assets from their filters."),
                _assetPools,
                _assetPoolOptions,
                _assetPool);
            _objectCount = Mathf.Max(1, EditorGUILayout.IntField(
                new GUIContent("Object Count", "Number of objects to place. Best Effort may return fewer when no valid placements remain."),
                _objectCount));
            if (DesignerUiPreferences.IsAdvanced)
                DrawGenerationWorkflowSettings();
        }

        private bool HasAdvancedGeneratorSettings()
        {
            LayerMask defaultMask = ResolveDefaultSurfaceMask();
            bool customSurfaceLayers = _floorSurfaceLayers.value != defaultMask.value ||
                                       _wallSurfaceLayers.value != defaultMask.value ||
                                       _ceilingSurfaceLayers.value != defaultMask.value;

            return _surfaceDiscoveryMode != DefaultSurfaceDiscoveryMode ||
                   _areaDecompositionMode != AreaDecompositionMode.Fast ||
                   customSurfaceLayers ||
                   !Mathf.Approximately(_floorSurfaceAngleDegrees, DefaultSurfaceAngleDegrees) ||
                   !Mathf.Approximately(_ceilingSurfaceAngleDegrees, DefaultSurfaceAngleDegrees) ||
                   _targetDistributionMode != TargetDistributionMode.Random ||
                   _supportDistributionEnabled ||
                   _relativeSource != RelativePlacementSource.None ||
                   _useGenerationSeed ||
                   !_bestEffort ||
                   _detailedDiagnostics;
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
            DrawDetailedDiagnosticsField();

            EditorGUI.BeginChangeCheck();
            bool bestEffort = EditorGUILayout.Toggle(
                new GUIContent("Best Effort", "Keep the largest valid partial result when the requested count cannot be reached. Disable to require a complete plan."),
                _bestEffort);

            if (EditorGUI.EndChangeCheck())
            {
                _bestEffort = bestEffort;
                EditorPrefs.SetBool(BestEffortKey, _bestEffort);
            }

            EditorGUI.BeginChangeCheck();
            bool useSeed = EditorGUILayout.Toggle(
                new GUIContent("Use Seed", "Use a fixed seed for reproducible generation and candidate-cache reuse."),
                _useGenerationSeed);

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
                int seed = EditorGUILayout.IntField(new GUIContent("Seed", "Random seed used by sampling, target selection, asset order, and rotations."), _generationSeed);

                if (EditorGUI.EndChangeCheck())
                {
                    _generationSeed = seed;
                    EditorPrefs.SetInt(GenerationSeedKey, _generationSeed);
                }

                if (GUILayout.Button(RandomizeSeedButtonContent, GUILayout.Width(90f)))
                {
                    _generationSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    EditorPrefs.SetInt(GenerationSeedKey, _generationSeed);
                    GUI.FocusControl(null);
                }
            }
        }

        private void DrawSurfaceDiscoveryModeField()
        {
            int selectedIndex = Array.IndexOf(SurfaceDiscoveryModes, _surfaceDiscoveryMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup(
                new GUIContent("Surface Source", "Controls where Floor, Wall, and Ceiling candidates may be discovered."),
                selectedIndex,
                SurfaceDiscoveryModeOptions);

            if (!EditorGUI.EndChangeCheck())
                return;

            _surfaceDiscoveryMode = SurfaceDiscoveryModes[Mathf.Clamp(selectedIndex, 0, SurfaceDiscoveryModes.Length - 1)];
            EditorPrefs.SetInt(SurfaceDiscoveryModeKey, (int)_surfaceDiscoveryMode);
            PlacementSolver.ClearCandidateCache();
        }

        private bool ShouldShowAreaDecomposition()
        {
            if (_surfaceDiscoveryMode == SurfaceDiscoveryMode.AllMatchingSurfacesInVolume)
                return false;

            return (_placementTargets & (PlacementTarget.Floor | PlacementTarget.Ceiling)) != 0;
        }

        private void DrawAreaDecompositionModeField()
        {
            int selectedIndex = Array.IndexOf(AreaDecompositionModes, _areaDecompositionMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            selectedIndex = EditorGUILayout.Popup(
                new GUIContent("Boundary Regions", "Controls how voxel floor and ceiling cells are converted into placement regions. It does not affect All Matching Surfaces or wall-only generation."),
                selectedIndex,
                AreaDecompositionOptions);
            _areaDecompositionMode = AreaDecompositionModes[Mathf.Clamp(selectedIndex, 0, AreaDecompositionModes.Length - 1)];
        }

        private void DrawDetailedDiagnosticsField()
        {
            EditorGUI.BeginChangeCheck();
            bool detailedDiagnostics = EditorGUILayout.Toggle(
                new GUIContent("Detailed Diagnostics", "Store per-attempt positions, bounds, and rejection details for debugging. Use only when needed because reports can become large."),
                _detailedDiagnostics);

            if (!EditorGUI.EndChangeCheck())
                return;

            _detailedDiagnostics = detailedDiagnostics;
            EditorPrefs.SetBool(DetailedDiagnosticsKey, _detailedDiagnostics);
        }

        private void DrawSurfaceClassificationSettings()
        {
            EditorGUI.BeginChangeCheck();

            float floorAngle = EditorGUILayout.Slider(
                new GUIContent("Floor Angle", "Maximum slope from upward-facing horizontal that still counts as a floor."),
                _floorSurfaceAngleDegrees,
                0f,
                89.9f);
            float ceilingAngle = EditorGUILayout.Slider(
                new GUIContent("Ceiling Angle", "Maximum slope from downward-facing horizontal that still counts as a ceiling."),
                _ceilingSurfaceAngleDegrees,
                0f,
                89.9f);

            if (!EditorGUI.EndChangeCheck())
                return;

            _floorSurfaceAngleDegrees = Mathf.Clamp(floorAngle, 0f, 89.9f);
            _ceilingSurfaceAngleDegrees = Mathf.Clamp(ceilingAngle, 0f, 89.9f);

            EditorPrefs.SetFloat(FloorSurfaceAngleKey, _floorSurfaceAngleDegrees);
            EditorPrefs.SetFloat(CeilingSurfaceAngleKey, _ceilingSurfaceAngleDegrees);
        }

        private void DrawGenerationButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(GenerateButtonContent) && CreateRequest(out GenerationRequest generateRequest))
                    GenerationWorkflow.Generate(generateRequest);

                if (GUILayout.Button(ClearGeneratedButtonContent))
                {
                    IAreaSource areaSource = CreateAreaSource();

                    if (areaSource != null)
                        GenerationWorkflow.Clear(areaSource);
                    else
                        Debug.LogWarning("No target area selected. Choose a target area/location in the Genix window before clearing generated objects.");
                }

                if (GUILayout.Button(RegenerateButtonContent) && CreateRequest(out GenerationRequest regenerateRequest))
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
                if (GUILayout.Button(ValidateSetupButtonContent))
                    ValidateSceneSetup();

                if (GUILayout.Button(SaveLayoutButtonContent))
                    SaveCurrentLayout();
            }

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
                _detailedDiagnostics,
                CreateSupportDistributionSettings());

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
                if (issue.Severity is not (SceneSetupIssueSeverity.Error or SceneSetupIssueSeverity.Warning))
                    continue;

                MessageType type = issue.Severity switch
                {
                    SceneSetupIssueSeverity.Error => MessageType.Error,
                    _ => MessageType.Warning
                };
                EditorGUILayout.HelpBox(issue.Message, type);
            }
        }

        private void DrawSurfaceLayerField(GUIContent label, ref LayerMask currentMask, string editorPrefsKey)
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
