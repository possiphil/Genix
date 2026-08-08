using System.Collections.Generic;
using System.Linq;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Editor.SceneConfiguration
{
    internal enum SceneSetupObjectType
    {
        Surface,
        ExclusionRegion
    }

    internal sealed class SceneSetupObjectEntry
    {
        public SceneSetupObjectType Type { get; }
        public GameObject GameObject { get; }
        public Collider SurfaceCollider { get; }
        public PlacementSurfaceDescriptor SurfaceDescriptor { get; }
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
        }

        public SceneSetupObjectEntry(PlacementExclusionRegion exclusionRegion)
        {
            Type = SceneSetupObjectType.ExclusionRegion;
            GameObject = exclusionRegion ? exclusionRegion.gameObject : null;
            ExclusionRegion = exclusionRegion;
        }

        public Object DetailTarget => Type == SceneSetupObjectType.ExclusionRegion
            ? ExclusionRegion
            : SurfaceDescriptor ? SurfaceDescriptor : GameObject;
    }

    /// <summary>Finds editable Genix surfaces and exclusion regions in all loaded scenes.</summary>
    internal static class SceneSetupObjectDiscovery
    {
        public static List<SceneSetupObjectEntry> Collect(LayerMask configuredSurfaceLayers)
        {
            List<SceneSetupObjectEntry> entries = new();
            HashSet<GameObject> surfaceObjects = new();
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
                    entries.Add(new SceneSetupObjectEntry(collider.gameObject, collider, descriptor));
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
            }

            foreach (PlacementExclusionRegion region in
                     Resources.FindObjectsOfTypeAll<PlacementExclusionRegion>())
            {
                if (IsSceneObject(region))
                    entries.Add(new SceneSetupObjectEntry(region));
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
