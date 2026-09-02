using Genix.Authoring;
using SpaceFoundationSystem;
using UnityEditor;
using UnityEngine;

namespace Genix.SpaceFoundation.Editor
{
    internal static class SfsAuthoringMenu
    {
        private const string Root = "GameObject/Genix/Space Foundation/";

        [MenuItem(Root + "Space Foundation", false, 10)]
        private static void CreateFoundation(MenuCommand command)
        {
            Transform parent = GetContextParent(command);
            SfsAuthoringSceneBuilder.CreateFoundation(1f, parent);
        }

        [MenuItem(Root + "Anchor", false, 11)]
        private static void CreateAnchor(MenuCommand command)
        {
            SpaceFoundationSystem.SpaceFoundation foundation = GetOrCreateFoundation();
            if (!foundation)
                return;
            Transform parent = GetContextParent(command);
            float voxelSize = foundation ? foundation.voxelSize : 1f;
            float range = Mathf.Max(40f, voxelSize * 8f);
            SfsAuthoringSceneBuilder.CreateAnchor(GetCreationPosition(), foundation, range, parent);
        }

        [MenuItem(Root + "Box Delimiter", false, 12)]
        private static void CreateBoxDelimiter(MenuCommand command)
        {
            SpaceFoundationSystem.SpaceFoundation foundation = GetOrCreateFoundation();
            if (!foundation)
                return;
            Transform parent = GetContextParent(command);
            SfsAuthoringSceneBuilder.CreateGridAlignedBoxDelimiter(
                GetCreationPosition(),
                new Vector3Int(4, 4, 1),
                foundation,
                parent);
        }

        [MenuItem(Root + "Convert Selected Colliders", false, 30)]
        private static void ConvertSelectedColliders()
        {
            SpaceFoundationSystem.SpaceFoundation foundation = GetOrCreateFoundation();
            if (!foundation)
                return;
            int converted = SfsAuthoringSceneBuilder.ConvertSelectedColliders(foundation, out string error);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning(error);
            else
                Debug.Log($"Converted {converted} collider object(s) to SFS delimiters.");
        }

        [MenuItem(Root + "Convert Selected Colliders", true)]
        private static bool ValidateConvertSelectedColliders()
        {
            foreach (GameObject gameObject in Selection.gameObjects)
            {
                if (gameObject.GetComponent<Collider>())
                    return true;
            }

            return false;
        }

        [MenuItem(Root + "Add Free Space Display", false, 31)]
        private static void AddFreeSpaceDisplay()
        {
            if (!SfsAuthoringSceneBuilder.TryAddFreeSpaceDisplay(Selection.activeGameObject, out string error))
                Debug.LogWarning(error);
        }

        [MenuItem(Root + "Add Free Space Display", true)]
        private static bool ValidateAddFreeSpaceDisplay()
        {
            for (Transform current = Selection.activeTransform; current; current = current.parent)
            {
                if (current.GetComponent<SfsAuthoringLayoutDisplay>() ||
                    current.Find("Delimiters") && current.Find("Anchors"))
                {
                    return true;
                }
            }

            return false;
        }

        [MenuItem(Root + "Open Space Setup", false, 50)]
        private static void OpenWindow() => SfsAuthoringWindow.Open();

        private static SpaceFoundationSystem.SpaceFoundation GetOrCreateFoundation()
        {
            if (Selection.activeGameObject &&
                Selection.activeGameObject.TryGetComponent(out SpaceFoundationSystem.SpaceFoundation selected))
                return selected;

            SpaceFoundationSystem.SpaceFoundation[] foundations = SfsAuthoringSceneBuilder.FindFoundations();
            if (foundations.Length == 1)
                return foundations[0];
            if (foundations.Length == 0)
                return SfsAuthoringSceneBuilder.CreateFoundation(1f);

            Debug.LogError("Multiple Space Foundations exist. Open Genix Space Setup and select the intended Foundation explicitly.");
            return null;
        }

        private static Transform GetContextParent(MenuCommand command)
        {
            return command.context is GameObject gameObject ? gameObject.transform : null;
        }

        private static Vector3 GetCreationPosition()
        {
            return SceneView.lastActiveSceneView
                ? SceneView.lastActiveSceneView.pivot
                : Vector3.zero;
        }
    }
}
