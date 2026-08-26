using System;
using Genix.Editor.Infrastructure;
using UnityEditor;

namespace Genix.Editor.Evaluation
{
    /// <summary>Persists completed campaigns as reviewable Unity report assets.</summary>
    internal static class GenerationEvaluationReportService
    {
        public static GenerationEvaluationReport Save(GenerationEvaluationCampaignResult campaign)
        {
            AssetFileService.EnsureFolder(ProjectContentPaths.EvaluationReports);
            GenerationEvaluationReport report = UnityEngine.ScriptableObject.CreateInstance<GenerationEvaluationReport>();
            report.Initialize(campaign);
            report.name = $"{campaign.suiteName} {DateTime.Now:yyyyMMdd-HHmmss}";
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{ProjectContentPaths.EvaluationReports}/{report.name}.asset");
            AssetDatabase.CreateAsset(report, path);
            EditorUtility.SetDirty(report);
            AssetDatabase.SaveAssets();
            return report;
        }
    }
}
