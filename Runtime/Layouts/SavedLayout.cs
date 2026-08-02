using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Layouts
{
    /// <summary>Persists a generated hierarchy, descriptive metadata, and asset summary as a reusable layout.</summary>
    [CreateAssetMenu(menuName = "Genix/Layouts/Generated Layout")]
    public sealed class SavedLayout : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField] private string notes;
        [SerializeField] private bool favorite;
        [SerializeField] private bool locked;
        [SerializeField] private string sceneName;
        [SerializeField] private string scenePath;
        [SerializeField] private string targetAreaName;
        [SerializeField] private string targetAreaId;
        [SerializeField] private string sourceType;
        [SerializeField] private PlacementTarget placementTargets = PlacementTarget.Floor;
        [SerializeField] private TargetDistributionMode targetDistributionMode = TargetDistributionMode.Random;
        [SerializeField] private TargetDistributionWeights targetDistributionWeights = TargetDistributionWeights.Default;
        [SerializeField] private AssetPool assetPool;
        [SerializeField] private string styleName;
        [SerializeField] private int objectCount;
        [SerializeField] private Bounds bounds;
        [SerializeField] private string createdAt;
        [SerializeField] private List<LayoutAssetSummary> assetSummaries = new();

        /// <summary>Gets display name.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        /// <summary>Gets prefab.</summary>
        public GameObject Prefab => prefab;
        /// <summary>Gets notes.</summary>
        public string Notes => notes;
        /// <summary>Indicates whether favorite.</summary>
        public bool Favorite => favorite;
        /// <summary>Indicates whether locked.</summary>
        public bool Locked => locked;
        /// <summary>Gets scene name.</summary>
        public string SceneName => sceneName;
        /// <summary>Gets scene path.</summary>
        public string ScenePath => scenePath;
        /// <summary>Gets target area name.</summary>
        public string TargetAreaName => targetAreaName;
        /// <summary>Gets target area id.</summary>
        public string TargetAreaId => targetAreaId;
        /// <summary>Gets source type.</summary>
        public string SourceType => sourceType;
        /// <summary>Gets placement targets.</summary>
        public PlacementTarget PlacementTargets => placementTargets;
        /// <summary>Gets target distribution mode.</summary>
        public TargetDistributionMode TargetDistributionMode => targetDistributionMode;
        /// <summary>Gets target distribution weights.</summary>
        public TargetDistributionWeights TargetDistributionWeights => targetDistributionWeights;
        /// <summary>Gets asset pool.</summary>
        public AssetPool AssetPool => assetPool;
        /// <summary>Gets style name.</summary>
        public string StyleName => styleName;
        /// <summary>Gets the number of object items.</summary>
        public int ObjectCount => objectCount;
        /// <summary>Gets bounds.</summary>
        public Bounds Bounds => bounds;
        /// <summary>Gets created at.</summary>
        public string CreatedAt => createdAt;
        /// <summary>Gets asset summaries.</summary>
        public IReadOnlyList<LayoutAssetSummary> AssetSummaries => assetSummaries != null
            ? assetSummaries
            : Array.Empty<LayoutAssetSummary>();

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(
            string newDisplayName,
            GameObject newPrefab,
            string newSceneName,
            string newScenePath,
            string newTargetAreaName,
            string newTargetAreaId,
            string newSourceType,
            PlacementTarget newPlacementTargets,
            TargetDistributionMode newTargetDistributionMode,
            TargetDistributionWeights newTargetDistributionWeights,
            AssetPool newAssetPool,
            string newStyleName,
            int newObjectCount,
            Bounds newBounds,
            string newCreatedAt,
            IEnumerable<LayoutAssetSummary> newAssetSummaries)
        {
            displayName = newDisplayName;
            prefab = newPrefab;
            notes = string.Empty;
            favorite = false;
            locked = false;
            sceneName = newSceneName;
            scenePath = newScenePath;
            targetAreaName = newTargetAreaName;
            targetAreaId = newTargetAreaId;
            sourceType = newSourceType;
            placementTargets = newPlacementTargets;
            targetDistributionMode = newTargetDistributionMode;
            targetDistributionWeights = newTargetDistributionWeights;
            assetPool = newAssetPool;
            styleName = newStyleName;
            objectCount = Mathf.Max(0, newObjectCount);
            bounds = newBounds;
            createdAt = newCreatedAt;
            assetSummaries = newAssetSummaries != null
                ? new List<LayoutAssetSummary>(newAssetSummaries)
                : new List<LayoutAssetSummary>();
        }

        /// <summary>Sets designer metadata.</summary>
        public void SetDesignerMetadata(string newDisplayName, string newNotes, bool newFavorite, bool newLocked)
        {
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? name : newDisplayName.Trim();
            notes = newNotes ?? string.Empty;
            favorite = newFavorite;
            locked = newLocked;
        }
    }
}
