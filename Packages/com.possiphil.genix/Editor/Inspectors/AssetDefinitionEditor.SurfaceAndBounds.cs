using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Assets;
using Genix.Editor.UI;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    public sealed partial class AssetDefinitionEditor
    {
        private void DrawSurfaceFitSection()
        {
            bool isWall = IsWallPlacementType();
            EditorGUILayout.Space(2f);
            EditorGUILayout.PropertyField(_surfaceFitMode, new GUIContent(
                "Surface Fit",
                isWall
                    ? "Strict uses the sampled wall contact. Adaptive probes the complete wall-facing footprint and is recommended for uneven or curved walls."
                    : "Strict requires the footprint to fit its sampled region. Adaptive probes the real surface and is recommended for uneven terrain."));

            if (IsAdaptiveSurfaceFit())
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_surfaceAlignmentMode, new GUIContent(
                        "Rotation",
                        isWall
                            ? "Align To Surface follows the fitted wall normal. Keep Upright follows its horizontal direction without tilting vertically."
                            : "Align To Surface follows the fitted normal. Keep Upright uses the fitted height without tilting."));

                    if (isWall)
                    {
                        int selectedDepth = EditorGUILayout.Popup(
                            new GUIContent(
                                "Depth",
                                "Average Depth uses the mean supported wall depth. Deepest embeds the asset at the most recessed supported probe to avoid visible gaps. Outermost uses the most protruding probe to minimize wall penetration."),
                            _surfaceHeightMode.enumValueIndex,
                            WallDepthModeLabels);
                        _surfaceHeightMode.enumValueIndex = selectedDepth;
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(_surfaceHeightMode, new GUIContent(
                            "Height",
                            "Choose the Average, Lowest, or Highest supported probe height as the placement height."));
                    }

                    EditorGUILayout.PropertyField(_maxSurfaceHeightDifference, new GUIContent(
                        isWall ? "Max Depth Difference (units)" : "Max Height Difference (units)",
                        isWall
                            ? "Reject the placement when supported wall probes vary more than this distance along the wall normal."
                            : "Reject the placement when supported footprint probes span a larger vertical range."));
                    EditorGUILayout.PropertyField(_minSurfaceSupport, new GUIContent(
                        "Min Support",
                        isWall
                            ? "Minimum fraction of the wall-facing footprint that must find a compatible wall surface."
                            : "Minimum fraction of footprint probes that must find a compatible surface."));
                }
            }

            EditorGUILayout.PropertyField(_surfaceSinkOffset, new GUIContent(
                "Sink Offset (units)",
                isWall
                    ? "Move the asset into the wall by this distance to compensate for pivots, mounts, or tiny visible gaps."
                    : "Move the asset into the support surface by this distance to compensate for pivots or tiny visible gaps."));
        }

        private void DrawBoundsSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Bounds", EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!_prefab.objectReferenceValue))
                {
                    if (GUILayout.Button(new GUIContent(
                            "Update from Prefab",
                            "Recalculate placement size and center offset from the prefab renderers and colliders."),
                        GUILayout.Width(140f)))
                        UpdateBoundsFromPrefab();
                }
            }

            EditorGUILayout.PropertyField(_boundsSize, BoundsSizeLabel);
            EditorGUILayout.PropertyField(_boundsCenterOffset, BoundsCenterLabel);

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_reserveClearance, new GUIContent(
                    "Reserve Clearance",
                    "Reserve an additional invisible volume that fixed geometry and other generated objects may not enter. Clearance is bidirectional: other visuals cannot enter it, and it cannot overlap other visuals, clearances, or fixed colliders."));

                using (new EditorGUI.DisabledScope(!_reserveClearance.boolValue))
                {
                    if (GUILayout.Button(new GUIContent(
                            "Copy Placement Bounds",
                            "Copy the placement bounds into the clearance volume, then adjust the sides that need extra room."),
                        GUILayout.Width(148f)))
                    {
                        _clearanceSize.vector3Value = _boundsSize.vector3Value;
                        _clearanceCenterOffset.vector3Value = _boundsCenterOffset.vector3Value;
                    }
                }
            }

            if (_reserveClearance.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_clearanceSize, new GUIContent(
                        "Clearance Size (units)",
                        "Full local-space size of the reserved volume. It rotates with the asset and creates no gameplay collider."));
                    EditorGUILayout.PropertyField(_clearanceCenterOffset, new GUIContent(
                        "Center Offset (units)",
                        "Clearance center relative to the prefab transform origin."));
                }
            }
        }
    }
}
