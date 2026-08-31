using System;
using System.Collections.Generic;
using System.Linq;
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
        public void WallCandidateOffsetsByDepthHeightAndPlacementHeight()
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

            Assert.That(candidate.Position, Is.EqualTo(new Vector3(1f, 5f, 1f)));
            Assert.That(Vector3.Dot(candidate.Rotation * Vector3.forward, Vector3.back), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(Vector3.Dot(candidate.Rotation * Vector3.up, Vector3.up), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CandidateFactoryUsesCorrectedBoundsForSurfaceOffset()
        {
            AssetDefinition asset = CreateAsset("Sideways Floor", PlacementType.Floor, new Vector3(2f, 3f, 4f));
            asset.SetPrefabRotationOffset(new Vector3(90f, 0f, 0f));
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Floor);
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);
            OrientedBounds bounds = CandidateFactory.GetBounds(candidate, asset);

            Assert.That(asset.BoundsSize.y, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(candidate.Position.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(bounds.ToAxisAlignedBounds().min.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(candidate.Rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void CandidateFactoryPlacesZeroHeightWallAssetFlushWithBaseline()
        {
            AssetDefinition asset = CreateAsset("Wall", PlacementType.Wall, new Vector3(2f, 3f, 4f));
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Wall);
            CandidateSeed seed = new(
                new Vector3(1f, 2f, 3f),
                Quaternion.identity,
                surfaceNormal: Vector3.back,
                placementType: PlacementType.Wall);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(candidate.Position.y, Is.EqualTo(3.5f));
        }

        [Test]
        public void CandidateFactoryAppliesSurfaceSinkWithoutAdaptiveFit()
        {
            AssetDefinition asset = CreateAsset("Strict Wall", PlacementType.Wall, new Vector3(2f, 3f, 0.4f));
            SetSerialized(asset, "surfaceSinkOffset", 0.05f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Wall);
            CandidateSeed seed = new(
                new Vector3(1f, 2f, 3f),
                Quaternion.identity,
                surfaceNormal: Vector3.back,
                placementType: PlacementType.Wall);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(candidate.Position.z, Is.EqualTo(2.85f).Within(0.0001f));
            Assert.That(candidate.HasSurfaceFit, Is.False);
        }

        [Test]
        public void ValidatorIgnoresConfiguredSurfaceSinkForTargetContainment()
        {
            AssetDefinition asset = CreateAsset("Sunk Wall", PlacementType.Wall, new Vector3(2f, 3f, 0.4f));
            SetSerialized(asset, "surfaceSinkOffset", 0.05f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Wall);
            CandidateSeed seed = new(
                new Vector3(0f, 2f, context.TargetBounds.max.z),
                Quaternion.identity,
                surfaceNormal: Vector3.back,
                placementType: PlacementType.Wall);
            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, asset),
                context,
                asset,
                out RejectionReason reason,
                out _);

            Assert.That(reason, Is.Not.EqualTo(RejectionReason.OutsideTargetVolume));
            Assert.That(reason, Is.Not.EqualTo(RejectionReason.ExceedsTargetHeight));
            Assert.That(valid || reason != RejectionReason.OutsideTargetVolume, Is.True);
        }

        [Test]
        public void CandidateFactoryAppliesAdaptiveWallFitAcrossTheWallFootprint()
        {
            GameObject wall = CreateGameObject("Adaptive Wall Surface");
            wall.transform.position = new Vector3(0f, 2.5f, -5.25f);
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(8f, 5f, 0.5f);
            Physics.SyncTransforms();
            AssetDefinition asset = CreateAsset("Adaptive Wall", PlacementType.Wall, new Vector3(2f, 2f, 0.5f));
            SetSerialized(asset, "surfaceFitMode", SurfaceFitMode.Adaptive);
            SetSerialized(asset, "minSurfaceSupport", 1f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Wall);
            CandidateSeed seed = new(
                new Vector3(0f, 1.5f, -5f),
                Quaternion.LookRotation(Vector3.forward, Vector3.up),
                collider,
                Vector3.forward,
                placementType: PlacementType.Wall);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(candidate.HasSurfaceFit, Is.True);
            Assert.That(candidate.SurfaceFit.SupportRatio, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(candidate.Position.z, Is.EqualTo(-4.75f).Within(0.001f));
        }

        [Test]
        public void ValidatorRejectsAdaptiveWallWithoutFootprintSupport()
        {
            GameObject wall = CreateGameObject("Narrow Adaptive Wall Surface");
            wall.transform.position = new Vector3(0f, 2.5f, -5.25f);
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.5f, 5f, 0.5f);
            Physics.SyncTransforms();
            AssetDefinition asset = CreateAsset("Unsupported Adaptive Wall", PlacementType.Wall, new Vector3(2f, 2f, 0.5f));
            SetSerialized(asset, "surfaceFitMode", SurfaceFitMode.Adaptive);
            SetSerialized(asset, "minSurfaceSupport", 1f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Wall);
            CandidateSeed seed = new(
                new Vector3(0f, 1.5f, -5f),
                Quaternion.LookRotation(Vector3.forward, Vector3.up),
                collider,
                Vector3.forward,
                placementType: PlacementType.Wall);
            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, asset),
                context,
                asset,
                out RejectionReason reason,
                out _);

            Assert.That(candidate.HasSurfaceFit, Is.False);
            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.InsufficientSurfaceSupport));
        }

        [Test]
        public void AdaptiveWallValidationRechecksTheFinalAlignedFootprint()
        {
            GameObject wall = CreateGameObject("Narrow Final Wall Surface");
            wall.transform.position = new Vector3(0f, 2.5f, -5.25f);
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.5f, 5f, 0.5f);
            Physics.SyncTransforms();
            AssetDefinition asset = CreateAsset(
                "Final Footprint Wall",
                PlacementType.Wall,
                new Vector3(2f, 2f, 0.5f));
            SetSerialized(asset, "surfaceFitMode", SurfaceFitMode.Adaptive);
            SetSerialized(asset, "minSurfaceSupport", 1f);
            PlacementCandidate candidate = new(
                new Vector3(0f, 2.5f, -4.75f),
                Quaternion.LookRotation(Vector3.forward, Vector3.up),
                collider,
                Vector3.forward,
                placementType: PlacementType.Wall,
                hasSurfaceFit: true,
                surfaceFit: new SurfaceFitResult(
                    new Vector3(0f, 2.5f, -5f),
                    Vector3.forward,
                    0f,
                    1f));

            bool supported = _area.ContainsPlacementFootprint(candidate, asset);

            Assert.That(supported, Is.False);
        }

        [Test]
        public void FixedWallHeightUsesAssetBottomAboveTargetMinimum()
        {
            AssetDefinition asset = CreateAsset("Fixed Wall", PlacementType.Wall, new Vector3(2f, 2f, 0.5f));
            SetSerialized(asset, "wallVerticalPlacementMode", WallVerticalPlacementMode.FixedHeight);
            SetSerialized(asset, "placementHeight", 1.25f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Wall);
            CandidateSeed lowSeed = new(
                new Vector3(-2f, 0f, -5f),
                Quaternion.identity,
                surfaceNormal: Vector3.forward,
                placementType: PlacementType.Wall);
            CandidateSeed highSeed = new(
                new Vector3(2f, 4f, -5f),
                Quaternion.identity,
                surfaceNormal: Vector3.forward,
                placementType: PlacementType.Wall);

            PlacementCandidate low = CandidateFactory.Create(lowSeed, context, asset, 0, 1, 0f);
            PlacementCandidate high = CandidateFactory.Create(highSeed, context, asset, 0, 1, 0f);

            float expectedCenterY = context.TargetBounds.min.y + 1.25f + asset.Height * 0.5f;
            Assert.That(low.Position.y, Is.EqualTo(expectedCenterY).Within(0.0001f));
            Assert.That(high.Position.y, Is.EqualTo(expectedCenterY).Within(0.0001f));
        }

        [Test]
        public void WallHeightRangeStaysBoundedAcrossRotations()
        {
            AssetDefinition asset = CreateAsset("Ranged Wall", PlacementType.Wall, new Vector3(2f, 1f, 0.5f));
            SetSerialized(asset, "wallVerticalPlacementMode", WallVerticalPlacementMode.HeightRange);
            SetSerialized(asset, "wallMinHeight", 1f);
            SetSerialized(asset, "wallMaxHeight", 3f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Wall);
            CandidateSeed seed = new(
                new Vector3(1.5f, 2f, -5f),
                Quaternion.identity,
                surfaceNormal: Vector3.forward,
                placementType: PlacementType.Wall);

            PlacementCandidate first = CandidateFactory.Create(seed, context, asset, 0, 8, 15f);
            PlacementCandidate second = CandidateFactory.Create(seed, context, asset, 1, 8, 15f);
            float bottomY = first.Position.y - asset.Height * 0.5f;

            Assert.That(bottomY, Is.InRange(context.TargetBounds.min.y + 1f, context.TargetBounds.min.y + 3f));
            Assert.That(second.Position.y, Is.EqualTo(first.Position.y).Within(0.0001f));
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
        public void WallRandomRollUsesMultipleFlushRotationVariants()
        {
            AssetDefinition asset = CreateAsset("Wall", PlacementType.Wall, new Vector3(2f, 3f, 0.4f));
            SetSerialized(asset, "randomRollRotation", true);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Wall);
            CandidateSeed seed = new(
                new Vector3(1f, 2f, 3f),
                Quaternion.LookRotation(Vector3.back, Vector3.up),
                surfaceNormal: Vector3.back,
                placementType: PlacementType.Wall);

            int rotationCount = CandidateFactory.GetRotationAttemptCount(context, asset, PlacementType.Wall);
            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, rotationCount, 90f);
            Bounds bounds = CandidateFactory.GetBounds(candidate, asset).ToAxisAlignedBounds();

            Assert.That(rotationCount, Is.EqualTo(8));
            Assert.That(Vector3.Dot(candidate.Rotation * Vector3.forward, Vector3.back), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(Mathf.Abs(Vector3.Dot(candidate.Rotation * Vector3.up, Vector3.up)), Is.LessThan(0.0001f));
            Assert.That(bounds.min.y, Is.EqualTo(seed.Position.y).Within(0.0001f));
        }

        [Test]
        public void WallAssetRelativeFacingDoesNotDisableRandomRoll()
        {
            AssetDefinition anchor = CreateAsset("Wall Relation Anchor", PlacementType.Wall, Vector3.one);
            AssetDefinition asset = CreateAsset("Related Wall Asset", PlacementType.Wall, Vector3.one);
            SetSerialized(asset, "randomRollRotation", true);
            asset.AssetRelativePlacement.ConfigureAsset(
                anchor,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                3f,
                AssetRelativeFacing.Toward);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, PlacementTarget.Wall);

            Assert.That(
                CandidateFactory.GetRotationAttemptCount(context, asset, PlacementType.Wall),
                Is.EqualTo(8));
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
        public void ValidatorAcceptsCandidateWithinTargetHeightTolerance()
        {
            Bounds targetBounds = new(new Vector3(0f, 2.5f, 0f), new Vector3(10f, 5f, 10f));
            Bounds candidateBounds = new(
                new Vector3(0f, 0.5f - 0.0001f, 0f),
                Vector3.one);

            Assert.That(PlacementValidator.FitsTargetHeight(candidateBounds, targetBounds), Is.True);
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
        public void ValidatorUsesThreeDimensionalPoissonSpacingForWalls()
        {
            AssetDefinition asset = CreateAsset("Wall", PlacementType.Wall, Vector3.one);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.BridsonPoissonDisk,
                PlacementTarget.Wall,
                poissonDistance: 1.2f);
            PlacementCandidate existing = WallCandidate(new Vector3(0f, 1f, -4.5f));
            context.Plan.Add(asset, existing, "Existing Wall Object");
            PlacementCandidate verticallySeparated = WallCandidate(new Vector3(0f, 3f, -4.5f));

            bool valid = PlacementValidator.TryValidateCandidate(
                verticallySeparated,
                new OrientedBounds(verticallySeparated.Position, Vector3.one, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void ValidatorRejectsWallCandidateWithin3DPoissonDistance()
        {
            AssetDefinition asset = CreateAsset("Wall", PlacementType.Wall, Vector3.one);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.BridsonPoissonDisk,
                PlacementTarget.Wall,
                poissonDistance: 1.2f);
            context.Plan.Add(asset, WallCandidate(new Vector3(0f, 1f, -4.5f)), "Existing Wall Object");
            PlacementCandidate nearby = WallCandidate(new Vector3(0f, 1.75f, -4.5f));

            bool valid = PlacementValidator.TryValidateCandidate(
                nearby,
                new OrientedBounds(nearby.Position, Vector3.one, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.TooCloseToGenerated));
            Assert.That(relatedName, Is.EqualTo("Existing Wall Object"));
        }

        [Test]
        public void ValidatorUsesThreeDimensionalPoissonSpacingForInsideSpace()
        {
            AssetDefinition asset = CreateAsset("Volume", PlacementType.InsideSpace, Vector3.one);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.BridsonPoissonDisk,
                PlacementTarget.InsideSpace,
                poissonDistance: 1.2f);
            context.Plan.Add(asset, InsideSpaceCandidate(new Vector3(0f, 1f, 0f)), "Existing Volume Object");
            PlacementCandidate verticallySeparated = InsideSpaceCandidate(new Vector3(0f, 3f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                verticallySeparated,
                new OrientedBounds(verticallySeparated.Position, Vector3.one, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void ValidatorRejectsInsideSpaceCandidateWithin3DPoissonDistance()
        {
            AssetDefinition asset = CreateAsset("Volume", PlacementType.InsideSpace, Vector3.one);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.BridsonPoissonDisk,
                PlacementTarget.InsideSpace,
                poissonDistance: 1.2f);
            context.Plan.Add(asset, InsideSpaceCandidate(new Vector3(0f, 1f, 0f)), "Existing Volume Object");
            PlacementCandidate nearby = InsideSpaceCandidate(new Vector3(0f, 1.75f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                nearby,
                new OrientedBounds(nearby.Position, Vector3.one, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.TooCloseToGenerated));
            Assert.That(relatedName, Is.EqualTo("Existing Volume Object"));
        }

        [Test]
        public void InsideSpaceSpacingUses3DDistance()
        {
            GameObject generated = GameObject.CreatePrimitive(PrimitiveType.Cube);
            generated.name = "Previous Volume Object";
            generated.transform.SetParent(_generatedRoot.transform);
            generated.transform.position = new Vector3(0f, 1f, 0f);
            _objects.Add(generated);
            Physics.SyncTransforms();
            AssetDefinition asset = CreateAsset("Volume", PlacementType.InsideSpace, Vector3.one);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.BridsonPoissonDisk,
                PlacementTarget.InsideSpace,
                poissonDistance: 1.2f,
                generatedObjects: SceneObjectIndex.CollectGenerated(_generatedRoot.transform));

            Assert.That(PlacementValidator.TryRejectByGeneratedSceneSpacing(
                new CandidateSeed(
                    new Vector3(0f, 3f, 0f),
                    Quaternion.identity,
                    placementType: PlacementType.InsideSpace),
                asset,
                context,
                out _), Is.False);
            Assert.That(PlacementValidator.TryRejectByGeneratedSceneSpacing(
                new CandidateSeed(
                    new Vector3(0f, 1.75f, 0f),
                    Quaternion.identity,
                    placementType: PlacementType.InsideSpace),
                asset,
                context,
                out string relatedName), Is.True);
            Assert.That(relatedName, Is.EqualTo("Previous Volume Object"));
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
        public void RelativePlacementUses3DDistanceToSelectedAnchor()
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
        public void RelativePlacementMatchesAssetInLocalFrontSector()
        {
            AssetDefinition desk = CreateAsset("Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Front,
                0.5f,
                3f,
                AssetRelativeFacing.Any);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(
                desk,
                new PlacementCandidate(
                    new Vector3(0f, 0.5f, 0f),
                    Quaternion.Euler(0f, 90f, 0f),
                    surfaceNormal: Vector3.up,
                    placementType: PlacementType.Floor),
                "Generated Desk");
            PlacementCandidate candidate = FloorCandidate(new Vector3(2f, 0f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
            Assert.That(relatedName, Is.EqualTo("Generated Desk"));
        }

        [Test]
        public void RelativePlacementReportsWrongSideForRotatedAnchor()
        {
            AssetDefinition desk = CreateAsset("Side Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Side Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Front,
                0f,
                3f,
                AssetRelativeFacing.Any);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(
                desk,
                new PlacementCandidate(
                    new Vector3(0f, 0.5f, 0f),
                    Quaternion.Euler(0f, 90f, 0f),
                    surfaceNormal: Vector3.up,
                    placementType: PlacementType.Floor),
                "Rotated Desk");
            PlacementCandidate candidate = FloorCandidate(new Vector3(0f, 0f, 2f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.WrongAssetRelationSide));
        }

        [Test]
        public void AssetRelativePlacementAcceptsAnySelectedLocalSide()
        {
            AssetDefinition desk = CreateAsset("Multi Side Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition bin = CreateAsset("Multi Side Bin", PlacementType.Floor, Vector3.one);
            bin.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Left,
                0f,
                3f,
                AssetRelativeFacing.Any);
            bin.AssetRelativePlacement.SetSides(new[]
            {
                AssetRelativeSide.Left,
                AssetRelativeSide.Right
            });
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(desk, FloorCandidate(Vector3.zero), "Desk");
            PlacementCandidate left = FloorCandidate(new Vector3(-2f, 0f, 0f));
            PlacementCandidate right = FloorCandidate(new Vector3(2f, 0f, 0f));
            PlacementCandidate front = FloorCandidate(new Vector3(0f, 0f, 2f));

            Assert.That(PlacementValidator.TryValidateCandidate(
                left,
                CandidateFactory.GetBounds(left, bin),
                context,
                bin,
                out _,
                out _), Is.True);
            Assert.That(PlacementValidator.TryValidateCandidate(
                right,
                CandidateFactory.GetBounds(right, bin),
                context,
                bin,
                out _,
                out _), Is.True);
            Assert.That(PlacementValidator.TryValidateCandidate(
                front,
                CandidateFactory.GetBounds(front, bin),
                context,
                bin,
                out RejectionReason reason,
                out _), Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.WrongAssetRelationSide));
        }

        [Test]
        public void AssetRelativePlacementSupportsAboveAndBelowSectors()
        {
            AssetDefinition desk = CreateAsset("Vertical Anchor", PlacementType.Floor, Vector3.one);
            AssetDefinition dependent = CreateAsset("Vertical Dependent", PlacementType.Floor, Vector3.one);
            dependent.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Above,
                0f,
                10f,
                AssetRelativeFacing.Any);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(desk, FloorCandidate(Vector3.zero), "Vertical Anchor");
            PlacementCandidate above = FloorCandidate(new Vector3(0f, 2f, 0f));
            PlacementCandidate below = FloorCandidate(new Vector3(0f, -2f, 0f));

            bool aboveValid = RelativeAnchorProvider.TryValidateCandidate(
                above,
                CandidateFactory.GetBounds(above, dependent),
                dependent,
                context,
                out RejectionReason aboveReason,
                out _);
            bool belowValid = RelativeAnchorProvider.TryValidateCandidate(
                below,
                CandidateFactory.GetBounds(below, dependent),
                dependent,
                context,
                out RejectionReason belowReason,
                out _);

            Assert.That(aboveValid, Is.True);
            Assert.That(aboveReason, Is.EqualTo(RejectionReason.None));
            Assert.That(belowValid, Is.False);
            Assert.That(belowReason, Is.EqualTo(RejectionReason.WrongAssetRelationSide));

            dependent.AssetRelativePlacement.SetSides(new[] { AssetRelativeSide.Below });
            Assert.That(RelativeAnchorProvider.TryValidateCandidate(
                below,
                CandidateFactory.GetBounds(below, dependent),
                dependent,
                context,
                out _,
                out _), Is.True);
        }

        [Test]
        public void HorizontalAssetRelationSidesIgnoreVerticalOffset()
        {
            AssetDefinition desk = CreateAsset("Raised Anchor", PlacementType.Floor, Vector3.one);
            AssetDefinition dependent = CreateAsset("Horizontal Dependent", PlacementType.Floor, Vector3.one);
            dependent.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Front,
                0f,
                10f,
                AssetRelativeFacing.Any);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate raisedAnchor = new(
                new Vector3(0f, 3.5f, 0f),
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor);
            context.Plan.Add(desk, raisedAnchor, "Raised Anchor");
            PlacementCandidate frontAndBelow = FloorCandidate(new Vector3(0f, 0f, 2f));

            bool valid = RelativeAnchorProvider.TryValidateCandidate(
                frontAndBelow,
                CandidateFactory.GetBounds(frontAndBelow, dependent),
                dependent,
                context,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void RelativePlacementLimitsDependentsPerNearestAnchor()
        {
            AssetDefinition desk = CreateAsset("Capacity Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition bin = CreateAsset("Capacity Bin", PlacementType.Floor, Vector3.one);
            bin.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                3f,
                AssetRelativeFacing.Any);
            bin.AssetRelativePlacement.SetPerAnchorLimit(true, 1);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(desk, FloorCandidate(new Vector3(-4f, 0f, 0f)), "Left Desk");
            context.Plan.Add(desk, FloorCandidate(new Vector3(4f, 0f, 0f)), "Right Desk");
            PlacementCandidate first = FloorCandidate(new Vector3(-2f, 0f, 0f));
            context.Plan.Add(bin, first, "Left Bin");
            PlacementCandidate secondAtLeft = FloorCandidate(new Vector3(-4f, 0f, 2.5f));
            PlacementCandidate firstAtRight = FloorCandidate(new Vector3(2f, 0f, 0f));

            bool secondLeftValid = PlacementValidator.TryValidateCandidate(
                secondAtLeft,
                CandidateFactory.GetBounds(secondAtLeft, bin),
                context,
                bin,
                out RejectionReason leftReason,
                out string relatedName);
            bool firstRightValid = PlacementValidator.TryValidateCandidate(
                firstAtRight,
                CandidateFactory.GetBounds(firstAtRight, bin),
                context,
                bin,
                out RejectionReason rightReason,
                out _);

            Assert.That(secondLeftValid, Is.False);
            Assert.That(leftReason, Is.EqualTo(RejectionReason.AssetRelationAnchorCapacityReached));
            Assert.That(relatedName, Is.EqualTo("Left Desk"));
            Assert.That(firstRightValid, Is.True);
            Assert.That(rightReason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void AssetRelativePlacementKeepsPerAnchorLimitAcrossRuns()
        {
            AssetDefinition desk = CreateAsset("Persistent Capacity Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition bin = CreateAsset("Persistent Capacity Bin", PlacementType.Floor, Vector3.one);
            bin.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                3f,
                AssetRelativeFacing.Any);
            bin.AssetRelativePlacement.SetPerAnchorLimit(true, 1);

            GameObject leftObject = CreateGameObject("Persistent Left Desk");
            leftObject.transform.position = new Vector3(-4f, 0.5f, 0f);
            AssetRelationAnchor leftAnchor = leftObject.AddComponent<AssetRelationAnchor>();
            leftAnchor.SetRepresentedAsset(desk);
            leftAnchor.SetCustomBounds(true, Vector3.zero, Vector3.one);
            GameObject rightObject = CreateGameObject("Persistent Right Desk");
            rightObject.transform.position = new Vector3(4f, 0.5f, 0f);
            AssetRelationAnchor rightAnchor = rightObject.AddComponent<AssetRelationAnchor>();
            rightAnchor.SetRepresentedAsset(desk);
            rightAnchor.SetCustomBounds(true, Vector3.zero, Vector3.one);

            GameObject existingBin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(existingBin);
            existingBin.name = "Existing Left Bin";
            existingBin.transform.SetParent(_generatedRoot.transform);
            existingBin.transform.position = new Vector3(-2f, 0.5f, 0f);
            existingBin.AddComponent<Genix.Layouts.GeneratedObjectMetadata>()
                .Initialize(PlacementType.Floor, sourceAsset: bin);
            SceneObjectIndex generated = SceneObjectIndex.CollectGenerated(_generatedRoot.transform);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                generatedObjects: generated);
            PlacementCandidate secondAtLeft = FloorCandidate(new Vector3(-4f, 0f, 2.5f));
            PlacementCandidate firstAtRight = FloorCandidate(new Vector3(2f, 0f, 0f));

            Assert.That(PlacementValidator.TryValidateCandidate(
                secondAtLeft,
                CandidateFactory.GetBounds(secondAtLeft, bin),
                context,
                bin,
                out RejectionReason leftReason,
                out _), Is.False);
            Assert.That(leftReason, Is.EqualTo(RejectionReason.AssetRelationAnchorCapacityReached));
            Assert.That(PlacementValidator.TryValidateCandidate(
                firstAtRight,
                CandidateFactory.GetBounds(firstAtRight, bin),
                context,
                bin,
                out RejectionReason rightReason,
                out _), Is.True);
            Assert.That(rightReason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void AssetRelativePlacementBetweenAllowsMaximumAndRejectsOverflow()
        {
            AssetDefinition desk = CreateAsset("Between Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition monitor = CreateAsset("Between Monitor", PlacementType.Floor, Vector3.one * 0.25f);
            monitor.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                10f,
                AssetRelativeFacing.Any);
            monitor.AssetRelativePlacement.SetCardinalityRange(1, 2);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(desk, FloorCandidate(Vector3.zero), "Desk");
            context.Plan.Add(monitor, FloorCandidate(new Vector3(-1f, 0f, 0f)), "First Monitor");

            PlacementCandidate second = FloorCandidate(new Vector3(1f, 0f, 0f));
            Assert.That(PlacementValidator.TryValidateCandidate(
                second,
                CandidateFactory.GetBounds(second, monitor),
                context,
                monitor,
                out RejectionReason secondReason,
                out _), Is.True);
            Assert.That(secondReason, Is.EqualTo(RejectionReason.None));
            context.Plan.Add(monitor, second, "Second Monitor");

            PlacementCandidate third = FloorCandidate(new Vector3(0f, 0f, 2f));
            Assert.That(PlacementValidator.TryValidateCandidate(
                third,
                CandidateFactory.GetBounds(third, monitor),
                context,
                monitor,
                out RejectionReason thirdReason,
                out _), Is.False);
            Assert.That(thirdReason, Is.EqualTo(RejectionReason.AssetRelationAnchorCapacityReached));
        }

        [Test]
        public void SceneRelationAnchorForwardYawOffsetDoesNotRotateSceneObject()
        {
            GameObject anchorObject = CreateGameObject("Offset Anchor");
            Quaternion originalRotation = Quaternion.Euler(0f, 30f, 0f);
            anchorObject.transform.rotation = originalRotation;
            AssetRelationAnchor anchor = anchorObject.AddComponent<AssetRelationAnchor>();

            anchor.SetForwardYawOffset(90f);

            Assert.That(
                Quaternion.Angle(anchorObject.transform.rotation, originalRotation),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Dot(anchor.Forward, originalRotation * Vector3.right),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void AssetRelativePlacementMatchesGeneratedAssetTag()
        {
            SemanticTag workstation = CreateAssetTag("Workstation");
            AssetDefinition desk = CreateAsset("Tagged Desk", PlacementType.Floor, Vector3.one);
            desk.AddTag(workstation);
            AssetDefinition chair = CreateAsset("Tagged Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureTag(
                workstation,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                3f,
                AssetRelativeFacing.Any);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(desk, FloorCandidate(Vector3.zero), "Tagged Desk Instance");
            PlacementCandidate candidate = FloorCandidate(new Vector3(2f, 0f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void AssetRelativePlacementCanRequireTheSameSupportSurface()
        {
            GameObject firstSurface = CreateGameObject("First Desktop");
            BoxCollider firstCollider = firstSurface.AddComponent<BoxCollider>();
            firstSurface.AddComponent<PlacementSurfaceDescriptor>();
            GameObject secondSurface = CreateGameObject("Second Desktop");
            BoxCollider secondCollider = secondSurface.AddComponent<BoxCollider>();
            secondSurface.AddComponent<PlacementSurfaceDescriptor>();
            AssetDefinition monitor = CreateAsset("Support Monitor", PlacementType.Floor, Vector3.one);
            AssetDefinition keyboard = CreateAsset("Support Keyboard", PlacementType.Floor, Vector3.one);
            keyboard.AssetRelativePlacement.ConfigureAsset(
                monitor,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                3f,
                AssetRelativeFacing.Any,
                sameSupportSurface: true);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(
                monitor,
                new PlacementCandidate(
                    new Vector3(0f, 0.5f, 0f),
                    Quaternion.identity,
                    firstCollider,
                    Vector3.up,
                    placementType: PlacementType.Floor),
                "Monitor On First Desktop");
            PlacementCandidate sameSurfaceCandidate = new(
                new Vector3(2f, 0.5f, 0f),
                Quaternion.identity,
                firstCollider,
                Vector3.up,
                placementType: PlacementType.Floor);
            PlacementCandidate otherSurfaceCandidate = new(
                new Vector3(2f, 0.5f, 0f),
                Quaternion.identity,
                secondCollider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool sameSurfaceValid = PlacementValidator.TryValidateCandidate(
                sameSurfaceCandidate,
                CandidateFactory.GetBounds(sameSurfaceCandidate, keyboard),
                context,
                keyboard,
                out RejectionReason sameSurfaceReason,
                out _);
            bool otherSurfaceValid = PlacementValidator.TryValidateCandidate(
                otherSurfaceCandidate,
                CandidateFactory.GetBounds(otherSurfaceCandidate, keyboard),
                context,
                keyboard,
                out RejectionReason otherSurfaceReason,
                out string relatedName);

            Assert.That(sameSurfaceValid, Is.True);
            Assert.That(sameSurfaceReason, Is.EqualTo(RejectionReason.None));
            Assert.That(otherSurfaceValid, Is.False);
            Assert.That(otherSurfaceReason, Is.EqualTo(RejectionReason.DifferentAssetRelationSupportSurface));
            Assert.That(relatedName, Is.EqualTo("Monitor On First Desktop"));
        }

        [Test]
        public void AssetRelativePlacementUsesGeneratedMetadataFromPreviousRun()
        {
            AssetDefinition desk = CreateAsset("Previous Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Previous Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Right,
                0f,
                3f,
                AssetRelativeFacing.Any);
            GameObject existing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(existing);
            existing.name = "Existing Generated Desk";
            existing.transform.SetParent(_generatedRoot.transform);
            existing.transform.position = new Vector3(0f, 0.5f, 0f);
            existing.AddComponent<Genix.Layouts.GeneratedObjectMetadata>()
                .Initialize(PlacementType.Floor, sourceAsset: desk);
            SceneObjectIndex generated = SceneObjectIndex.CollectGenerated(_generatedRoot.transform);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                generatedObjects: generated);
            PlacementCandidate candidate = FloorCandidate(new Vector3(2f, 0f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
            Assert.That(relatedName, Is.EqualTo("Existing Generated Desk"));
        }

        [Test]
        public void AssetRelativePlacementPreservesSupportIdentityAcrossRuns()
        {
            GameObject firstSurface = CreateGameObject("Persistent First Desktop");
            BoxCollider firstCollider = firstSurface.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor firstSupport = firstSurface.AddComponent<PlacementSurfaceDescriptor>();
            GameObject secondSurface = CreateGameObject("Persistent Second Desktop");
            BoxCollider secondCollider = secondSurface.AddComponent<BoxCollider>();
            secondSurface.AddComponent<PlacementSurfaceDescriptor>();
            AssetDefinition monitor = CreateAsset("Persistent Support Monitor", PlacementType.Floor, Vector3.one);
            AssetDefinition keyboard = CreateAsset("Persistent Support Keyboard", PlacementType.Floor, Vector3.one);
            keyboard.AssetRelativePlacement.ConfigureAsset(
                monitor,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                3f,
                AssetRelativeFacing.Any,
                sameSupportSurface: true);
            GameObject existing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(existing);
            existing.name = "Existing Monitor On First Desktop";
            existing.transform.SetParent(_generatedRoot.transform);
            existing.transform.position = new Vector3(0f, 0.5f, 0f);
            existing.AddComponent<Genix.Layouts.GeneratedObjectMetadata>()
                .Initialize(PlacementType.Floor, firstSupport, monitor);
            SceneObjectIndex generated = SceneObjectIndex.CollectGenerated(_generatedRoot.transform);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                generatedObjects: generated);
            PlacementCandidate sameSurfaceCandidate = new(
                new Vector3(2f, 0.5f, 0f),
                Quaternion.identity,
                firstCollider,
                Vector3.up,
                placementType: PlacementType.Floor);
            PlacementCandidate otherSurfaceCandidate = new(
                new Vector3(2f, 0.5f, 0f),
                Quaternion.identity,
                secondCollider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool sameSurfaceValid = PlacementValidator.TryValidateCandidate(
                sameSurfaceCandidate,
                CandidateFactory.GetBounds(sameSurfaceCandidate, keyboard),
                context,
                keyboard,
                out _,
                out _);
            bool otherSurfaceValid = PlacementValidator.TryValidateCandidate(
                otherSurfaceCandidate,
                CandidateFactory.GetBounds(otherSurfaceCandidate, keyboard),
                context,
                keyboard,
                out RejectionReason otherSurfaceReason,
                out _);

            Assert.That(sameSurfaceValid, Is.True);
            Assert.That(otherSurfaceValid, Is.False);
            Assert.That(otherSurfaceReason, Is.EqualTo(RejectionReason.DifferentAssetRelationSupportSurface));
        }

        [Test]
        public void RelativePlacementDistinguishesMissingAndDistantAnchors()
        {
            AssetDefinition desk = CreateAsset("Distance Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Distance Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                1f,
                AssetRelativeFacing.Any);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(new Vector3(4f, 0f, 0f));

            bool validWithoutAnchor = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out RejectionReason missingReason,
                out _);
            context.Plan.Add(desk, FloorCandidate(Vector3.zero), "Distant Desk");
            bool validOutsideRange = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out RejectionReason distanceReason,
                out _);

            Assert.That(validWithoutAnchor, Is.False);
            Assert.That(missingReason, Is.EqualTo(RejectionReason.MissingAssetRelationAnchor));
            Assert.That(validOutsideRange, Is.False);
            Assert.That(distanceReason, Is.EqualTo(RejectionReason.OutsideAssetRelationRange));
        }

        [Test]
        public void RelativeValidationAndAnchorLookupUseSameBoundsDistance()
        {
            AssetDefinition desk = CreateAsset("Bounds Distance Desk", PlacementType.Floor, new Vector3(4f, 1f, 2f));
            AssetDefinition chair = CreateAsset("Bounds Distance Chair", PlacementType.Floor, new Vector3(0.4f, 1f, 2f));
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0.2f,
                1.2f,
                AssetRelativeFacing.Any);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(desk, FloorCandidate(Vector3.zero), "Bounds Distance Desk Instance");
            PlacementCandidate candidate = new(
                new Vector3(3.5f, 0.5f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor);
            OrientedBounds candidateBounds = CandidateFactory.GetBounds(candidate, chair);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                candidateBounds,
                context,
                chair,
                out RejectionReason reason,
                out _);
            bool resolved = RelativeAnchorProvider.TryFindAssetAnchor(
                context,
                chair,
                candidate.Position,
                candidateBounds.ToAxisAlignedBounds(),
                null,
                out RelativeAnchor anchor);

            Assert.That(valid, Is.True, reason.ToString());
            Assert.That(resolved, Is.True);
            Assert.That(anchor.Name, Is.EqualTo("Bounds Distance Desk Instance"));
        }

        [Test]
        public void AssetRelativeValidationUsesTheAnchorThatDeterminedFacing()
        {
            AssetDefinition desk = CreateAsset("Facing Identity Desk", PlacementType.Floor, new Vector3(2f, 1f, 2f));
            AssetDefinition chair = CreateAsset("Facing Identity Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                1f,
                AssetRelativeFacing.Toward);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(desk, FloorCandidate(new Vector3(-3f, 0f, 0f)), "Far Desk");
            context.Plan.Add(desk, FloorCandidate(Vector3.zero), "Near Desk");
            PlacementCandidate candidate = new(
                new Vector3(1.6f, 0.5f, 0f),
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor,
                relationAnchorIdentity: "planned:Far Desk");

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.OutsideAssetRelationRange));
        }

        [TestCase(AssetRelativeFacing.Toward, 0f, 0f, -1f)]
        [TestCase(AssetRelativeFacing.Away, 0f, 0f, 1f)]
        [TestCase(AssetRelativeFacing.MatchForward, 1f, 0f, 0f)]
        public void AssetRelativeFacingUsesMatchedAnchor(
            AssetRelativeFacing facing,
            float expectedX,
            float expectedY,
            float expectedZ)
        {
            AssetDefinition desk = CreateAsset("Facing Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Facing Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                3f,
                facing);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(
                desk,
                new PlacementCandidate(
                    new Vector3(0f, 0.5f, 0f),
                    Quaternion.Euler(0f, 90f, 0f),
                    surfaceNormal: Vector3.up,
                    placementType: PlacementType.Floor),
                "Facing Desk Instance");
            CandidateSeed seed = new(
                new Vector3(0f, 0f, 2f),
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, chair, 0, 1, 0f);
            Vector3 expected = new(expectedX, expectedY, expectedZ);

            Assert.That(
                Vector3.Dot(candidate.Rotation * Vector3.forward, expected),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(CandidateFactory.GetRotationAttemptCount(context, chair, PlacementType.Floor), Is.EqualTo(1));
        }

        [Test]
        public void RelativeFacingVariationIsBoundedAndDeterministic()
        {
            AssetDefinition desk = CreateAsset("Varied Facing Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Varied Facing Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                3f,
                AssetRelativeFacing.Toward);
            chair.AssetRelativePlacement.SetFacingVariation(45f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(desk, FloorCandidate(Vector3.zero), "Facing Desk");
            CandidateSeed seed = new(
                new Vector3(0f, 0f, 2f),
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor);

            PlacementCandidate first = CandidateFactory.Create(seed, context, chair, 0, 1, 0f);
            PlacementCandidate second = CandidateFactory.Create(seed, context, chair, 0, 1, 0f);
            Vector3 exactDirection = Vector3.back;

            Assert.That(Vector3.Angle(first.Rotation * Vector3.forward, exactDirection), Is.LessThanOrEqualTo(45.001f));
            Assert.That(Quaternion.Angle(first.Rotation, second.Rotation), Is.LessThan(0.0001f));
        }

        [Test]
        public void AssetRelativeFacingOverridesSupportForwardOrientation()
        {
            AssetDefinition desk = CreateAsset("Priority Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Priority Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                3f,
                AssetRelativeFacing.Toward);
            SetSerialized(chair, "orientationMode", Genix.Orientation.OrientationMode.MatchSupportForward);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(desk, FloorCandidate(Vector3.zero), "Priority Desk Instance");
            GameObject support = CreateGameObject("Conflicting Forward Surface");
            support.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            BoxCollider collider = support.AddComponent<BoxCollider>();
            support.AddComponent<PlacementSurfaceDescriptor>();
            CandidateSeed seed = new(
                new Vector3(0f, 0f, 2f),
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, chair, 0, 1, 0f);

            Assert.That(
                Vector3.Dot(candidate.Rotation * Vector3.forward, Vector3.back),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(candidate.RelationAnchorIdentity, Is.EqualTo("planned:Priority Desk Instance"));
        }

        [Test]
        public void AssetRelativeFacingUsesRaisedFloorCenterForAnchorRange()
        {
            AssetDefinition desk = CreateAsset("Elevated Anchor Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Raised Center Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                1.1f,
                AssetRelativeFacing.Toward);
            GameObject anchorObject = CreateGameObject("Elevated Desk Anchor");
            anchorObject.transform.position = new Vector3(0.9f, 1f, 0f);
            AssetRelationAnchor anchor = anchorObject.AddComponent<AssetRelationAnchor>();
            anchor.SetRepresentedAsset(desk);
            anchor.SetCustomBounds(true, Vector3.zero, Vector3.one * 0.01f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, chair, 0, 1, 0f);

            Assert.That(candidate.RelationAnchorIdentity, Is.SameAs(anchor));
            Assert.That(
                Vector3.Angle(candidate.Rotation * Vector3.forward, new Vector3(0.9f, 0f, 0f)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void RelativeFacingUpdatesAfterAdaptiveHeightFit()
        {
            AssetDefinition marker = CreateAsset(
                "Adaptive Direction Marker",
                PlacementType.Floor,
                new Vector3(1f, 2f, 1f));
            AssetDefinition anchorAsset = CreateAsset(
                "Adaptive Direction Anchor",
                PlacementType.Floor,
                Vector3.one);
            SetSerialized(marker, "surfaceFitMode", SurfaceFitMode.Adaptive);
            marker.AssetRelativePlacement.ConfigureAsset(
                anchorAsset,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                1.1f,
                AssetRelativeFacing.MatchForward);

            GameObject anchorObject = CreateGameObject("Adaptive Direction Anchor");
            anchorObject.transform.SetPositionAndRotation(
                new Vector3(0.9f, 1f, 0f),
                Quaternion.Euler(0f, 90f, 0f));
            AssetRelationAnchor anchor = anchorObject.AddComponent<AssetRelationAnchor>();
            anchor.SetRepresentedAsset(anchorAsset);
            anchor.SetCustomBounds(true, Vector3.zero, Vector3.one * 0.01f);

            GameObject supportObject = CreateGameObject("Adaptive Direction Support");
            supportObject.transform.position = new Vector3(0f, -0.25f, 0f);
            BoxCollider supportCollider = supportObject.AddComponent<BoxCollider>();
            supportCollider.size = new Vector3(10f, 0.5f, 10f);
            Physics.SyncTransforms();

            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            CandidateSeed seed = new(
                new Vector3(0f, 3f, 0f),
                Quaternion.identity,
                supportCollider,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, marker, 0, 1, 0f);

            Assert.That(candidate.HasSurfaceFit, Is.True);
            Assert.That(candidate.Position.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(candidate.RelationAnchorIdentity, Is.SameAs(anchor));
            Assert.That(
                Vector3.Dot(candidate.Rotation * Vector3.forward, Vector3.right),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void RelativeFacingUsesNearestCenterWhenBoundsDistancesTie()
        {
            AssetDefinition desk = CreateAsset("Ambiguous Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Ambiguous Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                5f,
                AssetRelativeFacing.Toward);

            GameObject wideAnchorObject = CreateGameObject("Wide Distant Desk");
            wideAnchorObject.transform.position = new Vector3(3f, 0f, 0f);
            AssetRelationAnchor wideAnchor = wideAnchorObject.AddComponent<AssetRelationAnchor>();
            wideAnchor.SetRepresentedAsset(desk);
            wideAnchor.SetCustomBounds(true, Vector3.zero, new Vector3(5.9f, 1f, 1f));

            GameObject nearAnchorObject = CreateGameObject("Near Desk");
            nearAnchorObject.transform.position = new Vector3(0f, 0f, 0.06f);
            AssetRelationAnchor nearAnchor = nearAnchorObject.AddComponent<AssetRelationAnchor>();
            nearAnchor.SetRepresentedAsset(desk);
            nearAnchor.SetCustomBounds(true, Vector3.zero, Vector3.one * 0.01f);

            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, chair, 0, 1, 0f);

            Assert.That(
                Vector3.Dot(candidate.Rotation * Vector3.forward, Vector3.forward),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void OverlappingRelationRangesUseNearestMatchingAnchor()
        {
            AssetDefinition desk = CreateAsset("Overlapping Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Overlapping Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                4f,
                AssetRelativeFacing.Toward);

            GameObject leftObject = CreateGameObject("Left Desk");
            leftObject.transform.position = new Vector3(-1f, 0f, 0f);
            AssetRelationAnchor left = leftObject.AddComponent<AssetRelationAnchor>();
            left.SetRepresentedAsset(desk);
            left.SetCustomBounds(true, Vector3.zero, Vector3.one * 0.1f);

            GameObject rightObject = CreateGameObject("Right Desk");
            rightObject.transform.position = new Vector3(1f, 0f, 0f);
            AssetRelationAnchor right = rightObject.AddComponent<AssetRelationAnchor>();
            right.SetRepresentedAsset(desk);
            right.SetCustomBounds(true, Vector3.zero, Vector3.one * 0.1f);

            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            Assert.That(left.TryGetBounds(out Bounds leftBounds), Is.True);
            Assert.That(right.TryGetBounds(out Bounds rightBounds), Is.True);
            CandidateSeed seed = new(
                new Vector3(-0.25f, 0f, 0f),
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor);

            Assert.That(RelativeAnchorProvider.IsPotentialSeedForAnchor(
                context,
                seed,
                chair,
                new RelativeAnchor(
                    leftBounds.center,
                    leftBounds,
                    left.name,
                    left.Forward,
                    left.Right,
                    desk,
                    identity: left)), Is.True);
            Assert.That(RelativeAnchorProvider.IsPotentialSeedForAnchor(
                context,
                seed,
                chair,
                new RelativeAnchor(
                    rightBounds.center,
                    rightBounds,
                    right.name,
                    right.Forward,
                    right.Right,
                    desk,
                    identity: right)), Is.False);
        }

        [Test]
        public void RelativePlacementUsesExplicitSceneAnchorIdentityAndBounds()
        {
            SemanticTag workstation = CreateAssetTag("Scene Workstation");
            GameObject supportObject = CreateGameObject("Scene Desktop");
            BoxCollider supportCollider = supportObject.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor support = supportObject.AddComponent<PlacementSurfaceDescriptor>();
            GameObject anchorObject = CreateGameObject("Fixed Workstation");
            anchorObject.transform.position = new Vector3(0f, 0.5f, 0f);
            AssetRelationAnchor anchor = anchorObject.AddComponent<AssetRelationAnchor>();
            anchor.SetAssetTags(new[] { workstation });
            anchor.SetSupportSurface(support);
            anchor.SetCustomBounds(true, Vector3.zero, new Vector3(2f, 1f, 2f));
            AssetDefinition chair = CreateAsset("Scene Chair", PlacementType.Floor, Vector3.one);
            chair.AssetRelativePlacement.ConfigureTag(
                workstation,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Right,
                0f,
                2f,
                AssetRelativeFacing.Toward,
                sameSupportSurface: true);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = new(
                new Vector3(2f, 0.5f, 0f),
                Quaternion.identity,
                supportCollider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
            Assert.That(relatedName, Is.EqualTo("Fixed Workstation"));
        }

        [Test]
        public void SupportRulesAcceptAnyConfiguredTagWithinCategory()
        {
            TagCategory supportKind = CreateSurfaceCategory("Support Kind");
            SemanticTag desktop = CreateTag("Desktop", supportKind);
            SemanticTag shelf = CreateTag("Shelf", supportKind);
            AssetDefinition asset = CreateAsset("Monitor", PlacementType.Floor, Vector3.one);
            asset.SetRequiredSupportTags(new[] { desktop, shelf });
            GameObject support = CreateGameObject("Desk Top");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetSurfaceTags(new[] { desktop });
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool valid = PlacementSupportRules.TryValidate(
                seed,
                asset,
                CreateContext(SamplingAlgorithm.Random),
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void SupportRulesRequireOneMatchingTagFromEveryConfiguredCategory()
        {
            TagCategory supportKind = CreateSurfaceCategory("Support Kind");
            TagCategory supportCondition = CreateSurfaceCategory("Support Condition");
            SemanticTag desktop = CreateTag("Desktop", supportKind);
            SemanticTag shelf = CreateTag("Shelf", supportKind);
            SemanticTag dry = CreateTag("Dry", supportCondition);
            SemanticTag wet = CreateTag("Wet", supportCondition);
            AssetDefinition asset = CreateAsset("Category-Aware Monitor", PlacementType.Floor, Vector3.one);
            asset.SetRequiredSupportTags(new[] { desktop, shelf, dry });
            GameObject support = CreateGameObject("Incomplete Desk Top");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetSurfaceTags(new[] { desktop, wet });
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool incomplete = PlacementSupportRules.TryValidate(
                seed,
                asset,
                CreateContext(SamplingAlgorithm.Random),
                out RejectionReason incompleteReason,
                out _);

            descriptor.SetSurfaceTags(new[] { desktop, dry });
            bool complete = PlacementSupportRules.TryValidate(
                seed,
                asset,
                CreateContext(SamplingAlgorithm.Random),
                out RejectionReason completeReason,
                out _);

            Assert.That(incomplete, Is.False);
            Assert.That(incompleteReason, Is.EqualTo(RejectionReason.UnsupportedSupportSurface));
            Assert.That(complete, Is.True);
            Assert.That(completeReason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void ForbiddenSupportTagTakesPrecedenceOverRequiredTag()
        {
            SemanticTag desktop = CreateTag("Desktop");
            AssetDefinition asset = CreateAsset("Conflicting Monitor", PlacementType.Floor, Vector3.one);
            asset.SetRequiredSupportTags(new[] { desktop });
            asset.SetForbiddenSupportTags(new[] { desktop });
            GameObject support = CreateGameObject("Forbidden Desk Top");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetSurfaceTags(new[] { desktop });
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool valid = PlacementSupportRules.TryValidate(
                seed,
                asset,
                CreateContext(SamplingAlgorithm.Random),
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.UnsupportedSupportSurface));
            Assert.That(relatedName, Is.EqualTo(descriptor.name));
        }

        [Test]
        public void SurfaceAssetTagsAllowMatchingAssetsAndRejectUnrelatedAssets()
        {
            SemanticTag monitorTag = CreateAssetTag("Allowed Monitor");
            AssetDefinition monitor = CreateAsset("Allowed Monitor Asset", PlacementType.Floor, Vector3.one);
            AssetDefinition keyboard = CreateAsset("Rejected Keyboard", PlacementType.Floor, Vector3.one);
            monitor.AddTag(monitorTag);
            GameObject support = CreateGameObject("Restricted Desktop");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetAllowedAssetTags(new[] { monitorTag });
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            Assert.That(PlacementSupportRules.TryValidate(seed, monitor, context, out _, out _), Is.True);
            Assert.That(
                PlacementSupportRules.TryValidate(seed, keyboard, context, out RejectionReason reason, out _),
                Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.SurfaceRejectsAsset));
        }

        [Test]
        public void SurfaceForbiddenAssetTagTakesPrecedenceOverAllowedTag()
        {
            SemanticTag monitorTag = CreateAssetTag("Conflicting Monitor");
            AssetDefinition monitor = CreateAsset("Conflicting Monitor Asset", PlacementType.Floor, Vector3.one);
            monitor.AddTag(monitorTag);
            GameObject support = CreateGameObject("Conflicting Desktop");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetAllowedAssetTags(new[] { monitorTag });
            descriptor.SetForbiddenAssetTags(new[] { monitorTag });
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            Assert.That(
                PlacementSupportRules.TryValidate(
                    seed,
                    monitor,
                    CreateContext(SamplingAlgorithm.Random),
                    out RejectionReason reason,
                    out _),
                Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.SurfaceRejectsAsset));
        }

        [Test]
        public void SurfaceCategoryDefaultsToAnySupportTag()
        {
            SemanticTag desktop = CreateTag("Any Desktop");
            AssetDefinition asset = CreateAsset("Any Monitor", PlacementType.Floor, Vector3.one);
            asset.SetRequiredSupportTags(new[] { desktop });
            GameObject support = CreateGameObject("Generic Surface");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            support.AddComponent<PlacementSurfaceDescriptor>();
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            Assert.That(PlacementSupportRules.TryValidate(
                seed,
                asset,
                CreateContext(SamplingAlgorithm.Random),
                out _,
                out _), Is.True);
        }

        [Test]
        public void SurfaceCategoryNoneRejectsRequiredSupportTag()
        {
            SemanticTag desktop = CreateTag("No Desktop");
            AssetDefinition asset = CreateAsset("Rejected Monitor", PlacementType.Floor, Vector3.one);
            asset.SetRequiredSupportTags(new[] { desktop });
            GameObject support = CreateGameObject("Explicitly Untagged Surface");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetCategorySelection(desktop.Category, Array.Empty<SemanticTag>(), true);
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool valid = PlacementSupportRules.TryValidate(
                seed,
                asset,
                CreateContext(SamplingAlgorithm.Random),
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.UnsupportedSupportSurface));
        }

        [Test]
        public void RequiredNoneAndForbiddenAnyBothDisablePlacement()
        {
            SemanticTag supportTag = CreateTag("Blocked Support");
            AssetDefinition requiredNone = CreateAsset("Required None", PlacementType.Floor, Vector3.one);
            requiredNone.SetRequiredSupportNoneCategories(new[] { supportTag.Category });
            AssetDefinition forbiddenAny = CreateAsset("Forbidden Any", PlacementType.Floor, Vector3.one);
            forbiddenAny.SetForbiddenSupportAnyCategories(new[] { supportTag.Category });
            GameObject support = CreateGameObject("Support");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            support.AddComponent<PlacementSurfaceDescriptor>();
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            Assert.That(PlacementSupportRules.TryValidate(
                seed,
                requiredNone,
                context,
                out RejectionReason requiredReason,
                out _), Is.False);
            Assert.That(requiredReason, Is.EqualTo(RejectionReason.UnsupportedSupportSurface));
            Assert.That(PlacementSupportRules.TryValidate(
                seed,
                forbiddenAny,
                context,
                out RejectionReason forbiddenReason,
                out _), Is.False);
            Assert.That(forbiddenReason, Is.EqualTo(RejectionReason.UnsupportedSupportSurface));
        }

        [Test]
        public void NearWallAcceptsCloseCandidateAndRejectsDistantCandidate()
        {
            AssetDefinition asset = CreateAsset("Near Wall Desk", PlacementType.Floor, Vector3.one);
            asset.SetWallProximity(WallProximityMode.NearWall, 0.6f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate close = FloorCandidate(new Vector3(0f, 0f, -4f));
            PlacementCandidate distant = FloorCandidate(Vector3.zero);

            Assert.That(PlacementValidator.TryValidateCandidate(
                close,
                new OrientedBounds(close.Position, Vector3.one, Quaternion.identity),
                context,
                asset,
                out _,
                out _), Is.True);
            Assert.That(PlacementValidator.TryValidateCandidate(
                distant,
                new OrientedBounds(distant.Position, Vector3.one, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out string wallName), Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.TooFarFromWall));
            Assert.That(wallName, Is.EqualTo("Wall"));
        }

        [Test]
        public void AwayFromWallRejectsCandidateInsideClearance()
        {
            AssetDefinition asset = CreateAsset("Open Area Table", PlacementType.Floor, Vector3.one);
            asset.SetWallProximity(WallProximityMode.AwayFromWall, 1f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(new Vector3(0f, 0f, -4f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                new OrientedBounds(candidate.Position, Vector3.one, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out string wallName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.TooCloseToWall));
            Assert.That(wallName, Is.EqualTo("Wall"));
        }

        [Test]
        public void NearWallRecognizesSteepTerrainAndFlatTerrainProvidesNoWall()
        {
            PlacementArea ridgeArea = CreateTerrainArea("Wall Ridge", true);
            AssetDefinition asset = CreateAsset("Terrain Wall Desk", PlacementType.Floor, Vector3.one);
            asset.SetWallProximity(WallProximityMode.NearWall, 0.75f);
            GenerationContext ridgeContext = CreateContext(
                SamplingAlgorithm.Random,
                area: ridgeArea);
            OrientedBounds closeToSlope = new(
                new Vector3(-1.25f, 1f, 0f),
                Vector3.one,
                Quaternion.identity);

            Assert.That(WallProximityRules.TryValidate(
                asset,
                closeToSlope,
                ridgeContext,
                out RejectionReason ridgeReason,
                out string terrainName), Is.True, ridgeReason.ToString());
            Assert.That(terrainName, Is.EqualTo("Wall Ridge"));

            GameObject.Find("Wall Ridge").SetActive(false);
            Physics.SyncTransforms();
            PlacementArea flatArea = CreateTerrainArea("Flat Ground", false);
            GenerationContext flatContext = CreateContext(
                SamplingAlgorithm.Random,
                area: flatArea);
            Assert.That(WallProximityRules.TryValidate(
                asset,
                closeToSlope,
                flatContext,
                out RejectionReason flatReason,
                out _), Is.False);
            Assert.That(flatReason, Is.EqualTo(RejectionReason.MissingWallReference));
        }

        [Test]
        public void AnchorGroupMaximumIsSharedAcrossTaggedVariantsAtOneAnchor()
        {
            SemanticTag display = CreateAssetTag("Per Desk Display");
            AssetDefinition desk = CreateAsset("Quota Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition monitor = CreateAsset("Quota Monitor", PlacementType.Floor, Vector3.one * 0.25f);
            AssetDefinition laptop = CreateAsset("Quota Laptop", PlacementType.Floor, Vector3.one * 0.25f);
            monitor.AddTag(display);
            laptop.AddTag(display);
            monitor.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                4f,
                AssetRelativeFacing.Any);
            laptop.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                4f,
                AssetRelativeFacing.Any);
            AssetPoolAnchorGroupLimit group = new();
            group.ConfigureAsset(desk, display, AssetRelativeAnchorSource.SceneAnchors);
            group.SetCardinality(AssetRelativeCardinalityMode.AtMost, 1);
            _pool.SetAnchorGroupLimits(new[] { group });

            GameObject anchorObject = CreateGameObject("Quota Desk Anchor");
            AssetRelationAnchor anchor = anchorObject.AddComponent<AssetRelationAnchor>();
            anchor.SetRepresentedAsset(desk);
            anchor.SetCustomBounds(true, Vector3.zero, Vector3.one);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate monitorCandidate = new(
                new Vector3(1f, 0.5f, 0f),
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor,
                relationAnchorIdentity: anchor);
            context.Plan.Add(monitor, monitorCandidate, "Existing Monitor", anchor);
            PlacementCandidate laptopCandidate = new(
                new Vector3(-1f, 0.5f, 0f),
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor,
                relationAnchorIdentity: anchor);

            bool valid = RelativeAnchorProvider.TryValidateCandidate(
                laptopCandidate,
                CandidateFactory.GetBounds(laptopCandidate, laptop),
                laptop,
                context,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.AssetRelationGroupCapacityReached));
        }

        [Test]
        public void AnchorGroupMaximumIncludesPersistedAssignmentsAcrossRuns()
        {
            SemanticTag display = CreateAssetTag("Persistent Per Desk Display");
            AssetDefinition desk = CreateAsset("Persistent Quota Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition monitor = CreateAsset("Persistent Quota Monitor", PlacementType.Floor, Vector3.one * 0.25f);
            AssetDefinition laptop = CreateAsset("Persistent Quota Laptop", PlacementType.Floor, Vector3.one * 0.25f);
            monitor.AddTag(display);
            laptop.AddTag(display);
            monitor.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                4f,
                AssetRelativeFacing.Any);
            laptop.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                4f,
                AssetRelativeFacing.Any);
            AssetPoolAnchorGroupLimit group = new();
            group.ConfigureAsset(desk, display, AssetRelativeAnchorSource.SceneAnchors);
            group.SetCardinality(AssetRelativeCardinalityMode.AtMost, 1);
            _pool.SetAnchorGroupLimits(new[] { group });

            GameObject anchorObject = CreateGameObject("Persistent Quota Desk Anchor");
            AssetRelationAnchor anchor = anchorObject.AddComponent<AssetRelationAnchor>();
            anchor.SetRepresentedAsset(desk);
            anchor.SetCustomBounds(true, Vector3.zero, Vector3.one);
            GameObject existing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(existing);
            existing.transform.SetParent(_generatedRoot.transform);
            existing.AddComponent<Genix.Layouts.GeneratedObjectMetadata>().Initialize(
                PlacementType.Floor,
                null,
                monitor,
                RelativeAnchorProvider.GetPersistentIdentityKey(anchor));
            SceneObjectIndex generated = SceneObjectIndex.CollectGenerated(_generatedRoot.transform);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                generatedObjects: generated);
            PlacementCandidate laptopCandidate = new(
                new Vector3(1f, 0.5f, 0f),
                Quaternion.identity,
                surfaceNormal: Vector3.up,
                placementType: PlacementType.Floor,
                relationAnchorIdentity: anchor);

            bool valid = RelativeAnchorProvider.TryValidateCandidate(
                laptopCandidate,
                CandidateFactory.GetBounds(laptopCandidate, laptop),
                laptop,
                context,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.AssetRelationGroupCapacityReached));
        }

        [Test]
        public void SupportCapacityCountsObjectsAlreadyAcceptedIntoPlan()
        {
            AssetDefinition asset = CreateAsset("Capacity Asset", PlacementType.Floor, Vector3.one);
            GameObject support = CreateGameObject("Single Capacity Surface");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetCapacity(true, 1);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
            PlacementCandidate candidate = new(
                Vector3.up * 0.5f,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
            context.Plan.Add(asset, candidate, "First");

            bool valid = PlacementSupportRules.TryValidate(
                seed,
                asset,
                context,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.SupportCapacityReached));
        }

        [Test]
        public void LimitedSupportCapacityOfZeroRejectsFirstPlacement()
        {
            AssetDefinition asset = CreateAsset("Blocked Capacity Asset", PlacementType.Floor, Vector3.one);
            GameObject support = CreateGameObject("Blocked Surface");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetCapacity(true, 0);
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool valid = PlacementSupportRules.TryValidate(
                seed,
                asset,
                CreateContext(SamplingAlgorithm.Random),
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.SupportCapacityReached));
            Assert.That(relatedName, Is.EqualTo(descriptor.name));
        }

        [Test]
        public void SupportCapacityCountsExistingGeneratedMetadataAcrossRuns()
        {
            AssetDefinition asset = CreateAsset("Persistent Capacity Asset", PlacementType.Floor, Vector3.one);
            GameObject support = CreateGameObject("Persistent Capacity Surface");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            descriptor.SetCapacity(true, 1);
            GameObject existing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(existing);
            existing.name = "Existing Supported Object";
            existing.transform.SetParent(_generatedRoot.transform);
            existing.AddComponent<Genix.Layouts.GeneratedObjectMetadata>()
                .Initialize(PlacementType.Floor, descriptor);
            SceneObjectIndex generated = SceneObjectIndex.CollectGenerated(_generatedRoot.transform);
            GenerationContext context = CreateContext(
                SamplingAlgorithm.Random,
                generatedObjects: generated);
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool valid = PlacementSupportRules.TryValidate(
                seed,
                asset,
                context,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.SupportCapacityReached));
        }

        [Test]
        public void SupportTagCapacityCountsMatchingObjectsAcceptedIntoPlan()
        {
            SemanticTag monitorTag = CreateAssetTag("Monitor");
            AssetDefinition monitor = CreateAsset("Monitor Asset", PlacementType.Floor, Vector3.one);
            monitor.AddTag(monitorTag);
            GameObject support = CreateGameObject("Desktop");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            PlacementSurfaceCapacityRule rule = new();
            rule.ConfigureTag(monitorTag, 1);
            descriptor.SetAssetCapacityRules(new[] { rule });
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = new(
                Vector3.up * 0.5f,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
            context.Plan.Add(monitor, candidate, "First Monitor");
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool valid = PlacementSupportRules.TryValidate(
                seed,
                monitor,
                context,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.SupportAssetCapacityReached));
            Assert.That(relatedName, Does.Contain("Monitor"));
        }

        [Test]
        public void SupportTagCapacityDoesNotLimitUnrelatedAssets()
        {
            SemanticTag monitorTag = CreateAssetTag("Monitor Only");
            AssetDefinition monitor = CreateAsset("Tagged Monitor", PlacementType.Floor, Vector3.one);
            AssetDefinition keyboard = CreateAsset("Keyboard", PlacementType.Floor, Vector3.one);
            monitor.AddTag(monitorTag);
            GameObject support = CreateGameObject("Shared Desktop");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            PlacementSurfaceCapacityRule rule = new();
            rule.ConfigureTag(monitorTag, 1);
            descriptor.SetAssetCapacityRules(new[] { rule });
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = new(
                Vector3.up * 0.5f,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
            context.Plan.Add(monitor, candidate, "Monitor");
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool valid = PlacementSupportRules.TryValidate(seed, keyboard, context, out RejectionReason reason, out _);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void ConcreteAssetCapacityOfZeroBlocksOnlySelectedAsset()
        {
            AssetDefinition monitor = CreateAsset("Concrete Monitor", PlacementType.Floor, Vector3.one);
            AssetDefinition keyboard = CreateAsset("Concrete Keyboard", PlacementType.Floor, Vector3.one);
            GameObject support = CreateGameObject("Exact Asset Surface");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            PlacementSurfaceCapacityRule rule = new();
            rule.ConfigureAsset(monitor, 0);
            descriptor.SetAssetCapacityRules(new[] { rule });
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            Assert.That(
                PlacementSupportRules.TryValidate(seed, monitor, context, out RejectionReason monitorReason, out _),
                Is.False);
            Assert.That(monitorReason, Is.EqualTo(RejectionReason.SupportAssetCapacityReached));
            Assert.That(PlacementSupportRules.TryValidate(seed, keyboard, context, out _, out _), Is.True);
        }

        [Test]
        public void SupportCapacityCountsGeneratedMetadataAcrossRuns()
        {
            AssetDefinition monitor = CreateAsset("Persistent Monitor", PlacementType.Floor, Vector3.one);
            GameObject support = CreateGameObject("Persistent Desktop");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = support.AddComponent<PlacementSurfaceDescriptor>();
            PlacementSurfaceCapacityRule rule = new();
            rule.ConfigureAsset(monitor, 1);
            descriptor.SetAssetCapacityRules(new[] { rule });
            GameObject existing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(existing);
            existing.transform.SetParent(_generatedRoot.transform);
            existing.AddComponent<Genix.Layouts.GeneratedObjectMetadata>()
                .Initialize(PlacementType.Floor, descriptor, monitor);
            SceneObjectIndex generated = SceneObjectIndex.CollectGenerated(_generatedRoot.transform);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, generatedObjects: generated);
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            bool valid = PlacementSupportRules.TryValidate(seed, monitor, context, out RejectionReason reason, out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.SupportAssetCapacityReached));
        }

        [Test]
        public void PoolTagLimitIsSharedAcrossDifferentMatchingAssets()
        {
            SemanticTag screenTag = CreateAssetTag("Screen Quota");
            AssetDefinition monitorA = CreateAsset("Monitor A", PlacementType.Floor, Vector3.one);
            AssetDefinition monitorB = CreateAsset("Monitor B", PlacementType.Floor, Vector3.one);
            monitorA.AddTag(screenTag);
            monitorB.AddTag(screenTag);
            AssetPoolTagLimit limit = new();
            limit.Configure(screenTag, 1);
            _pool.SetTagPlacementLimits(new[] { limit });
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(monitorA, FloorCandidate(Vector3.zero), "First Screen");

            Assert.That(_pool.HasReachedPlacementLimit(monitorB, context.Plan), Is.True);
        }

        [Test]
        public void PoolTagLimitCountsExistingGeneratedMetadataAcrossRuns()
        {
            SemanticTag screenTag = CreateAssetTag("Persistent Screen Quota");
            AssetDefinition monitor = CreateAsset("Persistent Existing Monitor", PlacementType.Floor, Vector3.one);
            AssetDefinition laptop = CreateAsset("Persistent Candidate Laptop", PlacementType.Floor, Vector3.one);
            monitor.AddTag(screenTag);
            laptop.AddTag(screenTag);
            AssetPoolTagLimit limit = new();
            limit.Configure(screenTag, 1);
            _pool.SetTagPlacementLimits(new[] { limit });
            GameObject existing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(existing);
            existing.transform.SetParent(_generatedRoot.transform);
            existing.AddComponent<Genix.Layouts.GeneratedObjectMetadata>()
                .Initialize(PlacementType.Floor, null, monitor);
            SceneObjectIndex generated = SceneObjectIndex.CollectGenerated(_generatedRoot.transform);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, generatedObjects: generated);

            Assert.That(generated.GetAssetTagCount(screenTag), Is.EqualTo(1));
            Assert.That(
                _pool.HasReachedPlacementLimit(laptop, context),
                Is.True);
        }

        [Test]
        public void AssetSpacingRuleAppliesSymmetricallyAcrossGenerationOrder()
        {
            AssetDefinition chair = CreateAsset("Spacing Chair", PlacementType.Floor, Vector3.one);
            AssetDefinition heater = CreateAsset("Spacing Heater", PlacementType.Floor, Vector3.one);
            AssetSpacingRule rule = new();
            rule.ConfigureAsset(chair, 3f);
            heater.SetSpacingRules(new[] { rule });
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(heater, FloorCandidate(Vector3.zero), "Heater");
            PlacementCandidate chairCandidate = FloorCandidate(new Vector3(2f, 0f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                chairCandidate,
                CandidateFactory.GetBounds(chairCandidate, chair),
                context,
                chair,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.AssetSpacingViolation));
            Assert.That(relatedName, Is.EqualTo("Heater"));
        }

        [Test]
        public void AssetTagSpacingDoesNotRejectUnrelatedNeighbor()
        {
            SemanticTag hazardTag = CreateAssetTag("Spacing Hazard");
            AssetDefinition chair = CreateAsset("Tag Spacing Chair", PlacementType.Floor, Vector3.one);
            AssetDefinition plant = CreateAsset("Unrelated Plant", PlacementType.Floor, Vector3.one);
            AssetSpacingRule rule = new();
            rule.ConfigureTag(hazardTag, 4f);
            chair.SetSpacingRules(new[] { rule });
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(plant, FloorCandidate(Vector3.zero), "Plant");
            PlacementCandidate candidate = FloorCandidate(new Vector3(2f, 0f, 0f));

            Assert.That(PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out _,
                out _), Is.True);
        }

        [Test]
        public void CandidateClearanceRejectsPlannedVisualInsideReservedVolume()
        {
            AssetDefinition desk = CreateAsset("Clearance Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Clearance Chair", PlacementType.Floor, Vector3.one);
            desk.SetClearance(true, Vector3.one, new Vector3(2f, 0f, 0f));
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(chair, FloorCandidate(new Vector3(2f, 0f, 0f)), "Chair");
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, desk),
                context,
                desk,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.ClearanceBlocked));
            Assert.That(relatedName, Is.EqualTo("Chair"));
        }

        [Test]
        public void ExistingClearanceRejectsLaterVisualWithoutOwnClearance()
        {
            AssetDefinition desk = CreateAsset("Existing Clearance Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Later Clearance Chair", PlacementType.Floor, Vector3.one);
            desk.SetClearance(true, Vector3.one, new Vector3(-2f, 0f, 0f));
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            context.Plan.Add(desk, FloorCandidate(new Vector3(2f, 0f, 0f)), "Desk");
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.ClearanceBlocked));
            Assert.That(relatedName, Is.EqualTo("Desk"));
        }

        [Test]
        public void ExistingGeneratedMetadataRestoresClearanceAcrossRuns()
        {
            AssetDefinition desk = CreateAsset("Persistent Clearance Desk", PlacementType.Floor, Vector3.one);
            AssetDefinition chair = CreateAsset("Persistent Clearance Chair", PlacementType.Floor, Vector3.one);
            desk.SetClearance(true, Vector3.one, new Vector3(-2f, 0f, 0f));
            GameObject existing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(existing);
            existing.name = "Existing Desk";
            existing.transform.SetParent(_generatedRoot.transform);
            existing.transform.position = new Vector3(2f, 0.5f, 0f);
            existing.AddComponent<Genix.Layouts.GeneratedObjectMetadata>()
                .Initialize(PlacementType.Floor, sourceAsset: desk);
            SceneObjectIndex generated = SceneObjectIndex.CollectGenerated(_generatedRoot.transform);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, generatedObjects: generated);
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, chair),
                context,
                chair,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.ClearanceBlocked));
            Assert.That(relatedName, Is.EqualTo("Existing Desk"));
        }

        [Test]
        public void CandidateClearanceRejectsFixedCollider()
        {
            AssetDefinition desk = CreateAsset("Fixed Clearance Desk", PlacementType.Floor, Vector3.one);
            desk.SetClearance(true, Vector3.one, new Vector3(2f, 0f, 0f));
            CreateFixedBox("Fixed Obstacle", new Vector3(2f, 0.5f, 0f));
            SceneObjectIndex fixedObjects = SceneObjectIndex.CollectFixed(_areaSource, _generatedRoot.transform);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random, fixedObjects: fixedObjects);
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, desk),
                context,
                desk,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.ClearanceBlocked));
            Assert.That(relatedName, Is.EqualTo("Fixed Obstacle"));
        }

        [Test]
        public void ClearanceMustRemainInsideTargetVolume()
        {
            AssetDefinition desk = CreateAsset("Boundary Clearance Desk", PlacementType.Floor, Vector3.one);
            desk.SetClearance(true, new Vector3(2f, 1f, 1f), new Vector3(1f, 0f, 0f));
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(new Vector3(4.4f, 0f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                CandidateFactory.GetBounds(candidate, desk),
                context,
                desk,
                out RejectionReason reason,
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.ClearanceOutsideTargetVolume));
        }

        [Test]
        public void MatchSupportForwardUsesDescriptorTransformDirection()
        {
            AssetDefinition asset = CreateAsset("Support Facing", PlacementType.Floor, Vector3.one);
            SetSerialized(asset, "orientationMode", Genix.Orientation.OrientationMode.MatchSupportForward);
            GameObject support = CreateGameObject("Facing Surface");
            support.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            BoxCollider collider = support.AddComponent<BoxCollider>();
            support.AddComponent<PlacementSurfaceDescriptor>();
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            Assert.That(PlacementSupportRules.TryValidate(seed, asset, context, out _, out _), Is.True);
            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(
                Vector3.Dot(candidate.Rotation * Vector3.forward, Vector3.right),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void MatchSupportForwardUsesRelationAnchorSemanticFront()
        {
            AssetDefinition asset = CreateAsset("Semantic Support Facing", PlacementType.Floor, Vector3.one);
            SetSerialized(asset, "orientationMode", Genix.Orientation.OrientationMode.MatchSupportForward);
            GameObject support = CreateGameObject("Semantic Facing Surface");
            BoxCollider collider = support.AddComponent<BoxCollider>();
            support.AddComponent<PlacementSurfaceDescriptor>();
            AssetRelationAnchor anchor = support.AddComponent<AssetRelationAnchor>();
            anchor.SetForwardYawOffset(90f);
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(
                Vector3.Dot(candidate.Rotation * Vector3.forward, Vector3.right),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void MatchSupportForwardUsesColliderTransformWithoutDescriptor()
        {
            AssetDefinition asset = CreateAsset("Implicit Support Facing", PlacementType.Floor, Vector3.one);
            SetSerialized(asset, "orientationMode", Genix.Orientation.OrientationMode.MatchSupportForward);
            GameObject support = CreateGameObject("Implicit Facing Surface");
            support.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            BoxCollider collider = support.AddComponent<BoxCollider>();
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);

            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            Assert.That(PlacementSupportRules.TryValidate(seed, asset, context, out _, out _), Is.True);
            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(
                Vector3.Dot(candidate.Rotation * Vector3.forward, Vector3.left),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SupportFacingUsesLocalRightWhenForwardMatchesNormal()
        {
            AssetDefinition asset = CreateAsset("Support Facing Axis Fallback", PlacementType.Floor, Vector3.one);
            SetSerialized(asset, "orientationMode", Genix.Orientation.OrientationMode.MatchSupportForward);
            GameObject support = CreateGameObject("Vertical Forward Surface");
            support.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            BoxCollider collider = support.AddComponent<BoxCollider>();
            support.AddComponent<PlacementSurfaceDescriptor>();
            CandidateSeed seed = new(
                Vector3.zero,
                Quaternion.identity,
                collider,
                Vector3.up,
                placementType: PlacementType.Floor);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            Assert.That(PlacementSupportRules.TryValidate(seed, asset, context, out _, out _), Is.True);
            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(
                Vector3.Dot(candidate.Rotation * Vector3.forward, support.transform.right),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ValidatorRejectsCandidateInsideColliderFreeExclusionBox()
        {
            GameObject regionObject = CreateGameObject("Door Clearance");
            regionObject.transform.position = new Vector3(0f, 0.5f, 0f);
            PlacementExclusionRegion region = regionObject.AddComponent<PlacementExclusionRegion>();
            region.ConfigureBox(Vector3.zero, new Vector3(2f, 2f, 2f), PlacementTarget.Floor);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(Vector3.zero);

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                new OrientedBounds(candidate.Position, Vector3.one, Quaternion.identity),
                context,
                null,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.InsideExclusionRegion));
            Assert.That(relatedName, Is.EqualTo(region.name));
            Assert.That(region.GetComponent<Collider>(), Is.Null);
        }

        [Test]
        public void ValidatorKeepsReservedClearanceOutsideExclusionRegions()
        {
            GameObject regionObject = CreateGameObject("Machine Safety Zone");
            PlacementExclusionRegion region = regionObject.AddComponent<PlacementExclusionRegion>();
            region.ConfigureBox(Vector3.zero, Vector3.one, PlacementTarget.Floor);
            AssetDefinition asset = CreateAsset("Safety Barricade", PlacementType.Floor, Vector3.one * 0.4f);
            asset.SetClearance(true, new Vector3(2f, 0.4f, 2f), Vector3.zero);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate candidate = FloorCandidate(new Vector3(1.1f, 0.2f, 0f));

            bool valid = PlacementValidator.TryValidateCandidate(
                candidate,
                new OrientedBounds(candidate.Position, Vector3.one * 0.4f, Quaternion.identity),
                context,
                asset,
                out RejectionReason reason,
                out string relatedName);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.ClearanceBlocked));
            Assert.That(relatedName, Is.EqualTo(region.name));
        }

        [Test]
        public void SphereExclusionOnlyAffectsSelectedPlacementTargets()
        {
            GameObject regionObject = CreateGameObject("Radial Clearance");
            PlacementExclusionRegion region = regionObject.AddComponent<PlacementExclusionRegion>();
            region.ConfigureSphere(Vector3.zero, 2f, PlacementTarget.InsideSpace);
            OrientedBounds bounds = new(Vector3.zero, Vector3.one, Quaternion.identity);

            Assert.That(region.Intersects(bounds, PlacementType.InsideSpace), Is.True);
            Assert.That(region.Intersects(bounds, PlacementType.Floor), Is.False);
        }

        [Test]
        public void ChildColliderExclusionUsesHierarchyAndAllowsTaggedAssets()
        {
            SemanticTag pathFurniture = CreateAssetTag("Path Furniture");
            GameObject regionObject = CreateGameObject("Curved Path");
            PlacementExclusionRegion region = regionObject.AddComponent<PlacementExclusionRegion>();
            region.ConfigureChildColliders(PlacementTarget.Floor);
            region.SetExemptAssetTags(new[] { pathFurniture });
            GameObject segment = CreateGameObject("Path Segment");
            segment.transform.SetParent(regionObject.transform);
            segment.AddComponent<BoxCollider>().size = new Vector3(3f, 0.2f, 1f);
            Physics.SyncTransforms();

            AssetDefinition ordinary = CreateAsset("Ordinary Rock", PlacementType.Floor, Vector3.one);
            AssetDefinition exempt = CreateAsset("Path Bollard", PlacementType.Floor, Vector3.one);
            SerializedObject serialized = new(exempt);
            SerializedProperty tags = serialized.FindProperty("semanticTags");
            tags.arraySize = 1;
            tags.GetArrayElementAtIndex(0).objectReferenceValue = pathFurniture;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            OrientedBounds overlapping = new(Vector3.zero, Vector3.one, Quaternion.identity);

            Assert.That(region.Intersects(overlapping, PlacementType.Floor, ordinary), Is.True);
            Assert.That(region.Intersects(overlapping, PlacementType.Floor, exempt), Is.False);
            Assert.That(
                region.Intersects(
                    new OrientedBounds(Vector3.right * 5f, Vector3.one, Quaternion.identity),
                    PlacementType.Floor,
                    ordinary),
                Is.False);
        }

        [Test]
        public void AssetRelationCanRequireCompleteBoundsInsideSemanticRegion()
        {
            SemanticTag parking = CreateAssetTag("Parking Region");
            GameObject regionObject = CreateGameObject("Parking Region");
            AssetRelationAnchor region = regionObject.AddComponent<AssetRelationAnchor>();
            region.SetAssetTags(new[] { parking });
            region.SetCustomBounds(true, new Vector3(0f, 1f, 0f), new Vector3(4f, 4f, 4f));
            AssetDefinition car = CreateAsset("Region Car", PlacementType.Floor, Vector3.one);
            car.AssetRelativePlacement.ConfigureTag(
                parking,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                0.1f,
                AssetRelativeFacing.Any);
            car.AssetRelativePlacement.SetRequireInsideAnchorBounds(true);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            PlacementCandidate inside = FloorCandidate(Vector3.zero);
            PlacementCandidate crossingEdge = FloorCandidate(new Vector3(1.75f, 0f, 0f));

            Assert.That(RelativeAnchorProvider.TryValidateCandidate(
                inside,
                new OrientedBounds(inside.Position, Vector3.one, Quaternion.identity),
                car,
                context,
                out RejectionReason insideReason,
                out _), Is.True, insideReason.ToString());
            Assert.That(RelativeAnchorProvider.TryValidateCandidate(
                crossingEdge,
                new OrientedBounds(crossingEdge.Position, Vector3.one, Quaternion.identity),
                car,
                context,
                out RejectionReason outsideReason,
                out _), Is.False);
            Assert.That(outsideReason, Is.EqualTo(RejectionReason.OutsideAssetRelationBounds));
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
        public void ValidatorAcceptsCandidateThatOnlyTouchesFixedSurface()
        {
            GameObject fixedSurface = CreateGameObject("Fixed Floor");
            fixedSurface.transform.position = new Vector3(0f, -0.05f, 0f);
            BoxCollider surfaceCollider = fixedSurface.AddComponent<BoxCollider>();
            surfaceCollider.size = new Vector3(10f, 0.1f, 10f);
            Physics.SyncTransforms();
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
                out _);

            Assert.That(valid, Is.True);
            Assert.That(reason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void ValidatorRejectsFixedSurfacePenetrationBeyondTolerance()
        {
            GameObject fixedSurface = CreateGameObject("Fixed Floor");
            fixedSurface.transform.position = new Vector3(0f, -0.045f, 0f);
            BoxCollider surfaceCollider = fixedSurface.AddComponent<BoxCollider>();
            surfaceCollider.size = new Vector3(10f, 0.1f, 10f);
            Physics.SyncTransforms();
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
                out _);

            Assert.That(valid, Is.False);
            Assert.That(reason, Is.EqualTo(RejectionReason.OverlapsFixed));
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

        [Test]
        public void PathPlacementValidatesDistanceAndAuthoredSide()
        {
            SemanticTag path = CreateAssetTag("Trail Path");
            CreatePathSource(path);
            AssetDefinition asset = CreateAsset("Trail Bench", PlacementType.Floor, Vector3.one);
            asset.PathPlacement.Configure(
                path,
                1f,
                3f,
                PathPlacementSide.Right,
                PathPlacementFacing.Any,
                0f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            Assert.That(PathPlacementSource.TryValidate(
                context,
                asset,
                new Vector3(2f, 0f, 0f),
                out RejectionReason acceptedReason,
                out _), Is.True);
            Assert.That(acceptedReason, Is.EqualTo(RejectionReason.None));

            Assert.That(PathPlacementSource.TryValidate(
                context,
                asset,
                new Vector3(-2f, 0f, 0f),
                out RejectionReason sideReason,
                out _), Is.False);
            Assert.That(sideReason, Is.EqualTo(RejectionReason.WrongPathSide));

            Assert.That(PathPlacementSource.TryValidate(
                context,
                asset,
                new Vector3(4f, 0f, 0f),
                out RejectionReason distanceReason,
                out _), Is.False);
            Assert.That(distanceReason, Is.EqualTo(RejectionReason.OutsidePathDistance));
        }

        [Test]
        public void PathFacingTurnsFloorAssetTowardNearestPathPoint()
        {
            SemanticTag path = CreateAssetTag("Facing Path");
            CreatePathSource(path);
            AssetDefinition asset = CreateAsset("Facing Bench", PlacementType.Floor, Vector3.one);
            asset.PathPlacement.Configure(
                path,
                0f,
                5f,
                PathPlacementSide.Any,
                PathPlacementFacing.TowardPath,
                0f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);
            CandidateSeed seed = new(
                new Vector3(2f, 0f, 0f),
                Quaternion.identity,
                null,
                Vector3.up,
                placementType: PlacementType.Floor);

            PlacementCandidate candidate = CandidateFactory.Create(seed, context, asset, 0, 1, 0f);

            Assert.That(Vector3.Angle(candidate.Rotation * Vector3.forward, Vector3.left), Is.LessThan(0.01f));
        }

        [Test]
        public void PathPlacementRejectsConfiguredEndpointMargin()
        {
            SemanticTag path = CreateAssetTag("Endpoint Path");
            CreatePathSource(path);
            AssetDefinition asset = CreateAsset("Endpoint Sign", PlacementType.Floor, Vector3.one);
            asset.PathPlacement.Configure(
                path,
                0f,
                3f,
                PathPlacementSide.Any,
                PathPlacementFacing.AlongPath,
                0f,
                1.5f);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            Assert.That(PathPlacementSource.TryValidate(
                context,
                asset,
                new Vector3(2f, 0f, 3.5f),
                out RejectionReason endpointReason,
                out _), Is.False);
            Assert.That(endpointReason, Is.EqualTo(RejectionReason.TooCloseToPathEndpoint));

            Assert.That(PathPlacementSource.TryValidate(
                context,
                asset,
                new Vector3(2f, 0f, 0f),
                out RejectionReason centerReason,
                out _), Is.True);
            Assert.That(centerReason, Is.EqualTo(RejectionReason.None));
        }

        [Test]
        public void PathPlacementFacingRemainsContinuousAcrossPolylineVertices()
        {
            SemanticTag path = CreateAssetTag("Curved Path");
            GameObject sourceObject = CreateGameObject("Curved Path Source");
            PathPlacementSource source = sourceObject.AddComponent<PathPlacementSource>();
            source.SetPathTags(new[] { path });
            source.SetWorldPoints(new[]
            {
                new Vector3(0f, 0f, -2f),
                Vector3.zero,
                new Vector3(2f, 0f, 0f)
            });

            Assert.That(source.TryGetNearestFrame(
                new Vector3(0f, 0f, -0.01f),
                out PathPlacementFrame beforeVertex), Is.True);
            Assert.That(source.TryGetNearestFrame(
                new Vector3(0.01f, 0f, 0f),
                out PathPlacementFrame afterVertex), Is.True);

            Assert.That(Vector3.Angle(beforeVertex.Forward, afterVertex.Forward), Is.LessThan(1f));
            Assert.That(
                Vector3.Angle(beforeVertex.Forward, new Vector3(1f, 0f, 1f)),
                Is.LessThan(1f));
        }

        [Test]
        public void RegularPathStationsCreatePairedAnchorsOnBothSides()
        {
            SemanticTag path = CreateAssetTag("Station Path");
            CreatePathSource(path);
            AssetDefinition asset = CreateAsset("Station Bollard", PlacementType.Floor, Vector3.one * 0.25f);
            asset.AssetRelativePlacement.ConfigureTag(
                path,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                0.75f,
                AssetRelativeFacing.Any);
            asset.AssetRelativePlacement.ConfigurePathStations(
                PathPlacementSide.BothSides,
                4f,
                2f,
                0f,
                2);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            IReadOnlyList<RelativeAnchor> anchors = context.GetPathStationAnchors(asset);

            Assert.That(anchors, Has.Count.EqualTo(4));
            Assert.That(anchors.Where(anchor => anchor.Position.x < 0f).Count(), Is.EqualTo(2));
            Assert.That(anchors.Where(anchor => anchor.Position.x > 0f).Count(), Is.EqualTo(2));
            Assert.That(anchors, Has.All.Matches<RelativeAnchor>(anchor =>
                Mathf.Abs(Mathf.Abs(anchor.Position.x) - 2f) < 0.001f));
        }

        [Test]
        public void RegularPathStationsAreCachedPerAssetAndRun()
        {
            SemanticTag path = CreateAssetTag("Cached Path");
            CreatePathSource(path);
            AssetDefinition asset = CreateAsset("Cached Bollard", PlacementType.Floor, Vector3.one * 0.25f);
            asset.AssetRelativePlacement.ConfigureTag(
                path,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                0.75f,
                AssetRelativeFacing.Any);
            asset.AssetRelativePlacement.ConfigurePathStations(
                PathPlacementSide.BothSides,
                4f,
                2f,
                0f,
                2);
            GenerationContext context = CreateContext(SamplingAlgorithm.Random);

            IReadOnlyList<RelativeAnchor> first = context.GetPathStationAnchors(asset);
            IReadOnlyList<RelativeAnchor> second = context.GetPathStationAnchors(asset);

            Assert.That(second, Is.SameAs(first));
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

        private PathPlacementSource CreatePathSource(SemanticTag pathTag)
        {
            GameObject sourceObject = CreateGameObject("Path Source");
            PathPlacementSource source = sourceObject.AddComponent<PathPlacementSource>();
            source.SetPathTags(new[] { pathTag });
            source.SetWorldPoints(new[]
            {
                new Vector3(0f, 0f, -4f),
                new Vector3(0f, 0f, 4f)
            });
            return source;
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

        private PlacementArea CreateTerrainArea(string name, bool ridge)
        {
            const int resolution = 33;
            TerrainData data = new()
            {
                heightmapResolution = resolution,
                size = new Vector3(10f, 5f, 10f)
            };
            float[,] heights = new float[resolution, resolution];
            if (ridge)
            {
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    float normalizedX = x / (float)(resolution - 1);
                    float distance = Mathf.Abs(normalizedX - 0.5f);
                    heights[z, x] = distance < 0.25f
                        ? 1f - distance / 0.25f
                        : 0f;
                }
            }

            data.SetHeights(0, 0, heights);
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = name;
            terrainObject.transform.position = new Vector3(-5f, 0f, -5f);
            _objects.Add(data);
            _objects.Add(terrainObject);
            Physics.SyncTransforms();

            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                ~0,
                floorNormalYThreshold: 0.5f,
                ceilingNormalYThreshold: -0.5f,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);
            return new PlacementArea(
                new SpatialSourceInfo("Test", name, name),
                new Bounds(new Vector3(0f, 2.5f, 0f), new Vector3(10f, 5f, 10f)),
                null,
                null,
                cellSize: 1f,
                settings: settings);
        }

        private SemanticTag CreateTag(string name)
        {
            TagCategory category = CreateSurfaceCategory($"{name} Surface Category");
            return CreateTag(name, category);
        }

        private TagCategory CreateSurfaceCategory(string name)
        {
            TagCategory category = ScriptableObject.CreateInstance<TagCategory>();
            category.name = name;
            category.Initialize(true, TagCategoryUsage.Surface);
            _objects.Add(category);
            return category;
        }

        private SemanticTag CreateTag(string name, TagCategory category)
        {
            SemanticTag tag = ScriptableObject.CreateInstance<SemanticTag>();
            tag.name = name;
            tag.Initialize(category);
            _objects.Add(tag);
            return tag;
        }

        private SemanticTag CreateAssetTag(string name)
        {
            TagCategory category = ScriptableObject.CreateInstance<TagCategory>();
            category.name = $"{name} Asset Category";
            category.Initialize(true, TagCategoryUsage.Asset);
            SemanticTag tag = ScriptableObject.CreateInstance<SemanticTag>();
            tag.name = name;
            tag.Initialize(category);
            _objects.Add(category);
            _objects.Add(tag);
            return tag;
        }

        private static PlacementCandidate FloorCandidate(Vector3 surfacePosition) => new(
            surfacePosition + Vector3.up * 0.5f,
            Quaternion.identity,
            surfaceNormal: Vector3.up,
            placementType: PlacementType.Floor);

        private static PlacementCandidate WallCandidate(Vector3 position) => new(
            position,
            Quaternion.identity,
            surfaceNormal: Vector3.forward,
            placementType: PlacementType.Wall);

        private static PlacementCandidate InsideSpaceCandidate(Vector3 position) => new(
            position,
            Quaternion.identity,
            surfaceNormal: Vector3.up,
            placementType: PlacementType.InsideSpace);

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
                case bool boolValue:
                    property.boolValue = boolValue;
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
