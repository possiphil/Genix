using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
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
            DrawSectionHeader("Asset Pool Filters", () =>
            {
                if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                    ClearPoolFilters();
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _poolSearch = EditorGUILayout.TextField("Search", _poolSearch);
                _filterAssetPoolsByMode = EditorGUILayout.Toggle("Filter By Mode", _filterAssetPoolsByMode);

                if (_filterAssetPoolsByMode)
                    _poolModeFilter = DrawPoolModeFilterPopup(_poolModeFilter);

                string[] assetStateLabels = { "All", "Has Assets", "Empty" };
                int selectedAssetState = EditorGUILayout.Popup("Asset State", (int)_poolAssetStateFilter, assetStateLabels);
                _poolAssetStateFilter = (PoolAssetStateFilter)selectedAssetState;
            }
        }

        private void ClearPoolFilters()
        {
            _poolSearch = string.Empty;
            _filterAssetPoolsByMode = false;
            _poolModeFilter = AssetPoolMode.Static;
            _poolAssetStateFilter = PoolAssetStateFilter.All;
        }

        private static AssetPoolMode DrawPoolModeFilterPopup(AssetPoolMode currentMode)
        {
            AssetPoolMode[] modes =
            {
                AssetPoolMode.Static,
                AssetPoolMode.Dynamic
            };

            string[] labels =
            {
                AssetPoolMode.Static.ToDisplayName(),
                AssetPoolMode.Dynamic.ToDisplayName()
            };

            int selectedIndex = Array.IndexOf(modes, currentMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            selectedIndex = EditorGUILayout.Popup("Mode", selectedIndex, labels);
            return modes[selectedIndex];
        }

        private void DrawPoolList(AssetCatalog catalog)
        {
            List<AssetPool> assetPools = GetFilteredAssetPools(catalog);

            DrawSectionHeader($"Asset Pools ({assetPools.Count})", () =>
            {
                DrawPoolSortDropdown();

                if (GUILayout.Button("Create", GUILayout.Width(60f)))
                    CreatePool(AssetPoolMode.Static);

                using (new EditorGUI.DisabledScope(!_selectedPool))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                        DeleteSelectedPool();
                }

                using (new EditorGUI.DisabledScope(assetPools.Count == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                        ClearAssetPools();
                }
            });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(ListHeight)))
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

                if (assetPools.Count == 0)
                {
                    string message = catalog.AssetPools.Any(pool => pool)
                        ? "No asset pools match the current filters."
                        : "No asset pools created yet.";

                    EditorGUILayout.HelpBox(message, MessageType.Info);
                }
                else
                {
                    foreach (AssetPool pool in assetPools)
                        DrawPoolListItem(catalog, pool);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPoolSortDropdown()
        {
            PoolSortMode[] modes =
            {
                PoolSortMode.AlphabeticalAscending,
                PoolSortMode.AlphabeticalDescending,
                PoolSortMode.AssetCountDescending,
                PoolSortMode.AssetCountAscending,
                PoolSortMode.Mode
            };

            string[] labels =
            {
                "Alphabetical Ascending",
                "Alphabetical Descending",
                "Asset Count Descending",
                "Asset Count Ascending",
                "Mode"
            };

            _poolSortMode = DrawSortDropdown(_poolSortMode, modes, labels);
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
                EditorGUI.LabelField(infoRect, $"Mode: {pool.Mode.ToDisplayName()}    Assets: {assetCount}");
            }

            EditorGUILayout.Space(2f);
        }

    }
}

