using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Extensions;
using Genix.Placement;
using Genix.Profiling;
using Genix.Editor.UI;
using Genix.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Profiling
{
    /// <summary>Provides the generation profiler editor window.</summary>
    public sealed partial class GenerationProfilerWindow : EditorWindow
    {
        private const float SavedListHeight = 180f;

        private GenerationProfileReport _selectedReport;
        private Vector2 _currentScroll;
        private Vector2 _savedListScroll;
        private Vector2 _savedDetailsScroll;

        /// <summary>Opens or focuses the corresponding Genix editor window.</summary>
        [MenuItem("Tools/Genix Developer/Profiler", false, 10)]
        public static void Open()
        {
            GenixWindowDocking.Open<GenerationProfilerWindow>("Genix Profiler");
        }

        private void OnEnable()
        {
            GenerationProfilerService.Changed += Repaint;
            GenerationProfileCatalogService.Refresh();
        }

        private void OnDisable()
        {
            GenerationProfilerService.Changed -= Repaint;
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.Space(6f);
            DrawCurrentProfile();

            EditorGUILayout.Space(8f);
            DrawSavedProfiles();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                bool captureRuns = GUILayout.Toggle(
                    GenerationProfilerService.ProfilingEnabled,
                    new GUIContent(
                        "Capture Runs",
                        "Instrument subsequent Generate, Re-Generate, and Preview Run operations until disabled. This adds measurement overhead."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(92f));
                if (EditorGUI.EndChangeCheck())
                    GenerationProfilerService.SetProfilingEnabled(captureRuns);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    GenerationProfileCatalogService.Refresh();

                if (GUILayout.Button(
                        new GUIContent("Actions", "Delete saved profiling data."),
                        EditorStyles.toolbarDropDown,
                        GUILayout.Width(72f)))
                    ShowProfileActionsMenu();
            }
        }

        private void ShowProfileActionsMenu()
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Delete All Saved Profiles…"), false, ClearSavedProfiles);
            menu.ShowAsContext();
        }

        private void DrawCurrentProfile()
        {
            GenerationProfile profile = GenerationProfilerService.LastProfile;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Current Profile", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(profile == null))
                {
                    if (GUILayout.Button("Save", EditorStyles.miniButtonLeft, GUILayout.Width(58f)))
                    {
                        _selectedReport = GenerationProfileReportSaver.Save(profile);
                        GenerationProfileCatalogService.Refresh();
                    }

                    if (GUILayout.Button("Copy CSV", EditorStyles.miniButtonMid, GUILayout.Width(72f)))
                        CopyCsv(profile);

                    if (GUILayout.Button("Clear", EditorStyles.miniButtonRight, GUILayout.Width(58f)))
                        GenerationProfilerService.ClearLastProfile();
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (profile == null)
                {
                    DesignerTerminology.DrawEmptyState("No captured profile yet.");
                    return;
                }

                _currentScroll = EditorGUILayout.BeginScrollView(_currentScroll, GUILayout.MaxHeight(280f));
                DrawRunSummary(profile);
                DrawPhaseSummary(profile);
                DrawTargetProfiles(profile);
                EditorGUILayout.EndScrollView();
            }
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
                    if (GUILayout.Button("Copy CSV", GUILayout.Width(80f)))
                        CopyCsv(_selectedReport);

                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                        DeleteSelectedReport();
                }
            }
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

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _savedDetailsScroll = EditorGUILayout.BeginScrollView(_savedDetailsScroll, GUILayout.MaxHeight(360f));
                DrawRunSummary(_selectedReport);
                DrawPhaseSummary(_selectedReport);
                DrawTargetProfiles(_selectedReport);
                EditorGUILayout.EndScrollView();
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
    }
}
