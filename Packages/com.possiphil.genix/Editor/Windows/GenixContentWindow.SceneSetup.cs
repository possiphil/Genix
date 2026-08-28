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

        private static void SelectVisibleSceneObjects(IEnumerable<SceneSetupObjectEntry> entries)
        {
            Selection.objects = entries
                .Where(entry => entry.GameObject)
                .Select(entry => (UnityEngine.Object)entry.GameObject)
                .Distinct()
                .ToArray();
        }

        private void MarkSceneSetupDirty()
        {
            _sceneSetupDirty = true;
            Repaint();
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
