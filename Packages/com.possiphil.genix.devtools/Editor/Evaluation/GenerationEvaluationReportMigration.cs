using System;
using System.IO;
using System.Linq;
using Genix.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Evaluation
{
    /// <summary>Repairs reports created before the report type had its own matching source file.</summary>
    [InitializeOnLoad]
    internal static class GenerationEvaluationReportMigration
    {
        private const string MissingScriptLine = "  m_Script: {fileID: 0}";
        private static readonly string[] LegacyClassLines =
        {
            "  m_EditorClassIdentifier: Genix.Editor:Genix.Editor.Evaluation:GenerationEvaluationReport",
            "  m_EditorClassIdentifier: Genix.DevTools.Editor:Genix.Editor.Evaluation:GenerationEvaluationReport"
        };
        private const string CurrentClassLine =
            "  m_EditorClassIdentifier: Genix.DevTools.Editor::Genix.Editor.Evaluation.GenerationEvaluationReport";

        static GenerationEvaluationReportMigration() => EditorApplication.delayCall += RepairPersistedReports;

        internal static void RepairPersistedReports()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                !AssetDatabase.IsValidFolder(DevToolsContentPaths.EvaluationReports))
            {
                return;
            }

            string scriptGuid = ResolveReportScriptGuid();
            if (string.IsNullOrWhiteSpace(scriptGuid))
                return;

            string[] paths = AssetDatabase
                .FindAssets(string.Empty, new[] { DevToolsContentPaths.EvaluationReports })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            int repaired = 0;
            foreach (string path in paths)
            {
                string yaml = File.ReadAllText(path);
                if (!TryRewriteLegacyYaml(yaml, scriptGuid, out string rewritten))
                    continue;

                File.WriteAllText(path, rewritten);
                repaired++;
            }

            if (repaired == 0)
                return;

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Genix repaired {repaired:N0} persisted evaluation report asset(s).");
        }

        internal static bool TryRewriteLegacyYaml(
            string yaml,
            string scriptGuid,
            out string rewritten)
        {
            rewritten = yaml ?? string.Empty;
            string legacyClassLine = LegacyClassLines.FirstOrDefault(line =>
                yaml?.Contains(line, StringComparison.Ordinal) == true);
            if (string.IsNullOrWhiteSpace(yaml) ||
                string.IsNullOrWhiteSpace(scriptGuid) ||
                !yaml.Contains(MissingScriptLine, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(legacyClassLine))
            {
                return false;
            }

            rewritten = yaml
                .Replace(
                    MissingScriptLine,
                    $"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}")
                .Replace(legacyClassLine, CurrentClassLine);
            return true;
        }

        private static string ResolveReportScriptGuid()
        {
            GenerationEvaluationReport report = ScriptableObject.CreateInstance<GenerationEvaluationReport>();
            try
            {
                MonoScript script = MonoScript.FromScriptableObject(report);
                string path = script ? AssetDatabase.GetAssetPath(script) : string.Empty;
                return string.IsNullOrWhiteSpace(path)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(report);
            }
        }
    }
}
