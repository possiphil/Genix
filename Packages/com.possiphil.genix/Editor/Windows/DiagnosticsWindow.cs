using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Diagnostics;
using Genix.Editor.Inspectors;
using Genix.Editor.UI;
using Genix.Editor.Utilities;
using Genix.Diagnostics;
using Genix.Extensions;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Windows
{
    /// <summary>Provides the diagnostics editor window.</summary>
    public sealed class DiagnosticsWindow : EditorWindow
    {
        private const float ReportListHeight = 180f;

        private DiagnosticsReport _selectedReport;
        private UnityEditor.Editor _selectedReportEditor;

        private Vector2 _listScroll;
        private Vector2 _detailsScroll;
        private bool _showTechnicalDetails;

        /// <summary>Opens or focuses the corresponding Genix editor window.</summary>
        [MenuItem("Tools/Genix/Diagnostics", false, 30)]
        public static void Open()
        {
            GenixWindowDocking.Open<DiagnosticsWindow>("Genix Diagnostics");
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
            EditorGUILayout.Space(4f);

            DrawReportList();

            EditorGUILayout.Space(8f);

            DrawSelectedReport();
        }

        private void ShowBulkDeleteMenu(IReadOnlyList<DiagnosticsReport> reports)
        {
            GenericMenu menu = new();
            AddBulkDeleteMenuItem(
                menu,
                "Delete All Summary Reports…",
                reports.Any(report => report && report.Mode == DiagnosticsMode.Summary),
                () => ClearReports(DiagnosticsMode.Summary));
            AddBulkDeleteMenuItem(
                menu,
                "Delete All Detailed Reports…",
                reports.Any(report => report && report.Mode == DiagnosticsMode.Detailed),
                () => ClearReports(DiagnosticsMode.Detailed));
            menu.AddSeparator(string.Empty);
            AddBulkDeleteMenuItem(
                menu,
                "Delete All Reports…",
                reports.Count > 0,
                ClearCatalog);
            menu.ShowAsContext();
        }

        private static void AddBulkDeleteMenuItem(
            GenericMenu menu,
            string label,
            bool enabled,
            GenericMenu.MenuFunction action)
        {
            GUIContent content = new(label);
            if (enabled)
                menu.AddItem(content, false, action);
            else
                menu.AddDisabledItem(content);
        }

        private void ClearCatalog()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete All Diagnostics Reports",
                "Delete all diagnostics reports?\n\nThis cannot be undone.",
                "Delete All",
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
            List<DiagnosticsReport> reports = GetReports(catalog);

            DrawReportsHeader(reports);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(ReportListHeight)))
            {
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

                if (reports.Count == 0)
                    DesignerTerminology.DrawEmptyState("No diagnostics reports yet.");
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

                using (new EditorGUI.DisabledScope(!_selectedReport))
                {
                    if (GUILayout.Button(
                            "Delete Selected…",
                            EditorStyles.miniButtonLeft,
                            GUILayout.Width(112f)))
                        DeleteSelectedReport();
                }

                if (GUILayout.Button(
                        new GUIContent("▾", "Delete groups of saved diagnostics reports."),
                        EditorStyles.miniButtonRight,
                        GUILayout.Width(22f)))
                {
                    ShowBulkDeleteMenu(reports);
                }
            }
        }

        private void ClearReports(DiagnosticsMode mode)
        {
            string label = mode == DiagnosticsMode.Detailed ? "detailed" : "summary";

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Diagnostic Reports",
                $"Delete all {label} diagnostic reports?\n\nThis cannot be undone.",
                "Delete All",
                "Cancel");

            if (!confirmed)
                return;

            if (_selectedReport && _selectedReport.Mode == mode)
                ClearSelection();

            DiagnosticsCatalogService.ClearReports(mode);
            DiagnosticsCatalogService.Refresh();

            Repaint();
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
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Report Details", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!_selectedReport))
                {
                    _showTechnicalDetails = GUILayout.Toggle(
                        _showTechnicalDetails,
                        new GUIContent(
                            "Technical Details",
                            "Show run configuration, placement-search statistics, candidate data, and Scene View options. Available detail depends on how the report was recorded."),
                        EditorStyles.miniButton,
                        GUILayout.Width(112f));
                }

                GUILayout.Space(2f);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!_selectedReport)
                {
                    DesignerTerminology.DrawEmptyState("Select a report to inspect its result.");
                    return;
                }

                _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);

                _selectedReportEditor ??= UnityEditor.Editor.CreateEditor(_selectedReport);
                if (_selectedReportEditor is DiagnosticsReportEditor reportEditor)
                    reportEditor.TechnicalDetailsOverride = _showTechnicalDetails;
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

        private static List<DiagnosticsReport> GetReports(DiagnosticsCatalog catalog)
        {
            return catalog.Reports
                .Where(report => report)
                .OrderByDescending(report => report.CreatedAt, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetReportListTitle(DiagnosticsReport report)
        {
            string createdAt = string.IsNullOrWhiteSpace(report.CreatedAt)
                ? "Unknown Time"
                : report.CreatedAt;

            string target = string.IsNullOrWhiteSpace(report.TargetName)
                ? "Unknown Target Area"
                : report.TargetName;

            return $"{createdAt} - {target}";
        }

        private static string GetReportListInfo(DiagnosticsReport report)
        {
            string style = string.IsNullOrWhiteSpace(report.StyleName)
                ? "Unknown Style"
                : report.StyleName;

            string mode = report.IsDetailed ? "Detailed" : "Summary";
            string resultLabel = report.DryRun ? "Planned" : "Placed";
            return $"{mode}    {resultLabel}: {report.PlacedObjectCount}/{report.RequestedObjectCount}    Seed: {report.RandomSeed}    Style: {style}";
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
