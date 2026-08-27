using System;
using Genix.Assets;
using Genix.Placement;
using Genix.Semantics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Creates and opens the optional first-run room that accompanies Starter Content.</summary>
    public static class StarterRoomSceneBuilder
    {
        private const string ScenePath = "Assets/Genix/Starter Content/Scenes/Starter Room.unity";
        private const string DeskPrefabPath = "Assets/Genix/Starter Content/Prefabs/Desk.prefab";
        private const string DeskDefinitionPath = "Assets/Genix/Assets/Starter Content/Definitions/Desk.asset";
        private const string WallMaterialPath = "Assets/Genix/Starter Content/Materials/Wall.mat";
        private const string FloorMaterialPath = "Assets/Genix/Starter Content/Materials/Floor.mat";
        private const float VoxelSize = 1f;
        private const float SurfaceThickness = 0.2f;

        /// <summary>Creates the starter scene if it does not already exist.</summary>
        public static bool TryCreate(out string error)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                error = string.Empty;
                return true;
            }

            GameObject deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskPrefabPath);
            AssetDefinition deskDefinition = AssetDatabase.LoadAssetAtPath<AssetDefinition>(DeskDefinitionPath);
            Material wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
            Material floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            SemanticTag floorTag = FindTag("Support Type", "Floor");
            SemanticTag wallTag = FindTag("Support Type", "Wall");
            SemanticTag ceilingTag = FindTag("Support Type", "Ceiling");

            if (!deskPrefab || !deskDefinition || !wallMaterial || !floorMaterial ||
                !floorTag || !wallTag || !ceilingTag)
            {
                error = "Starter Content assets are incomplete. Run Set Up Starter Content again.";
                return false;
            }

            Scene previousScene = SceneManager.GetActiveScene();
            Scene scene = default;
            bool restoreUntitledScene = false;

            try
            {
                if (string.IsNullOrEmpty(previousScene.path) && previousScene.isDirty &&
                    !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    error = "Starter Room creation was cancelled.";
                    return false;
                }

                previousScene = SceneManager.GetActiveScene();
                restoreUntitledScene = string.IsNullOrEmpty(previousScene.path);
                scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    restoreUntitledScene ? NewSceneMode.Single : NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);

                SfsAuthoringRequest request = new()
                {
                    Name = "Starter Room",
                    LayoutType = SfsAuthoringLayoutType.BoundedLocation,
                    SizeMode = SfsAuthoringSizeMode.VoxelCounts,
                    Center = new Vector3(-0.5f, 1.5f, -0.5f),
                    VoxelCounts = new Vector3Int(8, 4, 8)
                };

                if (!SfsAuthoringPlanner.TryCreate(request, VoxelSize, out SfsAuthoringPlan plan, out error))
                    return false;

                SpaceFoundationSystem.SpaceFoundation foundation =
                    SfsAuthoringSceneBuilder.CreateFoundation(VoxelSize);
                GameObject layout = SfsAuthoringSceneBuilder.CreateLayout(plan, foundation, out error);
                if (!layout)
                    return false;

                Bounds roomBounds = plan.InteriorVolumes[0].ToWorldBounds(VoxelSize);
                BuildRoomGeometry(
                    layout.transform,
                    roomBounds,
                    floorMaterial,
                    wallMaterial,
                    floorTag,
                    wallTag,
                    ceilingTag);
                BuildDesk(scene, layout.transform, roomBounds, deskPrefab, deskDefinition);
                BuildLightingAndCamera(roomBounds);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    error = $"Unity could not save '{ScenePath}'.";
                    return false;
                }

                AssetDatabase.Refresh();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                if (restoreUntitledScene && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                else
                {
                    if (previousScene.IsValid() && previousScene.isLoaded)
                        SceneManager.SetActiveScene(previousScene);

                    if (scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>Opens the starter scene and schedules the regular SFS graph computation.</summary>
        public static bool TryOpenAndCompute(out string error)
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) && !TryCreate(out error))
                return false;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.path, ScenePath, StringComparison.Ordinal))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    error = "Opening the Starter Room was cancelled.";
                    return false;
                }

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            EditorApplication.delayCall += SfsAuthoringSceneBuilder.RunCompute;
            error = string.Empty;
            return true;
        }

        private static void BuildRoomGeometry(
            Transform layout,
            Bounds bounds,
            Material floorMaterial,
            Material wallMaterial,
            SemanticTag floorTag,
            SemanticTag wallTag,
            SemanticTag ceilingTag)
        {
            GameObject geometry = new("Geometry");
            geometry.transform.SetParent(layout, false);

            float halfThickness = SurfaceThickness * 0.5f;
            CreateSurface(
                geometry.transform,
                "Floor",
                new Vector3(bounds.center.x, bounds.min.y - halfThickness, bounds.center.z),
                new Vector3(bounds.size.x, SurfaceThickness, bounds.size.z),
                floorMaterial,
                floorTag);
            CreateSurface(
                geometry.transform,
                "Ceiling",
                new Vector3(bounds.center.x, bounds.max.y + halfThickness, bounds.center.z),
                new Vector3(bounds.size.x, SurfaceThickness, bounds.size.z),
                wallMaterial,
                ceilingTag);
            CreateSurface(
                geometry.transform,
                "Wall Left",
                new Vector3(bounds.min.x - halfThickness, bounds.center.y, bounds.center.z),
                new Vector3(SurfaceThickness, bounds.size.y, bounds.size.z),
                wallMaterial,
                wallTag);
            CreateSurface(
                geometry.transform,
                "Wall Right",
                new Vector3(bounds.max.x + halfThickness, bounds.center.y, bounds.center.z),
                new Vector3(SurfaceThickness, bounds.size.y, bounds.size.z),
                wallMaterial,
                wallTag);
            CreateSurface(
                geometry.transform,
                "Wall Back",
                new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - halfThickness),
                new Vector3(bounds.size.x, bounds.size.y, SurfaceThickness),
                wallMaterial,
                wallTag);
            CreateSurface(
                geometry.transform,
                "Wall Front",
                new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + halfThickness),
                new Vector3(bounds.size.x, bounds.size.y, SurfaceThickness),
                wallMaterial,
                wallTag);
        }

        private static void BuildDesk(
            Scene scene,
            Transform layout,
            Bounds roomBounds,
            GameObject prefab,
            AssetDefinition definition)
        {
            GameObject desk = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (!desk)
                throw new InvalidOperationException("Unity could not instantiate the Starter Content desk.");

            desk.name = "Desk";
            desk.transform.SetParent(layout, true);
            desk.transform.position = new Vector3(roomBounds.center.x, roomBounds.min.y, roomBounds.center.z);
            desk.transform.rotation = Quaternion.identity;

            PlacementSurfaceDescriptor desktop = desk.GetComponentInChildren<PlacementSurfaceDescriptor>(true);
            AssetRelationAnchor anchor = desk.AddComponent<AssetRelationAnchor>();
            anchor.SetRepresentedAsset(definition);
            anchor.SetSupportSurface(desktop);
        }

        private static void BuildLightingAndCamera(Bounds roomBounds)
        {
            GameObject lightObject = new("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.transform.position = roomBounds.center + new Vector3(6.5f, 4.5f, 6.5f);
            cameraObject.transform.rotation = Quaternion.LookRotation(
                roomBounds.center - cameraObject.transform.position,
                Vector3.up);
        }

        private static GameObject CreateSurface(
            Transform parent,
            string name,
            Vector3 worldCenter,
            Vector3 size,
            Material material,
            SemanticTag tag)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = name;
            surface.transform.SetParent(parent, true);
            surface.transform.position = worldCenter;
            surface.transform.rotation = Quaternion.identity;
            surface.transform.localScale = size;

            if (surface.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            surface.AddComponent<PlacementSurfaceDescriptor>().SetSurfaceTags(new[] { tag });
            return surface;
        }

        private static SemanticTag FindTag(string categoryName, string tagName)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(SemanticTag)}", new[] { "Assets/Genix" }))
            {
                SemanticTag tag = AssetDatabase.LoadAssetAtPath<SemanticTag>(AssetDatabase.GUIDToAssetPath(guid));
                if (tag && tag.Category &&
                    string.Equals(tag.Category.DisplayName, categoryName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(tag.DisplayName, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    return tag;
                }
            }

            return null;
        }
    }
}
