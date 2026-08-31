using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Authoring;
using SpaceFoundationSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Creates undoable SFS scene objects from validated voxel-cell plans.</summary>
    internal static class SfsAuthoringSceneBuilder
    {
        public const string DelimiterLayerName = "SFS Delimiter";
        public const string SpaceLayerName = "SFS Space";

        public static SpaceFoundationSystem.SpaceFoundation FindSingleFoundation()
        {
            SpaceFoundationSystem.SpaceFoundation[] foundations = FindFoundations();
            return foundations.Length == 1 ? foundations[0] : null;
        }

        public static SpaceFoundationSystem.SpaceFoundation[] FindFoundations() =>
            Object.FindObjectsByType<SpaceFoundationSystem.SpaceFoundation>(FindObjectsInactive.Include);

        public static SpaceFoundationSystem.SpaceFoundation CreateFoundation(float voxelSize, Transform parent = null)
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Space Foundation");

            GameObject gameObject = new("Space Foundation");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Space Foundation");
            if (parent)
                Undo.SetTransformParent(gameObject.transform, parent, "Parent Space Foundation");

            SpaceFoundationSystem.SpaceFoundation foundation = Undo.AddComponent<SpaceFoundationSystem.SpaceFoundation>(gameObject);
            foundation.voxelSize = Mathf.Max(0.001f, voxelSize);
            ConfigureFoundationLayerMask(foundation, out _);
            EditorUtility.SetDirty(foundation);
            Selection.activeGameObject = gameObject;
            Undo.CollapseUndoOperations(undoGroup);
            return foundation;
        }

        public static GameObject CreateLayout(
            SfsAuthoringPlan plan,
            SpaceFoundationSystem.SpaceFoundation foundation,
            out string error)
        {
            if (plan == null)
            {
                error = "No valid SFS authoring plan is available.";
                return null;
            }

            if (!foundation)
            {
                error = "Select or create a Space Foundation before creating a layout.";
                return null;
            }

            if (!EnsureLayer(DelimiterLayerName, out int delimiterLayer, out error))
                return null;

            ConfigureFoundationLayerMask(foundation, out _);

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Create {plan.Name}");

            GameObject root = new(plan.Name);
            Undo.RegisterCreatedObjectUndo(root, $"Create {plan.Name}");
            root.transform.position = plan.ActualCenter;
            SfsAuthoringLayoutDisplay display = Undo.AddComponent<SfsAuthoringLayoutDisplay>(root);
            display.Configure(plan.InteriorVolumes.Select(volume =>
            {
                Bounds worldBounds = volume.ToWorldBounds(plan.VoxelSize);
                return new Bounds(root.transform.InverseTransformPoint(worldBounds.center), worldBounds.size);
            }));
            EditorUtility.SetDirty(display);
            GameObject delimiterRoot = CreateChild("Delimiters", root.transform);
            GameObject anchorRoot = CreateChild("Anchors", root.transform);

            foreach (SfsAuthoringCellVolume delimiterPlan in plan.Delimiters)
                CreateDelimiter(delimiterPlan, plan.VoxelSize, delimiterLayer, delimiterRoot.transform);

            foreach (SfsAuthoringAnchorPlan anchorPlan in plan.Anchors)
                CreateAnchor(anchorPlan, plan.VoxelSize, foundation, anchorRoot.transform);

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Undo.CollapseUndoOperations(undoGroup);
            error = string.Empty;
            return root;
        }

        public static bool TryAddFreeSpaceDisplay(GameObject selectedObject, out string error)
        {
            Transform layoutRoot = FindLayoutRoot(selectedObject ? selectedObject.transform : null);
            if (!layoutRoot)
            {
                error = "Select an SFS Authoring layout parent or one of its children.";
                return false;
            }

            if (layoutRoot.TryGetComponent(out SfsAuthoringLayoutDisplay existing))
            {
                Selection.activeGameObject = layoutRoot.gameObject;
                EditorGUIUtility.PingObject(existing);
                error = string.Empty;
                return true;
            }

            Anchor anchor = layoutRoot.GetComponentInChildren<Anchor>(true);
            SpaceFoundationSystem.SpaceFoundation foundation = anchor ? anchor.correspondingSpaceFoundation : null;
            float voxelSize = foundation ? foundation.voxelSize : 0f;
            if (voxelSize <= 0f)
            {
                error = "The selected layout has no valid SFS Foundation reference or voxel size.";
                return false;
            }

            Transform delimiters = layoutRoot.Find("Delimiters");
            if (!TryGetBoundaryCenter(delimiters, "Boundary Left", layoutRoot, out Vector3 left) ||
                !TryGetBoundaryCenter(delimiters, "Boundary Right", layoutRoot, out Vector3 right) ||
                !TryGetBoundaryCenter(delimiters, "Boundary Bottom", layoutRoot, out Vector3 bottom) ||
                !TryGetBoundaryCenter(delimiters, "Boundary Top", layoutRoot, out Vector3 top) ||
                !TryGetBoundaryCenter(delimiters, "Boundary Back", layoutRoot, out Vector3 back) ||
                !TryGetBoundaryCenter(delimiters, "Boundary Front", layoutRoot, out Vector3 front))
            {
                error = "This older layout cannot be reconstructed safely. Recreate non-rectangular or grid layouts with the current SFS Authoring version.";
                return false;
            }

            Vector3 minimum = new(left.x + voxelSize, bottom.y + voxelSize, back.z + voxelSize);
            Vector3 maximum = new(right.x - voxelSize, top.y - voxelSize, front.z - voxelSize);
            Vector3 size = maximum - minimum + Vector3.one * voxelSize;
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
            {
                error = "The recovered boundary shell does not contain a positive free-space volume.";
                return false;
            }

            SfsAuthoringLayoutDisplay display = Undo.AddComponent<SfsAuthoringLayoutDisplay>(layoutRoot.gameObject);
            display.Configure(new[] { new Bounds((minimum + maximum) * 0.5f, size) });
            EditorUtility.SetDirty(display);
            Selection.activeGameObject = layoutRoot.gameObject;
            EditorGUIUtility.PingObject(display);
            error = string.Empty;
            return true;
        }

        public static Anchor CreateAnchor(
            Vector3 position,
            SpaceFoundationSystem.SpaceFoundation foundation,
            float range,
            Transform parent = null,
            string name = "SFS Anchor")
        {
            GameObject gameObject = new(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create SFS Anchor");
            gameObject.transform.position = position;
            parent = SanitizeAuthoringParent(parent);
            if (parent)
                gameObject.transform.SetParent(parent, true);

            Anchor anchor = Undo.AddComponent<Anchor>(gameObject);
            anchor.correspondingSpaceFoundation = foundation;
            SetAnchorRange(anchor, range);
            EditorUtility.SetDirty(anchor);
            Selection.activeGameObject = gameObject;
            return anchor;
        }

        public static Delimiter CreateBoxDelimiter(
            Vector3 center,
            Vector3 size,
            SpaceFoundationSystem.SpaceFoundation foundation,
            Transform parent = null,
            string name = "SFS Box Delimiter")
        {
            if (!EnsureLayer(DelimiterLayerName, out int layer, out string error))
            {
                Debug.LogError(error);
                return null;
            }

            ConfigureFoundationLayerMask(foundation, out _);
            GameObject gameObject = new(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create SFS Box Delimiter");
            gameObject.layer = layer;
            gameObject.transform.position = center;
            parent = SanitizeAuthoringParent(parent);
            if (parent)
                gameObject.transform.SetParent(parent, true);

            BoxCollider collider = Undo.AddComponent<BoxCollider>(gameObject);
            collider.size = new Vector3(
                Mathf.Max(0.001f, size.x),
                Mathf.Max(0.001f, size.y),
                Mathf.Max(0.001f, size.z));
            Delimiter delimiter = Undo.AddComponent<Delimiter>(gameObject);
            Selection.activeGameObject = gameObject;
            return delimiter;
        }

        public static Delimiter CreateGridAlignedBoxDelimiter(
            Vector3 requestedCenter,
            Vector3Int cellCounts,
            SpaceFoundationSystem.SpaceFoundation foundation,
            Transform parent = null,
            string name = "SFS Box Delimiter")
        {
            if (!foundation)
            {
                Debug.LogError("A Space Foundation is required to align the delimiter to its voxel grid.");
                return null;
            }

            float voxelSize = Mathf.Max(0.001f, foundation.voxelSize);
            cellCounts = new Vector3Int(
                Mathf.Max(1, cellCounts.x),
                Mathf.Max(1, cellCounts.y),
                Mathf.Max(1, cellCounts.z));
            Vector3 halfSpanInCells = (Vector3)(cellCounts - Vector3Int.one) * 0.5f;
            Vector3 requestedCell = requestedCenter / voxelSize - halfSpanInCells;
            Vector3Int minimumCell = new(
                Mathf.RoundToInt(requestedCell.x),
                Mathf.RoundToInt(requestedCell.y),
                Mathf.RoundToInt(requestedCell.z));
            Bounds bounds = new SfsAuthoringCellVolume(name, minimumCell, cellCounts).ToWorldBounds(voxelSize);
            float clearance = Mathf.Max(0.0001f, voxelSize * 0.08f);
            Vector3 colliderSize = new(
                Mathf.Max(0.001f, bounds.size.x - clearance),
                Mathf.Max(0.001f, bounds.size.y - clearance),
                Mathf.Max(0.001f, bounds.size.z - clearance));

            return CreateBoxDelimiter(bounds.center, colliderSize, foundation, parent, name);
        }

        public static int ConvertSelectedColliders(
            SpaceFoundationSystem.SpaceFoundation foundation,
            out string error)
        {
            if (!EnsureLayer(DelimiterLayerName, out int layer, out error))
                return 0;

            ConfigureFoundationLayerMask(foundation, out _);
            Collider[] colliders = Selection.gameObjects
                .SelectMany(value => value.GetComponents<Collider>())
                .Distinct()
                .ToArray();

            int converted = 0;
            foreach (Collider collider in colliders)
            {
                Undo.RecordObject(collider.gameObject, "Convert Collider to SFS Delimiter");
                collider.gameObject.layer = layer;
                if (!collider.TryGetComponent(out Delimiter _))
                {
                    Undo.AddComponent<Delimiter>(collider.gameObject);
                    converted++;
                }

                EditorUtility.SetDirty(collider.gameObject);
            }

            error = colliders.Length == 0
                ? "Select at least one GameObject with a Collider."
                : string.Empty;
            return converted;
        }

        public static bool ConfigureFoundationLayerMask(
            SpaceFoundationSystem.SpaceFoundation foundation,
            out string error)
        {
            if (!foundation)
            {
                error = "No Space Foundation is selected.";
                return false;
            }

            if (!EnsureLayer(DelimiterLayerName, out int layer, out error))
                return false;

            int requiredMask = 1 << layer;
            if ((foundation.delimitingLayerMask.value & requiredMask) != requiredMask)
            {
                Undo.RecordObject(foundation, "Configure SFS Delimiter Layer");
                foundation.delimitingLayerMask |= requiredMask;
                EditorUtility.SetDirty(foundation);
            }

            return true;
        }

        public static bool EnsureLayer(string layerName, out int layer, out string error)
        {
            layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                error = string.Empty;
                return true;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
            {
                error = $"Unity's TagManager could not be opened to create the '{layerName}' layer.";
                return false;
            }

            SerializedObject tagManager = new(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty candidate = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(candidate.stringValue))
                    continue;

                candidate.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                layer = i;
                error = string.Empty;
                return true;
            }

            error = $"No free user layer is available for '{layerName}'.";
            return false;
        }

        public static void RunCompute()
        {
            if (!EditorApplication.ExecuteMenuItem("SFS/Compute Graph"))
                Debug.LogError("The SFS Compute Graph command is unavailable.");
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new(name);
            Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void CreateDelimiter(
            SfsAuthoringCellVolume plan,
            float voxelSize,
            int layer,
            Transform parent)
        {
            Bounds bounds = plan.ToWorldBounds(voxelSize);
            float clearance = Mathf.Max(0.0001f, voxelSize * 0.08f);

            GameObject gameObject = new(plan.Name);
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {plan.Name}");
            gameObject.layer = layer;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = bounds.center;

            BoxCollider collider = Undo.AddComponent<BoxCollider>(gameObject);
            collider.size = new Vector3(
                Mathf.Max(0.001f, bounds.size.x - clearance),
                Mathf.Max(0.001f, bounds.size.y - clearance),
                Mathf.Max(0.001f, bounds.size.z - clearance));
            Undo.AddComponent<Delimiter>(gameObject);
        }

        private static void CreateAnchor(
            SfsAuthoringAnchorPlan plan,
            float voxelSize,
            SpaceFoundationSystem.SpaceFoundation foundation,
            Transform parent)
        {
            GameObject gameObject = new(plan.Name);
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {plan.Name}");
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = plan.ToWorldPosition(voxelSize);
            Anchor anchor = Undo.AddComponent<Anchor>(gameObject);
            anchor.correspondingSpaceFoundation = foundation;
            SetAnchorRange(anchor, plan.Range);
            EditorUtility.SetDirty(anchor);
        }

        private static void SetAnchorRange(Anchor anchor, float range)
        {
            SerializedObject serializedAnchor = new(anchor);
            SerializedProperty maxDistance = serializedAnchor.FindProperty("maxAnchorDistance");
            if (maxDistance != null)
            {
                maxDistance.floatValue = Mathf.Max(0.01f, range);
                serializedAnchor.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Transform SanitizeAuthoringParent(Transform parent)
        {
            if (!parent)
                return null;

            SpaceFoundationSystem.SpaceFoundation foundation = parent.GetComponentInParent<SpaceFoundationSystem.SpaceFoundation>();
            return foundation ? foundation.transform.parent : parent;
        }

        private static Transform FindLayoutRoot(Transform selected)
        {
            for (Transform current = selected; current; current = current.parent)
            {
                if (current.Find("Delimiters") && current.Find("Anchors"))
                    return current;
            }

            return null;
        }

        private static bool TryGetBoundaryCenter(
            Transform delimiterRoot,
            string name,
            Transform layoutRoot,
            out Vector3 localCenter)
        {
            localCenter = default;
            Transform boundary = delimiterRoot ? delimiterRoot.Find(name) : null;
            if (!boundary || !boundary.TryGetComponent(out BoxCollider collider))
                return false;

            localCenter = layoutRoot.InverseTransformPoint(boundary.TransformPoint(collider.center));
            return true;
        }
    }
}
