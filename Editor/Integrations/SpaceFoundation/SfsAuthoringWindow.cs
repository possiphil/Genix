using System;
using System.Collections.Generic;
using System.Linq;
using SpaceFoundationSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Creates voxel-aligned Space Foundation scenes and reusable location layouts.</summary>
    internal sealed class SfsAuthoringWindow : EditorWindow
    {
        private const int PreviewVolumeLimit = 500;
        private const float LabelWidth = 155f;

        [SerializeField] private SpaceFoundationSystem.SpaceFoundation _foundation;
        [SerializeField] private float _newFoundationVoxelSize = 1f;
        [SerializeField] private SfsAuthoringLayoutType _layoutType;
        [SerializeField] private SfsAuthoringSizeMode _sizeMode;
        [SerializeField] private SfsAuthoringCenterMode _centerMode;
        [SerializeField] private string _layoutName = "Location";
        [SerializeField] private Vector3 _manualCenter;
        [SerializeField] private Vector3 _worldSize = new(10f, 4f, 10f);
        [SerializeField] private Vector3Int _voxelCounts = new(10, 4, 10);
        [SerializeField] private Vector3Int _gridCounts = Vector3Int.one;
        [SerializeField] private Vector3Int _separatorCells = Vector3Int.one;
        [SerializeField] private bool _usePerAxisRoomSizes;
        [SerializeField] private List<int> _xRoomCells = new() { 10 };
        [SerializeField] private List<int> _yRoomCells = new() { 4 };
        [SerializeField] private List<int> _zRoomCells = new() { 10 };
        [SerializeField] private SfsFootprintTemplate _footprintTemplate;
        [SerializeField] private Vector2Int _footprintDimensions = new(4, 4);
        [SerializeField] private Vector2Int _footprintTileCells = new(4, 4);
        [SerializeField] private int _footprintHeightCells = 4;
        [SerializeField] private List<Vector2Int> _customFootprint = new();
        [SerializeField] private bool _automaticAnchorRange = true;
        [SerializeField] private float _anchorRangeOverride = 40f;
        [SerializeField] private bool _showAdvanced;
        [SerializeField] private bool _showValidation;
        [SerializeField] private bool _showPreview = true;

        private Vector2 _scroll;
        private SfsAuthoringPlan _plan;
        private string _planError = string.Empty;
        private List<SfsAuthoringValidationMessage> _sceneMessages = new();

        [MenuItem("Tools/Genix/SFS Authoring", false, 40)]
        public static void Open()
        {
            SfsAuthoringWindow window = GetWindow<SfsAuthoringWindow>("SFS Authoring");
            window.minSize = new Vector2(520f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            if (!_foundation)
                _foundation = SfsAuthoringSceneBuilder.FindSingleFoundation();

            SceneView.duringSceneGui += DrawScenePreview;
            Selection.selectionChanged += OnSelectionChanged;
            RebuildPlan();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScenePreview;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (_centerMode == SfsAuthoringCenterMode.SelectionBounds || _sizeMode == SfsAuthoringSizeMode.FitSelection)
                RebuildPlan();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawFoundationSection();
            EditorGUILayout.Space(8f);
            DrawElementActions();
            EditorGUILayout.Space(8f);
            DrawLayoutSection();
            EditorGUILayout.Space(8f);
            DrawPlanSummary();
            EditorGUILayout.Space(8f);
            DrawCreateActions();
            EditorGUILayout.Space(8f);
            DrawValidation();

            EditorGUILayout.EndScrollView();
            if (EditorGUI.EndChangeCheck())
            {
                NormalizeState();
                RebuildPlan();
                SceneView.RepaintAll();
            }
        }

        private void DrawFoundationSection()
        {
            EditorGUILayout.LabelField("Space Foundation", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _foundation = (SpaceFoundationSystem.SpaceFoundation)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Foundation",
                        "The Space Foundation that owns the generated anchors and defines voxel size and delimiter layers."),
                    _foundation,
                    typeof(SpaceFoundationSystem.SpaceFoundation),
                    true);

                if (_foundation)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.FloatField(new GUIContent("Voxel Size", "The selected Foundation's voxel size is preserved."), _foundation.voxelSize);
                }
                else
                {
                    _newFoundationVoxelSize = EditorGUILayout.FloatField(
                        new GUIContent("New Voxel Size", "Voxel size used only when this window creates a new Foundation."),
                        _newFoundationVoxelSize);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(_foundation ? "Select Foundation" : "Create Foundation"))
                    {
                        if (_foundation)
                        {
                            Selection.activeObject = _foundation.gameObject;
                            EditorGUIUtility.PingObject(_foundation.gameObject);
                        }
                        else
                        {
                            _foundation = SfsAuthoringSceneBuilder.CreateFoundation(_newFoundationVoxelSize);
                            RebuildPlan();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!_foundation))
                    {
                        if (GUILayout.Button(new GUIContent("Configure Layer", "Adds the SFS Delimiter layer to this Foundation's delimiting mask.")))
                            SfsAuthoringSceneBuilder.ConfigureFoundationLayerMask(_foundation, out _);
                    }
                }
            }
        }

        private void DrawElementActions()
        {
            EditorGUILayout.LabelField("Basic Elements", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Anchor", "Creates an SFS Anchor at the current layout center.")))
                {
                    SpaceFoundationSystem.SpaceFoundation foundation = EnsureFoundation();
                    if (foundation)
                    {
                        float range = Mathf.Max(40f, foundation.voxelSize * 8f);
                        SfsAuthoringSceneBuilder.CreateAnchor(ResolveCenter(), foundation, range);
                    }
                }

                if (GUILayout.Button(new GUIContent("Box Delimiter", "Creates a collider-backed SFS Delimiter at the current layout center.")))
                {
                    SpaceFoundationSystem.SpaceFoundation foundation = EnsureFoundation();
                    if (foundation)
                    {
                        float voxelSize = foundation.voxelSize;
                        SfsAuthoringSceneBuilder.CreateBoxDelimiter(
                            ResolveCenter(),
                            new Vector3(voxelSize * 4f, voxelSize * 4f, voxelSize * 0.92f),
                            foundation);
                    }
                }

                using (new EditorGUI.DisabledScope(!Selection.gameObjects.Any(value => value.GetComponent<Collider>())))
                {
                    if (GUILayout.Button(new GUIContent("Convert Colliders", "Adds Delimiter components and the correct layer to selected collider objects.")))
                    {
                        SpaceFoundationSystem.SpaceFoundation foundation = EnsureFoundation();
                        if (foundation)
                            SfsAuthoringSceneBuilder.ConvertSelectedColliders(foundation, out _);
                    }
                }
            }
        }

        private void DrawLayoutSection()
        {
            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _layoutType = (SfsAuthoringLayoutType)EditorGUILayout.EnumPopup(
                    new GUIContent("Layout Type", GetLayoutTooltip(_layoutType)),
                    _layoutType);
                _layoutName = EditorGUILayout.TextField(
                    new GUIContent("Name", "Name of the generated scene hierarchy."),
                    _layoutName);

                DrawCenterFields();
                EditorGUILayout.Space(3f);

                switch (_layoutType)
                {
                    case SfsAuthoringLayoutType.BoundedLocation:
                        DrawBoundedFields();
                        break;
                    case SfsAuthoringLayoutType.LocationGrid:
                        DrawGridFields();
                        break;
                    case SfsAuthoringLayoutType.FootprintLocation:
                        DrawFootprintFields();
                        break;
                }

                EditorGUILayout.Space(4f);
                _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced", true);
                if (_showAdvanced)
                {
                    _automaticAnchorRange = EditorGUILayout.Toggle(
                        new GUIContent("Automatic Anchor Range", "Covers the generated location plus a two-voxel safety margin."),
                        _automaticAnchorRange);
                    if (!_automaticAnchorRange)
                    {
                        _anchorRangeOverride = EditorGUILayout.FloatField(
                            new GUIContent("Anchor Range", "Maximum world-space distance that SFS may explore from every generated anchor."),
                            _anchorRangeOverride);
                    }

                    _showPreview = EditorGUILayout.Toggle(
                        new GUIContent("Scene Preview", "Shows planned interiors, delimiters, and anchors before scene objects are created."),
                        _showPreview);
                }
            }
        }

        private void DrawCenterFields()
        {
            _centerMode = (SfsAuthoringCenterMode)EditorGUILayout.EnumPopup(
                new GUIContent("Center Source", "Manual position, current Scene view pivot, or bounds of the selected objects."),
                _centerMode);

            if (_centerMode == SfsAuthoringCenterMode.Manual)
            {
                _manualCenter = EditorGUILayout.Vector3Field("Center", _manualCenter);
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Vector3Field("Resolved Center", ResolveCenter());
            }
        }

        private void DrawBoundedFields()
        {
            _sizeMode = (SfsAuthoringSizeMode)EditorGUILayout.EnumPopup(
                new GUIContent("Size Input", "World units round up to full cells; voxel counts specify exact free cells; Fit Selection uses selected geometry bounds."),
                _sizeMode);

            if (_sizeMode == SfsAuthoringSizeMode.WorldUnits)
                _worldSize = EditorGUILayout.Vector3Field("Requested Size", _worldSize);
            else if (_sizeMode == SfsAuthoringSizeMode.VoxelCounts)
                _voxelCounts = EditorGUILayout.Vector3IntField("Free Voxel Cells", _voxelCounts);
            else
                DrawSelectionBoundsStatus();
        }

        private void DrawGridFields()
        {
            _gridCounts = EditorGUILayout.Vector3IntField(
                new GUIContent("Grid Counts", "Number of locations on X, Y, and Z. For example, 1 x 2 x 1 creates two stacked locations."),
                _gridCounts);
            _separatorCells = EditorGUILayout.Vector3IntField(
                new GUIContent("Separator Cells", "Blocked voxel bands between neighbouring locations. One cell is the safe default."),
                _separatorCells);
            _usePerAxisRoomSizes = EditorGUILayout.Toggle(
                new GUIContent("Per-Axis Sizes", "Use a separate exact free-cell count for each X column, Y level, and Z row while keeping every row and column aligned."),
                _usePerAxisRoomSizes);

            if (!_usePerAxisRoomSizes)
            {
                _sizeMode = (SfsAuthoringSizeMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Room Size Input", "Uniform room size in world units or exact voxel counts."),
                    _sizeMode == SfsAuthoringSizeMode.FitSelection ? SfsAuthoringSizeMode.WorldUnits : _sizeMode);
                if (_sizeMode == SfsAuthoringSizeMode.WorldUnits)
                    _worldSize = EditorGUILayout.Vector3Field("Room Size", _worldSize);
                else
                    _voxelCounts = EditorGUILayout.Vector3IntField("Room Voxel Cells", _voxelCounts);
            }
            else
            {
                DrawAxisList("X Column Cells", _xRoomCells, _gridCounts.x);
                DrawAxisList("Y Level Cells", _yRoomCells, _gridCounts.y);
                DrawAxisList("Z Row Cells", _zRoomCells, _gridCounts.z);
            }
        }

        private void DrawFootprintFields()
        {
            _footprintTemplate = (SfsFootprintTemplate)EditorGUILayout.EnumPopup(
                new GUIContent("Footprint", "Choose a connected horizontal shape. Custom supports an editable occupancy mask."),
                _footprintTemplate);
            _footprintDimensions = EditorGUILayout.Vector2IntField(
                new GUIContent("Module Grid", "Width and depth of the footprint mask in modules."),
                _footprintDimensions);
            _footprintTileCells = EditorGUILayout.Vector2IntField(
                new GUIContent("Cells Per Module", "Free voxel cells represented by each occupied footprint module."),
                _footprintTileCells);
            _footprintHeightCells = EditorGUILayout.IntField(
                new GUIContent("Height Cells", "Free vertical voxel cells in the location."),
                _footprintHeightCells);

            DrawFootprintMask();
        }

        private void DrawFootprintMask()
        {
            HashSet<Vector2Int> mask = ResolveFootprintMask();
            bool editable = _footprintTemplate == SfsFootprintTemplate.Custom;
            EditorGUILayout.LabelField(
                new GUIContent(
                    "Occupancy Mask",
                    _footprintDimensions.x > 16 || _footprintDimensions.y > 16
                        ? "X marks occupied modules. The final mask must be 4-neighbour connected. The editor displays the first 16 x 16 modules; the planner supports up to 64 x 64."
                        : "X marks occupied modules. The final mask must be 4-neighbour connected."),
                EditorStyles.miniBoldLabel);

            int width = Mathf.Clamp(_footprintDimensions.x, 1, 16);
            int depth = Mathf.Clamp(_footprintDimensions.y, 1, 16);
            using (new EditorGUI.DisabledScope(!editable))
            {
                for (int z = depth - 1; z >= 0; z--)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(LabelWidth);
                        for (int x = 0; x < width; x++)
                        {
                            Vector2Int cell = new(x, z);
                            bool active = mask.Contains(cell);
                            bool next = GUILayout.Toggle(active, active ? "X" : ".", EditorStyles.miniButton, GUILayout.Width(24f));
                            if (editable && next != active)
                            {
                                if (next)
                                    AddCustomCell(cell);
                                else
                                    _customFootprint.Remove(cell);
                            }
                        }
                        GUILayout.FlexibleSpace();
                    }
                }
            }

        }

        private static void DrawAxisList(string label, IList<int> values, int count)
        {
            count = Mathf.Max(1, count);
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            for (int i = 0; i < count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(16f);
                    values[i] = EditorGUILayout.IntField($"{i + 1}", values[i]);
                }
            }
        }

        private void DrawSelectionBoundsStatus()
        {
            if (TryGetSelectionBounds(out Bounds bounds))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Vector3Field("Selection Center", bounds.center);
                    EditorGUILayout.Vector3Field("Selection Size", bounds.size);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select an object with a Renderer or Collider to fit a location.", MessageType.Warning);
            }
        }

        private void DrawPlanSummary()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_plan == null)
                {
                    EditorGUILayout.HelpBox(_planError, MessageType.Error);
                    return;
                }

                EditorGUILayout.LabelField("Locations", _plan.LocationCount.ToString());
                EditorGUILayout.LabelField("Delimiters", _plan.Delimiters.Count.ToString());
                EditorGUILayout.LabelField("Anchors", _plan.Anchors.Count.ToString());
                EditorGUILayout.Vector3Field("Requested Center", _plan.RequestedCenter);
                float centerDelta = Vector3.Distance(_plan.RequestedCenter, _plan.ActualCenter);
                EditorGUILayout.Vector3Field(new GUIContent(
                        "Actual Center",
                        centerDelta > 0.0001f
                            ? $"Center snapped by {centerDelta:0.###} world units to align with the SFS voxel grid."
                            : "Voxel-aligned center used by the generated layout."),
                    _plan.ActualCenter);
                EditorGUILayout.Vector3Field("Requested Size", _plan.RequestedSize);
                EditorGUILayout.Vector3Field(new GUIContent(
                        "Actual Voxel Size",
                        _plan.InteriorVolumes.Count > PreviewVolumeLimit
                            ? $"Scene preview is limited to {PreviewVolumeLimit} of {_plan.InteriorVolumes.Count} interior volumes; creation is not limited."
                            : "Voxel-aligned size used by the generated layout."),
                    _plan.ActualSize);
            }
        }

        private void DrawCreateActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_plan == null))
                {
                    if (GUILayout.Button("Create Layout", GUILayout.Height(32f)))
                        CreateLayout(computeAfterCreation: false);

                    if (GUILayout.Button(new GUIContent("Create + Compute", "Creates the layout, validates the scene, then runs the installed SFS Compute Graph command."), GUILayout.Height(32f)))
                        CreateLayout(computeAfterCreation: true);
                }
            }
        }

        private void DrawValidation()
        {
            _showValidation = EditorGUILayout.Foldout(_showValidation, "Scene Validation", true);
            if (!_showValidation)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("Validate Scene"))
                    _sceneMessages = SfsAuthoringValidator.ValidateScene(_foundation);

                foreach (SfsAuthoringValidationMessage message in _sceneMessages)
                {
                    if (message.Type is MessageType.Warning or MessageType.Error)
                        EditorGUILayout.HelpBox(message.Text, message.Type);
                }
            }
        }

        private void CreateLayout(bool computeAfterCreation)
        {
            SpaceFoundationSystem.SpaceFoundation foundation = EnsureFoundation();
            if (!foundation)
                return;

            GameObject created = SfsAuthoringSceneBuilder.CreateLayout(_plan, foundation, out string error);
            if (!created)
            {
                EditorUtility.DisplayDialog("SFS Authoring", error, "OK");
                return;
            }

            _sceneMessages = SfsAuthoringValidator.ValidateScene(foundation);
            if (!computeAfterCreation)
                return;

            SfsAuthoringValidationMessage firstError = _sceneMessages.FirstOrDefault(value => value.Type == MessageType.Error);
            if (!string.IsNullOrEmpty(firstError.Text))
            {
                _showValidation = true;
                EditorUtility.DisplayDialog(
                    "SFS Compute Blocked",
                    $"The layout was created, but Compute was not started:\n\n{firstError.Text}",
                    "OK");
                return;
            }

            Physics.SyncTransforms();
            EditorApplication.delayCall += SfsAuthoringSceneBuilder.RunCompute;
        }

        private SpaceFoundationSystem.SpaceFoundation EnsureFoundation()
        {
            if (_foundation)
                return _foundation;

            SpaceFoundationSystem.SpaceFoundation existing = SfsAuthoringSceneBuilder.FindSingleFoundation();
            if (existing)
            {
                _foundation = existing;
            }
            else
            {
                SpaceFoundationSystem.SpaceFoundation[] foundations = SfsAuthoringSceneBuilder.FindFoundations();
                if (foundations.Length > 1)
                {
                    EditorUtility.DisplayDialog(
                        "Select a Space Foundation",
                        "Multiple Space Foundations exist. Assign the intended one in the Foundation field before creating content.",
                        "OK");
                    return null;
                }

                _foundation = SfsAuthoringSceneBuilder.CreateFoundation(_newFoundationVoxelSize);
            }
            RebuildPlan();
            return _foundation;
        }

        private void NormalizeState()
        {
            _newFoundationVoxelSize = Mathf.Max(0.001f, _newFoundationVoxelSize);
            _worldSize = Max(_worldSize, 0.001f);
            _voxelCounts = Max(_voxelCounts, 1);
            _gridCounts = Max(_gridCounts, 1);
            _separatorCells = Max(_separatorCells, 1);
            _footprintDimensions = new Vector2Int(
                Mathf.Clamp(_footprintDimensions.x, 1, 64),
                Mathf.Clamp(_footprintDimensions.y, 1, 64));
            _footprintTileCells = new Vector2Int(
                Mathf.Max(1, _footprintTileCells.x),
                Mathf.Max(1, _footprintTileCells.y));
            _footprintHeightCells = Mathf.Max(1, _footprintHeightCells);
            _anchorRangeOverride = Mathf.Max(0.01f, _anchorRangeOverride);
            if (_layoutType == SfsAuthoringLayoutType.LocationGrid && _sizeMode == SfsAuthoringSizeMode.FitSelection)
                _sizeMode = SfsAuthoringSizeMode.WorldUnits;

            ResizeList(_xRoomCells, _gridCounts.x, _voxelCounts.x);
            ResizeList(_yRoomCells, _gridCounts.y, _voxelCounts.y);
            ResizeList(_zRoomCells, _gridCounts.z, _voxelCounts.z);
            _customFootprint.RemoveAll(value =>
                value.x < 0 || value.y < 0 ||
                value.x >= _footprintDimensions.x || value.y >= _footprintDimensions.y);
            if (_footprintTemplate == SfsFootprintTemplate.Custom && _customFootprint.Count == 0)
                _customFootprint.Add(Vector2Int.zero);
        }

        private void RebuildPlan()
        {
            float voxelSize = _foundation ? _foundation.voxelSize : Mathf.Max(0.001f, _newFoundationVoxelSize);
            SfsAuthoringRequest request = BuildRequest(voxelSize);
            SfsAuthoringPlanner.TryCreate(request, voxelSize, out _plan, out _planError);
        }

        private SfsAuthoringRequest BuildRequest(float voxelSize)
        {
            Vector3 center = ResolveCenter();
            Vector3 worldSize = _worldSize;
            if (_sizeMode == SfsAuthoringSizeMode.FitSelection && TryGetSelectionBounds(out Bounds selectionBounds))
            {
                center = selectionBounds.center;
                worldSize = selectionBounds.size;
            }

            Vector3Int uniformCells = _sizeMode == SfsAuthoringSizeMode.WorldUnits
                ? SfsAuthoringPlanner.WorldSizeToCells(_worldSize, voxelSize)
                : _voxelCounts;

            SfsAuthoringRequest request = new()
            {
                Name = _layoutName,
                LayoutType = _layoutType,
                SizeMode = _sizeMode,
                Center = center,
                WorldSize = worldSize,
                VoxelCounts = _voxelCounts,
                GridCounts = _gridCounts,
                UniformRoomCells = uniformCells,
                SeparatorCells = _separatorCells,
                UsePerAxisRoomSizes = _usePerAxisRoomSizes,
                FootprintTemplate = _footprintTemplate,
                FootprintDimensions = _footprintDimensions,
                FootprintTileCells = _footprintTileCells,
                FootprintHeightCells = _footprintHeightCells,
                AutomaticAnchorRange = _automaticAnchorRange,
                AnchorRangeOverride = _anchorRangeOverride
            };

            CopyList(_xRoomCells, request.XRoomCells);
            CopyList(_yRoomCells, request.YRoomCells);
            CopyList(_zRoomCells, request.ZRoomCells);
            foreach (Vector2Int cell in _customFootprint)
                request.CustomFootprint.Add(cell);
            return request;
        }

        private Vector3 ResolveCenter()
        {
            if (_centerMode == SfsAuthoringCenterMode.SceneViewPivot && SceneView.lastActiveSceneView)
                return SceneView.lastActiveSceneView.pivot;
            if (_centerMode == SfsAuthoringCenterMode.SelectionBounds && TryGetSelectionBounds(out Bounds bounds))
                return bounds.center;
            return _manualCenter;
        }

        private HashSet<Vector2Int> ResolveFootprintMask()
        {
            return SfsAuthoringPlanner.CreateFootprintMask(
                _footprintTemplate,
                _footprintDimensions,
                _customFootprint);
        }

        private void DrawScenePreview(SceneView sceneView)
        {
            if (!_showPreview || _plan == null)
                return;

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;

            Handles.color = new Color(0.2f, 0.85f, 0.55f, 0.8f);
            foreach (SfsAuthoringCellVolume interior in _plan.InteriorVolumes.Take(PreviewVolumeLimit))
            {
                Bounds bounds = interior.ToWorldBounds(_plan.VoxelSize);
                Handles.DrawWireCube(bounds.center, bounds.size);
            }

            Handles.color = new Color(1f, 0.55f, 0.18f, 0.9f);
            foreach (SfsAuthoringCellVolume delimiter in _plan.Delimiters.Take(PreviewVolumeLimit))
            {
                Bounds bounds = delimiter.ToWorldBounds(_plan.VoxelSize);
                Handles.DrawWireCube(bounds.center, bounds.size);
            }

            Handles.color = new Color(0.2f, 0.65f, 1f, 1f);
            float anchorSize = HandleUtility.GetHandleSize(_plan.ActualCenter) * 0.08f;
            foreach (SfsAuthoringAnchorPlan anchor in _plan.Anchors.Take(PreviewVolumeLimit))
                Handles.SphereHandleCap(0, anchor.ToWorldPosition(_plan.VoxelSize), Quaternion.identity, anchorSize, EventType.Repaint);

            Handles.zTest = previousZTest;
        }

        private static bool TryGetSelectionBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (GameObject selected in Selection.gameObjects)
            {
                foreach (Renderer renderer in selected.GetComponentsInChildren<Renderer>())
                    Encapsulate(renderer.bounds, ref bounds, ref found);
                foreach (Collider collider in selected.GetComponentsInChildren<Collider>())
                    Encapsulate(collider.bounds, ref bounds, ref found);
            }

            return found;
        }

        private static void Encapsulate(Bounds value, ref Bounds result, ref bool found)
        {
            if (!found)
            {
                result = value;
                found = true;
            }
            else
            {
                result.Encapsulate(value);
            }
        }

        private static void ResizeList(List<int> values, int count, int fallback)
        {
            count = Mathf.Max(1, count);
            while (values.Count < count)
                values.Add(Mathf.Max(1, values.Count > 0 ? values[^1] : fallback));
            while (values.Count > count)
                values.RemoveAt(values.Count - 1);
            for (int i = 0; i < values.Count; i++)
                values[i] = Mathf.Max(1, values[i]);
        }

        private static void CopyList(IReadOnlyList<int> source, List<int> target)
        {
            target.Clear();
            for (int i = 0; i < source.Count; i++)
                target.Add(source[i]);
        }

        private void AddCustomCell(Vector2Int cell)
        {
            if (!_customFootprint.Contains(cell))
                _customFootprint.Add(cell);
        }

        private static Vector3 Max(Vector3 value, float minimum) =>
            new(Mathf.Max(minimum, value.x), Mathf.Max(minimum, value.y), Mathf.Max(minimum, value.z));

        private static Vector3Int Max(Vector3Int value, int minimum) =>
            new(Mathf.Max(minimum, value.x), Mathf.Max(minimum, value.y), Mathf.Max(minimum, value.z));

        private static string GetLayoutTooltip(SfsAuthoringLayoutType layoutType)
        {
            return layoutType switch
            {
                SfsAuthoringLayoutType.BoundedLocation => "One closed, rectangular location with one anchor.",
                SfsAuthoringLayoutType.LocationGrid => "Aligned adjacent or stacked locations with shared separator bands.",
                SfsAuthoringLayoutType.FootprintLocation => "One connected non-rectangular location from a horizontal occupancy mask.",
                _ => string.Empty
            };
        }
    }
}
