using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Drawers;
using Genix.Editor.Utilities;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Extensions;
using Genix.Orientation;
using Genix.Semantics;
using Genix.Editor.TargetAreas;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixContentWindow : EditorWindow
    {
        private enum ContentTab
        {
            Assets,
            Tags,
            Locations,
            AssetPools
        }

        private enum AssetSortMode
        {
            AlphabeticalAscending,
            AlphabeticalDescending,
            SizeDescending,
            SizeAscending,
            PlacementType,
            TagCountDescending,
            TagCountAscending
        }

        private enum CategorySortMode
        {
            AlphabeticalAscending,
            AlphabeticalDescending,
            TagCountDescending,
            TagCountAscending,
            Mode
        }

        private enum TagSortMode
        {
            AlphabeticalAscending,
            AlphabeticalDescending,
            CategoryAscending,
            CategoryDescending
        }

        private enum PoolSortMode
        {
            AlphabeticalAscending,
            AlphabeticalDescending,
            AssetCountDescending,
            AssetCountAscending,
            Mode
        }

        private enum PoolAssetStateFilter
        {
            All,
            HasAssets,
            Empty
        }

        private int _prefabCreationSlotPickerControlId = -1;

        private string _staticPoolMessage;
        private MessageType _staticPoolMessageType = MessageType.Info;
        private double _staticPoolMessageUntil;

        private readonly List<GameObject> _prefabsToCreate = new();

        private string _assetCreationMessage;
        private MessageType _assetCreationMessageType = MessageType.Info;
        private double _assetCreationMessageUntil;

        private const float ListHeight = 240f;

        private ContentTab _tab = ContentTab.Assets;

        private AssetDefinition _selectedAsset;
        private TagCategory _selectedTagCategory;
        private SemanticTag _selectedSemanticTag;
        private AssetPool _selectedPool;

        private Object _selectedObjectEditorTarget;
        private UnityEditor.Editor _selectedObjectEditor;
        private UnityEditor.Editor _selectedCategoryEditor;
        private UnityEditor.Editor _selectedSemanticTagEditor;

        private Vector2 _listScroll;
        private Vector2 _windowScroll;
        private Vector2 _categoryScroll;
        private Vector2 _tagScroll;

        private string _assetSearch = string.Empty;
        private AssetSortMode _assetSortMode = AssetSortMode.AlphabeticalAscending;
        private bool _filterByPlacementType;
        private PlacementType _placementTypeFilter = PlacementType.Floor;
        private bool _filterByOrientationMode;
        private OrientationMode _orientationModeFilter = OrientationMode.None;
        private readonly Dictionary<TagCategory, List<SemanticTag>> _assetCategoryFilters = new();

        private string _categorySearch = string.Empty;
        private CategorySortMode _categorySortMode = CategorySortMode.AlphabeticalAscending;
        private bool _filterCategoriesByMode;
        private bool _categoryModeFilterAllowsMultiple = true;

        private TagSortMode _tagSortMode = TagSortMode.CategoryAscending;

        private string _poolSearch = string.Empty;
        private PoolSortMode _poolSortMode = PoolSortMode.AlphabeticalAscending;
        private bool _filterAssetPoolsByMode;
        private AssetPoolMode _poolModeFilter = AssetPoolMode.Static;
        private PoolAssetStateFilter _poolAssetStateFilter = PoolAssetStateFilter.All;

        private AssetPool _targetStaticPool;
        private readonly LocationPanelHost _locationPanel = new();

        [MenuItem("Tools/Genix/Assets", false, 20)]
        public static void Open()
        {
            GenixContentWindow window = GetWindow<GenixContentWindow>("Genix Assets");
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            AssetCatalogService.Refresh();
            _locationPanel.Refresh();
        }

        private void OnDisable()
        {
            DestroySelectedObjectEditor();
        }

        private void OnFocus()
        {
            AssetCatalogService.Refresh();
            _locationPanel.Refresh();
            Repaint();
        }

        private void OnProjectChange()
        {
            AssetCatalogService.Refresh();
            _locationPanel.Refresh();
            Repaint();
        }

        private void OnGUI()
        {
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);

            DrawToolbar();

            EditorGUILayout.Space(6f);

            switch (_tab)
            {
                case ContentTab.Assets:
                    DrawAssetsTab(catalog);
                    break;

                case ContentTab.Tags:
                    DrawTagsTab(catalog);
                    break;

                case ContentTab.Locations:
                    _locationPanel.Draw(catalog);
                    break;

                case ContentTab.AssetPools:
                    DrawAssetPoolsTab(catalog);
                    break;
            }

            EditorGUILayout.Space(8f);

            DrawSelectedObjectDetails();

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                ContentTab previousTab = _tab;

                _tab = (ContentTab)DrawToolbarTabsWithRightBorder(
                    (int)_tab,
                    new[] { "Assets", "Tags", "Locations", "Asset Pools" },
                    320f);

                if (_tab != previousTab)
                {
                    GUI.FocusControl(null);
                    DestroySelectedObjectEditor();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear Catalog", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                    ClearCatalog();
            }
        }

        private static int DrawToolbarTabsWithRightBorder(
            int selectedIndex,
            string[] labels,
            float width)
        {
            Rect toolbarRect = GUILayoutUtility.GetRect(
                width,
                width,
                EditorGUIUtility.singleLineHeight,
                EditorGUIUtility.singleLineHeight,
                EditorStyles.toolbarButton);

            selectedIndex = GUI.Toolbar(
                toolbarRect,
                selectedIndex,
                labels,
                EditorStyles.toolbarButton);

            DrawToolbarRightBorder(toolbarRect);

            return selectedIndex;
        }

        private static void DrawToolbarRightBorder(Rect toolbarRect)
        {
            Rect lineRect = new(
                toolbarRect.xMax - 1f,
                toolbarRect.y,
                1f,
                toolbarRect.height + 3f);

            EditorGUI.DrawRect(lineRect, new Color(0f, 0f, 0f, 0.55f));
        }

        private static void DrawSectionHeader(string title, Action drawButtons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, EditorStyles.boldLabel, GUILayout.ExpandWidth(false));

                GUILayout.Space(8f);
                GUILayout.FlexibleSpace();

                drawButtons?.Invoke();
            }
        }
    }
}
