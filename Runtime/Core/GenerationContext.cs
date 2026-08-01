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
        public GenerationPerformanceMode PerformanceMode { get; }
        public PlacementTarget PlacementTargets { get; }
        public TargetDistributionMode TargetDistributionMode { get; }
        public TargetDistributionWeights TargetDistributionWeights { get; }
        public RelativePlacementSettings RelativePlacement { get; }
        public bool UseRandomSeed { get; }
        public int RandomSeed { get; }
        public bool BestEffort { get; }
        public float AreaBuildMilliseconds { get; }
        public AreaBuildProfile AreaBuildProfile { get; }
        public GenerationRandom Random { get; }
        public GenerationPlan Plan { get; }
        internal SceneObjectIndex GeneratedSceneObjects { get; }
        internal SceneObjectIndex FixedSceneObjects { get; }
        internal SurfaceFitCache SurfaceFitCache { get; }
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
            PlacementArea area,
            float areaBuildMilliseconds = 0f)
            : this(
                request,
                generatedParent,
                area,
                areaBuildMilliseconds,
                null,
                SceneObjectIndex.CollectGenerated(generatedParent),
                SceneObjectIndex.CollectFixed(request?.AreaSource, generatedParent))
        {
        }

        internal GenerationContext(
            GenerationRequest request,
            Transform generatedParent,
            PlacementArea area,
            float areaBuildMilliseconds,
            AreaBuildProfile areaBuildProfile,
            SceneObjectIndex generatedSceneObjects = null,
            SceneObjectIndex fixedSceneObjects = null)
        {
            AreaSource = request.AreaSource;
            Area = area;
            AssetPool = request.AssetPool;
            Count = request.ObjectCount;

            GenerationMode = request.GenerationMode;
            PerformanceMode = request.PerformanceMode;
            PlacementTargets = request.PlacementTargets;
            TargetDistributionMode = request.TargetDistributionMode;
            TargetDistributionWeights = request.TargetDistributionWeights;
            RelativePlacement = request.RelativePlacement ?? RelativePlacementSettings.Disabled;
            UseRandomSeed = request.UseRandomSeed;
            BestEffort = request.BestEffort;
            AreaBuildMilliseconds = Mathf.Max(0f, areaBuildMilliseconds);
            AreaBuildProfile = areaBuildProfile;
            Random = GenerationRandom.Create(request.UseRandomSeed, request.RandomSeed);
            RandomSeed = Random.Seed;
            Plan = new GenerationPlan(Count);
            SurfaceFitCache = new SurfaceFitCache(Count);

            GeneratedParent = generatedParent;
            StyleSettings = request.StyleSettings;
            GeneratedSceneObjects = generatedSceneObjects ?? SceneObjectIndex.Empty;
            FixedSceneObjects = fixedSceneObjects ?? SceneObjectIndex.Empty;
            SceneRelativeAnchors = RelativeAnchorProvider.CollectSceneAnchors(this);
            SelectedRelativeAnchors = RelativeAnchorProvider.CollectSelectedAnchors(this);
        }
    }
}
