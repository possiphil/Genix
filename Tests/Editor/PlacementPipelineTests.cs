using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Placement.Providers;
using Genix.Profiling;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Semantics;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.PlacementArea)]
    public sealed class PlacementPipelineTests
    {
        private readonly List<UnityEngine.Object> _objects = new();
        private GameObject _areaRoot;
        private GameObject _generatedRoot;
        private AssetPool _pool;
        private StubAreaSource _areaSource;
        private PlacementArea _area;

        [SetUp]
        public void SetUp()
        {
            _areaRoot = CreateGameObject("Area Root");
            _generatedRoot = CreateGameObject("Generated Root");
            _pool = ScriptableObject.CreateInstance<AssetPool>();
            _pool.Initialize("Pool", AssetPoolMode.Static);
            _objects.Add(_pool);
            _areaSource = new StubAreaSource(_areaRoot.transform);
            AreaBuildSettings areaSettings = new(
                AreaDecompositionMode.Precise,
                ~0,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.SfsBoundaries);
            _area = new PlacementArea(
                new SpatialSourceInfo("Test", "Area", "placement-tests"),
                new Bounds(new Vector3(0f, 2.5f, 0f), new Vector3(10f, 5f, 10f)),
                new[] { SurfaceRegion.CreateFloor("Floor", -5f, 5f, -5f, 5f, 0f) },
                new[]
                {
                    SurfaceRegion.CreateWall(
                        "Wall",
                        new Vector3(-5f, 0f, -5f),
                        new Vector3(5f, 0f, -5f),
                        5f,
                        Vector3.forward)
                },
                settings: areaSettings,
                ceilingRegions: new[] { SurfaceRegion.CreateCeiling("Ceiling", -5f, 5f, -5f, 5f, 5f) });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object value in _objects)
            {
                if (value)
                    UnityEngine.Object.DestroyImmediate(value);
            }

            _objects.Clear();
            PlacementSolver.ClearCandidateCache();
            PlacementSolver.ClearSceneObjectCache();
        }

        [Test]
        public void CandidateFactoryRaisesFloorAssetByHalfItsHeight()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, new Vector3(2f, 4f, 2f));
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            CandidateSeed seed = new(Vector3.zero, Quaternion.identity, surfaceNormal: Vector3.up, placementType: PlacementType.Floor);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(candidate.Position, Is.EqualTo(new Vector3(0f, 2f, 0f)));
            Assert.That(candidate.SurfaceNormal, Is.EqualTo(Vector3.up));
            Assert.That(candidate.PlacementType, Is.EqualTo(PlacementType.Floor));
        }

        [Test]
        public void CandidateFactoryOffsetsWallByDepthAndPlacementHeight()
        {
            AssetDefinition asset = CreateAsset("Wall", PlacementType.Wall, new Vector3(2f, 3f, 4f));
            SetSerialized(asset, "placementHeight", 1.5f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Wall);
            CandidateSeed seed = new(
                new Vector3(1f, 2f, 3f),
                Quaternion.identity,
                surfaceNormal: Vector3.back,
                placementType: PlacementType.Wall);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(candidate.Position, Is.EqualTo(new Vector3(1f, 3.5f, 1f)));
            Assert.That(Vector3.Dot(candidate.Rotation * Vector3.up, Vector3.back), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(Vector3.Dot(candidate.Rotation * Vector3.forward, Vector3.back), Is.EqualTo(0f).Within(0.0001f));
        }

        [TestCase(PlacementType.Wall, 1)]
        [TestCase(PlacementType.Floor, 8)]
        [TestCase(PlacementType.InsideSpace, 8)]
        public void RotationAttemptCountMatchesPlacementPolicy(PlacementType placementType, int expected)
        {
            AssetDefinition asset = CreateAsset("Asset", placementType, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, ToTarget(placementType));

            Assert.That(CandidateFactory.GetRotationAttemptCount(context, asset, placementType), Is.EqualTo(expected));
        }

        [Test]
        public void ValidatorAcceptsContainedFloorCandidate()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                new OrientedBounds(new Vector3(0f, 0.5f, 0f), Vector3.one, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void ValidatorConvenienceOverloadsAcceptContainedCandidate()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);
            Bounds bounds = new(new Vector3(0f, 0.5f, 0f), Vector3.one);
            GenerationProfilerRecorder profiler = new();

            Assert.That(PlacementValidator.IsValidCandidate(candidate, bounds, context), Is.True);
            Assert.That(PlacementValidator.TryValidateCandidate(
                candidate,
                bounds,
                context,
                out RejectionReason noAssetReason,
                out _), Is.True);
            Assert.That(noAssetReason, Is.EqualTo(RejectionReason.None));
            Assert.That(PlacementValidator.TryValidateCandidate(
                candidate,
                bounds,
                context,
                asset,
                out RejectionReason assetReason,
                out _,
                profiler), Is.True);
            Assert.That(assetReason, Is.EqualTo(RejectionReason.None));
            Assert.That(profiler.Profile.GetTarget(PlacementType.Floor).ValidationSteps, Is.Not.Empty);
        }

        [Test]
        public void ValidatorRejectsCandidateAboveTargetHeightFirst()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(new Vector3(0f, 6f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                new OrientedBounds(new Vector3(0f, 6f, 0f), Vector3.one, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.ExceedsTargetHeight));
        }

        [Test]
        public void ValidatorRejectsFloorFootprintOutsideSurfaceRegion()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one * 2f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(new Vector3(5f, 1f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                new OrientedBounds(candidate.Position, Vector3.one * 2f, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.OutsideTargetArea));
        }

        [Test]
        public void AdaptiveValidatorReportsInsufficientSupportOutsideSurface()
        {
            AssetDefinition asset = CreateAsset("Adaptive", PlacementType.Floor, Vector3.one * 2f);
            SetSerialized(asset, "surfaceFitMode", SurfaceFitMode.Adaptive);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(new Vector3(5f, 1f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                new OrientedBounds(candidate.Position, Vector3.one * 2f, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.InsufficientSurfaceSupport));
        }

        [Test]
        public void ValidatorRejectsOverlapWithPlannedObjectAndReportsItsName()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);
            context.Plan.Add(asset, candidate, "Existing");

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, asset),
                context,
                asset,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.OverlapsGenerated));
            Assert.That(relatedName, Is.EqualTo("Existing"));
        }

        [Test]
        public void PoissonValidatorRejectsSpacingBeforePhysicalOverlap()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.BridsonPoissonDisk, poissonDistance: 3f);
            context.Plan.Add(asset, FloorCandidate(Vector3.zero), "Existing");
            PlacementCandidate candidate = FloorCandidate(new Vector3(2f, 0.5f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, asset),
                context,
                asset,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.TooCloseToGenerated));
            Assert.That(relatedName, Is.EqualTo("Existing"));
        }

        [Test]
        public void PlannedSpacingPrecheckSkipsWallsAndRejectsNearbyFloorSeeds()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.BridsonPoissonDisk, poissonDistance: 3f);
            context.Plan.Add(asset, FloorCandidate(Vector3.zero), "Existing");

            Assert.That(PlacementValidator.TryRejectByPlannedSpacing(
                new CandidateSeed(Vector3.right * 2f, Quaternion.identity, placementType: PlacementType.Wall),
                asset,
                context,
                out _), Is.False);
            Assert.That(PlacementValidator.TryRejectByPlannedSpacing(
                new CandidateSeed(Vector3.right * 2f, Quaternion.identity, placementType: PlacementType.Floor),
                asset,
                context,
                out string relatedName), Is.True);
            Assert.That(relatedName, Is.EqualTo("Existing"));
            Assert.That(PlacementValidator.TryRejectByPlannedSpacing(
                new CandidateSeed(Vector3.zero, Quaternion.identity, placementType: PlacementType.Floor),
                null,
                context,
                out _), Is.False);
        }

        [Test]
        public void ValidatorRejectsOverlapWithPreviouslyGeneratedSceneObject()
        {
            GameObject generated = GameObject.CreatePrimitive(PrimitiveType.Cube);
            generated.name = "Previous Preview";
            generated.transform.SetParent(_generatedRoot.transform);
            generated.transform.position = new Vector3(0f, 0.5f, 0f);
            _objects.Add(generated);
            Physics.SyncTransforms();
            SceneObjectIndex generatedObjects = SceneObjectIndex.CollectGenerated(_generatedRoot.transform);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                generatedObjects: generatedObjects);
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                new OrientedBounds(candidate.Position, Vector3.one, Quaternion.identity),
                context,
                null,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.OverlapsGenerated));
            Assert.That(relatedName, Is.EqualTo("Previous Preview"));
        }

        [Test]
        public void GeneratedSceneSpacingPrecheckRejectsNearbyNonOverlappingSeed()
        {
            GameObject generated = GameObject.CreatePrimitive(PrimitiveType.Cube);
            generated.name = "Nearby Preview";
            generated.transform.SetParent(_generatedRoot.transform);
            generated.transform.position = new Vector3(2f, 0.5f, 0f);
            _objects.Add(generated);
            Physics.SyncTransforms();
            SceneObjectIndex generatedObjects = SceneObjectIndex.CollectGenerated(_generatedRoot.transform);
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.BridsonPoissonDisk,
                poissonDistance: 3f,
                generatedObjects: generatedObjects);
            CandidateSeed seed = new(Vector3.zero, Quaternion.identity, placementType: PlacementType.Floor);

            Assert.That(PlacementValidator.TryRejectByGeneratedSceneSpacing(
                seed,
                asset,
                context,
                out string relatedName), Is.True);
            Assert.That(relatedName, Is.EqualTo("Nearby Preview"));
            Assert.That(PlacementValidator.TryRejectByGeneratedSceneSpacing(
                new CandidateSeed(Vector3.zero, Quaternion.identity, placementType: PlacementType.Wall),
                asset,
                context,
                out _), Is.False);
        }

        [Test]
        public void SolverRecordsEarlyPlannedSpacingRejection()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.BridsonPoissonDisk, poissonDistance: 3f);
            context.Plan.Add(asset, FloorCandidate(Vector3.zero), "Existing");
            CandidatePool pool = new(new List<CandidateSeed>
            {
                new(Vector3.right * 2f, Quaternion.identity, placementType: PlacementType.Floor)
            });
            GenerationProfilerRecorder profiler = new();

            bool found = PlacementSolver.TryGetValidCandidate(context, asset, pool, out _, profiler: profiler);

            Assert.That(found, Is.False);
            Assert.That(profiler.Profile.GetTarget(PlacementType.Floor).ValidationSteps,
                Has.Some.Matches<ValidationStepProfile>(step => step.Step == ValidationProfileStep.PlannedSpacing));
        }

        [Test]
        public void SolverRecordsEarlyGeneratedSceneSpacingRejection()
        {
            GameObject generated = GameObject.CreatePrimitive(PrimitiveType.Cube);
            generated.transform.SetParent(_generatedRoot.transform);
            generated.transform.position = new Vector3(2f, 0.5f, 0f);
            _objects.Add(generated);
            Physics.SyncTransforms();
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.BridsonPoissonDisk,
                poissonDistance: 3f,
                generatedObjects: SceneObjectIndex.CollectGenerated(_generatedRoot.transform));
            CandidatePool pool = new(new List<CandidateSeed>
            {
                new(Vector3.zero, Quaternion.identity, placementType: PlacementType.Floor)
            });
            GenerationProfilerRecorder profiler = new();

            bool found = PlacementSolver.TryGetValidCandidate(context, asset, pool, out _, profiler: profiler);

            Assert.That(found, Is.False);
            Assert.That(profiler.Profile.GetTarget(PlacementType.Floor).ValidationSteps,
                Has.Some.Matches<ValidationStepProfile>(step => step.Step == ValidationProfileStep.GeneratedSceneSpacing));
        }

        [Test]
        public void SolverRejectsOutsideInsideSpaceSeedBeforeRotationAttempts()
        {
            AssetDefinition asset = CreateAsset("Volume", PlacementType.InsideSpace, Vector3.one);
            PlacementArea volumeArea = new(
                new SpatialSourceInfo("Test", "Volume Area", "volume-area"),
                _area.WorldBounds,
                null,
                null,
                cellSize: 1f,
                subspaceCells: new[] { Vector3Int.zero });
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                PlacementTarget.InsideSpace,
                area: volumeArea);
            CandidatePool pool = new(new List<CandidateSeed>
            {
                new(new Vector3(20f, 1f, 0f), Quaternion.identity, placementType: PlacementType.InsideSpace)
            });
            GenerationProfilerRecorder profiler = new();

            bool found = PlacementSolver.TryGetValidCandidate(context, asset, pool, out _, profiler: profiler);

            Assert.That(found, Is.False);
            Assert.That(profiler.Profile.GetTarget(PlacementType.InsideSpace).ValidationSteps,
                Has.Some.Matches<ValidationStepProfile>(step => step.Step == ValidationProfileStep.Volume));
        }

        [Test]
        public void RelativePlacementUsesThreeDimensionalDistanceToSelectedAnchor()
        {
            GameObject selectedAnchor = CreateGameObject("Selected Anchor");
            selectedAnchor.transform.position = new Vector3(1f, 100f, 0f);
            RelativePlacementSettings relativePlacement = new(
                RelativePlacementSource.SelectedObjects,
                2f,
                ~0,
                new[] { selectedAnchor.transform });
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                relativePlacement: relativePlacement);
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);

            bool valid = RelativeAnchorProvider.IsCandidateInRange(
                candidate,
                context,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(relatedName, Is.Empty);
        }

        [Test]
        public void FaceTargetOrientationLooksTowardSelectedAnchor()
        {
            GameObject selectedAnchor = CreateGameObject("Selected Anchor");
            selectedAnchor.transform.position = new Vector3(3f, 0f, 0f);
            RelativePlacementSettings relativePlacement = new(
                RelativePlacementSource.SelectedObjects,
                5f,
                ~0,
                new[] { selectedAnchor.transform });
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                relativePlacement: relativePlacement);
            AssetDefinition asset = CreateAsset("Facing", PlacementType.Floor, Vector3.one);
            SetSerialized(asset, "orientationMode", Genix.Orientation.OrientationMode.FaceTarget);
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(Vector3.Dot(candidate.Rotation * Vector3.forward, Vector3.right), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ValidatorRejectsOverlapWithFixedSceneObject()
        {
            GameObject fixedObject = CreateFixedBox("Fixed Obstacle", new Vector3(0f, 0.5f, 0f));
            SceneObjectIndex fixedObjects = SceneObjectIndex.CollectFixed(
                _areaSource,
                _generatedRoot.transform,
                _area.WorldBounds,
                0f);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                fixedObjects: fixedObjects);
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                new OrientedBounds(candidate.Position, Vector3.one, Quaternion.identity),
                context,
                null,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.OverlapsFixed));
            Assert.That(relatedName, Is.EqualTo(fixedObject.GetComponent<BoxCollider>().name));
        }

        [Test]
        public void ValidatorEnforcesConfiguredFixedObjectClearance()
        {
            GameObject fixedObject = CreateFixedBox("Clearance Obstacle", new Vector3(1.6f, 0.5f, 0f));
            SceneObjectIndex fixedObjects = SceneObjectIndex.CollectFixed(
                _areaSource,
                _generatedRoot.transform,
                _area.WorldBounds,
                1f);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                placementSettings: new PlacementSettings(true, 1f),
                fixedObjects: fixedObjects);
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                new OrientedBounds(candidate.Position, Vector3.one, Quaternion.identity),
                context,
                null,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.TooCloseToFixed));
            Assert.That(relatedName, Is.EqualTo(fixedObject.GetComponent<BoxCollider>().name));
        }

        [Test]
        public void SolverSkipsMismatchedSeedAndReturnsNextValidCandidate()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            CandidatePool pool = new(new List<CandidateSeed>
            {
                new(Vector3.zero, Quaternion.identity, placementType: PlacementType.Wall),
                new(Vector3.zero, Quaternion.identity, surfaceNormal: Vector3.up, placementType: PlacementType.Floor)
            });

            bool found = PlacementSolver.TryGetValidCandidate(
                context,
                asset,
                pool,
                out PlacementCandidate candidate,
                generatedObjectName: "Generated");

            Assert.That(found, Is.True);
            Assert.That(candidate.PlacementType, Is.EqualTo(PlacementType.Floor));
            Assert.That(candidate.Position.y, Is.EqualTo(0.5f));
        }

        [Test]
        public void SolverReturnsFalseForEmptyPool()
        {
            AssetDefinition asset = CreateAsset("Floor", PlacementType.Floor, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            Assert.That(
                PlacementSolver.TryGetValidCandidate(context, asset, new CandidatePool(new List<CandidateSeed>()), out _),
                Is.False);
        }

        [Test]
        public void InsideSpaceProviderCreatesContainedVolumeSeeds()
        {
            GenerationContext context = CreateContext(SamplingAlgorithm.Grid, PlacementTarget.InsideSpace);
            InsideSpaceCandidateProvider provider = new(candidateCount: 12);

            List<CandidateSeed> seeds = provider.CreateCandidateSeeds(context);

            Assert.That(seeds, Is.Not.Empty);
            Assert.That(seeds, Has.All.Matches<CandidateSeed>(seed =>
                seed.PlacementType == PlacementType.InsideSpace && context.TargetBounds.Contains(seed.Position)));
        }

        [TestCase(SamplingAlgorithm.Random)]
        [TestCase(SamplingAlgorithm.Grid)]
        [TestCase(SamplingAlgorithm.JitteredGrid)]
        [TestCase(SamplingAlgorithm.Cluster)]
        [TestCase(SamplingAlgorithm.BridsonPoissonDisk)]
        public void FloorProviderProjectsSeedsOntoExplicitRegion(SamplingAlgorithm algorithm)
        {
            GenerationContext context = CreateContext(algorithm);
            HorizontalSurfaceCandidateProvider provider = new(candidateCount: 12);

            List<CandidateSeed> seeds = provider.CreateCandidateSeeds(context);

            Assert.That(seeds, Is.Not.Empty);
            Assert.That(seeds, Has.All.Matches<CandidateSeed>(seed =>
                seed.PlacementType == PlacementType.Floor &&
                Mathf.Abs(seed.Position.y) < 0.0001f &&
                seed.SurfaceNormal == Vector3.up));
        }

        [TestCase(SamplingAlgorithm.Random)]
        [TestCase(SamplingAlgorithm.Grid)]
        [TestCase(SamplingAlgorithm.JitteredGrid)]
        [TestCase(SamplingAlgorithm.Cluster)]
        [TestCase(SamplingAlgorithm.BridsonPoissonDisk)]
        public void CeilingProviderProjectsSeedsOntoExplicitRegion(SamplingAlgorithm algorithm)
        {
            GenerationContext context = CreateContext(algorithm, PlacementTarget.Ceiling);
            CeilingCandidateProvider provider = new(candidateCount: 12);

            List<CandidateSeed> seeds = provider.CreateCandidateSeeds(context);

            Assert.That(seeds, Is.Not.Empty);
            Assert.That(seeds, Has.All.Matches<CandidateSeed>(seed =>
                seed.PlacementType == PlacementType.Ceiling &&
                Mathf.Abs(seed.Position.y - 5f) < 0.0001f &&
                seed.SurfaceNormal == Vector3.down));
        }

        [TestCase(SamplingAlgorithm.Random)]
        [TestCase(SamplingAlgorithm.Grid)]
        [TestCase(SamplingAlgorithm.JitteredGrid)]
        [TestCase(SamplingAlgorithm.Cluster)]
        [TestCase(SamplingAlgorithm.BridsonPoissonDisk)]
        public void WallProviderProjectsSeedsOntoExplicitRegion(SamplingAlgorithm algorithm)
        {
            GenerationContext context = CreateContext(algorithm, PlacementTarget.Wall);
            WallCandidateProvider provider = new(candidateCount: 12);

            List<CandidateSeed> seeds = provider.CreateCandidateSeeds(context);

            Assert.That(seeds, Is.Not.Empty);
            Assert.That(seeds, Has.All.Matches<CandidateSeed>(seed =>
                seed.PlacementType == PlacementType.Wall &&
                Mathf.Abs(seed.Position.z + 5f) < 0.0001f &&
                seed.SurfaceNormal == Vector3.forward));
        }

        [Test]
        public void CombinedProviderPreservesEachRequestedPlacementType()
        {
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Grid,
                PlacementTarget.All);
            PlacementTargetCandidateProvider provider = new(
                PlacementTarget.All,
                candidateCount: 40);

            List<CandidateSeed> seeds = provider.CreateCandidateSeeds(context);

            Assert.That(seeds.Exists(seed => seed.PlacementType == PlacementType.Floor), Is.True);
            Assert.That(seeds.Exists(seed => seed.PlacementType == PlacementType.Wall), Is.True);
            Assert.That(seeds.Exists(seed => seed.PlacementType == PlacementType.Ceiling), Is.True);
            Assert.That(seeds.Exists(seed => seed.PlacementType == PlacementType.InsideSpace), Is.True);
        }

        [Test]
        public void CandidateSeedFactoryCreatesLazyRandomPoolOnDemand()
        {
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.InsideSpace);
            CandidatePool pool = CandidateSeedFactory.CreatePool(
                context,
                NullDiagnosticsSink.Instance,
                PlacementTarget.InsideSpace);

            Assert.That(pool.Count, Is.GreaterThan(0));
            Assert.That(pool.TryTakeNext(out CandidateSeed seed), Is.True);
            Assert.That(seed.PlacementType, Is.EqualTo(PlacementType.InsideSpace));
        }

        private GenerationContext CreateContext(
            SamplingAlgorithm algorithm,
            PlacementTarget targets = PlacementTarget.Floor,
            float poissonDistance = 1f,
            RelativePlacementSettings relativePlacement = null,
            PlacementSettings placementSettings = default,
            SceneObjectIndex fixedObjects = null,
            SceneObjectIndex generatedObjects = null,
            PlacementArea area = null)
        {
            StyleSettings style = new(
                string.Empty,
                algorithm,
                placementSettings,
                new CandidateSettings(2, 1, false),
                new GridSettings(1f, 0f),
                new ClusterSettings(2, 1f),
                new PoissonSettings(poissonDistance, 30));
            GenerationRequest request = new(
                _areaSource,
                _pool,
                10,
                targets,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                style,
                default,
                relativePlacement,
                useFixedSeed: true,
                randomSeed: 123);
            return new GenerationContext(
                request,
                _generatedRoot.transform,
                area ?? _area,
                0f,
                null,
                generatedObjects ?? SceneObjectIndex.Empty,
                fixedObjects ?? SceneObjectIndex.Empty);
        }

        private AssetDefinition CreateAsset(string name, PlacementType placementType, Vector3 size)
        {
            GameObject prefab = CreateGameObject(name + " Prefab");
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.name = name;
            asset.Initialize(prefab, size);
            SetSerialized(asset, "placementType", placementType);
            _pool.AddStaticAsset(asset);
            _objects.Add(asset);
            return asset;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject value = new(name);
            _objects.Add(value);
            return value;
        }

        private GameObject CreateFixedBox(string name, Vector3 position)
        {
            GameObject value = CreateGameObject(name);
            value.transform.position = position;
            value.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            return value;
        }

        private static PlacementCandidate FloorCandidate(Vector3 surfacePosition) => new(
            surfacePosition + Vector3.up * 0.5f,
            Quaternion.identity,
            surfaceNormal: Vector3.up,
            placementType: PlacementType.Floor);

        private static PlacementTarget ToTarget(PlacementType placementType) => placementType switch
        {
            PlacementType.Wall => PlacementTarget.Wall,
            PlacementType.Ceiling => PlacementTarget.Ceiling,
            PlacementType.InsideSpace => PlacementTarget.InsideSpace,
            _ => PlacementTarget.Floor
        };

        private static void SetSerialized<T>(AssetDefinition asset, string propertyName, T value)
        {
            SerializedObject serialized = new(asset);
            SerializedProperty property = serialized.FindProperty(propertyName);

            switch (value)
            {
                case Enum enumValue:
                    property.enumValueIndex = Convert.ToInt32(enumValue);
                    break;
                case float floatValue:
                    property.floatValue = floatValue;
                    break;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class StubAreaSource : IAreaSource
        {
            public SpatialSourceInfo SourceInfo { get; } = new("Test", "Area", "placement-tests");
            public Transform ParentTransform { get; }
            public IReadOnlyList<SemanticTag> SemanticTags => Array.Empty<SemanticTag>();
            public IReadOnlyList<TagCategory> AnyTagCategories => Array.Empty<TagCategory>();

            public StubAreaSource(Transform parentTransform)
            {
                ParentTransform = parentTransform;
            }

            public bool IsSourceCollider(Collider collider) => false;

            public bool TryBuildArea(AreaBuildSettings settings, out PlacementArea area, out string error)
            {
                area = null;
                error = "Not used.";
                return false;
            }
        }
    }
}
