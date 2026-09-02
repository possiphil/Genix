using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Evaluation
{
    /// <summary>Exposes single-run review capture to local command-line automation.</summary>
    public static class GenerationEvaluationReviewCaptureCommand
    {
        /// <summary>Captures one report run selected through command-line arguments.</summary>
        public static void CaptureSingleRun()
        {
            try
            {
                string reportPath = ReadArgument("-genixReviewReport");
                string runValue = ReadArgument("-genixReviewRunIndex");
                if (string.IsNullOrWhiteSpace(reportPath))
                    throw new InvalidOperationException("-genixReviewReport must name a report asset path.");
                if (!int.TryParse(runValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int runIndex))
                    throw new InvalidOperationException("-genixReviewRunIndex must be a zero-based integer.");

                GenerationEvaluationReport report = AssetDatabase.LoadAssetAtPath<GenerationEvaluationReport>(
                    reportPath);
                if (!report)
                    throw new InvalidOperationException($"Evaluation report was not found at '{reportPath}'.");

                if (!GenerationEvaluationReviewCaptureService.CaptureRun(
                        report,
                        runIndex,
                        out string contactSheet,
                        out string error))
                {
                    throw new InvalidOperationException(error);
                }

                Debug.Log($"Genix review capture completed: {contactSheet}");
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static string ReadArgument(string key)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.FindIndex(arguments, argument =>
                string.Equals(argument, key, StringComparison.Ordinal));
            return index >= 0 && index + 1 < arguments.Length
                ? arguments[index + 1]
                : string.Empty;
        }
    }
}
