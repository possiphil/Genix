using Genix.Styles;
using UnityEngine;

namespace Genix.Editor.State
{
    public sealed class StyleEditState
    {
        public StyleSettings EditingSettings;

        public StyleSettings SavedSettings { get; private set; }

        public bool HasPendingChanges { get; private set; }

        public void LoadFromPreset(StylePreset preset)
        {
            EditingSettings = preset.Settings;
            SavedSettings = preset.Settings;
            HasPendingChanges = false;
        }

        public void DiscardChanges()
        {
            EditingSettings = SavedSettings;
            HasPendingChanges = false;
        }

        public void UpdatePendingChanges()
        {
            HasPendingChanges = !StyleSettingsUtility.AreEqual(EditingSettings, SavedSettings);
        }

        public bool HasDescriptionChanged()
        {
            return EditingSettings.description != SavedSettings.description;
        }

        public bool HasAlgorithmChanged()
        {
            return EditingSettings.algorithm != SavedSettings.algorithm;
        }

        public bool HasPlacementSettingsChanged()
        {
            return HasPlacementUseFixedObjectClearanceChanged() || HasPlacementFixedObjectDistanceChanged();
        }

        public bool HasPlacementUseFixedObjectClearanceChanged()
        {
            return EditingSettings.placement.useFixedObjectClearance != SavedSettings.placement.useFixedObjectClearance;
        }

        public bool HasPlacementFixedObjectDistanceChanged()
        {
            return EditingSettings.placement.useFixedObjectClearance && !Mathf.Approximately(EditingSettings.placement.fixedObjectDistance, SavedSettings.placement.fixedObjectDistance);
        }

        public bool HasCandidateSettingsChanged()
        {
            return HasCandidateMultiplierChanged() || HasMinimumCandidatesChanged() || HasShuffleCandidatesChanged();
        }

        public bool HasCandidateMultiplierChanged()
        {
            return EditingSettings.candidates.multiplier != SavedSettings.candidates.multiplier;
        }

        public bool HasMinimumCandidatesChanged()
        {
            return EditingSettings.candidates.minimumCount != SavedSettings.candidates.minimumCount;
        }

        public bool HasShuffleCandidatesChanged()
        {
            return EditingSettings.candidates.shuffle != SavedSettings.candidates.shuffle;
        }

        public bool HasGridSettingsChanged()
        {
            return HasGridCellSizeChanged() || HasGridJitterChanged();
        }

        public bool HasGridCellSizeChanged()
        {
            return !Mathf.Approximately(EditingSettings.grid.cellSize, SavedSettings.grid.cellSize);
        }

        public bool HasGridJitterChanged()
        {
            return !Mathf.Approximately(EditingSettings.grid.jitterAmount, SavedSettings.grid.jitterAmount);
        }

        public bool HasClusterSettingsChanged()
        {
            return HasClusterCountChanged() || HasClusterRadiusChanged() || HasClusterUseMinCenterDistanceChanged() || HasClusterMinCenterDistanceChanged();
        }

        public bool HasClusterCountChanged()
        {
            return EditingSettings.cluster.count != SavedSettings.cluster.count;
        }

        public bool HasClusterRadiusChanged()
        {
            return !Mathf.Approximately(EditingSettings.cluster.radius, SavedSettings.cluster.radius);
        }

        public bool HasClusterUseMinCenterDistanceChanged()
        {
            return EditingSettings.cluster.useMinCenterDistance != SavedSettings.cluster.useMinCenterDistance;
        }

        public bool HasClusterMinCenterDistanceChanged()
        {
            return EditingSettings.cluster.useMinCenterDistance && !Mathf.Approximately(EditingSettings.cluster.minCenterDistance, SavedSettings.cluster.minCenterDistance);
        }

        public bool HasPoissonSettingsChanged()
        {
            return HasPoissonMinDistanceChanged() || HasPoissonAttemptsChanged();
        }

        public bool HasPoissonMinDistanceChanged()
        {
            return !Mathf.Approximately(EditingSettings.poisson.minDistance, SavedSettings.poisson.minDistance);
        }

        public bool HasPoissonAttemptsChanged()
        {
            return EditingSettings.poisson.attempts != SavedSettings.poisson.attempts;
        }
    }
}