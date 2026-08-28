using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Assets;
using Genix.Geometry;
using Genix.Orientation;
using Genix.Placement;
using Genix.Semantics;
using Genix.Styles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Evaluation
{
    internal static partial class OutdoorEvaluationSetupUtility
    {
        private static int ConfigureScene(
            Scene scene,
            SemanticTag outdoor,
            SemanticTag natural,
            TagCategory environmentCategory,
            TagCategory themeCategory,
            SemanticTag terrainSupport,
            SemanticTag pathSupport,
            SemanticTag waterSupport,
            SemanticTag pathFunction,
            SemanticTag restArea,
            SemanticTag parkingSpot)
        {
            GameObject terrainObject = FindSceneObject(scene, "Terrain");
            GameObject pathObject = FindSceneObject(scene, "Path");
            GameObject waterObject = FindSceneObject(scene, "Water");
            GameObject boundaryLeft = FindSceneObject(scene, "Boundary Left");
            if (!terrainObject || !pathObject || !waterObject || !boundaryLeft)
            {
                throw new InvalidOperationException(
                    "OutdoorEnvironment must contain Terrain, Path, Water, and Boundary Left objects.");
            }

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            BoxCollider boundary = boundaryLeft.GetComponent<BoxCollider>();
            Collider waterCollider = waterObject.GetComponent<Collider>();
            if (!terrain || !boundary || !waterCollider)
            {
                throw new InvalidOperationException(
                    "Outdoor Terrain, Water collider, or Boundary Left collider is missing.");
            }

            int placementLayer = LayerMask.NameToLayer("Placement Surface");
            if (placementLayer < 0)
                placementLayer = 8;

            SetLayerRecursively(terrainObject, placementLayer);
            SetLayerRecursively(pathObject, placementLayer);
            SetLayerRecursively(waterObject, placementLayer);
            ConfigureSurface(terrainObject, terrainSupport);
            RemoveSurfaceDescriptor(pathObject);
            RemoveExclusionRegions(pathObject);
            RemoveSurfaceDescriptor(waterObject);

            Transform[] pathSegments = GetPathSegments(pathObject);
            if (pathSegments.Length == 0)
                throw new InvalidOperationException("Outdoor Path contains no spline segments.");

            foreach (Transform segment in pathSegments)
            {
                ConfigureSurface(segment.gameObject, pathSupport);
                PlacementExclusionRegion exclusion =
                    segment.GetComponent<PlacementExclusionRegion>() ??
                    segment.gameObject.AddComponent<PlacementExclusionRegion>();
                exclusion.ConfigureChildColliders(PlacementTarget.Floor);
                exclusion.SetExemptAssetTags(new[] { pathFunction });
                EditorUtility.SetDirty(exclusion);
            }

            Transform sceneRoot = pathObject.transform.root;
            Transform previousSemanticRoot = sceneRoot.Find(SemanticRootName);
            if (previousSemanticRoot)
                UnityEngine.Object.DestroyImmediate(previousSemanticRoot.gameObject);

            GameObject semanticRootObject = new(SemanticRootName);
            SceneManager.MoveGameObjectToScene(semanticRootObject, scene);
            semanticRootObject.transform.SetParent(sceneRoot, false);
            Transform semanticRoot = semanticRootObject.transform;

            ConfigureBridgeExclusion(pathObject, pathSegments, semanticRoot);

            IReadOnlyList<(Transform Segment, PathFrame Frame)> pathFrames = pathSegments
                .Select(segment => (segment, CreatePathFrame(CollectPathPoints(segment.gameObject), boundary, terrain)))
                .OrderByDescending(entry => entry.Item2.MaximumProgress)
                .ToArray();
            IReadOnlyList<PathFrame> frames = pathFrames
                .Select(entry => entry.Frame)
                .OrderByDescending(frame => frame.MaximumProgress)
                .ToArray();
            foreach ((Transform segment, PathFrame segmentFrame) in pathFrames)
            {
                PathPlacementSource source = segment.GetComponent<PathPlacementSource>() ??
                                             segment.gameObject.AddComponent<PathPlacementSource>();
                source.SetPathTags(new[] { pathFunction });
                source.SetWorldPoints(CreatePathStations(new[] { segmentFrame }, 0.5f, 0f)
                    .Select(station => station.Position));
                EditorUtility.SetDirty(source);
            }

            PathFrame frame = frames[0];
            float side = ChooseTrailSide(frame, terrain, waterCollider);

            float parkingProgress = Mathf.Clamp(frame.MaximumProgress * 0.12f, 4f, 6f);
            CreatePathRegionAnchor(
                semanticRoot,
                "Parking Region",
                frame,
                terrain,
                parkingProgress,
                side,
                lateralDistance: 6.5f,
                size: new Vector2(5.5f, 9f),
                parkingSpot,
                facePath: false);

            const float restAreaFraction = 0.58f;
            float restAreaSide = ResolveUsableSide(
                frame,
                terrain,
                waterCollider,
                restAreaFraction,
                -side);
            float restProgress = Mathf.Clamp(
                frame.MaximumProgress * restAreaFraction,
                4f,
                frame.MaximumProgress - 1f);
            CreatePathRegionAnchor(
                semanticRoot,
                "Rest Area Region",
                frame,
                terrain,
                restProgress,
                restAreaSide,
                lateralDistance: 7f,
                size: new Vector2(12f, 8f),
                restArea,
                facePath: true);
            CreateWaterPlacementRegion(
                semanticRoot,
                terrain,
                waterCollider,
                waterSupport,
                placementLayer);

            Transform locationAnchor = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Environment" && item.parent && item.parent.name == "Anchors");
            if (locationAnchor)
            {
                SemanticTagSet tagSet = locationAnchor.GetComponent<SemanticTagSet>() ??
                                        locationAnchor.gameObject.AddComponent<SemanticTagSet>();
                tagSet.SetTagsForCategory(environmentCategory, new[] { outdoor });
                tagSet.SetTagsForCategory(themeCategory, new[] { natural });
                EditorUtility.SetDirty(tagSet);
            }

            return MaximumBollardCount;
        }

        private static void ConfigureSurface(GameObject gameObject, SemanticTag supportTag)
        {
            PlacementSurfaceDescriptor descriptor = gameObject.GetComponent<PlacementSurfaceDescriptor>() ??
                                                    gameObject.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetSurfaceTags(new[] { supportTag });
            descriptor.SetAllowedAssetTags(Array.Empty<SemanticTag>());
            descriptor.SetForbiddenAssetTags(Array.Empty<SemanticTag>());
            descriptor.SetCapacity(false, 0);
            descriptor.SetAssetCapacityRules(Array.Empty<PlacementSurfaceCapacityRule>());
            EditorUtility.SetDirty(descriptor);
        }

        private static void RemoveSurfaceDescriptor(GameObject gameObject)
        {
            foreach (PlacementSurfaceDescriptor descriptor in
                     gameObject.GetComponents<PlacementSurfaceDescriptor>())
            {
                UnityEngine.Object.DestroyImmediate(descriptor);
            }
        }

        private static void RemoveExclusionRegions(GameObject gameObject)
        {
            foreach (PlacementExclusionRegion region in gameObject.GetComponents<PlacementExclusionRegion>())
                UnityEngine.Object.DestroyImmediate(region);
        }

        private static void CreatePathRegionAnchor(
            Transform parent,
            string name,
            PathFrame frame,
            Terrain terrain,
            float progress,
            float side,
            float lateralDistance,
            Vector2 size,
            SemanticTag regionTag,
            bool facePath)
        {
            Vector3 center = frame.Sample(progress);
            Vector3 tangent = frame.Tangent(progress);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 position = GetTerrainPoint(terrain, center + right * lateralDistance * side);
            Vector3 forward = facePath ? -right * side : tangent;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude <= 0.001f)
                forward = tangent;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            position.y += 0.05f;

            GameObject region = new(name);
            region.transform.SetParent(parent, true);
            region.transform.SetPositionAndRotation(
                position,
                rotation);
            AssetRelationAnchor anchor = region.AddComponent<AssetRelationAnchor>();
            anchor.SetAssetTags(new[] { regionTag });
            anchor.SetCustomBounds(
                true,
                Vector3.zero,
                new Vector3(size.x, 20f, size.y));
            EditorUtility.SetDirty(anchor);
        }

        private static void CreateWaterPlacementRegion(
            Transform parent,
            Terrain terrain,
            Collider water,
            SemanticTag supportTag,
            int layer)
        {
            Bounds waterBounds = water.bounds;
            Bounds terrainBounds = new(
                terrain.transform.position + terrain.terrainData.size * 0.5f,
                terrain.terrainData.size);
            const float edgeInset = 0.35f;
            float minimumX = waterBounds.min.x + edgeInset;
            float maximumX = waterBounds.max.x - edgeInset;
            float minimumZ = waterBounds.min.z + edgeInset;
            float maximumZ = waterBounds.max.z - edgeInset;
            float surfaceY = waterBounds.max.y;
            Vector3 origin = new(waterBounds.center.x, surfaceY + 0.03f, waterBounds.center.z);

            List<Vector3> vertices = new();
            List<int> triangles = new();
            for (int z = 0; z < WaterGridResolution; z++)
            {
                float z0 = Mathf.Lerp(minimumZ, maximumZ, z / (float)WaterGridResolution);
                float z1 = Mathf.Lerp(minimumZ, maximumZ, (z + 1f) / WaterGridResolution);
                for (int x = 0; x < WaterGridResolution; x++)
                {
                    float x0 = Mathf.Lerp(minimumX, maximumX, x / (float)WaterGridResolution);
                    float x1 = Mathf.Lerp(minimumX, maximumX, (x + 1f) / WaterGridResolution);
                    if (!IsExposedWaterCell(terrain, terrainBounds, surfaceY, x0, x1, z0, z1))
                        continue;

                    int first = vertices.Count;
                    vertices.Add(new Vector3(x0 - origin.x, 0f, z0 - origin.z));
                    vertices.Add(new Vector3(x1 - origin.x, 0f, z0 - origin.z));
                    vertices.Add(new Vector3(x0 - origin.x, 0f, z1 - origin.z));
                    vertices.Add(new Vector3(x1 - origin.x, 0f, z1 - origin.z));
                    triangles.Add(first);
                    triangles.Add(first + 2);
                    triangles.Add(first + 1);
                    triangles.Add(first + 1);
                    triangles.Add(first + 2);
                    triangles.Add(first + 3);
                }
            }

            if (triangles.Count == 0)
                throw new InvalidOperationException("Outdoor Water exposes no stable continuous placement region.");

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(WaterPlacementMeshPath);
            if (!mesh)
            {
                mesh = new Mesh { name = "Outdoor Water Placement Region" };
                AssetDatabase.CreateAsset(mesh, WaterPlacementMeshPath);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            GameObject region = new("Water Placement Region");
            region.layer = layer;
            region.transform.SetParent(parent, true);
            region.transform.SetPositionAndRotation(origin, Quaternion.identity);
            MeshCollider regionCollider = region.AddComponent<MeshCollider>();
            regionCollider.sharedMesh = mesh;
            regionCollider.convex = false;
            ConfigureSurface(region, supportTag);
        }

        private static bool IsExposedWaterCell(
            Terrain terrain,
            Bounds terrainBounds,
            float surfaceY,
            float x0,
            float x1,
            float z0,
            float z1)
        {
            return IsExposedWaterPoint(terrain, terrainBounds, surfaceY, x0, z0) &&
                   IsExposedWaterPoint(terrain, terrainBounds, surfaceY, x1, z0) &&
                   IsExposedWaterPoint(terrain, terrainBounds, surfaceY, x0, z1) &&
                   IsExposedWaterPoint(terrain, terrainBounds, surfaceY, x1, z1);
        }

        private static bool IsExposedWaterPoint(
            Terrain terrain,
            Bounds terrainBounds,
            float surfaceY,
            float x,
            float z)
        {
            if (x < terrainBounds.min.x || x > terrainBounds.max.x ||
                z < terrainBounds.min.z || z > terrainBounds.max.z)
            {
                return false;
            }

            Vector3 point = new(x, surfaceY, z);
            float terrainY = terrain.SampleHeight(point) + terrain.transform.position.y;
            return surfaceY - terrainY >= MinimumWaterDepth;
        }

        private static void ConfigureBridgeExclusion(
            GameObject path,
            IReadOnlyCollection<Transform> pathSegments,
            Transform semanticRoot)
        {
            HashSet<Transform> segments = new(pathSegments);
            Transform[] bridgeRoots = path.transform
                .Cast<Transform>()
                .Where(child => !segments.Contains(child))
                .ToArray();

            Bounds combined = default;
            bool hasBounds = false;
            foreach (Transform bridgeRoot in bridgeRoots)
            {
                RemoveSurfaceDescriptor(bridgeRoot.gameObject);
                if (!BoundsUtility.TryGetRendererBounds(bridgeRoot, out Bounds bounds, true, false))
                    continue;

                if (!hasBounds)
                {
                    combined = bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(bounds);
                }
            }

            if (!hasBounds)
                return;

            GameObject exclusionObject = new("Bridge Exclusion Region");
            exclusionObject.transform.SetParent(semanticRoot, true);
            exclusionObject.transform.SetPositionAndRotation(combined.center, Quaternion.identity);
            PlacementExclusionRegion exclusion = exclusionObject.AddComponent<PlacementExclusionRegion>();
            exclusion.ConfigureBox(
                Vector3.zero,
                combined.size + new Vector3(0.4f, 0.4f, 0.4f),
                PlacementTarget.Floor | PlacementTarget.Wall);
            exclusion.SetExemptAssetTags(Array.Empty<SemanticTag>());
            EditorUtility.SetDirty(exclusion);
        }

        private static GameObject FindSceneObject(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(gameObject => gameObject.name == name);

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        private static bool SaveLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && string.IsNullOrWhiteSpace(scene.path))
                    return false;
            }

            return EditorSceneManager.SaveOpenScenes();
        }
    }
}

