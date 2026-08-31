using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Drawers;
using Genix.Editor.Layouts;
using Genix.Editor.Utilities;
using Genix.Assets;
using Genix.Editor.Assets;
using Genix.Extensions;
using Genix.Layouts;
using Genix.Orientation;
using Genix.Semantics;
using Genix.Editor.TargetAreas;
using Genix.Editor.UI;
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
            AssetPools,
            Tags,
            Locations,
            SceneSetup,
            Layouts
        }

        private static readonly GUIContent[] ContentTabOptions =
        {
            new("Assets", "Author prefab placement definitions."),
            new("Pools", "Choose assets manually or include them with reusable rules."),
            new("Tags", "Define semantic categories and tags."),
            new("Target Areas", "Inspect and tag available generation areas."),
            new("Scene Setup", "Configure surfaces, anchors, paths, and exclusion regions in the current scene."),
            new("Layouts", "Review, preview, and apply saved results.")
        };

        private enum AssetSortMode
        {
            AlphabeticalAscending,
            SizeDescending,
            SizeAscending,
            PlacementType,
            TagCountAscending
        }

        private enum LayoutSortMode
        {
            NewestFirst,
            NameAscending,
            TargetArea,
            ObjectCountDescending
        }

        private enum LayoutScopeFilter
        {
            CurrentScene,
            CurrentTargetArea,
            AllScenes
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
        private const int LayoutPageSize = 100;

        private ContentTab _tab = ContentTab.Assets;

        private AssetDefinition _selectedAsset;
        private TagCategory _selectedTagCategory;
        private SemanticTag _selectedSemanticTag;
        private AssetPool _selectedPool;
        private SavedLayout _selectedLayout;
        private Object _selectedSceneSetupObject;

        private Object _selectedObjectEditorTarget;
        private UnityEditor.Editor _selectedObjectEditor;
        private UnityEditor.Editor _selectedCategoryEditor;
        private UnityEditor.Editor _selectedSemanticTagEditor;

        private Vector2 _listScroll;
        private Vector2 _windowScroll;
        private Vector2 _categoryScroll;
        private Vector2 _tagScroll;
        private Vector2 _layoutListScroll;

        private string _assetSearch = string.Empty;
        private AssetSortMode _assetSortMode = AssetSortMode.AlphabeticalAscending;
        private bool _filterByPlacementType;
        private PlacementType _placementTypeFilter = PlacementType.Floor;
        private bool _filterByOrientationMode;
        private OrientationMode _orientationModeFilter = OrientationMode.None;
        private readonly Dictionary<TagCategory, List<SemanticTag>> _assetCategoryFilters = new();

        private string _categorySearch = string.Empty;

        private string _poolSearch = string.Empty;
        private bool _filterAssetPoolsByMode;
        private AssetPoolMode _poolModeFilter = AssetPoolMode.Static;

        private string _layoutSearch = string.Empty;
        private LayoutSortMode _layoutSortMode = LayoutSortMode.NewestFirst;
        private LayoutScopeFilter _layoutScopeFilter = LayoutScopeFilter.CurrentScene;
        private int _layoutPage;

        private AssetPool _targetStaticPool;
        private readonly LocationPanelHost _locationPanel = new();
        private readonly TargetAreaSelectorHost _layoutTargetAreaSelector = new();

        /// <summary>Opens or focuses the corresponding Genix editor window.</summary>
        [MenuItem("Tools/Genix/Content", false, 10)]
        public static void Open()
        {
            GenixWindowDocking.Open<GenixContentWindow>("Genix Content");
        }

        private void OnEnable()
        {
            AssetCatalogService.Refresh();
            _locationPanel.Refresh();
            _layoutTargetAreaSelector.Refresh();
            EditorApplication.hierarchyChanged += MarkSceneSetupDirty;
            Undo.undoRedoPerformed += MarkSceneSetupDirty;
            MarkSceneSetupDirty();
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= MarkSceneSetupDirty;
            Undo.undoRedoPerformed -= MarkSceneSetupDirty;
            DestroySelectedObjectEditor();
        }

        private void OnFocus()
        {
            AssetCatalogService.Refresh();
            _locationPanel.Refresh();
            _layoutTargetAreaSelector.Refresh();
            MarkSceneSetupDirty();
            Repaint();
        }

        private void OnProjectChange()
        {
            AssetCatalogService.Refresh();
            LayoutWorkflow.InvalidateLayoutCache();
            _locationPanel.Refresh();
            _layoutTargetAreaSelector.Refresh();
            Repaint();
        }

        private void OnGUI()
        {
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            DrawToolbar();

            _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);

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

                case ContentTab.Layouts:
                    DrawLayoutsTab();
                    break;

                case ContentTab.SceneSetup:
                    DrawSceneSetupTab(catalog);
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

                if (position.width < 560f)
                {
                    _tab = (ContentTab)EditorGUILayout.Popup(
                        (int)_tab,
                        ContentTabOptions,
                        EditorStyles.toolbarPopup);
                }
                else
                {
                    _tab = (ContentTab)GUILayout.Toolbar(
                        (int)_tab,
                        ContentTabOptions,
                        EditorStyles.toolbarButton,
                        GUILayout.ExpandWidth(true));
                }

                if (_tab != previousTab)
                {
                    GUI.FocusControl(null);
                    DestroySelectedObjectEditor();
                    _windowScroll = Vector2.zero;
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.FlexibleSpace();

                if (DesignerUiPreferences.IsAdvanced && GUILayout.Button(
                        new GUIContent("Actions", "Open content-library maintenance actions."),
                        EditorStyles.toolbarDropDown,
                        GUILayout.Width(72f)))
                    ShowContentActionsMenu();

                DesignerUiPreferences.DrawToolbarSelector();
            }
        }

        private void ShowContentActionsMenu()
        {
            GenericMenu menu = new();

            switch (_tab)
            {
                case ContentTab.Assets:
                    menu.AddItem(new GUIContent("Delete All Asset Definitions…"), false, ClearAssets);
                    break;
                case ContentTab.AssetPools:
                    menu.AddItem(new GUIContent("Delete All Asset Pools…"), false, ClearAssetPools);
                    break;
                case ContentTab.Tags:
                    if (_selectedTagCategory)
                    {
                        TagCategory category = _selectedTagCategory;
                        menu.AddItem(
                            new GUIContent($"Delete Tags in {category.DisplayName}…"),
                            false,
                            () => ClearTags(category));
                    }

                    menu.AddItem(new GUIContent("Delete All Tags…"), false, () => ClearTags(null));
                    menu.AddItem(new GUIContent("Delete All Categories and Tags…"), false, ClearCategories);
                    break;
            }

            if (menu.GetItemCount() > 0)
                menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("Delete All Genix Content…"), false, ClearCatalog);
            menu.ShowAsContext();
        }

        private void DrawSectionHeader(string title, Action drawButtons)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, EditorStyles.boldLabel, GUILayout.ExpandWidth(false));

                GUILayout.FlexibleSpace();

                if (position.width >= 520f)
                    drawButtons?.Invoke();
            }

            if (position.width < 520f && drawButtons != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    drawButtons.Invoke();
                }
            }
        }
    }
}
