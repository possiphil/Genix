using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Core;
using Genix.Layouts;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Generation
{
    internal static class SceneGenerationService
    {
        private const string CreateObjectUndoName = "Generated Genix Object";
        private const string RegenerateUndoName = "Regenerated Genix Objects";

        public static bool Apply(
            GenerationPlan plan,
            Transform parent,
            out string error)
        {
            List<GameObject> created = new();

            try
            {
                foreach (PlannedObject plannedObject in plan.Objects)
                    created.Add(Instantiate(plannedObject, parent));

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                foreach (GameObject createdObject in created)
                {
                    if (createdObject)
                        Undo.DestroyObjectImmediate(createdObject);
                }

                error = $"Genix could not apply the generation plan: {exception.Message}";
                return false;
            }
        }

        public static bool Clear(IAreaSource areaSource)
        {
            return GeneratedHierarchy.Clear(areaSource);
        }

        public static GameObject CreateSnapshot(IAreaSource areaSource)
        {
            if (!GeneratedHierarchy.TryGet(areaSource, out Transform generatedParent))
                return null;

            GameObject snapshotRoot = new("Genix Rollback Snapshot")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            foreach (Transform child in generatedParent)
            {
                GameObject clone = UnityEngine.Object.Instantiate(child.gameObject, snapshotRoot.transform, false);
                clone.name = child.name;
                SetHideFlagsRecursively(clone, HideFlags.HideAndDontSave);
            }

            return snapshotRoot;
        }

        public static void RestoreSnapshot(IAreaSource areaSource, GameObject snapshot)
        {
            if (!snapshot)
                return;

            GeneratedHierarchy.Clear(areaSource);
            Transform generatedParent = GeneratedHierarchy.GetOrCreate(areaSource);

            foreach (Transform snapshotChild in snapshot.transform)
            {
                GameObject restored = UnityEngine.Object.Instantiate(
                    snapshotChild.gameObject,
                    generatedParent,
                    false);
                restored.name = snapshotChild.name;
                SetHideFlagsRecursively(restored, HideFlags.None);
                Undo.RegisterCreatedObjectUndo(restored, RegenerateUndoName);
            }
        }

        public static void RemoveEmptyParent(Transform parent, bool existedBefore)
        {
            if (!existedBefore && parent && parent.childCount == 0)
                Undo.DestroyObjectImmediate(parent.gameObject);
        }

        private static GameObject Instantiate(PlannedObject plannedObject, Transform parent)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                plannedObject.Asset.Prefab,
                parent);
            Undo.RegisterCreatedObjectUndo(instance, CreateObjectUndoName);

            instance.name = plannedObject.ObjectName;
            instance.transform.rotation = plannedObject.Candidate.Rotation;
            instance.transform.position =
                plannedObject.Candidate.Position -
                plannedObject.Candidate.Rotation * plannedObject.Asset.BoundsCenterOffset;

            GeneratedObjectMetadata metadata = instance.GetComponent<GeneratedObjectMetadata>();

            if (!metadata)
                metadata = instance.AddComponent<GeneratedObjectMetadata>();

            metadata.Initialize(plannedObject.Candidate.PlacementType);
            return instance;
        }

        private static void SetHideFlagsRecursively(GameObject gameObject, HideFlags hideFlags)
        {
            if (!gameObject)
                return;

            gameObject.hideFlags = hideFlags;

            foreach (Transform child in gameObject.transform)
                SetHideFlagsRecursively(child.gameObject, hideFlags);
        }
    }
}
