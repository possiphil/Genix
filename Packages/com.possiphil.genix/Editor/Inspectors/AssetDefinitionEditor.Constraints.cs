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
        private void DrawAssetSpacingRules()
        {
            EditorGUILayout.Space(3f);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(
                    "Asset Spacing",
                    "Optional center-to-center distances from one asset or every asset carrying a selected tag. Distances are symmetric and the larger matching requirement wins. Floor and Ceiling use horizontal distance; Wall and Inside Space use 3D distance."),
                    EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Add", GUILayout.Width(44f)))
                {
                    int index = _spacingRules.arraySize;
                    _spacingRules.InsertArrayElementAtIndex(index);
                    SerializedProperty added = _spacingRules.GetArrayElementAtIndex(index);
                    added.FindPropertyRelative("scope").enumValueIndex = (int)AssetSpacingRuleScope.AssetTag;
                    added.FindPropertyRelative("asset").objectReferenceValue = null;
                    added.FindPropertyRelative("assetTag").objectReferenceValue = null;
                    added.FindPropertyRelative("minimumDistance").floatValue = 1f;
                }
            }

            if (_spacingRules.arraySize == 0)
                return;

            for (int i = 0; i < _spacingRules.arraySize; i++)
            {
                SerializedProperty rule = _spacingRules.GetArrayElementAtIndex(i);
                SerializedProperty scope = rule.FindPropertyRelative("scope");
                SerializedProperty targetAsset = rule.FindPropertyRelative("asset");
                SerializedProperty assetTag = rule.FindPropertyRelative("assetTag");
                SerializedProperty distance = rule.FindPropertyRelative("minimumDistance");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(scope, new GUIContent("Match By"));

                        if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Minus"), GUILayout.Width(24f)))
                        {
                            _spacingRules.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    if ((AssetSpacingRuleScope)scope.enumValueIndex == AssetSpacingRuleScope.Asset)
                    {
                        EditorGUILayout.PropertyField(targetAsset, new GUIContent(
                            "Neighbor Asset",
                            "Concrete asset definition whose instances must keep this distance."));
                    }
                    else
                    {
                        DrawAssetSpacingTagField(assetTag);
                    }

                    EditorGUI.BeginChangeCheck();
                    float value = EditorGUILayout.FloatField(new GUIContent(
                        "Minimum Distance (units)",
                        "Required center-to-center distance."),
                        distance.floatValue);
                    if (EditorGUI.EndChangeCheck())
                        distance.floatValue = Mathf.Max(0f, value);
                }
            }
        }

        private static void DrawAssetSpacingTagField(SerializedProperty property)
        {
            DrawAssetTagField(
                property,
                "Neighbor Tag",
                "Every neighboring asset carrying this asset-compatible semantic tag matches the rule.");
        }

        private static void DrawAssetTagField(
            SerializedProperty property,
            string fieldLabel,
            string tooltip)
        {
            SemanticTag current = property.objectReferenceValue as SemanticTag;
            string label = current ? current.DisplayName : "Select Asset Tag";
            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(fieldLabel, tooltip));

            if (!EditorGUI.DropdownButton(rect, new GUIContent(label), FocusType.Keyboard))
                return;

            GenericMenu menu = new();
            menu.AddItem(new GUIContent("None"), !current, () =>
            {
                property.serializedObject.Update();
                property.objectReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
            });
            menu.AddSeparator(string.Empty);

            AssetCatalog catalog = AssetCatalogService.GetOrCreate();
            foreach (SemanticTag tag in catalog.Tags
                         .Where(tag => tag && tag.SupportsAssets)
                         .OrderBy(tag => tag.Category.DisplayName)
                         .ThenBy(tag => tag.DisplayName))
            {
                SemanticTag captured = tag;
                menu.AddItem(
                    new GUIContent($"{tag.Category.DisplayName}/{tag.DisplayName}"),
                    current == tag,
                    () =>
                    {
                        property.serializedObject.Update();
                        property.objectReferenceValue = captured;
                        property.serializedObject.ApplyModifiedProperties();
                    });
            }

            menu.DropDown(rect);
        }

        private void DrawAssetRelativePlacement()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Placement Relative to Objects", EditorStyles.miniBoldLabel);
            SerializedProperty enabled = _assetRelativePlacement.FindPropertyRelative("enabled");
            EditorGUILayout.PropertyField(enabled, new GUIContent(
                "Use Object Relationship",
                "Place this asset relative to a matching generated object or scene anchor."));

            if (!enabled.boolValue)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty source = _assetRelativePlacement.FindPropertyRelative("source");
                SerializedProperty scope = _assetRelativePlacement.FindPropertyRelative("targetScope");
                SerializedProperty targetAsset = _assetRelativePlacement.FindPropertyRelative("targetAsset");
                SerializedProperty targetTag = _assetRelativePlacement.FindPropertyRelative("targetTag");
                SerializedProperty side = _assetRelativePlacement.FindPropertyRelative("side");
                SerializedProperty additionalSides = _assetRelativePlacement.FindPropertyRelative("additionalSides");
                SerializedProperty alignment = _assetRelativePlacement.FindPropertyRelative("alignment");
                SerializedProperty sameSupport = _assetRelativePlacement.FindPropertyRelative("requireSameSupportSurface");
                SerializedProperty insideAnchor = _assetRelativePlacement.FindPropertyRelative("requireInsideAnchorBounds");
                SerializedProperty minimum = _assetRelativePlacement.FindPropertyRelative("minimumDistance");
                SerializedProperty maximum = _assetRelativePlacement.FindPropertyRelative("maximumDistance");
                SerializedProperty facing = _assetRelativePlacement.FindPropertyRelative("facing");
                SerializedProperty facingVariation = _assetRelativePlacement.FindPropertyRelative("facingVariationDegrees");
                SerializedProperty cardinalityMode = _assetRelativePlacement.FindPropertyRelative("cardinalityMode");
                SerializedProperty cardinalityCount = _assetRelativePlacement.FindPropertyRelative("cardinalityCount");
                SerializedProperty cardinalityMaximumCount =
                    _assetRelativePlacement.FindPropertyRelative("cardinalityMaximumCount");
                SerializedProperty usePathStations = _assetRelativePlacement.FindPropertyRelative("usePathStations");
                SerializedProperty pathStationSides = _assetRelativePlacement.FindPropertyRelative("pathStationSides");
                SerializedProperty pathStationSpacing = _assetRelativePlacement.FindPropertyRelative("pathStationSpacing");
                SerializedProperty pathStationLateralOffset = _assetRelativePlacement.FindPropertyRelative("pathStationLateralOffset");
                SerializedProperty pathStationEndpointMargin = _assetRelativePlacement.FindPropertyRelative("pathStationEndpointMargin");
                SerializedProperty pathStationMaximumCount = _assetRelativePlacement.FindPropertyRelative("pathStationMaximumCount");

                EditorGUILayout.PropertyField(source, new GUIContent(
                    "Anchor Objects",
                    "Choose generated objects, fixed scene anchors, or both as relationship anchors."));
                EditorGUILayout.PropertyField(scope, new GUIContent(
                    "Find Anchor By",
                    "Match one exact asset definition or any anchor carrying a selected asset tag."));

                if ((AssetRelativeTargetScope)scope.enumValueIndex == AssetRelativeTargetScope.Asset)
                {
                    EditorGUILayout.PropertyField(targetAsset, new GUIContent(
                        "Anchor Asset",
                        "Match generated objects or scene anchors representing this asset."));
                }
                else
                {
                    DrawAssetTagField(
                        targetTag,
                        "Anchor Tag",
                        "Match generated objects and scene anchors carrying this tag.");
                }

                bool canUsePathStations =
                    (AssetRelativeTargetScope)scope.enumValueIndex == AssetRelativeTargetScope.AssetTag &&
                    (AssetRelativeAnchorSource)source.enumValueIndex is
                        AssetRelativeAnchorSource.Any or AssetRelativeAnchorSource.SceneAnchors;
                if (canUsePathStations)
                {
                    EditorGUILayout.PropertyField(usePathStations, new GUIContent(
                        "Create Regular Path Stations",
                        "Derive evenly spaced virtual anchors from matching paths instead of authoring each anchor manually."));
                    if (usePathStations.boolValue)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            DrawPathStationSides(pathStationSides);
                            pathStationSpacing.floatValue = Mathf.Max(0.1f, EditorGUILayout.FloatField(
                                new GUIContent("Station Spacing (units)", "Distance along the path between station groups."),
                                pathStationSpacing.floatValue));
                            pathStationLateralOffset.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(
                                new GUIContent("Side Offset (units)", "Horizontal distance from the path centerline."),
                                pathStationLateralOffset.floatValue));
                            pathStationEndpointMargin.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(
                                new GUIContent("End Margin (units)", "Path length ignored at both ends."),
                                pathStationEndpointMargin.floatValue));
                            pathStationMaximumCount.intValue = Mathf.Max(1, EditorGUILayout.IntField(
                                new GUIContent("Maximum Station Groups", "Maximum station groups across all matching paths in the target area."),
                                pathStationMaximumCount.intValue));
                        }
                    }
                }
                else
                {
                    usePathStations.boolValue = false;
                }

                DrawAssetRelativeSides(side, additionalSides);
                DrawAssetRelativeAlignment(
                    alignment,
                    GetAssetRelativeSides(side, additionalSides));
                EditorGUILayout.PropertyField(sameSupport, new GUIContent(
                    "Same Support Surface",
                    "Keep this asset and its anchor on the same configured support surface, such as one workbench."));
                EditorGUILayout.PropertyField(insideAnchor, new GUIContent(
                    "Stay Inside Anchor Area",
                    "Keep the complete asset inside the anchor bounds while still placing it on its normal support surface."));

                EditorGUI.BeginChangeCheck();
                float minValue = EditorGUILayout.FloatField(new GUIContent(
                    "Minimum Distance (units)",
                    "Minimum 3D distance from the nearest point on the anchor bounds."),
                    minimum.floatValue);
                float maxValue = EditorGUILayout.FloatField(new GUIContent(
                    "Maximum Distance (units)",
                    "Maximum 3D distance from the nearest point on the anchor bounds."),
                    maximum.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    minimum.floatValue = Mathf.Max(0f, minValue);
                    maximum.floatValue = Mathf.Max(minimum.floatValue, maxValue);
                }

                EditorGUILayout.PropertyField(facing, new GUIContent(
                    "Facing",
                    "Any keeps the normal orientation. Toward/Away face relative to the anchor center. Match Forward copies the anchor's local +Z direction. Asset-relative Facing takes precedence over the global Face Target orientation."));
                if ((AssetRelativeFacing)facing.enumValueIndex != AssetRelativeFacing.Any)
                {
                    facingVariation.floatValue = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent(
                        "Facing Variation (deg)",
                        "Maximum yaw variation in either direction. Zero follows the resolved facing exactly."),
                        facingVariation.floatValue), 0f, 180f);
                }

                EditorGUILayout.PropertyField(cardinalityMode, new GUIContent(
                    "Instances per Anchor",
                    "Choose whether each matching anchor has no limit, a maximum, a minimum, an exact count, or a range."));
                AssetRelativeCardinalityMode selectedCardinality =
                    (AssetRelativeCardinalityMode)cardinalityMode.enumValueIndex;
                if (selectedCardinality != AssetRelativeCardinalityMode.Unlimited)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        if (selectedCardinality == AssetRelativeCardinalityMode.Between)
                        {
                            cardinalityCount.intValue = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent(
                                "Minimum Count",
                                "Minimum instances generation must complete for each matching anchor."),
                                cardinalityCount.intValue));
                            cardinalityMaximumCount.intValue = Mathf.Max(
                                cardinalityCount.intValue,
                                EditorGUILayout.IntField(new GUIContent(
                                    "Maximum Count",
                                    "Maximum instances allowed per matching anchor, including existing generated output."),
                                    cardinalityMaximumCount.intValue));
                        }
                        else
                        {
                            cardinalityCount.intValue = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent(
                                "Count",
                                selectedCardinality switch
                                {
                                    AssetRelativeCardinalityMode.AtMost =>
                                        "Maximum optional instances assigned to each matching anchor. The count includes previous generated output.",
                                    AssetRelativeCardinalityMode.AtLeast =>
                                        "Minimum instances generation must complete for each matching anchor. Additional instances remain possible.",
                                    AssetRelativeCardinalityMode.Exactly =>
                                        "Exact instances generation must complete for each matching anchor. This is both a minimum and a maximum.",
                                    _ => string.Empty
                                }),
                                cardinalityCount.intValue));
                            cardinalityMaximumCount.intValue = cardinalityCount.intValue;
                        }
                    }
                }

                AssetRelativeFacing selectedFacing = (AssetRelativeFacing)facing.enumValueIndex;
                if (IsWallPlacementType() && selectedFacing != AssetRelativeFacing.Any)
                {
                    EditorGUILayout.HelpBox(
                        "Wall assets must remain flush with their support, so asset-relative Facing is ignored. Positional side and distance constraints still apply.",
                        MessageType.Warning);
                }
                bool missingTarget = (AssetRelativeTargetScope)scope.enumValueIndex == AssetRelativeTargetScope.Asset
                    ? !targetAsset.objectReferenceValue
                    : !targetTag.objectReferenceValue;
                if (missingTarget)
                {
                    EditorGUILayout.HelpBox(
                        "Select a target before this relationship can be satisfied.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawPathPlacement()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Placement Along Paths", EditorStyles.miniBoldLabel);
            SerializedProperty enabled = _pathPlacement.FindPropertyRelative("enabled");
            EditorGUILayout.PropertyField(enabled, new GUIContent(
                "Use Path Relationship",
                "Constrain distance, side, and facing relative to the nearest matching Genix path."));
            if (!enabled.boolValue)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty tag = _pathPlacement.FindPropertyRelative("pathTag");
                SerializedProperty minimum = _pathPlacement.FindPropertyRelative("minimumDistance");
                SerializedProperty maximum = _pathPlacement.FindPropertyRelative("maximumDistance");
                SerializedProperty endpointMargin = _pathPlacement.FindPropertyRelative("endpointMargin");
                SerializedProperty side = _pathPlacement.FindPropertyRelative("side");
                SerializedProperty facing = _pathPlacement.FindPropertyRelative("facing");
                SerializedProperty variation = _pathPlacement.FindPropertyRelative("facingVariationDegrees");

                DrawAssetTagField(
                    tag,
                    "Path Tag",
                    "Only Path Placement Sources carrying this asset-compatible semantic tag are considered.");
                EditorGUI.BeginChangeCheck();
                float minValue = EditorGUILayout.FloatField(new GUIContent(
                    "Minimum Distance (units)",
                    "Minimum horizontal center distance from the nearest path centerline."), minimum.floatValue);
                float maxValue = EditorGUILayout.FloatField(new GUIContent(
                    "Maximum Distance (units)",
                    "Maximum horizontal center distance from the nearest path centerline."), maximum.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    minimum.floatValue = Mathf.Max(0f, minValue);
                    maximum.floatValue = Mathf.Max(minimum.floatValue, maxValue);
                }
                endpointMargin.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent(
                    "End Margin (units)",
                    "Ignore this distance at both path ends."),
                    endpointMargin.floatValue));

                DrawPathConstraintSide(side);
                EditorGUILayout.PropertyField(facing, new GUIContent(
                    "Facing",
                    "Orient along, against, toward, or away from the nearest path. Any keeps the normal asset orientation."));
                if ((PathPlacementFacing)facing.enumValueIndex != PathPlacementFacing.Any)
                {
                    variation.floatValue = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent(
                        "Facing Variation (deg)",
                        "Maximum deterministic yaw variation in either direction from the path-relative direction."),
                        variation.floatValue), 0f, 180f);
                }

                if (!tag.objectReferenceValue)
                {
                    EditorGUILayout.HelpBox(
                        "Select a Path Tag before this constraint can be satisfied.",
                        MessageType.Warning);
                }
            }
        }

        private static void DrawPathConstraintSide(SerializedProperty side)
        {
            PathPlacementSide current = (PathPlacementSide)side.enumValueIndex;
            int index = current switch
            {
                PathPlacementSide.Left => 1,
                PathPlacementSide.Right => 2,
                _ => 0
            };
            index = EditorGUILayout.Popup(
                new GUIContent(
                    "Side",
                    "Any accepts both sides. Left and Right follow the authored path direction."),
                index,
                new[] { "Any", "Left", "Right" });
            side.enumValueIndex = (int)(index switch
            {
                1 => PathPlacementSide.Left,
                2 => PathPlacementSide.Right,
                _ => PathPlacementSide.Any
            });
        }

        private static void DrawPathStationSides(SerializedProperty sides)
        {
            PathPlacementSide current = (PathPlacementSide)sides.enumValueIndex;
            int index = current switch
            {
                PathPlacementSide.Left => 0,
                PathPlacementSide.Right => 1,
                _ => 2
            };
            index = EditorGUILayout.Popup(
                new GUIContent(
                    "Station Sides",
                    "Create one virtual anchor on the left, right, or both sides of each station. Both Sides keeps pairs aligned along the path."),
                index,
                new[] { "Left", "Right", "Both Sides" });
            sides.enumValueIndex = (int)(index switch
            {
                0 => PathPlacementSide.Left,
                1 => PathPlacementSide.Right,
                _ => PathPlacementSide.BothSides
            });
        }

        private static void DrawAssetRelativeSides(
            SerializedProperty primarySide,
            SerializedProperty additionalSides)
        {
            List<AssetRelativeSide> selected = GetAssetRelativeSides(primarySide, additionalSides);
            string summary = selected.Count switch
            {
                0 => "Any",
                <= 2 => string.Join(", ", selected.Select(value => value.ToString())),
                _ => $"{selected[0]}, {selected[1]} +{selected.Count - 2}"
            };
            Rect row = EditorGUILayout.GetControlRect();
            Rect button = EditorGUI.PrefixLabel(row, new GUIContent(
                "Place On Sides",
                "Accepted dominant-axis sectors around the anchor. Any disables the restriction; Front is local +Z, Back -Z, Left -X, Right +X, Above world +Y, and Below world -Y. Horizontal-only rules ignore height differences for backward compatibility."));

            if (!EditorGUI.DropdownButton(button, new GUIContent(summary), FocusType.Keyboard))
                return;

            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("Any"),
                selected.Count == 0,
                () => SetAssetRelativeSides(primarySide, additionalSides, Array.Empty<AssetRelativeSide>()));
            menu.AddSeparator(string.Empty);

            foreach (AssetRelativeSide side in new[]
                     {
                         AssetRelativeSide.Front,
                         AssetRelativeSide.Back,
                         AssetRelativeSide.Left,
                         AssetRelativeSide.Right,
                         AssetRelativeSide.Above,
                         AssetRelativeSide.Below
                     })
            {
                AssetRelativeSide captured = side;
                menu.AddItem(
                    new GUIContent(side.ToString()),
                    selected.Contains(side),
                    () =>
                    {
                        List<AssetRelativeSide> updated = GetAssetRelativeSides(primarySide, additionalSides);
                        if (!updated.Remove(captured))
                            updated.Add(captured);
                        SetAssetRelativeSides(primarySide, additionalSides, updated);
                    });
            }

            menu.DropDown(button);
        }

        private static List<AssetRelativeSide> GetAssetRelativeSides(
            SerializedProperty primarySide,
            SerializedProperty additionalSides)
        {
            List<AssetRelativeSide> sides = new();
            AssetRelativeSide primary = (AssetRelativeSide)primarySide.enumValueIndex;
            if (primary != AssetRelativeSide.Any)
                sides.Add(primary);

            for (int i = 0; i < additionalSides.arraySize; i++)
            {
                AssetRelativeSide side = (AssetRelativeSide)additionalSides
                    .GetArrayElementAtIndex(i)
                    .enumValueIndex;
                if (side != AssetRelativeSide.Any && !sides.Contains(side))
                    sides.Add(side);
            }

            return sides;
        }

        private static void DrawAssetRelativeAlignment(
            SerializedProperty alignment,
            IReadOnlyList<AssetRelativeSide> sides)
        {
            bool hasSingleHorizontalSide = sides.Count == 1 &&
                                           sides[0] is AssetRelativeSide.Front or AssetRelativeSide.Back or
                                               AssetRelativeSide.Left or AssetRelativeSide.Right;
            AssetRelativeAlignment current = (AssetRelativeAlignment)alignment.enumValueIndex;
            AssetRelativeAlignment[] values = sides.Count == 0
                ? new[] { AssetRelativeAlignment.Random }
                : hasSingleHorizontalSide
                ? new[]
                {
                    AssetRelativeAlignment.Random,
                    AssetRelativeAlignment.Center,
                    AssetRelativeAlignment.Start,
                    AssetRelativeAlignment.End
                }
                : new[]
                {
                    AssetRelativeAlignment.Random,
                    AssetRelativeAlignment.Center
                };
            string[] labels = sides.Count == 0
                ? new[] { "Random" }
                : hasSingleHorizontalSide
                ? sides[0] is AssetRelativeSide.Front or AssetRelativeSide.Back
                    ? new[] { "Random", "Center", "Left", "Right" }
                    : new[] { "Random", "Center", "Back", "Front" }
                : new[] { "Random", "Center" };
            int selected = Array.IndexOf(values, current);
            if (selected < 0)
                selected = 0;

            EditorGUI.BeginChangeCheck();
            selected = EditorGUILayout.Popup(new GUIContent(
                    "Side Alignment",
                    "Soft local preference after side and distance constraints. Random uses the fixed run seed. Center prefers the side midpoint. For one horizontal side, the remaining options prefer either local end. Above/Below and multi-side rules expose only Random or Center."),
                selected,
                labels);
            if (EditorGUI.EndChangeCheck())
                alignment.enumValueIndex = (int)values[selected];
        }

        private static void SetAssetRelativeSides(
            SerializedProperty primarySide,
            SerializedProperty additionalSides,
            IEnumerable<AssetRelativeSide> values)
        {
            List<AssetRelativeSide> sides = values
                .Where(value => value != AssetRelativeSide.Any)
                .Distinct()
                .ToList();
            SerializedObject serializedObject = primarySide.serializedObject;
            serializedObject.Update();
            primarySide.enumValueIndex = sides.Count > 0
                ? (int)sides[0]
                : (int)AssetRelativeSide.Any;
            additionalSides.ClearArray();
            for (int i = 1; i < sides.Count; i++)
            {
                int index = additionalSides.arraySize;
                additionalSides.InsertArrayElementAtIndex(index);
                additionalSides.GetArrayElementAtIndex(index).enumValueIndex = (int)sides[i];
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
