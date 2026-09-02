using System;
using System.Collections.Generic;
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
using Genix.Diagnostics;
using Genix.Extensions;
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
        private const float RunButtonGap = 4f;
        private const float PrimaryRunButtonHeight = 28f;
        private const SurfaceDiscoveryMode DefaultSurfaceDiscoveryMode = SurfaceDiscoveryMode.AllMatchingSurfacesInVolume;
        private static readonly GUIContent PreviewRunButtonContent = new(
            "Preview",
            "Plan the result without changing the scene. Use Apply Preview when the plan looks right.");
        private static readonly GUIContent ApplyPreviewButtonContent = new(
            "Apply Preview",
            "Generate the accepted placements from the last preview run.");
        private static readonly GUIContent GenerateButtonContent = new(
            "Generate",
            "Add another generated batch to the target area. Existing generated objects remain and participate in overlap and constraint checks.");
        private static readonly GUIContent ClearGeneratedButtonContent = new(
            "Delete Generated Objects…",
            "Remove all Genix-generated objects in the selected target area.");
        private static readonly GUIContent RegenerateButtonContent = new(
            "Regenerate",
            "Replace the generated result in this target area using the current settings.");
        private static readonly GUIContent RandomizeSeedButtonContent = new(
            "Randomize",
            "Choose a new fixed seed. The value remains reproducible until randomized again.");
        private static readonly GUIContent ValidateSetupButtonContent = new(
            "Check Setup",
            "Check the target area, assets, surfaces, and settings without generating objects.");
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
            GenerationConfigurationOptions.TargetDistributionModes;

        private static readonly GUIContent[] TargetDistributionOptions =
            GenerationConfigurationOptions.TargetDistributionLabels;

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
            new("Generated + Scene Objects", "Keep placements near generated objects or matching scene objects."),
            new("Generated Objects", "Keep new placements near objects already planned or generated by Genix."),
            new("Scene Objects", "Keep placements near existing scene objects on the selected layers."),
            new("Selected Objects", "Keep placements near the transforms currently selected in the Hierarchy or Scene view.")
        };

        private static readonly SurfaceDiscoveryMode[] SurfaceDiscoveryModes =
            GenerationConfigurationOptions.SurfaceDiscoveryModes;

        private static readonly GUIContent[] SurfaceDiscoveryModeOptions =
            GenerationConfigurationOptions.SurfaceDiscoveryLabels;

        private static readonly AreaDecompositionMode[] AreaDecompositionModes =
            GenerationConfigurationOptions.AreaDecompositionModes;

        private static readonly GUIContent[] AreaDecompositionOptions =
            GenerationConfigurationOptions.AreaDecompositionLabels;

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

        private const string DefaultStylePresetName = "Natural";
        private const string DefaultAssetPoolName = "Default Pool";

        private StylePreset[] _stylePresets = Array.Empty<StylePreset>();
        private string[] _stylePresetOptions = Array.Empty<string>();

        private AssetPool[] _assetPools = Array.Empty<AssetPool>();
        private string[] _assetPoolOptions = Array.Empty<string>();

        private SceneSetupReport _lastSceneSetupReport;
        private bool _showSurfaceSearchSettings;
        private bool _showDistributionSettings;
        private bool _showRelationshipSettings;
        private bool _showRunSettings;
        private Vector2 _scrollPosition;

        /// <summary>Opens or focuses the corresponding Genix editor window.</summary>
        [MenuItem("Tools/Genix/Generator", false, 0)]
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
            AssignDefaultReferencesIfMissing();
        }

        private void OnFocus()
        {
            DiagnosticsPreview.ClearCurrentReport();

            RefreshSelectableAssets();
            AssignDefaultReferencesIfMissing();
            Repaint();
        }

        private void OnProjectChange()
        {
            PlacementSolver.ClearCandidateCache();
            PlacementSolver.ClearSceneObjectCache();
            RefreshSelectableAssets();
            AssignDefaultReferencesIfMissing();
            Repaint();
        }

        private void OnHierarchyChange()
        {
            PlacementSolver.ClearCandidateCache();
            PlacementSolver.ClearSceneObjectCache();
            RefreshSelectableAssets();
            AssignDefaultReferencesIfMissing();
            Repaint();
        }

        private void OnGUI()
        {
            DesignerUiPreferences.DrawWindowToolbar();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            StarterContentInstaller.DrawSetupButton();

            DrawGenerationPresetSection();
            DrawInputSection();
            DrawPlacementSettingsSection();
            DrawStylePresetSection();

            if (DesignerUiPreferences.IsAdvanced)
                DrawAdvancedGeneratorSettings();

            _targetAreaSelector.DrawStatus();

            EditorGUILayout.Space(10f);

            DrawGenerationButtons();

            DrawLastRunSummary();

            EditorGUILayout.EndScrollView();
        }

        private void DrawInputSection()
        {
            AssignDefaultAssetPoolIfMissing();

            _targetAreaSelector.Draw(new GUIContent(
                "Target Area",
                "Spatial area in which Genix may search for valid placement positions. The available entries come from the selected area-provider integration."));

            _assetPool = AssetDropdown.DrawAssetPoolDropdownWithEditButton(
                new GUIContent(
                    "Asset Pool",
                    "Choose which assets Genix may place. Manual pools use an explicit list; rule-based pools find matching catalog assets."),
                _assetPools,
                _assetPoolOptions,
                _assetPool);
            _objectCount = Mathf.Max(1, EditorGUILayout.IntField(
                new GUIContent("Object Count", "Requested number of objects. Allow Partial Results may return fewer when valid space runs out."),
                _objectCount));
        }

        private void DrawAdvancedGeneratorSettings()
        {
            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);

            _showSurfaceSearchSettings = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showSurfaceSearchSettings,
                new GUIContent("Surface Search", "Control where Genix looks for floors, walls, and ceilings."));
            if (_showSurfaceSearchSettings)
            {
                EditorGUI.indentLevel++;
                DrawSurfaceDiscoveryModeField();

                if (ShouldShowAreaDecomposition())
                    DrawAreaDecompositionModeField();

                if (_surfaceDiscoveryMode != SurfaceDiscoveryMode.SfsBoundaries)
                {
                    DrawSurfaceLayerField(new GUIContent("Floor Layers", "Use upward-facing colliders on these layers as floors."), ref _floorSurfaceLayers, FloorSurfaceMaskKey);
                    DrawSurfaceLayerField(new GUIContent("Wall Layers", "Use near-vertical colliders on these layers as walls."), ref _wallSurfaceLayers, WallSurfaceMaskKey);
                    DrawSurfaceLayerField(new GUIContent("Ceiling Layers", "Use downward-facing colliders on these layers as ceilings."), ref _ceilingSurfaceLayers, CeilingSurfaceMaskKey);
                    DrawSurfaceClassificationSettings();
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _showDistributionSettings = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showDistributionSettings,
                new GUIContent("Distribution", "Control how placements are divided across targets and support surfaces."));
            if (_showDistributionSettings)
            {
                EditorGUI.indentLevel++;
                DrawAdvancedDistributionSettings();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _showRelationshipSettings = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showRelationshipSettings,
                new GUIContent("Global Proximity", "Keep every generated placement near an eligible scene or generated object."));
            if (_showRelationshipSettings)
            {
                EditorGUI.indentLevel++;
                DrawRelativePlacementSection();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _showRunSettings = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showRunSettings,
                new GUIContent("Run and Reproducibility", "Control partial results, repeatable seeds, and diagnostic capture."));
            if (_showRunSettings)
            {
                EditorGUI.indentLevel++;
                DrawGenerationWorkflowSettings();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

        }

        private void DrawGenerationWorkflowSettings()
        {
            DrawDetailedDiagnosticsField();

            EditorGUI.BeginChangeCheck();
            bool bestEffort = EditorGUILayout.Toggle(
                new GUIContent("Allow Partial Results", "Keep valid placements when Genix cannot reach the requested count. Disable to require a complete result."),
                _bestEffort);

            if (EditorGUI.EndChangeCheck())
            {
                _bestEffort = bestEffort;
                EditorPrefs.SetBool(BestEffortKey, _bestEffort);
            }

            EditorGUI.BeginChangeCheck();
            bool useSeed = EditorGUILayout.Toggle(
                new GUIContent("Fixed Seed", "Reuse one seed so unchanged settings produce a repeatable result."),
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
            selectedIndex = EditorGui.Popup(
                new GUIContent("Search Mode", "Choose how Genix finds floor, wall, and ceiling candidates."),
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

            selectedIndex = EditorGui.Popup(
                new GUIContent("Voxel Boundary Detail", "Choose how closely floor and ceiling regions preserve irregular voxel boundaries."),
                selectedIndex,
                AreaDecompositionOptions);
            _areaDecompositionMode = AreaDecompositionModes[Mathf.Clamp(selectedIndex, 0, AreaDecompositionModes.Length - 1)];
        }

        private void DrawDetailedDiagnosticsField()
        {
            EditorGUI.BeginChangeCheck();
            bool detailedDiagnostics = EditorGUILayout.Toggle(
                new GUIContent("Detailed Diagnostics", "Store every placement attempt for debugging. Reports become larger and generation may take longer."),
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
                new GUIContent("Floor Slope Limit", "Maximum slope that still counts as a floor."),
                _floorSurfaceAngleDegrees,
                0f,
                89.9f);
            float ceilingAngle = EditorGUILayout.Slider(
                new GUIContent("Ceiling Slope Limit", "Maximum downward-facing slope that still counts as a ceiling."),
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
            Rect headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            const float validateWidth = 90f;
            const float moreWidth = 54f;
            Rect moreRect = new(
                headerRect.xMax - moreWidth,
                headerRect.y,
                moreWidth,
                headerRect.height);
            Rect validateRect = new(
                moreRect.x - RunButtonGap - validateWidth,
                headerRect.y,
                validateWidth,
                headerRect.height);
            Rect labelRect = new(
                headerRect.x,
                headerRect.y,
                Mathf.Max(0f, validateRect.x - RunButtonGap - headerRect.x),
                headerRect.height);

            EditorGUI.LabelField(labelRect, "Run", EditorStyles.boldLabel);

            if (GUI.Button(validateRect, ValidateSetupButtonContent, EditorStyles.miniButton))
                ValidateSceneSetup();

            if (GUI.Button(
                    moreRect,
                    new GUIContent("More", "Open less common generation actions."),
                    EditorStyles.miniButton))
            {
                ShowGenerationActionsMenu();
            }

            GUIStyle primaryButton = new(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fixedHeight = PrimaryRunButtonHeight
            };

            GetRunButtonRects(PrimaryRunButtonHeight, out Rect generateRect, out Rect regenerateRect);
            if (GUI.Button(generateRect, GenerateButtonContent, primaryButton) &&
                CreateRequest(out GenerationRequest generateRequest))
            {
                GenerationWorkflow.Generate(generateRequest);
            }

            using (new EditorGUI.DisabledScope(!HasGeneratedObjectsInSelectedArea()))
            {
                if (GUI.Button(regenerateRect, RegenerateButtonContent) &&
                    CreateRequest(out GenerationRequest regenerateRequest))
                {
                    GenerationWorkflow.Regenerate(regenerateRequest);
                }
            }

            EditorGUILayout.Space(2f);

            GetRunButtonRects(EditorGUIUtility.singleLineHeight, out Rect previewRect, out Rect applyPreviewRect);
            if (GUI.Button(previewRect, PreviewRunButtonContent))
            {
                GenerationWorkflow.ClearPreviewPlan();

                if (CreateRequest(out GenerationRequest previewRequest))
                    GenerationWorkflow.Preview(previewRequest);
            }

            using (new EditorGUI.DisabledScope(!GenerationWorkflow.HasPreviewPlan))
            {
                if (GUI.Button(applyPreviewRect, ApplyPreviewButtonContent))
                    GenerationWorkflow.ApplyPreview();
            }

            DrawSceneSetupReport();
        }

        private static void GetRunButtonRects(float height, out Rect left, out Rect right)
        {
            Rect row = EditorGUILayout.GetControlRect(false, height);
            float columnWidth = Mathf.Max(0f, (row.width - RunButtonGap) * 0.5f);
            left = new Rect(row.x, row.y, columnWidth, row.height);
            right = new Rect(left.xMax + RunButtonGap, row.y, columnWidth, row.height);
        }

        private bool HasGeneratedObjectsInSelectedArea()
        {
            return GeneratedHierarchy.HasObjects(CreateAreaSource());
        }

        private void ShowGenerationActionsMenu()
        {
            GenericMenu menu = new();
            menu.AddItem(SaveLayoutButtonContent, false, SaveCurrentLayout);

            if (DiagnosticsStore.LastDiagnostics != null)
            {
                menu.AddItem(
                    new GUIContent("Clear Last Run", "Clear the current diagnostics and unapplied preview plan."),
                    false,
                    ClearLastRun);
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(ClearGeneratedButtonContent, false, ClearGeneratedObjects);
            menu.ShowAsContext();
        }

        private static void ClearLastRun()
        {
            GenerationWorkflow.ClearPreviewPlan();
            DiagnosticsStore.Clear();
        }

        private static void DrawLastRunSummary()
        {
            GenerationDiagnostics diagnostics = DiagnosticsStore.LastDiagnostics;
            if (diagnostics == null)
                return;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Last Run", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string result = diagnostics.PlacedObjectCount >= diagnostics.RequestedObjectCount
                    ? "Complete"
                    : diagnostics.PlacedObjectCount > 0 ? "Partial" : "No placements";
                EditorGUILayout.LabelField("Result", result);
                EditorGUILayout.LabelField("Target Area", diagnostics.TargetName);
                EditorGUILayout.LabelField(
                    diagnostics.DryRun ? "Planned" : "Placed",
                    $"{diagnostics.PlacedObjectCount}/{diagnostics.RequestedObjectCount}");

                if (!string.IsNullOrWhiteSpace(diagnostics.TopRejectionReason))
                    EditorGUILayout.LabelField("Top Rejection", diagnostics.TopRejectionReason);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Save Report", "Save the richest diagnostics data recorded for this run.")))
                        DiagnosticsReportSaver.SaveReport(diagnostics);

                    if (GUILayout.Button(new GUIContent("Open Reports", "Browse saved diagnostics reports.")))
                        DiagnosticsWindow.Open();
                }
            }
        }

        private void ClearGeneratedObjects()
        {
            IAreaSource areaSource = CreateAreaSource();

            if (areaSource == null)
            {
                Debug.LogWarning("No target area selected. Choose a target area before deleting generated objects.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Delete Generated Objects",
                    $"Delete all Genix-generated objects in '{areaSource.SourceInfo.SourceName}'?",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            GenerationWorkflow.Clear(areaSource);
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
