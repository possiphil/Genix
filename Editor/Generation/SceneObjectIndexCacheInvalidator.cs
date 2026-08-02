using Genix.Placement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Generation
{
    /// <summary>Invalidates placement scene indices when editor hierarchy or scene state changes.</summary>
    [InitializeOnLoad]
    internal static class SceneObjectIndexCacheInvalidator
    {
        static SceneObjectIndexCacheInvalidator()
        {
            EditorApplication.hierarchyChanged += Clear;
            Undo.undoRedoPerformed += Clear;
            EditorSceneManager.sceneOpened += (_, _) => Clear();
            EditorSceneManager.sceneClosed += _ => Clear();
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => Clear();
        }

        private static void Clear()
        {
            PlacementSolver.ClearSceneObjectCache();
        }
    }
}
