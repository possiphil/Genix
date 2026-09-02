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
        /// <summary>Name of the evaluated assertion.</summary>
        public string name = string.Empty;

        /// <summary>Outcome recorded for the assertion.</summary>
        public EvaluationCheckStatus status;

        /// <summary>Number of violations found by the assertion.</summary>
        public int violations;

        /// <summary>Human-readable evidence or failure context.</summary>
        public string message = string.Empty;
    }

    /// <summary>One named count retained for machine-readable evaluation evidence.</summary>
    [Serializable]
    public sealed class GenerationEvaluationCountRecord
    {
        /// <summary>Name of the counted item or rejection reason.</summary>
        public string name = string.Empty;

        /// <summary>Recorded number of occurrences.</summary>
        public int count;
    }

    /// <summary>Aggregated occurrence evidence for one asset or support kind across a scenario.</summary>
    public sealed class GenerationEvaluationCoverageRecord
    {
        /// <summary>Name of the asset or support kind.</summary>
        public string name = string.Empty;

        /// <summary>Number of runs in which the item occurred at least once.</summary>
        public int runsPresent;

        /// <summary>Total number of runs included in the coverage calculation.</summary>
        public int totalRuns;

        /// <summary>Total number of occurrences across all included runs.</summary>
        public int totalCount;

        /// <summary>Gets the fraction of scenario runs in which the item occurred at least once.</summary>
        public float RunCoverage => totalRuns > 0 ? runsPresent / (float)totalRuns : 0f;
    }

    /// <summary>All objective and subjective observations from one deterministic generation run.</summary>
    [Serializable]
    public sealed class GenerationEvaluationRunRecord
    {
        /// <summary>Name of the evaluated scenario.</summary>
        public string scenario = string.Empty;

        /// <summary>Evaluation category assigned to the scenario.</summary>
        public string scenarioKind = string.Empty;

        /// <summary>Asset path of the scene used for the run.</summary>
        public string scene = string.Empty;

        /// <summary>Persistent identifier of the selected area provider.</summary>
        public string areaProviderId = string.Empty;

        /// <summary>Identifier of the target area within the provider.</summary>
        public string targetId = string.Empty;

        /// <summary>Name of the generation preset used for the run.</summary>
        public string preset = string.Empty;

        /// <summary>Deterministic random seed used for generation.</summary>
        public int seed;

        /// <summary>Number of objects requested by the scenario.</summary>
        public int requestedCount;

        /// <summary>Number of objects placed by the generator.</summary>
        public int placedCount;

        /// <summary>Whether the generation operation completed successfully.</summary>
        public bool generationSucceeded;

        /// <summary>Number of candidate poses tested during generation.</summary>
        public int testedCandidates;

        /// <summary>Number of tested candidates rejected by placement constraints.</summary>
        public int rejectedCandidates;

        /// <summary>Most frequent candidate rejection reason, if available.</summary>
        public string topRejection = string.Empty;

        /// <summary>Reason generation stopped before or after satisfying the request.</summary>
        public string stopReason = string.Empty;

        /// <summary>Minimum placement distance configured for the run, in metres.</summary>
        public float minimumPlacementDistance;

        /// <summary>Names of assets eligible for selection in this run.</summary>
        public List<string> eligibleAssetNames = new();

        /// <summary>Names of support kinds expected to be represented in scenario coverage.</summary>
        public List<string> expectedSupportNames = new();

        /// <summary>Placed-object counts grouped by asset.</summary>
        public List<GenerationEvaluationCountRecord> assetCounts = new();

        /// <summary>Placed-object counts grouped by support kind.</summary>
        public List<GenerationEvaluationCountRecord> supportCounts = new();

        /// <summary>Rejected-candidate counts grouped by reason.</summary>
        public List<GenerationEvaluationCountRecord> rejectionCounts = new();

        /// <summary>Last known asset path of the saved review layout.</summary>
        public string layoutAssetPath = string.Empty;

        /// <summary>Unity asset GUID used to resolve the saved layout after it moves.</summary>
        public string layoutGuid = string.Empty;

        /// <summary>Human rating assigned during visual review.</summary>
        public EvaluationVisualRating visualRating;

        /// <summary>Reviewer notes supporting the assigned visual rating.</summary>
        public string visualNotes = string.Empty;

        /// <summary>Project-relative path of the standardized review-capture manifest.</summary>
        public string visualReviewCaptureManifestPath = string.Empty;

        /// <summary>SHA-256 digest of the review-capture manifest.</summary>
        public string visualReviewCaptureManifestSha256 = string.Empty;

        /// <summary>UTC timestamp at which the review views were captured.</summary>
        public string visualReviewCapturedAtUtc = string.Empty;

        /// <summary>Automatic assertion results recorded for the run.</summary>
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
        /// <summary>Name of the evaluation suite.</summary>
        public string suiteName = string.Empty;

        /// <summary>Project-relative asset path of the evaluation suite.</summary>
        public string suiteAssetPath = string.Empty;

        /// <summary>UTC timestamp at which the campaign artifact was created.</summary>
        public string createdAtUtc = string.Empty;

        /// <summary>Unity version used to execute the campaign.</summary>
        public string unityVersion = string.Empty;

        /// <summary>Operating-system description reported by the execution environment.</summary>
        public string operatingSystem = string.Empty;

        /// <summary>Digest of the suite and its evaluation-relevant dependencies.</summary>
        public string suiteDependencyHash = string.Empty;

        /// <summary>Scope selected for the campaign, such as the complete suite or one scenario.</summary>
        public string runScope = "Unknown";

        /// <summary>Selected scenario index for a scoped run, or -1 for the complete suite.</summary>
        public int selectedScenarioIndex = -1;

        /// <summary>Number of runs expected when the campaign began.</summary>
        public int expectedRunCount;

        /// <summary>Whether every expected generation run completed.</summary>
        public bool campaignCompleted;

        /// <summary>Whether the campaign was cancelled before completion.</summary>
        public bool campaignCancelled;

        /// <summary>Recorded results for completed generation runs.</summary>
        public List<GenerationEvaluationRunRecord> runs = new();
    }

}
