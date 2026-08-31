using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.UI;
using Genix.Extensions;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixContentWindow
    {
        private void DrawAssetPoolsTab(AssetCatalog catalog)
        {
            DrawPoolFilters();

            EditorGUILayout.Space(6f);

            DrawPoolList(catalog);
        }

        private void DrawPoolFilters()
        {
            DrawSectionHeader("Pool Filters", () =>
            {
                if ((!string.IsNullOrWhiteSpace(_poolSearch) || _filterAssetPoolsByMode) &&
                    GUILayout.Button("Reset", GUILayout.Width(60f)))
                    ClearPoolFilters();
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _poolSearch = EditorGUILayout.TextField(
                    new GUIContent("Search", "Filter asset pools by display name."),
                    _poolSearch);
                DrawPoolModeFilterPopup();
            }
        }

        private void ClearPoolFilters()
        {
            _poolSearch = string.Empty;
            _filterAssetPoolsByMode = false;
            _poolModeFilter = AssetPoolMode.Static;
        }

        private void DrawPoolModeFilterPopup()
        {
            AssetPoolMode[] modes =
            {
                AssetPoolMode.Static,
                AssetPoolMode.Dynamic
            };

            string[] labels =
            {
                "Any",
                DesignerTerminology.AssetPoolMode(AssetPoolMode.Static),
                DesignerTerminology.AssetPoolMode(AssetPoolMode.Dynamic)
            };

            int selectedIndex = _filterAssetPoolsByMode
                ? Array.IndexOf(modes, _poolModeFilter) + 1
                : 0;

            selectedIndex = EditorGUILayout.Popup(
                new GUIContent("Pool Type", "Manual pools contain a chosen list. Rule-based pools include matching catalog assets."),
                selectedIndex,
                labels);
            _filterAssetPoolsByMode = selectedIndex > 0;

            if (_filterAssetPoolsByMode)
                _poolModeFilter = modes[selectedIndex - 1];
        }

        private void DrawPoolList(AssetCatalog catalog)
        {
            List<AssetPool> assetPools = GetFilteredAssetPools(catalog);

            DrawSectionHeader($"Asset Pools ({assetPools.Count})", () =>
            {
                if (GUILayout.Button("New Pool", GUILayout.Width(72f)))
                    CreatePool(AssetPoolMode.Static);

                using (new EditorGUI.DisabledScope(!_selectedPool))
                {
                    if (GUILayout.Button("Delete…", GUILayout.Width(64f)))
                        DeleteSelectedPool();
                }
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(ListHeight)))
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

                if (assetPools.Count == 0)
                    DesignerTerminology.DrawEmptyState("No pools match the current filters.");
                else
                {
                    foreach (AssetPool pool in assetPools)
                        DrawPoolListItem(catalog, pool);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPoolListItem(
            AssetCatalog catalog,
            AssetPool pool)
        {
            bool selected = GetSelectedObject() == pool;
            GUIStyle style = selected ? EditorStyles.helpBox : GUIStyle.none;

            using (new EditorGUILayout.VerticalScope(style))
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 40f);

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                    SelectObject(pool);

                Rect titleRect = new(rowRect.x, rowRect.y, rowRect.width, 18f);
                Rect infoRect = new(rowRect.x, rowRect.y + 18f, rowRect.width, 18f);

                int assetCount = pool.ResolveAssets(catalog).Count;

                EditorGUI.LabelField(titleRect, pool.name, EditorStyles.boldLabel);
                EditorGUI.LabelField(
                    infoRect,
                    $"{DesignerTerminology.AssetPoolMode(pool.Mode)} · {assetCount} matching asset(s)");
            }

            EditorGUILayout.Space(2f);
        }

    }
}
