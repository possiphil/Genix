using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Placement;
using Genix.Styles;
using UnityEngine;

namespace Genix.Core
{
    /// <summary>
    /// Owns the resolved area, deterministic random stream, scene indices, caches, and mutable plan for one run.
    /// </summary>
    /// <remarks>Contexts are per-run objects and must not be shared between concurrent generation operations.</remarks>
    public sealed class GenerationContext
    {
        /// <summary>Gets area source.</summary>
        public IAreaSource AreaSource { get; }
        /// <summary>Gets area.</summary>
        public PlacementArea Area { get; }
        /// <summary>Gets asset pool.</summary>
        public AssetPool AssetPool { get; }
        /// <summary>Gets the number of stored items.</summary>
        public int Count { get; }

        /// <summary>Gets placement targets.</summary>
        public PlacementTarget PlacementTargets { get; }
        /// <summary>Gets target distribution mode.</summary>
        public TargetDistributionMode TargetDistributionMode { get; }
        /// <summary>Gets target distribution weights.</summary>
        public TargetDistributionWeights TargetDistributionWeights { get; }
        /// <summary>Gets relative placement.</summary>
        public RelativePlacementSettings RelativePlacement { get; }
        /// <summary>Indicates whether fixed seed.</summary>
        public bool UseFixedSeed { get; }
        /// <summary>Gets random seed.</summary>
        public int RandomSeed { get; }
        /// <summary>Indicates whether best effort.</summary>
        public bool BestEffort { get; }
        /// <summary>Gets the measured area build time in milliseconds.</summary>
        public float AreaBuildMilliseconds { get; }
        /// <summary>Gets area build profile.</summary>
        public AreaBuildProfile AreaBuildProfile { get; }
        /// <summary>Gets random.</summary>
        public GenerationRandom Random { get; }
        /// <summary>Gets plan.</summary>
        public GenerationPlan Plan { get; }
        internal SceneObjectIndex GeneratedSceneObjects { get; }
        internal SceneObjectIndex FixedSceneObjects { get; }
        internal SurfaceFitCache SurfaceFitCache { get; }
        internal IReadOnlyList<RelativeAnchor> SceneRelativeAnchors { get; }
        internal IReadOnlyList<RelativeAnchor> SelectedRelativeAnchors { get; }

        /// <summary>Gets target bounds.</summary>
        public Bounds TargetBounds => Area.WorldBounds;
        /// <summary>Gets generated parent.</summary>
        public Transform GeneratedParent { get; }
        /// <summary>Gets fixed object root.</summary>
        public Transform FixedObjectRoot => AreaSource.ParentTransform;
        /// <summary>Gets style settings.</summary>
        public StyleSettings StyleSettings { get; }
        /// <summary>Gets cell size.</summary>
        public float CellSize => StyleSettings.grid.cellSize;

        /// <summary>Creates a context for callers that already own a placement area.</summary>
        /// <param name="request">Request whose generation settings are copied into the context.</param>
        /// <param name="generatedParent">Parent used for generated-object discovery and later scene application.</param>
        /// <param name="area">Resolved placement area.</param>
        /// <param name="areaBuildMilliseconds">Optional measured area-build duration.</param>
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

            PlacementTargets = request.PlacementTargets;
            TargetDistributionMode = request.TargetDistributionMode;
            TargetDistributionWeights = request.TargetDistributionWeights;
            RelativePlacement = request.RelativePlacement ?? RelativePlacementSettings.Disabled;
            UseFixedSeed = request.UseFixedSeed;
            BestEffort = request.BestEffort;
            AreaBuildMilliseconds = Mathf.Max(0f, areaBuildMilliseconds);
            AreaBuildProfile = areaBuildProfile;
            Random = GenerationRandom.Create(request.UseFixedSeed, request.RandomSeed);
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
