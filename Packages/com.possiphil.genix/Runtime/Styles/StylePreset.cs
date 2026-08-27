using UnityEngine;

namespace Genix.Styles
{
    /// <summary>Persists a reusable set of generation-style settings as a Unity asset.</summary>
    [CreateAssetMenu(menuName = "Genix/Style Preset")]
    public sealed class StylePreset : ScriptableObject
    {
        [SerializeField] private StyleSettings settings;
        [SerializeField, HideInInspector] private StyleSettings defaults;

        /// <summary>Gets settings.</summary>
        public StyleSettings Settings => settings;

        /// <summary>Copies this preset into the supplied mutable style settings.</summary>
        public void Apply(StyleSettings styleSettings)
        {
            StyleSettingsUtility.ClearUnusedSettings(ref styleSettings);
            settings = styleSettings;
        }

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(StyleSettings styleSettings)
        {
            StyleSettingsUtility.ClearUnusedSettings(ref styleSettings);
            settings = styleSettings;
            defaults = styleSettings;
        }

        /// <summary>Restores the built-in style defaults.</summary>
        public void RestoreDefaults()
        {
            settings = defaults;
        }

        /// <summary>Sets current settings as defaults.</summary>
        public void SetCurrentSettingsAsDefaults()
        {
            defaults = settings;
        }
    }
}
