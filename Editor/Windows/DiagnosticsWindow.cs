using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Diagnostics;
using Genix.Diagnostics;
using Genix.Editor.Genix.Editor.Diagnostics;
using Genix.Extensions;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed class DiagnosticsWindow : EditorWindow
    {
        private enum ReportFilter
        {
            Summary,
            Detailed
        }

        private enum ReportSortMode
        {
            NewestFirst,
            OldestFirst,
            TargetAscending,
            TargetDescending,
            PlacedCountDescending,
            PlacedCountAscending,
            Style
        }

        private const float ReportListHeight = 180f;

        private ReportFilter _filter = ReportFilter.Summary;
        private ReportSortMode _sortMode = ReportSortMode.NewestFirst;
        private DiagnosticsReport _selectedReport;
        private UnityEditor.Editor _selectedReportEditor;

        private Vector2 _listScroll;
        private Vector2 _detailsScroll;

        [MenuItem("Tools/Genix/Diagnostics", false, 10)]
        public static void Open()
        {
            DiagnosticsWindow window = GetWindow<DiagnosticsWindow>("Genix Diagnostics");

            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            DiagnosticsCatalogService.Refresh();
        }

        private void OnDisable()
        {
            DestroySelectedReportEditor();
            DiagnosticsPreview.ClearCurrentReport();
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.Space(6f);

            DrawReportList();

            EditorGUILayout.Space(8f);

            DrawSelectedReport();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _filter = (ReportFilter)DrawToolbarTabsWithRightBorder(
                    (int)_filter,
                    new[] { "Summary", "Detailed" },
                    180f);

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
                toolbarRect.y - 1f,
                1f,
                toolbarRect.height + 3f);

            EditorGUI.DrawRect(lineRect, new Color(0f, 0f, 0f, 0.55f));
        }

        private void ClearCatalog()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Diagnostics Catalog",
                "Delete all diagnostics reports?\n\nThis cannot be undone.",
                "Clear Catalog",
                "Cancel");

            if (!confirmed)
                return;

            ClearSelection();

            DiagnosticsCatalogService.Clear();

            Repaint();
        }

        private void DrawReportList()
        {
            DiagnosticsCatalog catalog = DiagnosticsCatalogService.GetOrCreate();
            List<DiagnosticsReport> reports = GetFilteredReports(catalog);

            DrawReportsHeader(reports);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(ReportListHeight)))
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

                if (reports.Count == 0)
                {
                    EditorGUILayout.HelpBox("No reports found.", MessageType.Info);
                }
                else
                {
                    foreach (DiagnosticsReport report in reports)
                        DrawReportListItem(report);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawReportsHeader(IReadOnlyList<DiagnosticsReport> reports)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Reports ({reports.Count})", EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                DrawReportSortDropdown();

                using (new EditorGUI.DisabledScope(!_selectedReport))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                        DeleteSelectedReport();
                }

                using (new EditorGUI.DisabledScope(reports.Count == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                        ClearCurrentModeReports();
                }
            }
        }

        private void DrawReportSortDropdown()
        {
            ReportSortMode[] modes =
            {
                ReportSortMode.NewestFirst,
                ReportSortMode.OldestFirst,
                ReportSortMode.TargetAscending,
                ReportSortMode.TargetDescending,
                ReportSortMode.PlacedCountDescending,
                ReportSortMode.PlacedCountAscending,
                ReportSortMode.Style
            };

            string[] labels =
            {
                "Newest First",
                "Oldest First",
                "Target Ascending",
                "Target Descending",
                "Placed Count Descending",
                "Placed Count Ascending",
                "Style"
            };

            int selectedIndex = Array.IndexOf(modes, _sortMode);

            if (selectedIndex < 0)
                selectedIndex = 0;

            GUILayout.Label("Sort by", EditorStyles.label, GUILayout.Width(42f));

            selectedIndex = EditorGUILayout.Popup(
                selectedIndex,
                labels,
                GUILayout.Width(180f));

            _sortMode = modes[selectedIndex];
        }

        private void ClearCurrentModeReports()
        {
            DiagnosticsMode mode = GetSelectedDiagnosticsMode();

            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Diagnostic Reports",
                $"Delete all {_filter.ToDisplayName()} diagnostic reports?\n\nThis cannot be undone.",
                "Clear",
                "Cancel");

            if (!confirmed)
                return;

            if (_selectedReport && _selectedReport.Mode == mode)
                ClearSelection();

            DiagnosticsCatalogService.ClearReports(mode);
            DiagnosticsCatalogService.Refresh();

            Repaint();
        }

        private DiagnosticsMode GetSelectedDiagnosticsMode()
        {
            return _filter == ReportFilter.Summary
                ? DiagnosticsMode.Summary
                : DiagnosticsMode.Detailed;
        }

        private void DeleteSelectedReport()
        {
            if (!_selectedReport)
                return;

            DiagnosticsReport reportToDelete = _selectedReport;

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Diagnostics Report",
                $"Delete report '{reportToDelete.name}'?\n\nThis cannot be undone.",
                "Delete",
                "Cancel");

            if (!confirmed)
                return;

            ClearSelection();

            DiagnosticsCatalogService.DeleteReport(reportToDelete);

            Repaint();
        }

        private void ClearSelection()
        {
            _selectedReport = null;

            DestroySelectedReportEditor();
            DiagnosticsPreview.ClearCurrentReport();

            Repaint();
        }

        private void DrawReportListItem(DiagnosticsReport report)
        {
            bool selected = report == _selectedReport;

            GUIStyle containerStyle = selected
                ? EditorStyles.helpBox
                : GUIStyle.none;

            using (new EditorGUILayout.VerticalScope(containerStyle))
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 36f);

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                    SelectReport(report);

                Rect titleRect = new(rowRect.x, rowRect.y, rowRect.width, 18f);
                Rect infoRect = new(rowRect.x, rowRect.y + 18f, rowRect.width, 18f);

                EditorGUI.LabelField(titleRect, GetReportListTitle(report), EditorStyles.boldLabel);
                EditorGUI.LabelField(infoRect, GetReportListInfo(report));
            }

            EditorGUILayout.Space(2f);
        }

        private void DrawSelectedReport()
        {
            EditorGUILayout.LabelField("Selected Report", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!_selectedReport)
                {
                    EditorGUILayout.HelpBox("Select a report from the list.", MessageType.Info);
                    return;
                }

                _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

                _selectedReportEditor ??= UnityEditor.Editor.CreateEditor(_selectedReport);
                _selectedReportEditor.OnInspectorGUI();

                EditorGUILayout.EndScrollView();
            }
        }

        private void SelectReport(DiagnosticsReport report)
        {
            if (_selectedReport == report)
                return;

            _selectedReport = report;

            DestroySelectedReportEditor();

            DiagnosticsPreview.SetReport(_selectedReport);

            Repaint();
        }

        private List<DiagnosticsReport> GetFilteredReports(DiagnosticsCatalog catalog)
        {
            DiagnosticsMode mode = _filter == ReportFilter.Summary
                ? DiagnosticsMode.Summary
                : DiagnosticsMode.Detailed;

            IEnumerable<DiagnosticsReport> reports = catalog.Reports
                .Where(report => report && report.Mode == mode)
                .ToList();

            return SortReports(reports);
        }

        private List<DiagnosticsReport> SortReports(IEnumerable<DiagnosticsReport> reports)
        {
            return _sortMode switch
            {
                ReportSortMode.OldestFirst => reports
                    .OrderBy(report => report.CreatedAt, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                ReportSortMode.TargetAscending => reports
                    .OrderBy(report => report.TargetName, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(report => report.CreatedAt, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                ReportSortMode.TargetDescending => reports
                    .OrderByDescending(report => report.TargetName, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(report => report.CreatedAt, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                ReportSortMode.PlacedCountDescending => reports
                    .OrderByDescending(report => report.PlacedObjectCount)
                    .ThenByDescending(report => report.CreatedAt, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                ReportSortMode.PlacedCountAscending => reports
                    .OrderBy(report => report.PlacedObjectCount)
                    .ThenByDescending(report => report.CreatedAt, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                ReportSortMode.Style => reports
                    .OrderBy(report => report.StyleName, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(report => report.CreatedAt, StringComparer.OrdinalIgnoreCase)
                    .ToList(),

                _ => reports
                    .OrderByDescending(report => report.CreatedAt, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private static string GetReportListTitle(DiagnosticsReport report)
        {
            string createdAt = string.IsNullOrWhiteSpace(report.CreatedAt)
                ? "Unknown Time"
                : report.CreatedAt;

            string target = string.IsNullOrWhiteSpace(report.TargetName)
                ? "Unknown Target"
                : report.TargetName;

            return $"{createdAt} - {target}";
        }

        private static string GetReportListInfo(DiagnosticsReport report)
        {
            string mode = string.IsNullOrWhiteSpace(report.GenerationMode)
                ? "Unknown Mode"
                : report.GenerationMode;

            string style = string.IsNullOrWhiteSpace(report.StyleName)
                ? "Unknown Style"
                : report.StyleName;
            string seed = report.UseRandomSeed ? $"    Seed: {report.RandomSeed}" : string.Empty;

            return $"Mode: {mode}    Style: {style}    Placed: {report.PlacedObjectCount}/{report.RequestedObjectCount}{seed}";
        }

        private void DestroySelectedReportEditor()
        {
            if (!_selectedReportEditor)
                return;

            DestroyImmediate(_selectedReportEditor);
            _selectedReportEditor = null;
        }
    }
}
