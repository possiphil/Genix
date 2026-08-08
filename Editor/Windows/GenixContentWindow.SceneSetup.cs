using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.SceneConfiguration;
using Genix.Extensions;
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
            ExclusionRegions
        }

        private const float SceneSetupMinimumTableWidth = 600f;
        private readonly List<SceneSetupObjectEntry> _sceneSetupEntries = new();
        private Vector2 _sceneSetupScroll;
        private string _sceneSetupSearch = string.Empty;
        private SceneSetupTypeFilter _sceneSetupTypeFilter;
        private bool _sceneSetupIssuesOnly;
        private bool _sceneSetupDirty = true;

        private void DrawSceneSetupTab(AssetCatalog catalog)
        {
            EnsureSceneSetupEntries();

            EditorGUILayout.HelpBox(
                "Review semantic placement surfaces and collider-free exclusion regions in the loaded scenes. Use the table for common edits and select a row for complete settings.",
                MessageType.Info);

            DrawSceneSetupFilters();
            EditorGUILayout.Space(6f);

            LayerMask configuredLayers = GenixEditorWindow.GetConfiguredSurfaceLayerMask();
            List<SceneSetupObjectEntry> visibleEntries = GetVisibleSceneSetupEntries(configuredLayers);
            int issueCount = visibleEntries.Count(entry => HasSceneSetupIssue(entry, configuredLayers));
            int missingDescriptorCount = visibleEntries.Count(entry =>
                entry.Type == SceneSetupObjectType.Surface &&
                entry.SurfaceCollider &&
                !entry.SurfaceDescriptor);

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

                if (GUILayout.Button("Refresh", GUILayout.Width(68f)))
                    RefreshSceneSetupEntries();
            });

            if (issueCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{issueCount} visible object{(issueCount == 1 ? string.Empty : "s")} need attention. Informational notes can describe optional semantic setup; warnings indicate ineffective configuration.",
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
                    new GUIContent("Type", "Show surfaces, exclusion regions, or both."),
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
                    Rect emptyRect = GUILayoutUtility.GetRect(
                        tableWidth,
                        42f,
                        GUILayout.Width(tableWidth));
                    EditorGUI.HelpBox(
                        emptyRect,
                        _sceneSetupEntries.Count == 0
                            ? "No placement surfaces or exclusion regions were found in the loaded scenes."
                            : "No scene objects match the current filters.",
                        MessageType.Info);
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
            EditorGUI.LabelField(columns.Tags, "Surface Tags", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(columns.Capacity, new GUIContent("Cap.", "Maximum placements supported by this surface."), EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(columns.Forward, new GUIContent("Fwd", "Whether this surface provides a preferred forward direction."), EditorStyles.miniBoldLabel);
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

            bool selected = _selectedSceneSetupObject == entry.DetailTarget;
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
                EditorGUI.LabelField(columns.Forward, "-", EditorStyles.centeredGreyMiniLabel);
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
                        descriptor.LimitCapacity ? descriptor.MaxCapacity.ToString() : "∞",
                        descriptor.LimitCapacity
                            ? $"Maximum {descriptor.MaxCapacity} placements on this surface."
                            : "Unlimited placement capacity."),
                    EditorStyles.miniButton))
            {
                ShowCapacityMenu(descriptor);
            }

            EditorGUI.BeginChangeCheck();
            bool useForward = EditorGUI.Toggle(columns.Forward, descriptor.UsePreferredForward);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(descriptor, "Change Preferred Surface Forward");
                descriptor.SetPreferredForwardEnabled(useForward);
                MarkSceneObjectChanged(descriptor);
            }
        }

        private static void DrawUnavailableSceneSetupFields(SceneSetupColumns columns)
        {
            EditorGUI.LabelField(columns.Layer, "-", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.LabelField(columns.Tags, "Collider-free", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.LabelField(columns.Capacity, "-", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.LabelField(columns.Forward, "-", EditorStyles.centeredGreyMiniLabel);
        }

        private List<SceneSetupObjectEntry> GetVisibleSceneSetupEntries(LayerMask configuredLayers)
        {
            IEnumerable<SceneSetupObjectEntry> entries = _sceneSetupEntries.Where(entry =>
                entry != null && entry.GameObject);

            entries = _sceneSetupTypeFilter switch
            {
                SceneSetupTypeFilter.Surfaces => entries.Where(entry => entry.Type == SceneSetupObjectType.Surface),
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
            string tags = entry.SurfaceDescriptor
                ? string.Join(" ", entry.SurfaceDescriptor.SurfaceTags.Where(tag => tag).Select(tag => tag.DisplayName))
                : string.Empty;
            return $"{entry.Type} {tags} {GetSceneSetupStatus(entry, configuredLayers, out _)}";
        }

        private static bool HasSceneSetupIssue(
            SceneSetupObjectEntry entry,
            LayerMask configuredLayers) =>
            GetSceneSetupStatus(entry, configuredLayers, out _) != "Ready";

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

        private void DrawSelectedSceneSetupValidation()
        {
            EnsureSceneSetupEntries();
            SceneSetupObjectEntry entry = _sceneSetupEntries.FirstOrDefault(candidate =>
                candidate.DetailTarget == _selectedSceneSetupObject);

            if (entry == null)
                return;

            string status = GetSceneSetupStatus(
                entry,
                GenixEditorWindow.GetConfiguredSurfaceLayerMask(),
                out MessageType messageType);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                status == "Ready"
                    ? "Ready. This object is active and its current Genix configuration is effective."
                    : status,
                messageType == MessageType.None ? MessageType.Info : messageType);
            EditorGUILayout.Space(4f);
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
                _sceneSetupEntries.All(entry => entry.DetailTarget != _selectedSceneSetupObject))
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
            public Rect Capacity { get; }
            public Rect Forward { get; }
            public Rect Status { get; }

            public SceneSetupColumns(Rect row)
            {
                const float gap = 4f;
                float x = row.x + 2f;
                float height = row.height - 2f;
                float y = row.y + 1f;

                float compactWidth = 38f + gap + 38f + gap + 26f;
                float flexibleWidth = Mathf.Max(180f, row.width - compactWidth - gap * 4f);
                float objectWidth = Mathf.Clamp(flexibleWidth * 0.38f, 150f, 220f);
                float layerWidth = Mathf.Clamp(flexibleWidth * 0.24f, 105f, 135f);

                Object = Take(ref x, y, objectWidth, height, gap);
                Layer = Take(ref x, y, layerWidth, height, gap);
                Tags = Take(ref x, y, Mathf.Max(120f, row.xMax - x - compactWidth - gap - 2f), height, gap);
                Capacity = Take(ref x, y, 38f, height, gap);
                Forward = Take(ref x, y, 38f, height, gap);
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
