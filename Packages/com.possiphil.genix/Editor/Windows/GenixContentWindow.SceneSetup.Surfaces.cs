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
                    if (GUI.Button(columns.Tags, "Configure", EditorStyles.miniButton))
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
            Undo.SetCurrentGroupName("Configure Genix Surfaces");

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
    }
}
