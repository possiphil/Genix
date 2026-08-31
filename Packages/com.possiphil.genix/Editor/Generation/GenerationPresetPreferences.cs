using Genix.Core;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Generation
{
    /// <summary>Remembers the last generation preset selected by this editor user for the current project.</summary>
    internal static class GenerationPresetPreferences
    {
        private const string DefaultPresetKeyPrefix = "Genix.GenerationPreset.DefaultGuid";

        private static string DefaultPresetKey =>
            $"{DefaultPresetKeyPrefix}.{Hash128.Compute(Application.dataPath)}";

        public static GenerationPreset GetDefault()
        {
            string guid = EditorPrefs.GetString(DefaultPresetKey, string.Empty);

            if (string.IsNullOrWhiteSpace(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            GenerationPreset preset = AssetDatabase.LoadAssetAtPath<GenerationPreset>(path);

            if (!preset)
                EditorPrefs.DeleteKey(DefaultPresetKey);

            return preset;
        }

        public static void SetDefault(GenerationPreset preset)
        {
            string path = preset ? AssetDatabase.GetAssetPath(preset) : string.Empty;
            string guid = AssetDatabase.AssetPathToGUID(path);

            if (string.IsNullOrWhiteSpace(guid))
            {
                ClearDefault();
                return;
            }

            EditorPrefs.SetString(DefaultPresetKey, guid);
        }

        public static void ClearDefault()
        {
            EditorPrefs.DeleteKey(DefaultPresetKey);
        }
    }
}
