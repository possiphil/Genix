using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Placement.Providers;
using Genix.Profiling;
using Genix.Layouts;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Semantics;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.PlacementArea)]
    public sealed class SurfaceProjectionTests
    {
        private const int TestLayer = 31;
        private readonly List<UnityEngine.Object> _objects = new();
        private PlacementArea _area;
        private BoxCollider _floor;
        private BoxCollider _ceiling;
        private AssetDefinition _asset;

        [SetUp]
        public void SetUp()
        {
            int mask = 1 << TestLayer;
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                mask,
                surfaceRaycastHeight: 1f,
                surfaceRaycastDistance: 20f,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);

            _floor = CreateBox("Projection Floor", new Vector3(0f, -0.5f, 0f), new Vector3(8f, 1f, 8f));
            _ceiling = CreateBox("Projection Ceiling", new Vector3(0f, 5.5f, 0f), new Vector3(8f, 1f, 8f));
            _area = new PlacementArea(
                new SpatialSourceInfo("Test", "Projection Area", "projection-area"),
                new Bounds(new Vector3(0f, 2.5f, 0f), new Vector3(10f, 5f, 10f)),
                null,
                null,
                cellSize: 1f,
                settings: settings);

            GameObject prefab = CreateObject("Projection Asset Prefab");
            _asset = ScriptableObject.CreateInstance<AssetDefinition>();
            _objects.Add(_asset);
            _asset.Initialize(prefab, new Vector3(2f, 1f, 2f));
            GenerationTestScene.SetSerialized(_asset, "minSurfaceSupport", 1f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i])
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void FloorProjectionFindsNearestMatchingCollider()
        {
            bool projected = _area.TryProjectToFloor(new Vector3(1f, 4f, 1f), out SurfacePoint point);

            Assert.That(projected, Is.True);
            Assert.That(point.SurfaceCollider, Is.SameAs(_floor));
            Assert.That(point.Position.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(Vector3.Dot(point.Normal, Vector3.up), Is.GreaterThan(0.99f));
        }

        [Test]
        public void CeilingProjectionFindsMatchingUnderside()
        {
            bool projected = _area.TryProjectToCeiling(new Vector3(-1f, 1f, -1f), out SurfacePoint point);

            Assert.That(projected, Is.True);
            Assert.That(point.SurfaceCollider, Is.SameAs(_ceiling));
            Assert.That(point.Position.y, Is.EqualTo(5f).Within(0.001f));
            Assert.That(Vector3.Dot(point.Normal, Vector3.down), Is.GreaterThan(0.99f));
        }

        [Test]
        public void GeneratedColliderWithSurfaceDescriptorRemainsProjectableInLaterRun()
        {
            GameObject generatedRoot = CreateObject("Generated Support Root");
            generatedRoot.AddComponent<GeneratedObjectMetadata>().Initialize(PlacementType.Floor);
            GameObject supportObject = CreateObject("Generated Desk Top");
            supportObject.transform.SetParent(generatedRoot.transform);
            BoxCollider support = supportObject.AddComponent<BoxCollider>();
            supportObject.transform.position = new Vector3(0f, 1.5f, 0f);
            supportObject.transform.localScale = new Vector3(4f, 1f, 4f);
            supportObject.layer = TestLayer;
            supportObject.AddComponent<PlacementSurfaceDescriptor>();
            Physics.SyncTransforms();

            bool projected = _area.TryProjectToFloor(new Vector3(0f, 4f, 0f), out SurfacePoint point);

            Assert.That(projected, Is.True);
            Assert.That(point.SurfaceCollider, Is.SameAs(support));
            Assert.That(point.Position.y, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void CollectFloorSurfacesReturnsEveryMatchingLevel()
        {
            BoxCollider raisedFloor = CreateBox(
                "Raised Floor",
                new Vector3(0f, 1.5f, 0f),
                new Vector3(8f, 1f, 8f));
            Physics.SyncTransforms();
            List<SurfacePoint> points = new();

            int count = _area.CollectFloorSurfaces(Vector3.zero, points);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(points.Exists(point => point.SurfaceCollider == _floor), Is.True);
            Assert.That(points.Exists(point => point.SurfaceCollider == raisedFloor), Is.True);
        }

        [Test]
        public void SurfaceFitReturnsFullSupportOnFlatCollider()
        {
            bool valid = _area.TryEvaluateSurfaceFit(
                Vector3.zero,
                Quaternion.identity,
                _asset,
                _floor,
                null,
                PlacementType.Floor,
                out SurfaceFitResult result);

            Assert.That(valid, Is.True);
            Assert.That(result.SupportRatio, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.Position.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.HeightDifference, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FullSupportRejectsFootprintOutsideAreaBeforeRaycasts()
        {
            GenerationProfilerRecorder profiler = new();

            bool valid = _area.TryEvaluateSurfaceFit(
                new Vector3(5f, 0f, 0f),
                Quaternion.identity,
                _asset,
                _floor,
                null,
                PlacementType.Floor,
                out _,
                profiler);

            Assert.That(valid, Is.False);
            Assert.That(profiler.Profile.GetTarget(PlacementType.Floor).RaycastCalls, Is.Zero);
        }

        [Test]
        public void SurfaceFitCacheReusesEquivalentProbe()
        {
            GenerationProfilerRecorder profiler = new();
            SurfaceFitCache cache = new();

            bool first = cache.TryEvaluate(
                _area,
                Vector3.zero,
                Quaternion.identity,
                _asset,
                _floor,
                null,
                PlacementType.Floor,
                out SurfaceFitResult firstResult,
                profiler);
            int raycastsAfterFirst = profiler.Profile.GetTarget(PlacementType.Floor).RaycastCalls;
            bool second = cache.TryEvaluate(
                _area,
                new Vector3(0.0002f, 0f, 0f),
                Quaternion.identity,
                _asset,
                _floor,
                null,
                PlacementType.Floor,
                out SurfaceFitResult secondResult,
                profiler);

            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(secondResult.Position, Is.EqualTo(firstResult.Position));
            Assert.That(profiler.Profile.GetTarget(PlacementType.Floor).RaycastCalls, Is.EqualTo(raycastsAfterFirst));
        }

        [Test]
        public void WallProjectionFindsVerticalColliderFacingArea()
        {
            BoxCollider wall = CreateBox(
                "Projection Wall",
                new Vector3(0f, 2.5f, -0.5f),
                new Vector3(8f, 5f, 1f));
            Physics.SyncTransforms();

            bool projected = _area.TryProjectToWall(
                new Vector3(1f, 2f, 0f),
                Vector3.forward,
                null,
                out SurfacePoint point);

            Assert.That(projected, Is.True);
            Assert.That(point.SurfaceCollider, Is.SameAs(wall));
            Assert.That(point.Position.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(Vector3.Dot(point.Normal, Vector3.forward), Is.GreaterThan(0.99f));
        }

        [Test]
        public void WallProjectionFindsPositiveBoundaryAtCenteredVoxelEdge()
        {
            GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _objects.Add(wallObject);
            wallObject.name = "Positive Boundary Plane";
            wallObject.layer = TestLayer;
            wallObject.transform.position = new Vector3(0f, 0f, 0.5f);
            wallObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            wallObject.transform.localScale = new Vector3(0.1f, 1f, 0.1f);
            MeshCollider wall = wallObject.GetComponent<MeshCollider>();
            Physics.SyncTransforms();

            int mask = 1 << TestLayer;
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                mask,
                surfaceRaycastDistance: 20f,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.NearSfsBoundaries);
            PlacementArea occupiedArea = new(
                new SpatialSourceInfo("Test", "Occupied Projection Area", "occupied-projection-area"),
                new Bounds(Vector3.zero, Vector3.one),
                null,
                null,
                cellSize: 1f,
                settings: settings,
                subspaceCells: new[] { Vector3Int.zero });

            bool projected = occupiedArea.TryProjectToWall(
                new Vector3(0f, 0f, 0.5f),
                Vector3.back,
                null,
                out SurfacePoint point);

            Assert.That(projected, Is.True);
            Assert.That(point.SurfaceCollider, Is.SameAs(wall));
            Assert.That(point.Position.z, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(Vector3.Dot(point.Normal, Vector3.back), Is.GreaterThan(0.99f));
        }

        [Test]
        public void SourceColliderIsExcludedFromProjection()
        {
            int mask = 1 << TestLayer;
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                mask,
                surfaceRaycastHeight: 1f,
                surfaceRaycastDistance: 20f,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);
            PlacementArea sourceExcludedArea = new(
                new SpatialSourceInfo("Test", "Source Excluded", "source-excluded"),
                _area.WorldBounds,
                null,
                null,
                settings: settings,
                isSourceCollider: collider => collider == _floor);

            bool projected = sourceExcludedArea.TryProjectToFloor(Vector3.zero, out _);

            Assert.That(projected, Is.False);
        }

        [Test]
        public void ExplicitHorizontalRegionRejectsCoordinatesOutsideItsBounds()
        {
            SurfaceRegion floorRegion = SurfaceRegion.CreateFloor("Limited Floor", -1f, 1f, -1f, 1f, 0f);
            SurfaceRegion ceilingRegion = SurfaceRegion.CreateCeiling("Limited Ceiling", -1f, 1f, -1f, 1f, 5f);

            Assert.That(_area.TryProjectToFloor(Vector3.right * 2f, floorRegion, out _), Is.False);
            Assert.That(_area.TryProjectToCeiling(Vector3.right * 2f, ceilingRegion, out _), Is.False);
        }

        [Test]
        public void CollectCeilingSurfacesReturnsEveryMatchingUnderside()
        {
            BoxCollider lowerCeiling = CreateBox(
                "Lower Ceiling",
                new Vector3(0f, 3.5f, 0f),
                new Vector3(8f, 1f, 8f));
            Physics.SyncTransforms();
            List<SurfacePoint> points = new();

            int count = _area.CollectCeilingSurfaces(Vector3.zero, points);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(points.Exists(point => point.SurfaceCollider == _ceiling), Is.True);
            Assert.That(points.Exists(point => point.SurfaceCollider == lowerCeiling), Is.True);
        }

        [Test]
        public void SurfaceCollectionRejectsMissingDestinationList()
        {
            Assert.That(_area.CollectFloorSurfaces(Vector3.zero, null), Is.Zero);
            Assert.That(_area.CollectCeilingSurfaces(Vector3.zero, null), Is.Zero);
        }

        [Test]
        public void ProjectionRejectsTargetsWhoseSurfaceLayersAreDisabled()
        {
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                0,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);
            PlacementArea disabled = new(
                new SpatialSourceInfo("Test", "Disabled", "disabled"),
                _area.WorldBounds,
                null,
                null,
                settings: settings);

            Assert.That(disabled.TryProjectToFloor(Vector3.zero, out _), Is.False);
            Assert.That(disabled.TryProjectToCeiling(Vector3.zero, out _), Is.False);
            Assert.That(disabled.TryProjectToWall(Vector3.zero, Vector3.forward, null, out _), Is.False);
        }

        [Test]
        public void WallProjectionFallsBackToForwardForMissingNormal()
        {
            BoxCollider wall = CreateBox(
                "Fallback Wall",
                new Vector3(0f, 2.5f, -0.5f),
                new Vector3(8f, 5f, 1f));
            Physics.SyncTransforms();

            bool projected = _area.TryProjectToWall(
                new Vector3(1f, 2f, 0f),
                Vector3.zero,
                null,
                out SurfacePoint point);

            Assert.That(projected, Is.True);
            Assert.That(point.SurfaceCollider, Is.SameAs(wall));
            Assert.That(Vector3.Dot(point.Normal, Vector3.forward), Is.GreaterThan(0.99f));
        }

        [Test]
        public void SurfaceFitRejectsMissingAssetsAndUnsupportedPlacementTypes()
        {
            Assert.That(_area.TryEvaluateSurfaceFit(
                Vector3.zero,
                Quaternion.identity,
                null,
                _floor,
                null,
                PlacementType.Floor,
                out _), Is.False);
            Assert.That(_area.TryEvaluateSurfaceFit(
                Vector3.zero,
                Quaternion.identity,
                _asset,
                _floor,
                null,
                PlacementType.InsideSpace,
                out _), Is.False);
        }

        [Test]
        public void WallSurfaceFitProbesTheWallFacingFootprint()
        {
            BoxCollider wall = CreateBox(
                "Adaptive Wall",
                new Vector3(0f, 2.5f, -4.75f),
                new Vector3(8f, 5f, 0.5f));
            Physics.SyncTransforms();

            bool valid = _area.TryEvaluateSurfaceFit(
                new Vector3(0f, 2.5f, -4.5f),
                Quaternion.LookRotation(Vector3.forward, Vector3.up),
                _asset,
                wall,
                null,
                PlacementType.Wall,
                out SurfaceFitResult result);

            Assert.That(valid, Is.True);
            Assert.That(result.SupportRatio, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.HeightDifference, Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(result.Normal, Vector3.forward), Is.GreaterThan(0.99f));
        }

        [Test]
        public void WallSurfaceFitRejectsFootprintWithoutRequiredSupport()
        {
            BoxCollider wall = CreateBox(
                "Narrow Adaptive Wall",
                new Vector3(0f, 2.5f, -4.75f),
                new Vector3(0.5f, 5f, 0.5f));
            Physics.SyncTransforms();

            bool valid = _area.TryEvaluateSurfaceFit(
                new Vector3(0f, 2.5f, -4.5f),
                Quaternion.LookRotation(Vector3.forward, Vector3.up),
                _asset,
                wall,
                null,
                PlacementType.Wall,
                out _);

            Assert.That(valid, Is.False);
        }

        [Test]
        public void TerrainSurfaceFitUsesDirectHeightSamplingWithoutPhysicsRaycasts()
        {
            TerrainCollider terrain = CreateFlatTerrain("Projection Terrain", 2f);
            GenerationProfilerRecorder profiler = new();

            bool valid = _area.TryEvaluateSurfaceFit(
                new Vector3(0f, 2f, 0f),
                Quaternion.identity,
                _asset,
                terrain,
                null,
                PlacementType.Floor,
                out SurfaceFitResult result,
                profiler);

            Assert.That(valid, Is.True);
            Assert.That(result.SupportRatio, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.Position.y, Is.EqualTo(2f).Within(0.01f));
            Assert.That(result.Normal.y, Is.GreaterThan(0.99f));
            Assert.That(profiler.Profile.GetTarget(PlacementType.Floor).RaycastCalls, Is.Zero);
        }

        [TestCase(SamplingAlgorithm.Random)]
        [TestCase(SamplingAlgorithm.BridsonPoissonDisk)]
        public void AllMatchingProvidersCollectMultipleFloorAndCeilingLevels(SamplingAlgorithm algorithm)
        {
            BoxCollider raisedFloor = CreateBox(
                "Provider Raised Floor",
                new Vector3(0f, 1.5f, 0f),
                new Vector3(10f, 1f, 10f));
            BoxCollider lowerCeiling = CreateBox(
                "Provider Lower Ceiling",
                new Vector3(0f, 3.5f, 0f),
                new Vector3(10f, 1f, 10f));
            Physics.SyncTransforms();
            GenerationProfilerRecorder floorProfiler = new();
            GenerationProfilerRecorder ceilingProfiler = new();

            List<CandidateSeed> floorSeeds = new HorizontalSurfaceCandidateProvider(candidateCount: 16)
                .CreateCandidateSeeds(
                    CreateContext(PlacementTarget.Floor, algorithm),
                    profiler: floorProfiler);
            List<CandidateSeed> ceilingSeeds = new CeilingCandidateProvider(candidateCount: 16)
                .CreateCandidateSeeds(
                    CreateContext(PlacementTarget.Ceiling, algorithm),
                    profiler: ceilingProfiler);

            Assert.That(floorSeeds, Has.Some.Matches<CandidateSeed>(seed => seed.SurfaceCollider == raisedFloor));
            Assert.That(ceilingSeeds, Has.Some.Matches<CandidateSeed>(seed => seed.SurfaceCollider == lowerCeiling));
            Assert.That(floorSeeds, Has.All.Matches<CandidateSeed>(seed => seed.PlacementType == PlacementType.Floor));
            Assert.That(ceilingSeeds, Has.All.Matches<CandidateSeed>(seed => seed.PlacementType == PlacementType.Ceiling));
            Assert.That(floorProfiler.Profile.GetTarget(PlacementType.Floor).RawSamples, Is.GreaterThan(0));
            Assert.That(ceilingProfiler.Profile.GetTarget(PlacementType.Ceiling).ProjectionAttempts, Is.GreaterThan(0));
        }

        [Test]
        public void AllMatchingWallProviderProjectsOntoPhysicalBoundaryWalls()
        {
            BoxCollider south = CreateBox("South Wall", new Vector3(0f, 2.5f, -4.75f), new Vector3(10f, 6f, 0.5f));
            BoxCollider north = CreateBox("North Wall", new Vector3(0f, 2.5f, 4.75f), new Vector3(10f, 6f, 0.5f));
            BoxCollider west = CreateBox("West Wall", new Vector3(-4.75f, 2.5f, 0f), new Vector3(0.5f, 6f, 10f));
            BoxCollider east = CreateBox("East Wall", new Vector3(4.75f, 2.5f, 0f), new Vector3(0.5f, 6f, 10f));
            Physics.SyncTransforms();
            _area = CreateAllMatchingArea("Boundary Wall Area");

            List<CandidateSeed> seeds = new WallCandidateProvider(candidateCount: 32)
                .CreateCandidateSeeds(CreateContext(PlacementTarget.Wall, SamplingAlgorithm.Random));

            Assert.That(seeds, Is.Not.Empty);
            Assert.That(seeds, Has.All.Matches<CandidateSeed>(seed =>
                seed.PlacementType == PlacementType.Wall &&
                Mathf.Abs(seed.SurfaceNormal.y) < 0.01f));
            Assert.That(seeds, Has.Some.Matches<CandidateSeed>(seed =>
                seed.SurfaceCollider == south ||
                seed.SurfaceCollider == north ||
                seed.SurfaceCollider == west ||
                seed.SurfaceCollider == east));
            Assert.That(seeds.Count, Is.LessThanOrEqualTo(32));
        }

        [Test]
        public void AllMatchingWallProviderDiscoversWallInsideTheVolume()
        {
            GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _objects.Add(wallObject);
            wallObject.name = "Interior Wall Plane";
            wallObject.layer = TestLayer;
            wallObject.transform.position = new Vector3(0f, 2.5f, 0f);
            wallObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            wallObject.transform.localScale = new Vector3(0.8f, 1f, 0.5f);
            MeshCollider interiorWall = wallObject.GetComponent<MeshCollider>();
            Physics.SyncTransforms();
            _area = CreateAllMatchingArea("Interior Wall Area");

            List<CandidateSeed> seeds = new WallCandidateProvider(candidateCount: 64)
                .CreateCandidateSeeds(CreateContext(PlacementTarget.Wall, SamplingAlgorithm.Grid));

            Assert.That(seeds, Has.Some.Matches<CandidateSeed>(seed => seed.SurfaceCollider == interiorWall));
            Assert.That(seeds.Count, Is.LessThanOrEqualTo(64));
        }

        [Test]
        public void AllMatchingWallProviderDiscoversBothSidesOfTerrainRidge()
        {
            CreateRidgeTerrain("Provider Ridge Terrain");
            int mask = 1 << TestLayer;
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                mask,
                surfaceRaycastHeight: 1f,
                surfaceRaycastDistance: 20f,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);
            _area = new PlacementArea(
                new SpatialSourceInfo("Test", "Ridge Area", "ridge-area"),
                new Bounds(new Vector3(0f, 2.5f, 0f), new Vector3(8f, 5f, 8f)),
                null,
                null,
                cellSize: 1f,
                settings: settings);

            List<CandidateSeed> seeds = new WallCandidateProvider(candidateCount: 128)
                .CreateCandidateSeeds(CreateContext(PlacementTarget.Wall, SamplingAlgorithm.Grid));

            Assert.That(_area.HasTerrainSurfaces, Is.True);
            Assert.That(_area.SupportsPlacementType(PlacementType.Wall), Is.True);
            Assert.That(seeds, Has.Some.Matches<CandidateSeed>(seed => seed.SurfaceNormal.x > 0.25f));
            Assert.That(seeds, Has.Some.Matches<CandidateSeed>(seed => seed.SurfaceNormal.x < -0.25f));
            Assert.That(seeds, Has.All.Matches<CandidateSeed>(seed => seed.PlacementType == PlacementType.Wall));
            Assert.That(seeds.Count, Is.LessThanOrEqualTo(128));
        }

        [Test]
        public void SurfaceFitRejectsFootprintWithoutRequiredSupport()
        {
            BoxCollider smallPlatform = CreateBox(
                "Small Platform",
                new Vector3(0f, 1.5f, 0f),
                new Vector3(1f, 1f, 1f));
            Physics.SyncTransforms();

            bool valid = _area.TryEvaluateSurfaceFit(
                new Vector3(0f, 2f, 0f),
                Quaternion.identity,
                _asset,
                smallPlatform,
                null,
                PlacementType.Floor,
                out _);

            Assert.That(valid, Is.False);
        }

        [Test]
        public void CeilingSurfaceFitUsesDownwardNormal()
        {
            bool valid = _area.TryEvaluateSurfaceFit(
                new Vector3(0f, 5f, 0f),
                Quaternion.identity,
                _asset,
                _ceiling,
                null,
                PlacementType.Ceiling,
                out SurfaceFitResult result);

            Assert.That(valid, Is.True);
            Assert.That(result.Position.y, Is.EqualTo(5f).Within(0.001f));
            Assert.That(Vector3.Dot(result.Normal, Vector3.down), Is.GreaterThan(0.99f));
        }

        private BoxCollider CreateBox(string name, Vector3 position, Vector3 size)
        {
            GameObject value = CreateObject(name);
            value.layer = TestLayer;
            value.transform.position = position;
            BoxCollider collider = value.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        private TerrainCollider CreateFlatTerrain(string name, float surfaceHeight)
        {
            const int resolution = 33;
            TerrainData data = new()
            {
                heightmapResolution = resolution,
                size = new Vector3(8f, 5f, 8f)
            };
            float[,] heights = new float[resolution, resolution];
            float normalizedHeight = surfaceHeight / data.size.y;

            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
                heights[z, x] = normalizedHeight;

            data.SetHeights(0, 0, heights);
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = name;
            terrainObject.layer = TestLayer;
            terrainObject.transform.position = new Vector3(-4f, 0f, -4f);
            _objects.Add(data);
            _objects.Add(terrainObject);
            Physics.SyncTransforms();
            return terrainObject.GetComponent<TerrainCollider>();
        }

        private TerrainCollider CreateRidgeTerrain(string name)
        {
            const int resolution = 33;
            TerrainData data = new()
            {
                heightmapResolution = resolution,
                size = new Vector3(8f, 5f, 8f)
            };
            float[,] heights = new float[resolution, resolution];

            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = x / (float)(resolution - 1);
                float ridgeDistance = Mathf.Abs(normalizedX - 0.5f);
                heights[z, x] = ridgeDistance < 0.25f
                    ? 1f - ridgeDistance / 0.25f
                    : 0f;
            }

            data.SetHeights(0, 0, heights);
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = name;
            terrainObject.layer = TestLayer;
            terrainObject.transform.position = new Vector3(-4f, 0f, -4f);
            _objects.Add(data);
            _objects.Add(terrainObject);
            Physics.SyncTransforms();
            return terrainObject.GetComponent<TerrainCollider>();
        }

        private PlacementArea CreateAllMatchingArea(string name)
        {
            int mask = 1 << TestLayer;
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                mask,
                surfaceRaycastHeight: 1f,
                surfaceRaycastDistance: 20f,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);
            return new PlacementArea(
                new SpatialSourceInfo("Test", name, name),
                new Bounds(new Vector3(0f, 2.5f, 0f), new Vector3(10f, 5f, 10f)),
                null,
                null,
                cellSize: 1f,
                settings: settings);
        }

        private GenerationContext CreateContext(PlacementTarget target, SamplingAlgorithm algorithm)
        {
            GameObject generatedRoot = CreateObject("Provider Generated Root");
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            pool.Initialize("Provider Pool", AssetPoolMode.Static);
            _objects.Add(pool);
            StubAreaSource source = new(generatedRoot.transform, _area);
            StyleSettings style = new(
                string.Empty,
                algorithm,
                new PlacementSettings(),
                new CandidateSettings(2, 1, false),
                new GridSettings(1f, 0f),
                new ClusterSettings(2, 1f),
                new PoissonSettings(1f, 30));
            GenerationRequest request = new(
                source,
                pool,
                8,
                target,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                style,
                default,
                useFixedSeed: true,
                randomSeed: 123);
            return new GenerationContext(request, generatedRoot.transform, _area);
        }

        private GameObject CreateObject(string name)
        {
            GameObject value = new(name);
            _objects.Add(value);
            return value;
        }

        private sealed class StubAreaSource : IAreaSource
        {
            private readonly PlacementArea _area;

            public SpatialSourceInfo SourceInfo => _area.SourceInfo;
            public Transform ParentTransform { get; }
            public IReadOnlyList<SemanticTag> SemanticTags => Array.Empty<SemanticTag>();
            public IReadOnlyList<TagCategory> AnyTagCategories => Array.Empty<TagCategory>();

            public StubAreaSource(Transform parentTransform, PlacementArea area)
            {
                ParentTransform = parentTransform;
                _area = area;
            }

            public bool IsSourceCollider(Collider collider) => false;

            public bool TryBuildArea(AreaBuildSettings settings, out PlacementArea area, out string error)
            {
                area = _area;
                error = string.Empty;
                return true;
            }
        }
    }
}
