using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Layouts;
using UnityEngine;

namespace Genix.Editor.Evaluation
{
    /// <summary>Outcome severity of one automatic evaluation check.</summary>
    public enum EvaluationCheckStatus
    {
        /// <summary>The assertion was not applicable or could not be evaluated.</summary>
        NotApplicable,
        /// <summary>The assertion completed successfully.</summary>
        Passed,
        /// <summary>The assertion found a quality violation.</summary>
        Failed
    }

    /// <summary>Aggregate status of the automatic evidence recorded for one generation run.</summary>
    public enum EvaluationAutomaticVerdict
    {
        /// <summary>Generation succeeded and every recorded automatic check passed.</summary>
        Passed,
        /// <summary>Evidence is incomplete because no checks were emitted or at least one check was unavailable.</summary>
        Incomplete,
        /// <summary>Generation failed or at least one automatic check found a violation.</summary>
        Failed
    }

    /// <summary>Human review assigned to one persisted generated layout.</summary>
    public enum EvaluationVisualRating
    {
        /// <summary>The run has not yet been inspected.</summary>
        NotReviewed,
        /// <summary>No evaluation-relevant visible defect was found under the predefined rubric.</summary>
        Pass,
        /// <summary>One or more minor visible defects were found, but no major defect invalidates the tested configuration.</summary>
        Acceptable,
        /// <summary>The result contains at least one major visible defect that invalidates the tested condition.</summary>
        Fail
    }

    /// <summary>One named, machine-readable assertion result.</summary>
    [Serializable]
    public sealed class GenerationEvaluationCheckRecord
    {
        public string name = string.Empty;
        public EvaluationCheckStatus status;
        public int violations;
        public string message = string.Empty;
    }

    /// <summary>One named count retained for machine-readable evaluation evidence.</summary>
    [Serializable]
    public sealed class GenerationEvaluationCountRecord
    {
        public string name = string.Empty;
        public int count;
    }

    /// <summary>Aggregated occurrence evidence for one asset or support kind across a scenario.</summary>
    public sealed class GenerationEvaluationCoverageRecord
    {
        public string name = string.Empty;
        public int runsPresent;
        public int totalRuns;
        public int totalCount;

        /// <summary>Gets the fraction of scenario runs in which the item occurred at least once.</summary>
        public float RunCoverage => totalRuns > 0 ? runsPresent / (float)totalRuns : 0f;
    }

    /// <summary>All objective and subjective observations from one deterministic generation run.</summary>
    [Serializable]
    public sealed class GenerationEvaluationRunRecord
    {
        public string scenario = string.Empty;
        public string scenarioKind = string.Empty;
        public string scene = string.Empty;
        public string areaProviderId = string.Empty;
        public string targetId = string.Empty;
        public string preset = string.Empty;
        public int seed;
        public int requestedCount;
        public int placedCount;
        public bool generationSucceeded;
        public int testedCandidates;
        public int rejectedCandidates;
        public string topRejection = string.Empty;
        public string stopReason = string.Empty;
        public float minimumPlacementDistance;
        public List<string> eligibleAssetNames = new();
        public List<string> expectedSupportNames = new();
        public List<GenerationEvaluationCountRecord> assetCounts = new();
        public List<GenerationEvaluationCountRecord> supportCounts = new();
        public List<GenerationEvaluationCountRecord> rejectionCounts = new();
        public string layoutAssetPath = string.Empty;
        public string layoutGuid = string.Empty;
        public EvaluationVisualRating visualRating;
        public string visualNotes = string.Empty;
        public List<GenerationEvaluationCheckRecord> checks = new();

        /// <summary>Gets the aggregate automatic-evidence verdict without folding unavailable evidence into a pass.</summary>
        public EvaluationAutomaticVerdict AutomaticVerdict
        {
            get
            {
                if (!generationSucceeded || checks != null && checks.Exists(check => check.status == EvaluationCheckStatus.Failed))
                    return EvaluationAutomaticVerdict.Failed;

                if (checks == null || checks.Count == 0 || checks.Exists(check => check.status == EvaluationCheckStatus.NotApplicable))
                    return EvaluationAutomaticVerdict.Incomplete;

                return EvaluationAutomaticVerdict.Passed;
            }
        }

        /// <summary>Gets whether generation succeeded and every recorded automatic check passed.</summary>
        public bool AutomaticChecksPassed => AutomaticVerdict == EvaluationAutomaticVerdict.Passed;

        /// <summary>Gets whether the run names a persisted layout intended for visual review.</summary>
        public bool HasLayoutReference => !string.IsNullOrWhiteSpace(layoutAssetPath);

        /// <summary>Gets whether a visual rating was assigned to a run with a persisted layout reference.</summary>
        public bool VisualReviewCompleted =>
            HasLayoutReference && visualRating != EvaluationVisualRating.NotReviewed;

        /// <summary>Gets whether an Acceptable or Fail rating contains its required observable note.</summary>
        public bool VisualReviewNoteValid =>
            visualRating is not (EvaluationVisualRating.Acceptable or EvaluationVisualRating.Fail) ||
            !string.IsNullOrWhiteSpace(visualNotes);

        /// <summary>Gets whether the referenced layout still exists without loading its captured prefab.</summary>
        public bool HasMissingLayoutAsset => HasLayoutReference && string.IsNullOrWhiteSpace(ResolvedLayoutAssetPath);

        /// <summary>Gets whether the assigned rating is backed by a retained layout and any required note.</summary>
        public bool VisualReviewEvidenceValid =>
            VisualReviewCompleted && VisualReviewNoteValid && !HasMissingLayoutAsset;

        /// <summary>
        /// Gets whether the record contains invalid visual evidence, such as an unbacked rating, a missing
        /// required note, or a referenced layout that can no longer be loaded. An unreviewed retained layout is
        /// incomplete rather than invalid.
        /// </summary>
        public bool HasInvalidVisualReviewEvidence =>
            HasMissingLayoutAsset ||
            visualRating != EvaluationVisualRating.NotReviewed &&
            (!HasLayoutReference || !VisualReviewNoteValid);

        /// <summary>Gets the current asset path without loading the layout or its captured prefab dependencies.</summary>
        public string ResolvedLayoutAssetPath
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(layoutGuid))
                {
                    string guidPath = UnityEditor.AssetDatabase.GUIDToAssetPath(layoutGuid);
                    if (!string.IsNullOrWhiteSpace(guidPath))
                        return guidPath;
                }

                if (string.IsNullOrWhiteSpace(layoutAssetPath))
                    return string.Empty;

                return string.IsNullOrWhiteSpace(UnityEditor.AssetDatabase.AssetPathToGUID(layoutAssetPath))
                    ? string.Empty
                    : layoutAssetPath;
            }
        }

        /// <summary>
        /// Loads the saved layout on demand. Call this only for operations that need its captured prefab, such as
        /// applying the layout; existence checks should use <see cref="ResolvedLayoutAssetPath"/> instead.
        /// </summary>
        public SavedLayout LoadLayout()
        {
            string path = ResolvedLayoutAssetPath;
            return string.IsNullOrWhiteSpace(path)
                ? null
                : UnityEditor.AssetDatabase.LoadAssetAtPath<SavedLayout>(path);
        }
    }

    /// <summary>Builds informational scenario-wide occurrence coverage without changing automatic verdicts.</summary>
    internal static class GenerationEvaluationCoverage
    {
        public static IReadOnlyList<GenerationEvaluationCoverageRecord> BuildAssetCoverage(
            IEnumerable<GenerationEvaluationRunRecord> runs) =>
            Build(
                runs,
                run => run.eligibleAssetNames ?? Enumerable.Empty<string>(),
                run => run.assetCounts ?? Enumerable.Empty<GenerationEvaluationCountRecord>());

        public static IReadOnlyList<GenerationEvaluationCoverageRecord> BuildSupportCoverage(
            IEnumerable<GenerationEvaluationRunRecord> runs) =>
            Build(
                runs,
                run => run.expectedSupportNames ?? Enumerable.Empty<string>(),
                run => run.supportCounts ?? Enumerable.Empty<GenerationEvaluationCountRecord>());

        private static IReadOnlyList<GenerationEvaluationCoverageRecord> Build(
            IEnumerable<GenerationEvaluationRunRecord> source,
            Func<GenerationEvaluationRunRecord, IEnumerable<string>> expectedSelector,
            Func<GenerationEvaluationRunRecord, IEnumerable<GenerationEvaluationCountRecord>> countSelector)
        {
            GenerationEvaluationRunRecord[] runs = source?.Where(run => run != null).ToArray() ??
                                                     Array.Empty<GenerationEvaluationRunRecord>();
            string[] names = runs
                .SelectMany(run => expectedSelector(run)
                    .Concat(countSelector(run).Where(count => count != null).Select(count => count.name)))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return names.Select(name => new GenerationEvaluationCoverageRecord
                {
                    name = name,
                    runsPresent = runs.Count(run => Count(run, name, countSelector) > 0),
                    totalRuns = runs.Length,
                    totalCount = runs.Sum(run => Count(run, name, countSelector))
                })
                .ToArray();
        }

        private static int Count(
            GenerationEvaluationRunRecord run,
            string name,
            Func<GenerationEvaluationRunRecord, IEnumerable<GenerationEvaluationCountRecord>> selector) =>
            selector(run)
                .Where(count => count != null && string.Equals(count.name, name, StringComparison.OrdinalIgnoreCase))
                .Sum(count => count.count);
    }

    /// <summary>Serializable campaign artifact exported as JSON and tabular CSV.</summary>
    [Serializable]
    public sealed class GenerationEvaluationCampaignResult
    {
        public string suiteName = string.Empty;
        public string suiteAssetPath = string.Empty;
        public string createdAtUtc = string.Empty;
        public string unityVersion = string.Empty;
        public string operatingSystem = string.Empty;
        public string suiteDependencyHash = string.Empty;
        public string runScope = "Unknown";
        public int selectedScenarioIndex = -1;
        public int expectedRunCount;
        public bool campaignCompleted;
        public bool campaignCancelled;
        public List<GenerationEvaluationRunRecord> runs = new();
    }

}
