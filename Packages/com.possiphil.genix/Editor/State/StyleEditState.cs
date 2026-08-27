using Genix.Styles;
using UnityEngine;

namespace Genix.Editor.State
{
    /// <summary>Tracks editable, saved, and default style settings for an editor session.</summary>
    public sealed class StyleEditState
    {
        /// <summary>Stores editing settings.</summary>
        public StyleSettings EditingSettings;

        /// <summary>Gets saved settings.</summary>
        public StyleSettings SavedSettings { get; private set; }

        /// <summary>Indicates whether pending changes.</summary>
        public bool HasPendingChanges { get; private set; }

        /// <summary>Copies editable style state from the supplied preset.</summary>
        public void LoadFromPreset(StylePreset preset)
        {
            EditingSettings = preset.Settings;
            SavedSettings = preset.Settings;
            HasPendingChanges = false;
        }

        /// <summary>Discards pending edits and restores the source preset values.</summary>
        public void DiscardChanges()
        {
            EditingSettings = SavedSettings;
            HasPendingChanges = false;
        }

        /// <summary>Recomputes whether the edited style differs from its source preset.</summary>
        public void UpdatePendingChanges()
        {
            HasPendingChanges = !StyleSettingsUtility.AreEqual(EditingSettings, SavedSettings);
        }

        /// <summary>Determines whether description changed.</summary>
        public bool HasDescriptionChanged()
        {
            return EditingSettings.description != SavedSettings.description;
        }

        /// <summary>Determines whether algorithm changed.</summary>
        public bool HasAlgorithmChanged()
        {
            return EditingSettings.algorithm != SavedSettings.algorithm;
        }

        /// <summary>Determines whether placement settings changed.</summary>
        public bool HasPlacementSettingsChanged()
        {
            return HasPlacementUseFixedObjectClearanceChanged() || HasPlacementFixedObjectDistanceChanged();
        }

        /// <summary>Determines whether placement use fixed object clearance changed.</summary>
        public bool HasPlacementUseFixedObjectClearanceChanged()
        {
            return EditingSettings.placement.useFixedObjectClearance != SavedSettings.placement.useFixedObjectClearance;
        }

        /// <summary>Determines whether placement fixed object distance changed.</summary>
        public bool HasPlacementFixedObjectDistanceChanged()
        {
            return EditingSettings.placement.useFixedObjectClearance && !Mathf.Approximately(EditingSettings.placement.fixedObjectDistance, SavedSettings.placement.fixedObjectDistance);
        }

        /// <summary>Determines whether candidate settings changed.</summary>
        public bool HasCandidateSettingsChanged()
        {
            return HasCandidateMultiplierChanged() || HasMinimumCandidatesChanged() || HasShuffleCandidatesChanged();
        }

        /// <summary>Determines whether candidate multiplier changed.</summary>
        public bool HasCandidateMultiplierChanged()
        {
            return EditingSettings.candidates.multiplier != SavedSettings.candidates.multiplier;
        }

        /// <summary>Determines whether minimum candidates changed.</summary>
        public bool HasMinimumCandidatesChanged()
        {
            return EditingSettings.candidates.minimumCount != SavedSettings.candidates.minimumCount;
        }

        /// <summary>Determines whether shuffle candidates changed.</summary>
        public bool HasShuffleCandidatesChanged()
        {
            return EditingSettings.candidates.shuffle != SavedSettings.candidates.shuffle;
        }

        /// <summary>Determines whether grid settings changed.</summary>
        public bool HasGridSettingsChanged()
        {
            return HasGridCellSizeChanged() || HasGridJitterChanged();
        }

        /// <summary>Determines whether grid cell size changed.</summary>
        public bool HasGridCellSizeChanged()
        {
            return !Mathf.Approximately(EditingSettings.grid.cellSize, SavedSettings.grid.cellSize);
        }

        /// <summary>Determines whether grid jitter changed.</summary>
        public bool HasGridJitterChanged()
        {
            return !Mathf.Approximately(EditingSettings.grid.jitterAmount, SavedSettings.grid.jitterAmount);
        }

        /// <summary>Determines whether cluster settings changed.</summary>
        public bool HasClusterSettingsChanged()
        {
            return HasClusterCountChanged() || HasClusterRadiusChanged() || HasClusterUseMinCenterDistanceChanged() || HasClusterMinCenterDistanceChanged();
        }

        /// <summary>Determines whether cluster count changed.</summary>
        public bool HasClusterCountChanged()
        {
            return EditingSettings.cluster.count != SavedSettings.cluster.count;
        }

        /// <summary>Determines whether cluster radius changed.</summary>
        public bool HasClusterRadiusChanged()
        {
            return !Mathf.Approximately(EditingSettings.cluster.radius, SavedSettings.cluster.radius);
        }

        /// <summary>Determines whether cluster use min center distance changed.</summary>
        public bool HasClusterUseMinCenterDistanceChanged()
        {
            return EditingSettings.cluster.useMinCenterDistance != SavedSettings.cluster.useMinCenterDistance;
        }

        /// <summary>Determines whether cluster min center distance changed.</summary>
        public bool HasClusterMinCenterDistanceChanged()
        {
            return EditingSettings.cluster.useMinCenterDistance && !Mathf.Approximately(EditingSettings.cluster.minCenterDistance, SavedSettings.cluster.minCenterDistance);
        }

        /// <summary>Determines whether poisson settings changed.</summary>
        public bool HasPoissonSettingsChanged()
        {
            return HasPoissonMinDistanceChanged() || HasPoissonAttemptsChanged();
        }

        /// <summary>Determines whether poisson min distance changed.</summary>
        public bool HasPoissonMinDistanceChanged()
        {
            return !Mathf.Approximately(EditingSettings.poisson.minDistance, SavedSettings.poisson.minDistance);
        }

        /// <summary>Determines whether poisson attempts changed.</summary>
        public bool HasPoissonAttemptsChanged()
        {
            return EditingSettings.poisson.attempts != SavedSettings.poisson.attempts;
        }
    }
}
