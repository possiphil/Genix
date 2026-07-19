using System.Collections.Generic;
using Genix.Areas;
using Genix.Editor.Generation;
using Genix.Editor.Genix.Editor.Common;
using Genix.Editor.Utilities;
using Genix.Layouts;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Layouts
{
    internal static class LayoutApplyService
    {
        private const string UndoName = "Applied Genix Layout";

        public static bool Apply(SavedLayout layout, IAreaSource areaSource, out string error)
        {
            if (!Validate(layout, areaSource, out error))
                return false;

            UndoStep.ExecuteAsSingleStep(UndoName, () =>
            {
                GeneratedHierarchy.Clear(areaSource);
                Transform generatedParent = GeneratedHierarchy.GetOrCreate(areaSource);
                GameObject wrapper = Instantiate(layout.Prefab);
                wrapper.name = layout.DisplayName;
                Unpack(wrapper);

                foreach (Transform child in GetDirectChildren(wrapper.transform))
                {
                    child.SetParent(generatedParent, false);
                    Undo.RegisterCreatedObjectUndo(child.gameObject, UndoName);
                }

                Object.DestroyImmediate(wrapper);
            });

            error = string.Empty;
            return true;
        }

        private static bool Validate(SavedLayout layout, IAreaSource areaSource, out string error)
        {
            if (!layout)
            {
                error = "No layout is selected.";
                return false;
            }

            if (!layout.Prefab)
            {
                error = $"Layout '{layout.DisplayName}' has no saved prefab.";
                return false;
            }

            if (areaSource == null)
            {
                error = "No Target Area is selected.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static GameObject Instantiate(GameObject prefab)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (!instance)
                instance = Object.Instantiate(prefab);

            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static void Unpack(GameObject root)
        {
            if (root && PrefabUtility.IsPartOfPrefabInstance(root))
            {
                PrefabUtility.UnpackPrefabInstance(
                    root,
                    PrefabUnpackMode.OutermostRoot,
                    InteractionMode.AutomatedAction);
            }
        }

        private static List<Transform> GetDirectChildren(Transform parent)
        {
            List<Transform> children = new();

            foreach (Transform child in parent)
                children.Add(child);

            return children;
        }
    }
}
