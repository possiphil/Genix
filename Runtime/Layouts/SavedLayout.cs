using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Layouts
{
    [CreateAssetMenu(menuName = "Genix/Layouts/Generated Layout")]
    public sealed class SavedLayout : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField] private string notes;
        [SerializeField] private bool favorite;
        [SerializeField] private bool locked;
        [SerializeField] private string targetAreaName;
        [SerializeField] private string targetAreaId;
        [SerializeField] private string sourceType;
        [SerializeField] private GenerationMode generationMode;
        [SerializeField] private PlacementTarget placementTargets = PlacementTarget.Floor;
        [SerializeField] private TargetDistributionMode targetDistributionMode = TargetDistributionMode.Random;
        [SerializeField] private TargetDistributionWeights targetDistributionWeights = TargetDistributionWeights.Default;
        [SerializeField] private AssetPool assetPool;
        [SerializeField] private string styleName;
        [SerializeField] private int objectCount;
        [SerializeField] private Bounds bounds;
        [SerializeField] private string createdAt;
        [SerializeField] private List<LayoutAssetSummary> assetSummaries = new();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public GameObject Prefab => prefab;
        public string Notes => notes;
        public bool Favorite => favorite;
        public bool Locked => locked;
        public string TargetAreaName => targetAreaName;
        public string TargetAreaId => targetAreaId;
        public string SourceType => sourceType;
        public GenerationMode GenerationMode => generationMode;
        public PlacementTarget PlacementTargets => placementTargets;
        public TargetDistributionMode TargetDistributionMode => targetDistributionMode;
        public TargetDistributionWeights TargetDistributionWeights => targetDistributionWeights;
        public AssetPool AssetPool => assetPool;
        public string StyleName => styleName;
        public int ObjectCount => objectCount;
        public Bounds Bounds => bounds;
        public string CreatedAt => createdAt;
        public IReadOnlyList<LayoutAssetSummary> AssetSummaries => assetSummaries;

        public void Initialize(
            string newDisplayName,
            GameObject newPrefab,
            string newTargetAreaName,
            string newTargetAreaId,
            string newSourceType,
            GenerationMode newGenerationMode,
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
            targetAreaName = newTargetAreaName;
            targetAreaId = newTargetAreaId;
            sourceType = newSourceType;
            generationMode = newGenerationMode;
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

        public void SetDesignerMetadata(string newDisplayName, string newNotes, bool newFavorite, bool newLocked)
        {
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? name : newDisplayName.Trim();
            notes = newNotes ?? string.Empty;
            favorite = newFavorite;
            locked = newLocked;
        }
    }
}
