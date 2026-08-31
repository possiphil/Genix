using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.SceneConfiguration;
using Genix.Extensions;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixContentWindow
    {
        private void DrawSurfaceRelationField(
            AssetCatalog catalog,
            SceneSetupObjectEntry entry,
            Rect fieldRect)
        {
            AssetRelationAnchor anchor = entry.GameObject.GetComponentInParent<AssetRelationAnchor>();

            if (anchor)
            {
                string summary = anchor.RepresentedAsset
                    ? anchor.RepresentedAsset.AssetName
                    : anchor.AssetTags.Count > 0
                        ? GetRelationAnchorTagSummary(anchor)
                        : "Configure";
                if (GUI.Button(
                        fieldRect,
                        new GUIContent(
                            summary,
                            $"Uses the Asset Relation Anchor on {anchor.gameObject.name}. Click to change its represented asset."),
                        EditorStyles.miniButton))
                {
                    ShowRelationAnchorAssetMenu(catalog, anchor);
                    _selectedSceneSetupObject = anchor;
                }

                return;
            }

            if (GUI.Button(
                    fieldRect,
                    new GUIContent(
                        "Add Anchor",
                        "Add an Asset Relation Anchor to this object and optionally assign its represented asset."),
                    EditorStyles.miniButton))
            {
                ShowAddRelationAnchorMenu(
                    catalog,
                    new[] { entry.GameObject },
                    entry.SurfaceDescriptor);
            }
        }

        private void DrawRelationAnchorEntryFields(
            AssetCatalog catalog,
            AssetRelationAnchor anchor,
            SceneSetupColumns columns)
        {
            EditorGUI.LabelField(columns.Layer, "-", EditorStyles.centeredGreyMiniLabel);

            if (GUI.Button(
                    columns.Tags,
                    new GUIContent(GetRelationAnchorTagSummary(anchor), GetRelationAnchorTagTooltip(anchor)),
                    EditorStyles.miniButton))
            {
                ShowRelationAnchorTagMenu(catalog, anchor);
            }

            if (GUI.Button(
                    columns.Relation,
                    new GUIContent(
                        anchor.RepresentedAsset ? anchor.RepresentedAsset.AssetName : "None",
                        "Concrete asset represented by this fixed scene object. Front starts at local +Z and can be corrected with Front Direction Offset in Details."),
                    EditorStyles.miniButton))
            {
                ShowRelationAnchorAssetMenu(catalog, anchor);
            }

            EditorGUI.LabelField(columns.Capacity, "-", EditorStyles.centeredGreyMiniLabel);
        }

        private static List<GameObject> GetSelectedRelationAnchorTargets() => Selection.gameObjects
            .Where(gameObject =>
                gameObject &&
                gameObject.scene.IsValid() &&
                gameObject.scene.isLoaded &&
                !gameObject.GetComponent<AssetRelationAnchor>() &&
                !gameObject.GetComponentInParent<GeneratedObjectMetadata>())
            .Distinct()
            .ToList();

        private void ShowAddRelationAnchorMenu(
            AssetCatalog catalog,
            IReadOnlyList<GameObject> targets,
            PlacementSurfaceDescriptor preferredSupport = null)
        {
            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("Without Identity"),
                false,
                () => AddRelationAnchors(targets, null, preferredSupport));
            menu.AddSeparator(string.Empty);

            foreach (AssetDefinition asset in catalog.Assets
                         .Where(asset => asset)
                         .OrderBy(asset => asset.AssetName))
            {
                AssetDefinition captured = asset;
                menu.AddItem(
                    new GUIContent($"Represented Asset/{asset.AssetName}"),
                    false,
                    () => AddRelationAnchors(targets, captured, preferredSupport));
            }

            if (catalog.Assets.All(asset => !asset))
                menu.AddDisabledItem(new GUIContent("Represented Asset/No assets available"));

            menu.ShowAsContext();
        }

        private void AddRelationAnchors(
            IReadOnlyList<GameObject> targets,
            AssetDefinition representedAsset,
            PlacementSurfaceDescriptor preferredSupport = null)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(targets.Count == 1
                ? "Add Relation Anchor"
                : "Add Relation Anchors");
            AssetRelationAnchor firstAnchor = null;

            foreach (GameObject targetObject in targets)
            {
                if (!targetObject || targetObject.GetComponent<AssetRelationAnchor>())
                    continue;

                AssetRelationAnchor anchor = Undo.AddComponent<AssetRelationAnchor>(targetObject);
                Undo.RecordObject(anchor, "Configure Asset Relation Anchor");
                anchor.SetRepresentedAsset(representedAsset);

                PlacementSurfaceDescriptor ownSupport =
                    targetObject.GetComponent<PlacementSurfaceDescriptor>();
                PlacementSurfaceDescriptor[] childSupports =
                    targetObject.GetComponentsInChildren<PlacementSurfaceDescriptor>(true);
                if (targets.Count == 1 && preferredSupport)
                    anchor.SetSupportSurface(preferredSupport);
                else if (ownSupport)
                    anchor.SetSupportSurface(ownSupport);
                else if (childSupports.Length == 1)
                    anchor.SetSupportSurface(childSupports[0]);

                EditorUtility.SetDirty(anchor);
                EditorSceneManager.MarkSceneDirty(targetObject.scene);
                firstAnchor ??= anchor;
            }

            Undo.CollapseUndoOperations(undoGroup);
            MarkSceneSetupDirty();

            if (firstAnchor)
                _selectedSceneSetupObject = firstAnchor;
        }

        private static string GetRelationAnchorTagSummary(AssetRelationAnchor anchor)
        {
            List<SemanticTag> tags = anchor.AssetTags.Where(tag => tag).ToList();
            return tags.Count switch
            {
                0 => "None",
                <= 2 => string.Join(", ", tags.Select(tag => tag.DisplayName)),
                _ => $"{tags[0].DisplayName}, {tags[1].DisplayName} +{tags.Count - 2}"
            };
        }

        private static string GetRelationAnchorTagTooltip(AssetRelationAnchor anchor)
        {
            List<string> tags = anchor.AssetTags
                .Where(tag => tag)
                .Select(tag => tag.Category
                    ? $"{tag.Category.DisplayName}: {tag.DisplayName}"
                    : tag.DisplayName)
                .ToList();
            return tags.Count > 0
                ? string.Join("\n", tags)
                : "No additional asset-compatible identities.";
        }

        private void ShowRelationAnchorAssetMenu(
            AssetCatalog catalog,
            AssetRelationAnchor anchor)
        {
            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("None"),
                !anchor.RepresentedAsset,
                () => SetRelationAnchorAsset(anchor, null));
            menu.AddSeparator(string.Empty);

            foreach (AssetDefinition asset in catalog.Assets
                         .Where(asset => asset)
                         .OrderBy(asset => asset.AssetName))
            {
                AssetDefinition captured = asset;
                menu.AddItem(
                    new GUIContent(asset.AssetName),
                    anchor.RepresentedAsset == asset,
                    () => SetRelationAnchorAsset(anchor, captured));
            }

            if (catalog.Assets.All(asset => !asset))
                menu.AddDisabledItem(new GUIContent("No assets available"));

            menu.ShowAsContext();
        }

        private void SetRelationAnchorAsset(
            AssetRelationAnchor anchor,
            AssetDefinition representedAsset)
        {
            Undo.RecordObject(anchor, "Change Relation Anchor Asset");
            anchor.SetRepresentedAsset(representedAsset);
            MarkSceneObjectChanged(anchor);
            MarkSceneSetupDirty();
        }

        private void ShowRelationAnchorTagMenu(
            AssetCatalog catalog,
            AssetRelationAnchor anchor)
        {
            GenericMenu menu = new();
            List<SemanticTag> selected = anchor.AssetTags.Where(tag => tag).ToList();
            menu.AddItem(
                new GUIContent("None"),
                selected.Count == 0,
                () => SetRelationAnchorTags(anchor, Array.Empty<SemanticTag>()));
            menu.AddSeparator(string.Empty);

            foreach (SemanticTag tag in catalog.Tags
                         .Where(tag => tag && tag.SupportsAssets)
                         .OrderBy(tag => tag.Category.DisplayName)
                         .ThenBy(tag => tag.DisplayName))
            {
                SemanticTag captured = tag;
                menu.AddItem(
                    new GUIContent($"{tag.Category.DisplayName}/{tag.DisplayName}"),
                    selected.Contains(tag),
                    () => ToggleRelationAnchorTag(anchor, captured));
            }

            if (catalog.Tags.All(tag => !tag || !tag.Category || !tag.Category.SupportsAssets))
                menu.AddDisabledItem(new GUIContent("No asset-compatible tags available"));

            menu.ShowAsContext();
        }

        private void ToggleRelationAnchorTag(AssetRelationAnchor anchor, SemanticTag tag)
        {
            List<SemanticTag> tags = anchor.AssetTags.Where(existing => existing).ToList();
            if (!tags.Remove(tag))
                tags.Add(tag);
            SetRelationAnchorTags(anchor, tags);
        }

        private void SetRelationAnchorTags(
            AssetRelationAnchor anchor,
            IEnumerable<SemanticTag> tags)
        {
            Undo.RecordObject(anchor, "Change Relation Anchor Tags");
            anchor.SetAssetTags(tags);
            MarkSceneObjectChanged(anchor);
            MarkSceneSetupDirty();
        }
    }
}
