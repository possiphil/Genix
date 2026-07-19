using System.Collections.Generic;
using Genix.Editor.Utilities;
using Genix.Assets;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Drawers
{
    public static class AssetDropdown
    {
        public static StylePreset DrawStylePresetDropdownWithEditButton(string label, IReadOnlyList<StylePreset> presets, string[] options, StylePreset selectedPreset)
        {
            return DrawDropdownWithEditButton(label, presets, options, selectedPreset, "No Style Presets Found");
        }

        public static AssetPool DrawAssetPoolDropdownWithEditButton(string label, IReadOnlyList<AssetPool> assetPools, string[] options, AssetPool selectedPool)
        {
            return DrawDropdownWithEditButton(label, assetPools, options, selectedPool, "No Asset Pools Found");
        }

        private static T DrawDropdown<T>(string label, IReadOnlyList<T> assets, string[] options, T selectedAsset, string emptyLabel) where T : Object
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

        private static T DrawDropdownWithEditButton<T>(string label, IReadOnlyList<T> assets, string[] options, T selectedAsset, string emptyLabel) where T : Object
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                T newSelection = DrawDropdown(label, assets, options, selectedAsset, emptyLabel);
                EditorGui.DrawEditAssetButton(newSelection);

                return newSelection;
            }
        }

        private static void DrawEmptyDropdown(string label, string emptyLabel)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.Popup(label, 0, new[] { emptyLabel });
        }
    }
}
