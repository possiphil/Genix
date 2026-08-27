using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Genix.Editor.SceneConfiguration
{
    internal enum SceneSetupObjectType
    {
        Surface,
        RelationAnchor,
        ExclusionRegion
    }

    internal sealed class SceneSetupObjectEntry
    {
        public SceneSetupObjectType Type { get; }
        public GameObject GameObject { get; }
        public Collider SurfaceCollider { get; }
        public PlacementSurfaceDescriptor SurfaceDescriptor { get; }
        public AssetRelationAnchor RelationAnchor { get; }
        public PlacementExclusionRegion ExclusionRegion { get; }

        public SceneSetupObjectEntry(
            GameObject gameObject,
            Collider surfaceCollider,
            PlacementSurfaceDescriptor surfaceDescriptor)
        {
            Type = SceneSetupObjectType.Surface;
            GameObject = gameObject;
            SurfaceCollider = surfaceCollider;
            SurfaceDescriptor = surfaceDescriptor;
            RelationAnchor = gameObject ? gameObject.GetComponent<AssetRelationAnchor>() : null;
        }

        public SceneSetupObjectEntry(PlacementExclusionRegion exclusionRegion)
        {
            Type = SceneSetupObjectType.ExclusionRegion;
            GameObject = exclusionRegion ? exclusionRegion.gameObject : null;
            ExclusionRegion = exclusionRegion;
        }

        public SceneSetupObjectEntry(AssetRelationAnchor relationAnchor)
        {
            Type = SceneSetupObjectType.RelationAnchor;
            GameObject = relationAnchor ? relationAnchor.gameObject : null;
            RelationAnchor = relationAnchor;
        }

        public Object DetailTarget => Type switch
        {
            SceneSetupObjectType.ExclusionRegion => ExclusionRegion,
            SceneSetupObjectType.RelationAnchor => RelationAnchor,
            _ => SurfaceDescriptor ? SurfaceDescriptor : GameObject
        };

        public bool MatchesDetailTarget(Object target) =>
            target &&
            (DetailTarget == target || RelationAnchor == target);
    }

    /// <summary>Captures designer-authored placement-surface settings for explicit copy and paste.</summary>
    internal sealed class PlacementSurfaceSettingsSnapshot
    {
        private readonly SemanticTag[] _surfaceTags;
        private readonly TagCategory[] _noneTagCategories;
        private readonly SemanticTag[] _allowedAssetTags;
        private readonly SemanticTag[] _forbiddenAssetTags;
        private readonly bool _limitCapacity;
        private readonly int _maxCapacity;
        private readonly CapacityRuleSnapshot[] _capacityRules;

        public string SourceName { get; }

        private PlacementSurfaceSettingsSnapshot(PlacementSurfaceDescriptor source)
        {
            SourceName = source.gameObject.name;
            _surfaceTags = source.SurfaceTags.Where(tag => tag).ToArray();
            _noneTagCategories = source.NoneTagCategories.Where(category => category).ToArray();
            _allowedAssetTags = source.AllowedAssetTags.Where(tag => tag).ToArray();
            _forbiddenAssetTags = source.ForbiddenAssetTags.Where(tag => tag).ToArray();
            _limitCapacity = source.LimitCapacity;
            _maxCapacity = source.MaxCapacity;
            _capacityRules = source.AssetCapacityRules
                .Where(rule => rule != null)
                .Select(rule => new CapacityRuleSnapshot(rule))
                .ToArray();
        }

        public static PlacementSurfaceSettingsSnapshot Capture(PlacementSurfaceDescriptor source) =>
            source ? new PlacementSurfaceSettingsSnapshot(source) : null;

        public void ApplyTo(PlacementSurfaceDescriptor target)
        {
            if (!target)
                return;

            target.ResetTagSelections();
            target.SetSurfaceTags(_surfaceTags);

            foreach (TagCategory category in _noneTagCategories)
                target.SetCategorySelection(category, new List<SemanticTag>(), selectNone: true);

            target.SetAllowedAssetTags(_allowedAssetTags);
            target.SetForbiddenAssetTags(_forbiddenAssetTags);
            target.SetCapacity(_limitCapacity, _maxCapacity);
            target.SetAssetCapacityRules(_capacityRules.Select(rule => rule.CreateRule()));
        }

        private readonly struct CapacityRuleSnapshot
        {
            private readonly PlacementSurfaceCapacityRuleScope _scope;
            private readonly AssetDefinition _asset;
            private readonly SemanticTag _assetTag;
            private readonly int _maxCapacity;

            public CapacityRuleSnapshot(PlacementSurfaceCapacityRule source)
            {
                _scope = source.Scope;
                _asset = source.Asset;
                _assetTag = source.AssetTag;
                _maxCapacity = source.MaxCapacity;
            }

            public PlacementSurfaceCapacityRule CreateRule()
            {
                PlacementSurfaceCapacityRule rule = new();

                if (_scope == PlacementSurfaceCapacityRuleScope.Asset)
                    rule.ConfigureAsset(_asset, _maxCapacity);
                else
                    rule.ConfigureTag(_assetTag, _maxCapacity);

                return rule;
            }
        }
    }

    /// <summary>Creates explicit, editable collider regions for otherwise hidden support surfaces.</summary>
    internal static class SupportSurfaceRegionAuthoring
    {
        private const string RegionName = "Support Surface";
        private const float DefaultSize = 1f;
        private const float MinimumThickness = 0.01f;
        private const float MaximumThickness = 0.05f;

        /// <summary>Returns whether a support region can be added below the selected scene object.</summary>
        public static bool CanCreate(GameObject selectedObject) =>
            selectedObject &&
            selectedObject.scene.IsValid() &&
            selectedObject.scene.isLoaded &&
            !EditorUtility.IsPersistent(selectedObject) &&
            !selectedObject.GetComponentInParent<GeneratedObjectMetadata>();

        /// <summary>
        /// Creates a thin box whose top initially matches the authored object's upper bound.
        /// Moving or duplicating this child exposes additional horizontal support levels to Genix.
        /// </summary>
        public static GameObject Create(
            GameObject selectedObject,
            LayerMask configuredSurfaceLayers,
            bool selectCreatedObject = true)
        {
            if (!CanCreate(selectedObject))
                return null;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Support Surface Region");

            PlacementSurfaceDescriptor descriptor =
                selectedObject.GetComponentInParent<PlacementSurfaceDescriptor>();
            if (!descriptor)
                descriptor = Undo.AddComponent<PlacementSurfaceDescriptor>(selectedObject);

            GameObject owner = descriptor.gameObject;
            Bounds localBounds = GetLocalContentBounds(owner);
            float thickness = Mathf.Clamp(
                localBounds.size.y * 0.02f,
                MinimumThickness,
                MaximumThickness);

            string name = GameObjectUtility.GetUniqueNameForSibling(owner.transform, RegionName);
            GameObject region = new(name)
            {
                layer = ResolveSurfaceLayer(owner, configuredSurfaceLayers)
            };
            Undo.RegisterCreatedObjectUndo(region, "Create Support Surface Region");
            Undo.SetTransformParent(region.transform, owner.transform, "Parent Support Surface Region");

            region.transform.localPosition = new Vector3(
                localBounds.center.x,
                localBounds.max.y - thickness * 0.5f,
                localBounds.center.z);
            region.transform.localRotation = Quaternion.identity;
            region.transform.localScale = Vector3.one;

            BoxCollider collider = Undo.AddComponent<BoxCollider>(region);
            collider.center = Vector3.zero;
            collider.size = new Vector3(
                Mathf.Max(MinimumThickness, localBounds.size.x),
                thickness,
                Mathf.Max(MinimumThickness, localBounds.size.z));
            collider.isTrigger = false;

            EditorUtility.SetDirty(descriptor);
            EditorUtility.SetDirty(collider);
            EditorSceneManager.MarkSceneDirty(owner.scene);
            PlacementSolver.ClearCandidateCache();
            Undo.CollapseUndoOperations(undoGroup);

            if (selectCreatedObject)
            {
                Selection.activeGameObject = region;
                EditorGUIUtility.PingObject(region);
            }

            return region;
        }

        private static Bounds GetLocalContentBounds(GameObject owner)
        {
            Bounds bounds = default;
            bool hasBounds = false;

            foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || renderer.GetComponentInParent<GeneratedObjectMetadata>())
                    continue;

                EncapsulateTransformedBounds(
                    renderer.localBounds,
                    renderer.transform,
                    owner.transform,
                    ref bounds,
                    ref hasBounds);
            }

            if (!hasBounds)
            {
                foreach (Collider collider in owner.GetComponentsInChildren<Collider>(true))
                {
                    if (!collider || collider.GetComponentInParent<GeneratedObjectMetadata>())
                        continue;

                    EncapsulateWorldBounds(
                        collider.bounds,
                        owner.transform,
                        ref bounds,
                        ref hasBounds);
                }
            }

            return hasBounds
                ? bounds
                : new Bounds(Vector3.zero, Vector3.one * DefaultSize);
        }

        private static void EncapsulateTransformedBounds(
            Bounds source,
            Transform sourceTransform,
            Transform targetTransform,
            ref Bounds target,
            ref bool initialized)
        {
            Vector3 min = source.min;
            Vector3 max = source.max;

            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 localPoint = new(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z);
                Vector3 targetPoint = targetTransform.InverseTransformPoint(
                    sourceTransform.TransformPoint(localPoint));
                EncapsulatePoint(targetPoint, ref target, ref initialized);
            }
        }

        private static void EncapsulateWorldBounds(
            Bounds source,
            Transform targetTransform,
            ref Bounds target,
            ref bool initialized)
        {
            Vector3 min = source.min;
            Vector3 max = source.max;

            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 worldPoint = new(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z);
                EncapsulatePoint(
                    targetTransform.InverseTransformPoint(worldPoint),
                    ref target,
                    ref initialized);
            }
        }

        private static void EncapsulatePoint(
            Vector3 point,
            ref Bounds bounds,
            ref bool initialized)
        {
            if (!initialized)
            {
                bounds = new Bounds(point, Vector3.zero);
                initialized = true;
                return;
            }

            bounds.Encapsulate(point);
        }

        private static int ResolveSurfaceLayer(GameObject owner, LayerMask configuredSurfaceLayers)
        {
            if (ContainsLayer(configuredSurfaceLayers, owner.layer))
                return owner.layer;

            foreach (Collider collider in owner.GetComponentsInChildren<Collider>(true))
            {
                if (collider && ContainsLayer(configuredSurfaceLayers, collider.gameObject.layer))
                    return collider.gameObject.layer;
            }

            for (int layer = 0; layer < 32; layer++)
            {
                if (ContainsLayer(configuredSurfaceLayers, layer))
                    return layer;
            }

            return owner.layer;
        }

        private static bool ContainsLayer(LayerMask mask, int layer) =>
            (mask.value & (1 << layer)) != 0;
    }

    /// <summary>Finds editable Genix surfaces, fixed relation anchors, and exclusion regions in loaded scenes.</summary>
    internal static class SceneSetupObjectDiscovery
    {
        public static List<SceneSetupObjectEntry> Collect(LayerMask configuredSurfaceLayers)
        {
            List<SceneSetupObjectEntry> entries = new();
            HashSet<GameObject> surfaceObjects = new();
            HashSet<GameObject> representedObjects = new();
            HashSet<PlacementSurfaceDescriptor> representedDescriptors = new();

            foreach (Collider collider in Resources.FindObjectsOfTypeAll<Collider>())
            {
                if (!IsSceneObject(collider) || collider.GetComponentInParent<GeneratedObjectMetadata>())
                    continue;

                PlacementSurfaceDescriptor descriptor =
                    collider.GetComponentInParent<PlacementSurfaceDescriptor>();
                bool usesConfiguredLayer =
                    (configuredSurfaceLayers.value & (1 << collider.gameObject.layer)) != 0;

                if (!descriptor && !usesConfiguredLayer)
                    continue;

                if (descriptor)
                    representedDescriptors.Add(descriptor);

                if (surfaceObjects.Add(collider.gameObject))
                {
                    entries.Add(new SceneSetupObjectEntry(collider.gameObject, collider, descriptor));
                    representedObjects.Add(collider.gameObject);
                }
            }

            foreach (PlacementSurfaceDescriptor descriptor in
                     Resources.FindObjectsOfTypeAll<PlacementSurfaceDescriptor>())
            {
                if (!IsSceneObject(descriptor) ||
                    representedDescriptors.Contains(descriptor) ||
                    descriptor.GetComponentInParent<GeneratedObjectMetadata>())
                {
                    continue;
                }

                entries.Add(new SceneSetupObjectEntry(descriptor.gameObject, null, descriptor));
                representedObjects.Add(descriptor.gameObject);
            }

            foreach (PlacementExclusionRegion region in
                     Resources.FindObjectsOfTypeAll<PlacementExclusionRegion>())
            {
                if (IsSceneObject(region))
                    entries.Add(new SceneSetupObjectEntry(region));
            }

            foreach (AssetRelationAnchor anchor in Resources.FindObjectsOfTypeAll<AssetRelationAnchor>())
            {
                if (IsSceneObject(anchor) &&
                    !representedObjects.Contains(anchor.gameObject) &&
                    !anchor.GetComponentInParent<GeneratedObjectMetadata>())
                {
                    entries.Add(new SceneSetupObjectEntry(anchor));
                }
            }

            return entries
                .OrderBy(entry => entry.GameObject.scene.name)
                .ThenBy(entry => GetHierarchyPath(entry.GameObject))
                .ThenBy(entry => entry.Type)
                .ToList();
        }

        private static bool IsSceneObject(Component component) =>
            component &&
            component.gameObject.scene.IsValid() &&
            component.gameObject.scene.isLoaded;

        private static string GetHierarchyPath(GameObject gameObject)
        {
            if (!gameObject)
                return string.Empty;

            string path = gameObject.name;
            Transform parent = gameObject.transform.parent;

            while (parent)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return path;
        }
    }
}
