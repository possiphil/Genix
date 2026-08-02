using Genix.Areas;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Generation
{
    /// <summary>Creates and resolves per-area groups below the scene's Genix root.</summary>
    public static class GeneratedHierarchy
    {
        private const string RootName = "Genix";
        private const string CreateUndoName = "Created Genix Generated Parent";

        /// <summary>Returns the generated-object group for an area, creating it when necessary.</summary>
        public static Transform GetOrCreate(IAreaSource areaSource)
        {
            Transform root = GetOrCreateRoot();
            string groupName = AreaName.ToUnitySafeDisplayName(areaSource.SourceInfo.SourceName);

            Transform existingGroup = root.Find(groupName);
            if (existingGroup)
                return existingGroup;

            GameObject group = new(groupName);
            Undo.RegisterCreatedObjectUndo(group, CreateUndoName);

            group.transform.SetParent(root);
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;

            return group.transform;
        }

        /// <summary>Attempts to resolve the existing generated-object group for an area.</summary>
        public static bool TryGet(IAreaSource areaSource, out Transform group)
        {
            group = null;

            if (areaSource == null)
                return false;

            GameObject root = GameObject.Find(RootName);
            if (!root)
                return false;

            string groupName = AreaName.ToUnitySafeDisplayName(areaSource.SourceInfo.SourceName);
            group = root.transform.Find(groupName);

            return group;
        }

        /// <summary>Removes the generated-object group for an area through Unity Undo.</summary>
        public static bool Clear(IAreaSource areaSource)
        {
            GameObject root = GameObject.Find(RootName);
            if (!root)
                return false;

            string groupName = AreaName.ToUnitySafeDisplayName(areaSource.SourceInfo.SourceName);
            Transform group = root.transform.Find(groupName);

            if (!group)
                return false;

            Undo.DestroyObjectImmediate(group.gameObject);
            return true;
        }

        private static Transform GetOrCreateRoot()
        {
            GameObject root = GameObject.Find(RootName);

            if (root)
                return root.transform;

            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, CreateUndoName);
            return root.transform;
        }
    }
}
