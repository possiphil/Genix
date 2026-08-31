using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Utilities;
using Genix.Assets;
using Genix.Editor.Assets;
using Genix.Editor.Common;
using Genix.Editor.UI;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    /// <summary>Provides guided authoring for asset placement, bounds, orientation, and semantic metadata.</summary>
    [CustomEditor(typeof(AssetDefinition))]
    public sealed partial class AssetDefinitionEditor : UnityEditor.Editor
    {


























        private static readonly GUIContent AssetNameLabel = new(
            "Asset Name",
            "Designer-facing name used in pools, diagnostics, and generated object names.");
        private static readonly GUIContent PrefabLabel = new(
            "Prefab",
            "Prefab instantiated for accepted placements. Assigning it refreshes the placement bounds.");
        private static readonly GUIContent PrefabRotationOffsetLabel = new(
            "Prefab Rotation",
            "Correct imported prefab axes without editing the prefab. For wall assets, make the visible front point along local +Z.");
        private static readonly GUIContent PlacementTypeLabel = new(
            "Placement Type",
            "Surface or volume target this asset can use: Floor, Wall, Ceiling, or Inside Space.");
        private static readonly GUIContent OrientationModeLabel = new(
            "Orientation",
            "Choose whether the asset keeps its sampled rotation, faces a target, or follows its support object's forward direction.");
        private static readonly GUIContent BoundsSizeLabel = new(
            "Size (units)",
            "Source-prefab local bounds before Rotation Offset. Genix derives corrected placement dimensions for containment, spacing, overlap, and surface fit.");
        private static readonly GUIContent BoundsCenterLabel = new(
            "Center Offset (units)",
            "Source-prefab local offset to the bounds center. Prefab scale and Rotation Offset are applied automatically during placement.");
        private static readonly string[] WallDepthModeLabels =
        {
            "Average Depth",
            "Deepest",
            "Outermost"
        };

        private SerializedProperty _prefab;
        private SerializedProperty _semanticTags;
        private SerializedProperty _anyTagCategories;
        private SerializedProperty _requiredSupportTags;
        private SerializedProperty _forbiddenSupportTags;
        private SerializedProperty _requiredSupportNoneCategories;
        private SerializedProperty _forbiddenSupportAnyCategories;
        private SerializedProperty _limitPlacements;
        private SerializedProperty _maxPlacements;
        private SerializedProperty _spacingRules;
        private SerializedProperty _assetRelativePlacement;
        private SerializedProperty _pathPlacement;
        private SerializedProperty _placementType;
        private SerializedProperty _wallVerticalPlacementMode;
        private SerializedProperty _placementHeight;
        private SerializedProperty _wallMinHeight;
        private SerializedProperty _wallMaxHeight;
        private SerializedProperty _prefabRotationOffset;
        private SerializedProperty _boundsSize;
        private SerializedProperty _boundsCenterOffset;
        private SerializedProperty _reserveClearance;
        private SerializedProperty _clearanceSize;
        private SerializedProperty _clearanceCenterOffset;
        private SerializedProperty _orientationMode;
        private SerializedProperty _surfaceFitMode;
        private SerializedProperty _surfaceAlignmentMode;
        private SerializedProperty _surfaceHeightMode;
        private SerializedProperty _maxSurfaceHeightDifference;
        private SerializedProperty _minSurfaceSupport;
        private SerializedProperty _surfaceSinkOffset;
        private SerializedProperty _randomYawRotation;
        private SerializedProperty _randomPitchRotation;
        private SerializedProperty _randomRollRotation;
        private SerializedProperty _wallProximityMode;
        private SerializedProperty _wallDistance;

        private void OnEnable()
        {
            _prefab = serializedObject.FindProperty("prefab");
            _semanticTags = serializedObject.FindProperty("semanticTags");
            _anyTagCategories = serializedObject.FindProperty("anyTagCategories");
            _requiredSupportTags = serializedObject.FindProperty("requiredSupportTags");
            _forbiddenSupportTags = serializedObject.FindProperty("forbiddenSupportTags");
            _requiredSupportNoneCategories = serializedObject.FindProperty("requiredSupportNoneCategories");
            _forbiddenSupportAnyCategories = serializedObject.FindProperty("forbiddenSupportAnyCategories");
            _limitPlacements = serializedObject.FindProperty("limitPlacements");
            _maxPlacements = serializedObject.FindProperty("maxPlacements");
            _spacingRules = serializedObject.FindProperty("spacingRules");
            _assetRelativePlacement = serializedObject.FindProperty("assetRelativePlacement");
            _pathPlacement = serializedObject.FindProperty("pathPlacement");
            _placementType = serializedObject.FindProperty("placementType");
            _wallVerticalPlacementMode = serializedObject.FindProperty("wallVerticalPlacementMode");
            _placementHeight = serializedObject.FindProperty("placementHeight");
            _wallMinHeight = serializedObject.FindProperty("wallMinHeight");
            _wallMaxHeight = serializedObject.FindProperty("wallMaxHeight");
            _prefabRotationOffset = serializedObject.FindProperty("prefabRotationOffset");
            _boundsSize = serializedObject.FindProperty("boundsSize");
            _boundsCenterOffset = serializedObject.FindProperty("boundsCenterOffset");
            _reserveClearance = serializedObject.FindProperty("reserveClearance");
            _clearanceSize = serializedObject.FindProperty("clearanceSize");
            _clearanceCenterOffset = serializedObject.FindProperty("clearanceCenterOffset");
            _orientationMode = serializedObject.FindProperty("orientationMode");
            _surfaceFitMode = serializedObject.FindProperty("surfaceFitMode");
            _surfaceAlignmentMode = serializedObject.FindProperty("surfaceAlignmentMode");
            _surfaceHeightMode = serializedObject.FindProperty("surfaceHeightMode");
            _maxSurfaceHeightDifference = serializedObject.FindProperty("maxSurfaceHeightDifference");
            _minSurfaceSupport = serializedObject.FindProperty("minSurfaceSupport");
            _surfaceSinkOffset = serializedObject.FindProperty("surfaceSinkOffset");
            _randomYawRotation = serializedObject.FindProperty("randomYawRotation");
            _randomPitchRotation = serializedObject.FindProperty("randomPitchRotation");
            _randomRollRotation = serializedObject.FindProperty("randomRollRotation");
            _wallProximityMode = serializedObject.FindProperty("wallProximityMode");
            _wallDistance = serializedObject.FindProperty("wallDistance");
        }

        /// <summary>Draws and applies the custom Inspector interface.</summary>

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawAssetNameField();
            DrawPrefabSection();

            EditorGUILayout.Space(4f);

            DrawPlacementSection();

            EditorGUILayout.Space(4f);

            if (DesignerUiPreferences.IsAdvanced)
            {
                DrawSupportSurfaceSection();

                EditorGUILayout.Space(4f);

                DrawBoundsSection();

                EditorGUILayout.Space(6f);
            }

            DrawSemanticTagsSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAssetNameField()
        {
            EditorGUI.BeginChangeCheck();

            string assetName = EditorGUILayout.DelayedTextField(AssetNameLabel, target.name);

            if (!EditorGUI.EndChangeCheck())
                return;

            AssetCatalogService.Rename(
                target,
                assetName,
                "New Genix Asset");

            serializedObject.Update();
        }

        private void DrawPrefabSection()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(_prefab, PrefabLabel);

            if (EditorGUI.EndChangeCheck())
                UpdateBoundsFromPrefab();

            EditorGUILayout.PropertyField(_prefabRotationOffset, PrefabRotationOffsetLabel);
        }

        private void DrawPlacementSection()
        {
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_placementType, PlacementTypeLabel);
            EditorGUILayout.PropertyField(_limitPlacements, new GUIContent(
                "Limit Placements",
                "Limit this asset across existing and newly planned Genix output in the target area."));

            if (_limitPlacements.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUI.BeginChangeCheck();
                    int maximum = EditorGUILayout.IntField(new GUIContent(
                        "Max Placements",
                        "Maximum instances across existing and newly planned Genix output."),
                        _maxPlacements.intValue);
                    if (EditorGUI.EndChangeCheck())
                        _maxPlacements.intValue = Mathf.Max(1, maximum);
                }
            }

            if (DesignerUiPreferences.IsAdvanced)
            {
                DrawAssetSpacingRules();
                DrawAssetRelativePlacement();
                DrawPathPlacement();
            }

            if (IsWallPlacementType())
            {
                DrawWallHeightSection();

                EditorGUILayout.PropertyField(_randomRollRotation, new GUIContent(
                    "Random Roll",
                    "Try deterministic rotations around the wall normal while keeping the asset flush with the wall."));
                if (DesignerUiPreferences.IsAdvanced)
                    DrawSurfaceFitSection();
            }
            else if (IsInsideSpacePlacementType())
            {
                EditorGUILayout.PropertyField(_randomYawRotation, RotationLabel("Yaw", "vertical axis"));
                EditorGUILayout.PropertyField(_randomPitchRotation, RotationLabel("Pitch", "side axis"));
                EditorGUILayout.PropertyField(_randomRollRotation, RotationLabel("Roll", "forward axis"));
            }
            else
            {
                EditorGUILayout.PropertyField(_randomYawRotation, RotationLabel("Yaw", "surface normal"));
                if (DesignerUiPreferences.IsAdvanced)
                    DrawSurfaceFitSection();
            }

            EditorGUILayout.PropertyField(_orientationMode, OrientationModeLabel);

            if (DesignerUiPreferences.IsAdvanced && IsFloorOrCeilingPlacementType())
                DrawWallProximitySection();

            if (UsesSupportForward() && (IsWallPlacementType() || IsInsideSpacePlacementType()))
            {
                EditorGUILayout.HelpBox(
                    IsWallPlacementType()
                        ? "Wall assets already face the sampled wall normal. Use Random Roll to vary their rotation instead of Match Support Forward."
                        : "Inside Space has no supporting surface. Match Support Forward therefore cannot resolve a direction.",
                    MessageType.Warning);
            }
        }

        private static void DrawSectionHeader(string title, Action drawButtons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, EditorStyles.boldLabel, GUILayout.ExpandWidth(false));

                GUILayout.Space(8f);
                GUILayout.FlexibleSpace();

                drawButtons?.Invoke();
            }
        }

        private void UpdateBoundsFromPrefab()
        {
            GameObject prefab = _prefab.objectReferenceValue as GameObject;

            if (!AssetDefinitionFactory.TryGetPrefabBounds(prefab, out Vector3 boundsSize, out Vector3 boundsCenterOffset))
                return;

            _boundsSize.vector3Value = boundsSize;
            _boundsCenterOffset.vector3Value = boundsCenterOffset;
        }

        private bool IsWallPlacementType()
        {
            if (_placementType.enumValueIndex < 0 ||
                _placementType.enumValueIndex >= _placementType.enumNames.Length)
            {
                return false;
            }

            return _placementType.enumNames[_placementType.enumValueIndex] == nameof(PlacementType.Wall);
        }

        private bool IsInsideSpacePlacementType()
        {
            if (_placementType.enumValueIndex < 0 ||
                _placementType.enumValueIndex >= _placementType.enumNames.Length)
            {
                return false;
            }

            return _placementType.enumNames[_placementType.enumValueIndex] == nameof(PlacementType.InsideSpace);
        }

        private bool IsFloorOrCeilingPlacementType()
        {
            if (_placementType.enumValueIndex < 0 ||
                _placementType.enumValueIndex >= _placementType.enumNames.Length)
            {
                return false;
            }

            string placementType = _placementType.enumNames[_placementType.enumValueIndex];
            return placementType is nameof(PlacementType.Floor) or nameof(PlacementType.Ceiling);
        }

        private bool IsAdaptiveSurfaceFit()
        {
            if (_surfaceFitMode.enumValueIndex < 0 ||
                _surfaceFitMode.enumValueIndex >= _surfaceFitMode.enumNames.Length)
            {
                return false;
            }

            return _surfaceFitMode.enumNames[_surfaceFitMode.enumValueIndex] == nameof(SurfaceFitMode.Adaptive);
        }

        private bool UsesSupportForward()
        {
            if (_orientationMode.enumValueIndex < 0 ||
                _orientationMode.enumValueIndex >= _orientationMode.enumNames.Length)
            {
                return false;
            }

            return _orientationMode.enumNames[_orientationMode.enumValueIndex] ==
                   nameof(global::Genix.Orientation.OrientationMode.MatchSupportForward);
        }

        private static GUIContent RotationLabel(string name, string axis) =>
            new($"Random {name}", $"Apply a random rotation around the asset's {axis}.");
    }
}
