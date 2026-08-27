using System.Collections.Generic;
using Genix.Editor.Utilities;
using Genix.Assets;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Drawers
{
    /// <summary>Draws searchable selectors for Genix project assets in custom editor interfaces.</summary>
    public static class AssetDropdown
    {
        /// <summary>Draws a style-preset selector with a shortcut to inspect the selected preset.</summary>
        public static StylePreset DrawStylePresetDropdownWithEditButton(string label, IReadOnlyList<StylePreset> presets, string[] options, StylePreset selectedPreset)
        {
            return DrawStylePresetDropdownWithEditButton(new GUIContent(label), presets, options, selectedPreset);
        }

        /// <summary>Draws a style-preset selector with a shortcut to inspect the selected preset.</summary>
        public static StylePreset DrawStylePresetDropdownWithEditButton(GUIContent label, IReadOnlyList<StylePreset> presets, string[] options, StylePreset selectedPreset)
        {
            return DrawDropdownWithEditButton(label, presets, options, selectedPreset, "No Style Presets Found");
        }

        /// <summary>Draws an asset-pool selector with a shortcut to inspect the selected pool.</summary>
        public static AssetPool DrawAssetPoolDropdownWithEditButton(string label, IReadOnlyList<AssetPool> assetPools, string[] options, AssetPool selectedPool)
        {
            return DrawAssetPoolDropdownWithEditButton(new GUIContent(label), assetPools, options, selectedPool);
        }

        /// <summary>Draws an asset-pool selector with a shortcut to inspect the selected pool.</summary>
        public static AssetPool DrawAssetPoolDropdownWithEditButton(GUIContent label, IReadOnlyList<AssetPool> assetPools, string[] options, AssetPool selectedPool)
        {
            return DrawDropdownWithEditButton(label, assetPools, options, selectedPool, "No Asset Pools Found");
        }

        private static T DrawDropdown<T>(GUIContent label, IReadOnlyList<T> assets, string[] options, T selectedAsset, string emptyLabel) where T : Object
        {
            if (assets == null || assets.Count == 0)
            {
                DrawEmptyDropdown(label, emptyLabel);
                return null;
            }

            if (options == null || options.Length != assets.Count)
            {
                DrawEmptyDropdown(label, "Invalid Dropdown Options");
                return selectedAsset;
            }

            int selectedIndex = EditorAssets.GetAssetDropdownIndex(assets, selectedAsset);
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, options);

            return assets[newIndex];
        }

        private static T DrawDropdownWithEditButton<T>(GUIContent label, IReadOnlyList<T> assets, string[] options, T selectedAsset, string emptyLabel) where T : Object
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                T newSelection = DrawDropdown(label, assets, options, selectedAsset, emptyLabel);
                EditorGui.DrawEditAssetButton(newSelection);

                return newSelection;
            }
        }

        private static void DrawEmptyDropdown(GUIContent label, string emptyLabel)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Popup(label, 0, new[] { emptyLabel });
        }
    }
}
