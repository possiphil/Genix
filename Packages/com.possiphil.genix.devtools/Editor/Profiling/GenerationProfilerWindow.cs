using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Core;
using Genix.Editor.Drawers;
using Genix.Editor.Generation;
using Genix.Editor.Infrastructure;
using Genix.Editor.TargetAreas;
using Genix.Editor.UI;
using Genix.Editor.Utilities;
using Genix.Profiling;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Profiling
{
    /// <summary>Provides the generation profiler editor window.</summary>
    public sealed partial class GenerationProfilerWindow : EditorWindow
    {
        private const string SelectedPresetKey = "Genix.Profiler.SelectedGenerationPreset";
        private const string RunTypeKey = "Genix.Profiler.RunType";
        private const float SavedListHeight = 180f;

        private static readonly GUIContent[] RunTypeOptions =
        {
            new("Preview", "Profile planning and prepare a preview without placing scene objects."),
            new("Generate", "Profile planning and scene application. Generated objects are added to the selected target area.")
        };

        private readonly TargetAreaSelectorHost _targetAreaSelector = new();

        private GenerationPreset _selectedGenerationPreset;
        private GenerationPreset[] _generationPresets = Array.Empty<GenerationPreset>();
        private string[] _generationPresetOptions = Array.Empty<string>();
        private GenerationProfilerRunType _runType;
        private string _runError = string.Empty;
        private GenerationProfileReport _selectedReport;
        private Vector2 _currentScroll;
        private Vector2 _savedListScroll;
        private Vector2 _savedDetailsScroll;

        /// <summary>Opens or focuses the corresponding Genix editor window.</summary>
        [MenuItem("Tools/Genix Developer/Profiler", false, 10)]
        public static void Open()
        {
            GenerationProfilerWindow window = GenixWindowDocking.Open<GenerationProfilerWindow>("Genix Profiler");
            window.minSize = new Vector2(660f, 520f);
        }

        private void OnEnable()
        {
            GenerationProfilerService.Changed += Repaint;
            EditorApplication.projectChanged += HandleProjectChanged;
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;

            _targetAreaSelector.Refresh();
            RefreshGenerationPresets();
            SetGenerationPreset(LoadRememberedGenerationPreset());
            _runType = Enum.IsDefined(
                typeof(GenerationProfilerRunType),
                EditorPrefs.GetInt(RunTypeKey, (int)GenerationProfilerRunType.Preview))
                ? (GenerationProfilerRunType)EditorPrefs.GetInt(RunTypeKey)
                : GenerationProfilerRunType.Preview;
            GenerationProfileCatalogService.Refresh();
        }

        private void OnDisable()
        {
            GenerationProfilerService.Changed -= Repaint;
            EditorApplication.projectChanged -= HandleProjectChanged;
            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);
            DrawProfileRun();

            if (GenerationProfilerService.LastProfile != null)
            {
                EditorGUILayout.Space(8f);
                DrawProfileResult();
            }

            EditorGUILayout.Space(8f);
            DrawSavedProfiles();
        }

        private void DrawProfileRun()
        {
            EditorGUILayout.LabelField("Profile Run", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.28f, 130f, 190f);
                EditorGUI.BeginChangeCheck();

                try
                {
                    _targetAreaSelector.Draw(new GUIContent(
                        "Target Area",
                        "Location in the currently open scene used for this profile run."));
                    DrawGenerationPreset();
                    DrawRunType();
                    _targetAreaSelector.DrawStatus();
                }
                finally
                {
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                }

                if (EditorGUI.EndChangeCheck())
                    _runError = string.Empty;

                if (!string.IsNullOrWhiteSpace(_runError))
                    EditorGUILayout.HelpBox(_runError, MessageType.Error);

                EditorGUILayout.Space(4f);
                GUIStyle primaryButton = new(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 28f
                };
                using (new EditorGUI.DisabledScope(!_selectedGenerationPreset))
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                "Profile Run",
                                "Run the selected preset once with detailed instrumentation. Use Benchmarks for controlled performance comparisons."),
                            primaryButton))
                        RunProfile();
                }
            }
        }

        private void DrawGenerationPreset()
        {
            GenerationPreset preset = AssetDropdown.DrawGenerationPresetDropdownWithEditButton(
                new GUIContent(
                    "Generation Preset",
                    "Complete generation configuration used by this profile run."),
                _generationPresets,
                _generationPresetOptions,
                _selectedGenerationPreset);
            if (preset != _selectedGenerationPreset)
                SetGenerationPreset(preset);
        }

        private void DrawRunType()
        {
            Rect row = EditorGUILayout.GetControlRect();
            Rect field = EditorGUI.PrefixLabel(
                row,
                new GUIContent(
                    "Run Type",
                    "Preview measures planning without placing objects. Generate also measures scene application."));
            int selected = GUI.Toolbar(field, (int)_runType, RunTypeOptions);
            GenerationProfilerRunType runType = (GenerationProfilerRunType)Mathf.Clamp(selected, 0, RunTypeOptions.Length - 1);
            if (runType == _runType)
                return;

            _runType = runType;
            EditorPrefs.SetInt(RunTypeKey, (int)_runType);
        }

        private void RunProfile()
        {
            _runError = string.Empty;
            if (!GenerationProfilerRunService.TryRun(
                    _targetAreaSelector.CreateAreaSource(),
                    _selectedGenerationPreset,
                    _runType,
                    out _runError))
            {
                return;
            }

            _currentScroll = Vector2.zero;
            Repaint();
        }

        private void DrawProfileResult()
        {
            GenerationProfile profile = GenerationProfilerService.LastProfile;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Profile Result", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Save", GUILayout.Width(58f)))
                {
                    _selectedReport = GenerationProfileReportSaver.Save(profile);
                }

                if (GUILayout.Button(
                        new GUIContent("Export CSV", "Export the detailed profile measurements as a CSV file."),
                        GUILayout.Width(82f)))
                    ExportCsv(profile);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _currentScroll = EditorGUILayout.BeginScrollView(_currentScroll, GUILayout.MaxHeight(280f));
                float previousLabelWidth = BeginProfileDetailsLabelLayout();
                try
                {
                    DrawRunSummary(profile);
                    DrawPhaseSummary(profile);
                    DrawTargetProfiles(profile);
                }
                finally
                {
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private float BeginProfileDetailsLabelLayout()
        {
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.28f, 175f, 210f);
            return previousLabelWidth;
        }

        private void DrawSavedProfiles()
        {
            GenerationProfileCatalog catalog = GenerationProfileCatalogService.GetOrCreate();
            List<GenerationProfileReport> reports = catalog.Reports
                .Where(report => report)
                .OrderByDescending(report => report.CreatedAt)
                .ToList();

            DrawSavedHeader(reports);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(SavedListHeight)))
            {
                _savedListScroll = EditorGUILayout.BeginScrollView(_savedListScroll);

                if (reports.Count == 0)
                    DesignerTerminology.DrawEmptyState("No saved profiles yet.");
                else
                {
                    foreach (GenerationProfileReport report in reports)
                        DrawSavedProfileListItem(report);
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(6f);
            DrawSelectedSavedProfile();
        }

        private void DrawSavedHeader(IReadOnlyList<GenerationProfileReport> reports)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Saved Profiles ({reports.Count})", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!_selectedReport))
                {
                    if (GUILayout.Button(
                            "Delete Selected…",
                            EditorStyles.miniButtonLeft,
                            GUILayout.Width(112f)))
                        DeleteSelectedReport();
                }

                if (GUILayout.Button(
                        new GUIContent("▾", "Delete all saved profile reports."),
                        EditorStyles.miniButtonRight,
                        GUILayout.Width(22f)))
                    ShowBulkDeleteMenu(reports);
            }
        }

        private void ShowBulkDeleteMenu(IReadOnlyList<GenerationProfileReport> reports)
        {
            GenericMenu menu = new();
            if (reports.Count > 0)
                menu.AddItem(new GUIContent("Delete All Saved Profiles…"), false, ClearSavedProfiles);
            else
                menu.AddDisabledItem(new GUIContent("Delete All Saved Profiles…"));
            menu.ShowAsContext();
        }

        private void DrawSavedProfileListItem(GenerationProfileReport report)
        {
            bool selected = report == _selectedReport;
            GUIStyle style = selected ? EditorStyles.helpBox : GUIStyle.none;

            using (new EditorGUILayout.VerticalScope(style))
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 40f);
                string title = GetReportListTitle(report);

                if (GUI.Button(rowRect, new GUIContent(string.Empty, title), GUIStyle.none))
                    SelectReport(report);

                Rect titleRect = new(rowRect.x, rowRect.y, rowRect.width, 18f);
                Rect infoRect = new(rowRect.x, rowRect.y + 18f, rowRect.width, 18f);
                EditorGUI.LabelField(titleRect, title, EditorStyles.boldLabel);
                EditorGUI.LabelField(infoRect, GetReportListInfo(report), EditorStyles.miniLabel);
            }
        }

        private void DrawSelectedSavedProfile()
        {
            if (!_selectedReport)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Profile Details", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(
                        new GUIContent("Export CSV", "Export the selected profile's detailed measurements as a CSV file."),
                        GUILayout.Width(82f)))
                    ExportCsv(_selectedReport);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _savedDetailsScroll = EditorGUILayout.BeginScrollView(_savedDetailsScroll, GUILayout.MaxHeight(360f));
                float previousLabelWidth = BeginProfileDetailsLabelLayout();
                try
                {
                    DrawRunSummary(_selectedReport);
                    DrawPhaseSummary(_selectedReport);
                    DrawTargetProfiles(_selectedReport);
                }
                finally
                {
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void SelectReport(GenerationProfileReport report)
        {
            _selectedReport = report;
            Selection.activeObject = report;
        }

        private void DeleteSelectedReport()
        {
            if (!_selectedReport)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Profile",
                "Delete the selected profile report?\n\nThis cannot be undone.",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            GenerationProfileReport report = _selectedReport;
            _selectedReport = null;
            GenerationProfileCatalogService.DeleteReport(report);
            Repaint();
        }

        private void ClearSavedProfiles()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Saved Profiles",
                "Delete all saved profile reports?\n\nThis cannot be undone.",
                "Clear",
                "Cancel");

            if (!confirmed)
                return;

            _selectedReport = null;
            GenerationProfileCatalogService.Clear();
            Repaint();
        }

        private void RefreshGenerationPresets()
        {
            _generationPresets = EditorAssets.LoadAssetsFromFolder<GenerationPreset>(
                ProjectContentPaths.GenerationPresets,
                (left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
            _generationPresetOptions = EditorAssets.CreateAssetOptions(_generationPresets);
        }

        private GenerationPreset LoadRememberedGenerationPreset()
        {
            string guid = EditorPrefs.GetString(SelectedPresetKey, string.Empty);
            GenerationPreset remembered = AssetDatabase.LoadAssetAtPath<GenerationPreset>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (remembered && _generationPresets.Contains(remembered))
                return remembered;

            GenerationPreset defaultPreset = GenerationPresetPreferences.GetDefault();
            return defaultPreset && _generationPresets.Contains(defaultPreset)
                ? defaultPreset
                : _generationPresets.FirstOrDefault();
        }

        private void SetGenerationPreset(GenerationPreset preset)
        {
            _selectedGenerationPreset = preset;
            string path = preset ? AssetDatabase.GetAssetPath(preset) : string.Empty;
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid))
                EditorPrefs.DeleteKey(SelectedPresetKey);
            else
                EditorPrefs.SetString(SelectedPresetKey, guid);
        }

        private void HandleProjectChanged()
        {
            RefreshGenerationPresets();
            if (!_selectedGenerationPreset || !_generationPresets.Contains(_selectedGenerationPreset))
                SetGenerationPreset(LoadRememberedGenerationPreset());
            GenerationProfileCatalogService.Refresh();
            _runError = string.Empty;
            Repaint();
        }

        private void HandleHierarchyChanged()
        {
            _targetAreaSelector.Refresh();
            _runError = string.Empty;
            Repaint();
        }
    }
}
