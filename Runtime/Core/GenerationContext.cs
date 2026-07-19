using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Placement;
using Genix.Styles;
using UnityEngine;

namespace Genix.Core
{
    public sealed class GenerationContext
    {
        public IAreaSource AreaSource { get; }
        public PlacementArea Area { get; }
        public AssetPool AssetPool { get; }
        public int Count { get; }

        public GenerationMode GenerationMode { get; }
        public PlacementTarget PlacementTargets { get; }
        public TargetDistributionMode TargetDistributionMode { get; }
        public TargetDistributionWeights TargetDistributionWeights { get; }
        public RelativePlacementSettings RelativePlacement { get; }
        public bool UseRandomSeed { get; }
        public int RandomSeed { get; }
        public bool BestEffort { get; }
        public GenerationRandom Random { get; }
        public GenerationPlan Plan { get; }
        internal IReadOnlyList<RelativeAnchor> SceneRelativeAnchors { get; }
        internal IReadOnlyList<RelativeAnchor> SelectedRelativeAnchors { get; }

        public Bounds TargetBounds => Area.WorldBounds;
        public Transform GeneratedParent { get; }
        public Transform FixedObjectRoot => AreaSource.ParentTransform;
        public StyleSettings StyleSettings { get; }
        public float CellSize => StyleSettings.grid.cellSize;

        public GenerationContext(
            GenerationRequest request,
            Transform generatedParent,
            PlacementArea area)
        {
            AreaSource = request.AreaSource;
            Area = area;
            AssetPool = request.AssetPool;
            Count = request.ObjectCount;

            GenerationMode = request.GenerationMode;
            PlacementTargets = request.PlacementTargets;
            TargetDistributionMode = request.TargetDistributionMode;
            TargetDistributionWeights = request.TargetDistributionWeights;
            RelativePlacement = request.RelativePlacement ?? RelativePlacementSettings.Disabled;
            UseRandomSeed = request.UseRandomSeed;
            RandomSeed = request.RandomSeed;
            BestEffort = request.BestEffort;
            Random = GenerationRandom.Create(request.UseRandomSeed, request.RandomSeed);
            Plan = new GenerationPlan();

            GeneratedParent = generatedParent;
            StyleSettings = request.StyleSettings;
            SceneRelativeAnchors = RelativeAnchorProvider.CollectSceneAnchors(this);
            SelectedRelativeAnchors = RelativeAnchorProvider.CollectSelectedAnchors(this);
        }
    }
}
