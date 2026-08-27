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
        private enum SceneSetupTypeFilter
        {
            All,
            Surfaces,
            RelationAnchors,
            ExclusionRegions
        }

        private const float SceneSetupMinimumTableWidth = 720f;
        private readonly List<SceneSetupObjectEntry> _sceneSetupEntries = new();
        private Vector2 _sceneSetupScroll;
        private string _sceneSetupSearch = string.Empty;
        private SceneSetupTypeFilter _sceneSetupTypeFilter;
        private bool _sceneSetupIssuesOnly;
        private bool _sceneSetupDirty = true;
        private PlacementSurfaceSettingsSnapshot _surfaceSettingsClipboard;

        private void DrawSceneSetupTab(AssetCatalog catalog)
        {
            EnsureSceneSetupEntries();

            DrawSceneSetupFilters();
            EditorGUILayout.Space(6f);

            LayerMask configuredLayers = GenixEditorWindow.GetConfiguredSurfaceLayerMask();
            List<SceneSetupObjectEntry> visibleEntries = GetVisibleSceneSetupEntries(configuredLayers);
            int issueCount = visibleEntries.Count(entry => HasSceneSetupIssue(entry, configuredLayers));
            int missingDescriptorCount = visibleEntries.Count(entry =>
                entry.Type == SceneSetupObjectType.Surface &&
                entry.SurfaceCollider &&
                !entry.SurfaceDescriptor);
            List<GameObject> selectedAnchorTargets = GetSelectedRelationAnchorTargets();
            GameObject selectedSupportTarget = Selection.activeGameObject;

            DrawSectionHeader($"Scene Objects ({visibleEntries.Count})", () =>
            {
                using (new EditorGUI.DisabledScope(visibleEntries.Count == 0))
                {
                    if (GUILayout.Button("Select Visible", GUILayout.Width(94f)))
                        SelectVisibleSceneObjects(visibleEntries);
                }

                using (new EditorGUI.DisabledScope(missingDescriptorCount == 0))
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                $"Add Descriptors ({missingDescriptorCount})",
                                "Add an empty Placement Surface Descriptor to each visible surface that does not inherit one."),
                            GUILayout.Width(132f)))
                    {
                        AddMissingSurfaceDescriptors(visibleEntries);
                    }
                }

                using (new EditorGUI.DisabledScope(selectedAnchorTargets.Count == 0))
                {
                    string label = selectedAnchorTargets.Count == 1
                        ? "Add Anchor"
                        : $"Add Anchors ({selectedAnchorTargets.Count})";
                    if (GUILayout.Button(
                            new GUIContent(
                                label,
                                "Add Asset Relation Anchors to the selected scene objects and optionally assign their represented asset."),
                            GUILayout.Width(selectedAnchorTargets.Count == 1 ? 88f : 116f)))
                    {
                        ShowAddRelationAnchorMenu(catalog, selectedAnchorTargets);
                    }
                }

                using (new EditorGUI.DisabledScope(
                           !SupportSurfaceRegionAuthoring.CanCreate(selectedSupportTarget)))
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                "Add Support",
                                "Create a thin, editable support-surface child under the selected object. Use this for internal shelf boards or other levels hidden inside one combined collider."),
                            GUILayout.Width(88f)))
                    {
                        SupportSurfaceRegionAuthoring.Create(
                            selectedSupportTarget,
                            configuredLayers);
                        MarkSceneSetupDirty();
                    }
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(68f)))
                    RefreshSceneSetupEntries();
            });

            if (issueCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{issueCount} visible object{(issueCount == 1 ? string.Empty : "s")} need attention.",
                    MessageType.Warning);
            }

            DrawSceneSetupTable(catalog, visibleEntries, configuredLayers);
        }

        private void DrawSceneSetupFilters()
        {
            DrawSectionHeader("Scene Filters", () =>
            {
                if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                {
                    _sceneSetupSearch = string.Empty;
                    _sceneSetupTypeFilter = SceneSetupTypeFilter.All;
                    _sceneSetupIssuesOnly = false;
                    GUI.FocusControl(null);
                }
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _sceneSetupSearch = EditorGUILayout.TextField(
                    new GUIContent("Search", "Filter by object, scene, tag, or configuration status."),
                    _sceneSetupSearch);
                _sceneSetupTypeFilter = (SceneSetupTypeFilter)EditorGUILayout.EnumPopup(
                    new GUIContent("Type", "Show surfaces, fixed relation anchors, exclusion regions, or all scene setup objects."),
                    _sceneSetupTypeFilter);
                _sceneSetupIssuesOnly = EditorGUILayout.Toggle(
                    new GUIContent("Needs Attention", "Hide objects whose current configuration is ready."),
                    _sceneSetupIssuesOnly);
            }
        }

        private void DrawSceneSetupTable(
            AssetCatalog catalog,
            IReadOnlyList<SceneSetupObjectEntry> entries,
            LayerMask configuredLayers)
        {
            float tableHeight = Mathf.Clamp(48f + entries.Count * 24f, 110f, 420f);
            float tableWidth = Mathf.Max(SceneSetupMinimumTableWidth, position.width - 38f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _sceneSetupScroll = EditorGUILayout.BeginScrollView(
                    _sceneSetupScroll,
                    false,
                    true,
                    GUILayout.Height(tableHeight));

                Rect headerRect = GUILayoutUtility.GetRect(
                    tableWidth,
                    22f,
                    GUILayout.Width(tableWidth));
                DrawSceneSetupHeader(headerRect);

                if (entries.Count == 0)
                {
                    GUILayoutUtility.GetRect(
                        tableWidth,
                        EditorGUIUtility.singleLineHeight,
                        GUILayout.Width(tableWidth));
                }
                else
                {
                    foreach (SceneSetupObjectEntry entry in entries)
                    {
                        Rect rowRect = GUILayoutUtility.GetRect(
                            tableWidth,
                            24f,
                            GUILayout.Width(tableWidth));
                        DrawSceneSetupRow(catalog, entry, configuredLayers, rowRect);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawSceneSetupHeader(Rect rowRect)
        {
            EditorGUI.DrawRect(rowRect, EditorGUIUtility.isProSkin
                ? new Color(0.16f, 0.16f, 0.16f)
                : new Color(0.78f, 0.78f, 0.78f));

            SceneSetupColumns columns = new(rowRect);
            EditorGUI.LabelField(columns.Object, "Object", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(columns.Layer, "Layer", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(columns.Tags, "Tags", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(columns.Relation, "Relation Asset", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(columns.Capacity, new GUIContent("Cap.", "Maximum placements supported by this surface."), EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(columns.Status, new GUIContent("", "Validation status. Select the row for the complete message."), EditorStyles.miniBoldLabel);
        }

        private void DrawSceneSetupRow(
            AssetCatalog catalog,
            SceneSetupObjectEntry entry,
            LayerMask configuredLayers,
            Rect rowRect)
        {
            if (!entry.GameObject)
                return;

            bool selected = entry.MatchesDetailTarget(_selectedSceneSetupObject);
            if (selected)
                EditorGUI.DrawRect(rowRect, new Color(0.18f, 0.48f, 0.82f, 0.22f));

            SceneSetupColumns columns = new(rowRect);
            string objectTooltip = $"Select {entry.GameObject.name} in {entry.GameObject.scene.name}.";

            if (GUI.Button(
                    columns.Object,
                    new GUIContent(entry.GameObject.name, objectTooltip),
                    selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
            {
                Selection.activeGameObject = entry.GameObject;
                EditorGUIUtility.PingObject(entry.GameObject);
                SelectObject(entry.DetailTarget);
            }

            if (entry.Type == SceneSetupObjectType.Surface)
                DrawSurfaceEntryFields(catalog, entry, columns);
            else if (entry.Type == SceneSetupObjectType.RelationAnchor)
                DrawRelationAnchorEntryFields(catalog, entry.RelationAnchor, columns);
            else
                DrawUnavailableSceneSetupFields(columns);

            string status = GetSceneSetupStatus(entry, configuredLayers, out MessageType statusType);
            GUIContent statusContent = GetSceneSetupStatusContent(status, statusType);
            GUI.Label(columns.Status, statusContent, EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawSurfaceEntryFields(
            AssetCatalog catalog,
            SceneSetupObjectEntry entry,
            SceneSetupColumns columns)
        {
            if (entry.SurfaceCollider)
            {
                EditorGUI.BeginChangeCheck();
                int newLayer = EditorGUI.LayerField(columns.Layer, entry.GameObject.layer);
                if (EditorGUI.EndChangeCheck())
                    SetSceneObjectLayer(entry.GameObject, newLayer);
            }
            else
            {
                EditorGUI.LabelField(columns.Layer, "No collider", EditorStyles.centeredGreyMiniLabel);
            }

            PlacementSurfaceDescriptor descriptor = entry.SurfaceDescriptor;

            if (!descriptor)
            {
                using (new EditorGUI.DisabledScope(!entry.SurfaceCollider))
                {
                    if (GUI.Button(columns.Tags, "Add Descriptor", EditorStyles.miniButton))
                        AddSurfaceDescriptor(entry.GameObject);
                }

                EditorGUI.LabelField(columns.Capacity, "-", EditorStyles.centeredGreyMiniLabel);
                DrawSurfaceRelationField(catalog, entry, columns.Relation);
                return;
            }

            if (GUI.Button(
                    columns.Tags,
                    new GUIContent(GetSurfaceTagSummary(descriptor), GetSurfaceTagTooltip(descriptor)),
                    EditorStyles.miniButton))
            {
                ShowSurfaceTagMenu(catalog, descriptor);
            }

            if (GUI.Button(
                    columns.Capacity,
                    new GUIContent(
                        GetCapacitySummary(descriptor),
                        GetCapacityTooltip(descriptor)),
                    EditorStyles.miniButton))
            {
                ShowCapacityMenu(descriptor);
            }

            DrawSurfaceRelationField(catalog, entry, columns.Relation);
        }

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
                        "Concrete Asset Definition represented by this fixed scene object. Front starts at local +Z and may be corrected through Front Yaw Offset in Details."),
                    EditorStyles.miniButton))
            {
                ShowRelationAnchorAssetMenu(catalog, anchor);
            }

            EditorGUI.LabelField(columns.Capacity, "-", EditorStyles.centeredGreyMiniLabel);
        }

        private static void DrawUnavailableSceneSetupFields(SceneSetupColumns columns)
        {
            EditorGUI.LabelField(columns.Layer, "-", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.LabelField(columns.Tags, "Collider-free", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.LabelField(columns.Relation, "-", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.LabelField(columns.Capacity, "-", EditorStyles.centeredGreyMiniLabel);
        }

        private List<SceneSetupObjectEntry> GetVisibleSceneSetupEntries(LayerMask configuredLayers)
        {
            IEnumerable<SceneSetupObjectEntry> entries = _sceneSetupEntries.Where(entry =>
                entry != null && entry.GameObject);

            entries = _sceneSetupTypeFilter switch
            {
                SceneSetupTypeFilter.Surfaces => entries.Where(entry => entry.Type == SceneSetupObjectType.Surface),
                SceneSetupTypeFilter.RelationAnchors => entries.Where(entry => entry.RelationAnchor),
                SceneSetupTypeFilter.ExclusionRegions => entries.Where(entry => entry.Type == SceneSetupObjectType.ExclusionRegion),
                _ => entries
            };

            if (_sceneSetupIssuesOnly)
                entries = entries.Where(entry => HasSceneSetupIssue(entry, configuredLayers));

            string search = _sceneSetupSearch?.Trim();
            if (!string.IsNullOrEmpty(search))
            {
                entries = entries.Where(entry =>
                    entry.GameObject.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.GameObject.scene.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    GetSceneSetupSearchText(entry, configuredLayers)
                        .IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return entries.ToList();
        }

        private static string GetSceneSetupSearchText(
            SceneSetupObjectEntry entry,
            LayerMask configuredLayers)
        {
            string surfaceTags = entry.SurfaceDescriptor
                ? string.Join(" ", entry.SurfaceDescriptor.SurfaceTags.Where(tag => tag).Select(tag => tag.DisplayName))
                : string.Empty;
            string anchorTags = entry.RelationAnchor
                ? string.Join(" ", entry.RelationAnchor.AssetTags.Where(tag => tag).Select(tag => tag.DisplayName))
                : string.Empty;
            string representedAsset = entry.RelationAnchor && entry.RelationAnchor.RepresentedAsset
                ? entry.RelationAnchor.RepresentedAsset.AssetName
                : string.Empty;
            return $"{entry.Type} {surfaceTags} {anchorTags} {representedAsset} {GetSceneSetupStatus(entry, configuredLayers, out _)}";
        }

        private static bool HasSceneSetupIssue(
            SceneSetupObjectEntry entry,
            LayerMask configuredLayers)
        {
            GetSceneSetupStatus(entry, configuredLayers, out MessageType messageType);
            return messageType is MessageType.Warning or MessageType.Error;
        }

        private static string GetSceneSetupStatus(
            SceneSetupObjectEntry entry,
            LayerMask configuredLayers,
            out MessageType messageType)
        {
            messageType = MessageType.None;

            if (!entry.GameObject.activeInHierarchy)
            {
                messageType = MessageType.Warning;
                return "Inactive";
            }

            if (entry.RelationAnchor)
            {
                if (!entry.RelationAnchor || !entry.RelationAnchor.enabled)
                {
                    messageType = MessageType.Warning;
                    return "Anchor disabled";
                }

                if (!entry.RelationAnchor.RepresentedAsset && entry.RelationAnchor.AssetTags.Count == 0)
                {
                    messageType = MessageType.Warning;
                    return "No relation identity";
                }

                if (entry.Type == SceneSetupObjectType.RelationAnchor)
                    return "Ready";
            }

            if (entry.Type == SceneSetupObjectType.ExclusionRegion)
            {
                if (!entry.ExclusionRegion || !entry.ExclusionRegion.enabled)
                {
                    messageType = MessageType.Warning;
                    return "Disabled";
                }

                if (entry.ExclusionRegion.AffectedTargets == PlacementTarget.None)
                {
                    messageType = MessageType.Warning;
                    return "No affected targets";
                }

                return "Ready";
            }

            if (!entry.SurfaceCollider)
            {
                messageType = MessageType.Warning;
                return "No collider";
            }

            if (!entry.SurfaceCollider.enabled)
            {
                messageType = MessageType.Warning;
                return "Collider disabled";
            }

            if ((configuredLayers.value & (1 << entry.GameObject.layer)) == 0)
            {
                messageType = MessageType.Warning;
                return "Layer not sampled";
            }

            if (!entry.SurfaceDescriptor)
            {
                messageType = MessageType.Info;
                return "No semantic descriptor";
            }

            if (entry.SurfaceDescriptor.LimitCapacity && entry.SurfaceDescriptor.MaxCapacity == 0)
            {
                messageType = MessageType.Warning;
                return "Capacity blocks placement";
            }

            return "Ready";
        }

        private static GUIContent GetSceneSetupStatusContent(string status, MessageType messageType)
        {
            string iconName = messageType switch
            {
                MessageType.Warning => "console.warnicon.sml",
                MessageType.Error => "console.erroricon.sml",
                MessageType.Info => "console.infoicon.sml",
                _ => "TestPassed"
            };
            GUIContent icon = EditorGUIUtility.IconContent(iconName);
            return new GUIContent(string.Empty, icon.image, status);
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
                ? "Add Asset Relation Anchor"
                : "Add Asset Relation Anchors");
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
                         .Where(tag => tag && tag.Category && tag.Category.SupportsAssets)
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

        private static string GetSurfaceTagSummary(PlacementSurfaceDescriptor descriptor)
        {
            List<SemanticTag> tags = descriptor.SurfaceTags.Where(tag => tag).ToList();
            List<TagCategory> noneCategories = descriptor.NoneTagCategories.Where(category => category).ToList();

            if (tags.Count == 0)
                return noneCategories.Count == 0 ? "Any" : $"None ({noneCategories.Count})";

            if (tags.Count <= 2)
                return string.Join(", ", tags.Select(tag => tag.DisplayName));

            return $"{tags[0].DisplayName}, {tags[1].DisplayName} +{tags.Count - 2}";
        }

        private static string GetSurfaceTagTooltip(PlacementSurfaceDescriptor descriptor)
        {
            List<string> tags = descriptor.SurfaceTags
                .Where(tag => tag)
                .Select(tag => tag.Category
                    ? $"{tag.Category.DisplayName}: {tag.DisplayName}"
                    : tag.DisplayName)
                .ToList();
            tags.AddRange(descriptor.NoneTagCategories
                .Where(category => category)
                .Select(category => $"{category.DisplayName}: None"));
            tags.Add("Categories not listed here use Any.");
            return string.Join("\n", tags);
        }

        private void ShowSurfaceTagMenu(AssetCatalog catalog, PlacementSurfaceDescriptor descriptor)
        {
            GenericMenu menu = new();
            List<TagCategory> categories = catalog.Categories
                .Where(category => category && category.SupportsSurfaces)
                .OrderBy(category => category.DisplayName)
                .ToList();

            foreach (TagCategory category in categories)
            {
                List<SemanticTag> tags = catalog.Tags
                    .Where(tag => tag && tag.Category == category)
                    .OrderBy(tag => tag.DisplayName)
                    .ToList();

                TagCategory capturedCategory = category;
                menu.AddItem(
                    new GUIContent($"{category.DisplayName}/Any"),
                    descriptor.AcceptsAnyTag(category),
                    () => SetSurfaceCategorySelection(
                        descriptor,
                        capturedCategory,
                        Array.Empty<SemanticTag>(),
                        false));
                menu.AddItem(
                    new GUIContent($"{category.DisplayName}/None"),
                    descriptor.AcceptsNoTag(category),
                    () => SetSurfaceCategorySelection(
                        descriptor,
                        capturedCategory,
                        Array.Empty<SemanticTag>(),
                        true));

                if (tags.Count > 0)
                    menu.AddSeparator($"{category.DisplayName}/");

                foreach (SemanticTag tag in tags)
                {
                    SemanticTag capturedTag = tag;
                    bool selected = descriptor.HasTag(capturedTag);
                    menu.AddItem(
                        new GUIContent($"{category.DisplayName}/{capturedTag.DisplayName}"),
                        selected && !descriptor.AcceptsAnyTag(category),
                        () => ToggleSurfaceTag(descriptor, capturedTag));
                }
            }

            if (categories.Count == 0)
                menu.AddDisabledItem(new GUIContent("No Surface tag categories"));

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Reset All to Any"), false, () => ResetSurfaceTags(descriptor));
            menu.ShowAsContext();
        }

        private void ToggleSurfaceTag(PlacementSurfaceDescriptor descriptor, SemanticTag tag)
        {
            List<SemanticTag> tags = descriptor.SurfaceTags
                .Where(existing => existing && existing.Category == tag.Category)
                .ToList();
            if (!tags.Remove(tag))
                tags.Add(tag);
            SetSurfaceCategorySelection(descriptor, tag.Category, tags, false);
        }

        private void SetSurfaceCategorySelection(
            PlacementSurfaceDescriptor descriptor,
            TagCategory category,
            IEnumerable<SemanticTag> tags,
            bool selectNone)
        {
            Undo.RecordObject(descriptor, "Change Surface Tags");
            descriptor.SetCategorySelection(category, tags, selectNone);
            MarkSceneObjectChanged(descriptor);
        }

        private void ResetSurfaceTags(PlacementSurfaceDescriptor descriptor)
        {
            Undo.RecordObject(descriptor, "Reset Surface Tags");
            descriptor.ResetTagSelections();
            MarkSceneObjectChanged(descriptor);
        }

        private void ShowCapacityMenu(PlacementSurfaceDescriptor descriptor)
        {
            GenericMenu menu = new();
            menu.AddItem(
                new GUIContent("Unlimited"),
                !descriptor.LimitCapacity,
                () => SetSurfaceCapacity(descriptor, false, descriptor.MaxCapacity));
            menu.AddSeparator(string.Empty);

            foreach (int capacity in new[] { 0, 1, 2, 4, 8, 16 })
            {
                int capturedCapacity = capacity;
                menu.AddItem(
                    new GUIContent($"Maximum/{capturedCapacity}"),
                    descriptor.LimitCapacity && descriptor.MaxCapacity == capturedCapacity,
                    () => SetSurfaceCapacity(descriptor, true, capturedCapacity));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Edit Custom Value in Details"), false, () => SelectObject(descriptor));
            menu.ShowAsContext();
        }

        private static string GetCapacitySummary(PlacementSurfaceDescriptor descriptor)
        {
            string total = descriptor.LimitCapacity ? descriptor.MaxCapacity.ToString() : "∞";
            int specificRules = descriptor.AssetCapacityRules.Count(rule => rule?.IsConfigured == true);
            return specificRules > 0 ? $"{total} +{specificRules}" : total;
        }

        private static string GetCapacityTooltip(PlacementSurfaceDescriptor descriptor)
        {
            List<string> lines = new()
            {
                descriptor.LimitCapacity
                    ? $"Total capacity: {descriptor.MaxCapacity} placements."
                    : "Total capacity: Unlimited."
            };
            lines.AddRange(descriptor.AssetCapacityRules
                .Where(rule => rule?.IsConfigured == true)
                .Select(rule => $"{rule.DisplayName}: maximum {rule.MaxCapacity}"));

            if (lines.Count == 1)
                lines.Add("No asset-specific limits.");

            lines.Add("Open Details to edit asset-specific limits.");
            return string.Join("\n", lines);
        }

        private void SetSurfaceCapacity(
            PlacementSurfaceDescriptor descriptor,
            bool limited,
            int capacity)
        {
            Undo.RecordObject(descriptor, "Change Surface Capacity");
            descriptor.SetCapacity(limited, capacity);
            MarkSceneObjectChanged(descriptor);
        }

        private void SetSceneObjectLayer(GameObject gameObject, int layer)
        {
            Undo.RecordObject(gameObject, "Change Placement Surface Layer");
            gameObject.layer = layer;
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            PlacementSolver.ClearCandidateCache();
            MarkSceneSetupDirty();
        }

        private void AddSurfaceDescriptor(GameObject gameObject)
        {
            if (!gameObject || gameObject.GetComponentInParent<PlacementSurfaceDescriptor>())
                return;

            PlacementSurfaceDescriptor descriptor = Undo.AddComponent<PlacementSurfaceDescriptor>(gameObject);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            _selectedSceneSetupObject = descriptor;
            MarkSceneSetupDirty();
        }

        private void AddMissingSurfaceDescriptors(IEnumerable<SceneSetupObjectEntry> entries)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Placement Surface Descriptors");

            foreach (SceneSetupObjectEntry entry in entries)
            {
                if (entry.Type != SceneSetupObjectType.Surface ||
                    !entry.SurfaceCollider ||
                    entry.SurfaceDescriptor ||
                    entry.GameObject.GetComponentInParent<PlacementSurfaceDescriptor>())
                {
                    continue;
                }

                Undo.AddComponent<PlacementSurfaceDescriptor>(entry.GameObject);
                EditorSceneManager.MarkSceneDirty(entry.GameObject.scene);
            }

            Undo.CollapseUndoOperations(undoGroup);
            MarkSceneSetupDirty();
        }

        private static void SelectVisibleSceneObjects(IEnumerable<SceneSetupObjectEntry> entries)
        {
            Selection.objects = entries
                .Where(entry => entry.GameObject)
                .Select(entry => (UnityEngine.Object)entry.GameObject)
                .Distinct()
                .ToArray();
        }

        private void MarkSceneObjectChanged(Component component)
        {
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            Repaint();
        }

        private void MarkSceneSetupDirty()
        {
            _sceneSetupDirty = true;
            Repaint();
        }

        private void DrawSceneSetupSettingsClipboard(UnityEngine.Object selectedObject)
        {
            if (selectedObject is not PlacementSurfaceDescriptor descriptor)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent(
                        "Surface Settings",
                        "Copy semantic tags, accepted-asset rules, total capacity, and asset-specific capacity limits between placement surfaces."),
                    EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(
                        new GUIContent(
                            "Copy",
                            "Copy this placement surface's designer-authored settings."),
                        GUILayout.Width(58f)))
                {
                    _surfaceSettingsClipboard = PlacementSurfaceSettingsSnapshot.Capture(descriptor);
                    ShowNotification(new GUIContent($"Copied surface settings from {descriptor.gameObject.name}."));
                }

                using (new EditorGUI.DisabledScope(_surfaceSettingsClipboard == null))
                {
                    string sourceName = _surfaceSettingsClipboard?.SourceName;
                    if (GUILayout.Button(
                            new GUIContent(
                                "Paste",
                                string.IsNullOrWhiteSpace(sourceName)
                                    ? "Copy surface settings before pasting."
                                    : $"Paste the surface settings copied from {sourceName}."),
                            GUILayout.Width(58f)))
                    {
                        Undo.RecordObject(descriptor, "Paste Placement Surface Settings");
                        _surfaceSettingsClipboard.ApplyTo(descriptor);
                        EditorUtility.SetDirty(descriptor);
                        EditorSceneManager.MarkSceneDirty(descriptor.gameObject.scene);
                        PlacementSolver.ClearCandidateCache();
                        DestroySelectedObjectEditor();
                        MarkSceneSetupDirty();
                        ShowNotification(new GUIContent($"Pasted surface settings to {descriptor.gameObject.name}."));
                    }
                }
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawSelectedSceneSetupValidation()
        {
            EnsureSceneSetupEntries();
            SceneSetupObjectEntry entry = _sceneSetupEntries.FirstOrDefault(candidate =>
                candidate.MatchesDetailTarget(_selectedSceneSetupObject));

            if (entry == null)
                return;

            string status = GetSceneSetupStatus(
                entry,
                GenixEditorWindow.GetConfiguredSurfaceLayerMask(),
                out MessageType messageType);
            if (messageType is MessageType.Warning or MessageType.Error)
            {
                EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(status, messageType);
                EditorGUILayout.Space(4f);
            }
        }

        private void EnsureSceneSetupEntries()
        {
            if (!_sceneSetupDirty)
                return;

            RefreshSceneSetupEntries();
        }

        private void RefreshSceneSetupEntries()
        {
            _sceneSetupEntries.Clear();
            _sceneSetupEntries.AddRange(SceneSetupObjectDiscovery.Collect(
                GenixEditorWindow.GetConfiguredSurfaceLayerMask()));
            _sceneSetupDirty = false;

            if (_selectedSceneSetupObject &&
                _sceneSetupEntries.All(entry => !entry.MatchesDetailTarget(_selectedSceneSetupObject)))
            {
                _selectedSceneSetupObject = null;
                DestroySelectedObjectEditor();
            }

            Repaint();
        }

        private readonly struct SceneSetupColumns
        {
            public Rect Object { get; }
            public Rect Layer { get; }
            public Rect Tags { get; }
            public Rect Relation { get; }
            public Rect Capacity { get; }
            public Rect Status { get; }

            public SceneSetupColumns(Rect row)
            {
                const float gap = 4f;
                float x = row.x + 2f;
                float height = row.height - 2f;
                float y = row.y + 1f;

                const float relationWidth = 120f;
                float compactWidth = relationWidth + 38f + 26f + gap * 3f;
                float flexibleWidth = Mathf.Max(300f, row.width - compactWidth - gap * 2f - 4f);
                float objectWidth = Mathf.Clamp(flexibleWidth * 0.35f, 150f, 220f);
                float layerWidth = Mathf.Clamp(flexibleWidth * 0.23f, 105f, 135f);

                Object = Take(ref x, y, objectWidth, height, gap);
                Layer = Take(ref x, y, layerWidth, height, gap);
                Tags = Take(ref x, y, Mathf.Max(120f, row.xMax - x - compactWidth - gap - 2f), height, gap);
                Relation = Take(ref x, y, relationWidth, height, gap);
                Capacity = Take(ref x, y, 38f, height, gap);
                Status = new Rect(x, y, 26f, height);
            }

            private static Rect Take(
                ref float x,
                float y,
                float width,
                float height,
                float gap)
            {
                Rect rect = new(x, y, width, height);
                x += width + gap;
                return rect;
            }
        }
    }
}
