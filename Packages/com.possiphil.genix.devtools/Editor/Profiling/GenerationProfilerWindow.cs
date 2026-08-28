using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Extensions;
using Genix.Placement;
using Genix.Profiling;
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
            GenerationProfilerWindow window = GetWindow<GenerationProfilerWindow>("Genix Profiler");
            window.Show();
            window.Focus();
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

                GUILayout.Space(6f);

                using (new EditorGUI.DisabledScope(GenerationProfilerService.LastProfile == null))
                {
                    if (GUILayout.Button("Save Profile", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                    {
                        _selectedReport = GenerationProfileReportSaver.Save(GenerationProfilerService.LastProfile);
                        GenerationProfileCatalogService.Refresh();
                    }

                    if (GUILayout.Button("Copy CSV", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                        CopyCsv(GenerationProfilerService.LastProfile);

                    if (GUILayout.Button("Clear Current", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                        GenerationProfilerService.ClearLastProfile();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    GenerationProfileCatalogService.Refresh();

                if (GUILayout.Button("Clear Saved", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                    ClearSavedProfiles();
            }
        }

        private void DrawCurrentProfile()
        {
            EditorGUILayout.LabelField("Current Profile", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GenerationProfile profile = GenerationProfilerService.LastProfile;

                if (profile == null)
                    return;

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
                {
                    GUILayout.Space(EditorGUIUtility.singleLineHeight);
                }
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
                if (GUILayout.Button(GetReportListTitle(report), EditorStyles.boldLabel))
                    SelectReport(report);

                EditorGUILayout.LabelField(GetReportListInfo(report), EditorStyles.miniLabel);
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
