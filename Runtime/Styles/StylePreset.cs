using UnityEngine;

namespace Genix.Styles
{
    [CreateAssetMenu(menuName = "Genix/Style Preset")]
    public sealed class StylePreset : ScriptableObject
    {
        [SerializeField] private StyleSettings settings;
        [SerializeField, HideInInspector] private StyleSettings defaults;

        public StyleSettings Settings => settings;

        public void Apply(StyleSettings styleSettings)
        {
            StyleSettingsUtility.ClearUnusedSettings(ref styleSettings);
            settings = styleSettings;
        }

        public void Initialize(StyleSettings styleSettings)
        {
            StyleSettingsUtility.ClearUnusedSettings(ref styleSettings);
            settings = styleSettings;
            defaults = styleSettings;
        }

        public void RestoreDefaults()
        {
            settings = defaults;
        }

        public void SetCurrentSettingsAsDefaults()
        {
            defaults = settings;
        }
    }
}