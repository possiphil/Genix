using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Genix.Editor.Evaluation
{
    /// <summary>Persistent Unity artifact used to review layouts and re-export human ratings.</summary>
    public sealed class GenerationEvaluationReport : ScriptableObject
    {
        [SerializeField] private string suiteName = string.Empty;
        [SerializeField] private string suiteAssetPath = string.Empty;
        [SerializeField] private string createdAtUtc = string.Empty;
        [SerializeField] private string unityVersion = string.Empty;
        [SerializeField] private string operatingSystem = string.Empty;
        [FormerlySerializedAs("projectRevisionHash")]
        [SerializeField] private string suiteDependencyHash = string.Empty;
        [SerializeField] private string runScope = "Unknown";
        [SerializeField] private int selectedScenarioIndex = -1;
        [SerializeField] private int expectedRunCount;
        [SerializeField] private bool campaignCompleted;
        [SerializeField] private bool campaignCancelled;
        [SerializeField] private List<GenerationEvaluationRunRecord> runs = new();

        /// <summary>Gets the evaluated suite name.</summary>
        public string SuiteName => suiteName;
        /// <summary>Gets the evaluated suite asset path.</summary>
        public string SuiteAssetPath => suiteAssetPath;
        /// <summary>Gets the campaign creation timestamp in UTC.</summary>
        public string CreatedAtUtc => createdAtUtc;
        /// <summary>Gets whether the report represents Run All or a selected-scenario invocation.</summary>
        public string RunScope => string.IsNullOrWhiteSpace(runScope) ? "Unknown" : runScope;
        /// <summary>Gets the selected scenario index, or -1 for a full-suite invocation.</summary>
        public int SelectedScenarioIndex => selectedScenarioIndex;
        /// <summary>Gets the number of runs expected for the recorded invocation scope.</summary>
        public int ExpectedRunCount => Mathf.Max(0, expectedRunCount);
        /// <summary>Gets whether every expected run completed without cancellation or runner error.</summary>
        public bool CampaignCompleted => campaignCompleted;
        /// <summary>Gets whether the campaign was stopped by a cancellation request.</summary>
        public bool CampaignCancelled => campaignCancelled;
        /// <summary>Gets all persisted run observations.</summary>
        public IReadOnlyList<GenerationEvaluationRunRecord> Runs => runs;

        /// <summary>Copies one campaign, including partial-run metadata, into this report asset.</summary>
        public void Initialize(GenerationEvaluationCampaignResult campaign)
        {
            suiteName = campaign?.suiteName ?? string.Empty;
            suiteAssetPath = campaign?.suiteAssetPath ?? string.Empty;
            createdAtUtc = campaign?.createdAtUtc ?? string.Empty;
            unityVersion = campaign?.unityVersion ?? string.Empty;
            operatingSystem = campaign?.operatingSystem ?? string.Empty;
            suiteDependencyHash = campaign?.suiteDependencyHash ?? string.Empty;
            runScope = campaign?.runScope ?? "Unknown";
            selectedScenarioIndex = campaign?.selectedScenarioIndex ?? -1;
            expectedRunCount = campaign?.expectedRunCount ?? 0;
            campaignCompleted = campaign?.campaignCompleted ?? false;
            campaignCancelled = campaign?.campaignCancelled ?? false;
            runs = campaign?.runs ?? new List<GenerationEvaluationRunRecord>();
        }

        /// <summary>Creates an export model containing the current visual review values.</summary>
        public GenerationEvaluationCampaignResult ToCampaign() => new()
        {
            suiteName = suiteName,
            suiteAssetPath = suiteAssetPath,
            createdAtUtc = createdAtUtc,
            unityVersion = unityVersion,
            operatingSystem = operatingSystem,
            suiteDependencyHash = suiteDependencyHash,
            runScope = runScope,
            selectedScenarioIndex = selectedScenarioIndex,
            expectedRunCount = expectedRunCount,
            campaignCompleted = campaignCompleted,
            campaignCancelled = campaignCancelled,
            runs = runs
        };
    }
}
