using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.SpaceFoundation.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsFoundation = SpaceFoundationSystem.SpaceFoundation;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.Tests.SpaceFoundation
{
    [Category("Genix.Preset.Quick")]
    [Category("Genix.Preset.Full")]
    [Category("Genix.Area.Spatial")]
    public sealed class SfsVoxelAreaTests
    {
        private readonly List<UnityEngine.Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object value in _objects)
            {
                if (value)
                    UnityEngine.Object.DestroyImmediate(value);
            }

            _objects.Clear();
        }

        [Test]
        public void FloodFillIncludesOwnedBoundaryButDoesNotCrossForeignBoundary()
        {
            const string anchorId = "target";
            Dictionary<Vector3Int, string> borderOwners = new()
            {
                [new Vector3Int(-1, 0, 0)] = anchorId,
                [new Vector3Int(1, 0, 0)] = "other"
            };
            VoxelBounds bounds = new(new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0));

            HashSet<Vector3Int> result = VoxelFloodFill.Fill(
                Vector3Int.zero,
                anchorId,
                borderOwners,
                bounds);

            Assert.That(result.Contains(Vector3Int.zero), Is.True);
            Assert.That(result.Contains(new Vector3Int(-1, 0, 0)), Is.True);
            Assert.That(result.Contains(new Vector3Int(1, 0, 0)), Is.False);
        }

        [Test]
        public void InteriorSeedSearchReturnsUnownedCellAdjacentToTargetBoundary()
        {
            const string anchorId = "target";
            Vector3Int border = Vector3Int.zero;
            Dictionary<Vector3Int, string> borderOwners = new()
            {
                [border] = anchorId
            };
            VoxelBounds bounds = new(Vector3Int.one * -1, Vector3Int.one);

            bool found = VoxelFloodFill.TryFindInteriorSeed(
                new[] { border },
                anchorId,
                borderOwners,
                bounds,
                out Vector3Int seed);

            Assert.That(found, Is.True);
            Assert.That(bounds.Contains(seed), Is.True);
            Assert.That(borderOwners.ContainsKey(seed), Is.False);
            Assert.That((seed - border).sqrMagnitude, Is.EqualTo(1));
        }

        [Test]
        public void AdjacentCellsExposeOnlyOuterWallsAndMergeCollinearFaces()
        {
            VoxelCellMask mask = new(new[]
            {
                Vector3Int.zero,
                Vector3Int.right
            });

            VoxelSurfaceExtractor.VoxelSurfaceExtraction extraction =
                VoxelSurfaceExtractor.ExtractSurfaces(mask, 2f, true, true, true);

            Assert.That(extraction.FloorCells.Count, Is.EqualTo(2));
            Assert.That(extraction.CeilingCells.Count, Is.EqualTo(2));
            Assert.That(extraction.WallRegions.Count, Is.EqualTo(4));
            Assert.That(
                extraction.WallRegions.Count(region =>
                    Mathf.Approximately(Vector3.Distance(region.WallStart, region.WallEnd), 4f)),
                Is.EqualTo(2));
        }

        [Test]
        public void SurfaceExtractionHonorsRequestedKinds()
        {
            VoxelCellMask mask = new(new[] { Vector3Int.zero });

            VoxelSurfaceExtractor.VoxelSurfaceExtraction extraction =
                VoxelSurfaceExtractor.ExtractSurfaces(mask, 1f, false, true, false);

            Assert.That(extraction.FloorCells, Is.Empty);
            Assert.That(extraction.CeilingCells, Is.EquivalentTo(new[] { Vector3Int.zero }));
            Assert.That(extraction.WallRegions, Is.Empty);
        }

        [Test]
        public void PersistentCacheKeyIsIndependentOfBorderDictionaryOrder()
        {
            SfsFoundation foundation = CreateFoundation();
            Dictionary<Vector3Int, string> firstBorders = new()
            {
                [Vector3Int.zero] = "target",
                [Vector3Int.right] = "target",
                [Vector3Int.up] = "other"
            };
            Dictionary<Vector3Int, string> reversedBorders = firstBorders
                .Reverse()
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            PersistentSubspaceCacheKey first = CreateCacheKey(foundation, firstBorders);
            PersistentSubspaceCacheKey reversed = CreateCacheKey(foundation, reversedBorders);

            Assert.That(first, Is.EqualTo(reversed));
            Assert.That(first.ToStableString(), Is.EqualTo(reversed.ToStableString()));
        }

        [Test]
        public void PersistentCacheKeyChangesWithVoxelSize()
        {
            SfsFoundation foundation = CreateFoundation();
            Dictionary<Vector3Int, string> borders = new() { [Vector3Int.zero] = "target" };
            foundation.voxelSize = 1f;
            PersistentSubspaceCacheKey first = CreateCacheKey(foundation, borders);
            foundation.voxelSize = 2f;
            PersistentSubspaceCacheKey second = CreateCacheKey(foundation, borders);

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void PersistentCacheKeyChangesWithBorderOwnership()
        {
            SfsFoundation foundation = CreateFoundation();
            PersistentSubspaceCacheKey first = CreateCacheKey(
                foundation,
                new Dictionary<Vector3Int, string> { [Vector3Int.zero] = "target" });
            PersistentSubspaceCacheKey second = CreateCacheKey(
                foundation,
                new Dictionary<Vector3Int, string> { [Vector3Int.zero] = "other" });

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void FoundationCacheIdentityUsesPersistentUnityObjectIdentity()
        {
            string prefabPath = $"Assets/GenixSfsCacheIdentityTest_{System.Guid.NewGuid():N}.prefab";

            try
            {
                GameObject source = CreateGameObject("Persistent SFS Foundation");
                source.AddComponent<SfsFoundation>().assetName = "Identity Test";

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
                SfsFoundation firstFoundation = prefab.GetComponent<SfsFoundation>();
                string first = PersistentSubspaceCacheKey
                    .CreateLiveSnapshot(firstFoundation, "a0")
                    .ToStableString();

                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
                SfsFoundationUtility.ClearCacheIdentitiesForTests();
                GameObject reloadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                string reloaded = PersistentSubspaceCacheKey
                    .CreateLiveSnapshot(reloadedPrefab.GetComponent<SfsFoundation>(), "a0")
                    .ToStableString();

                Assert.That(first, Does.StartWith("global:"));
                Assert.That(reloaded, Is.EqualTo(first));
            }
            finally
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test]
        public void PersistentCacheAssetsHaveMonoScriptsAndReloadAsConcreteTypes()
        {
            string suffix = System.Guid.NewGuid().ToString("N");
            string subspacePath = $"Assets/GenixSubspaceCacheAssetTest_{suffix}.asset";
            string areaPath = $"Assets/GenixAreaCacheAssetTest_{suffix}.asset";

            try
            {
                SfsSubspaceCacheAsset subspace = ScriptableObject.CreateInstance<SfsSubspaceCacheAsset>();
                SfsAreaCacheAsset area = ScriptableObject.CreateInstance<SfsAreaCacheAsset>();

                Assert.That(MonoScript.FromScriptableObject(subspace), Is.Not.Null);
                Assert.That(MonoScript.FromScriptableObject(area), Is.Not.Null);

                AssetDatabase.CreateAsset(subspace, subspacePath);
                AssetDatabase.CreateAsset(area, areaPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(subspacePath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(areaPath, ImportAssetOptions.ForceUpdate);

                Assert.That(AssetDatabase.LoadAssetAtPath<SfsSubspaceCacheAsset>(subspacePath), Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<SfsAreaCacheAsset>(areaPath), Is.Not.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(subspacePath);
                AssetDatabase.DeleteAsset(areaPath);
            }
        }

        [Test]
        public void PreciseAreaDecompositionPreservesMissingCells()
        {
            HashSet<Vector3Int> cells = new()
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(1, 0, 0),
                new Vector3Int(0, 0, 1)
            };

            List<SurfaceRegion> regions = AreaDecomposer.CreateHorizontalRegions(
                cells,
                2f,
                AreaDecompositionMode.Precise,
                SurfaceKind.Floor);

            Assert.That(regions, Has.Count.EqualTo(2));
            Assert.That(regions.Any(region => region.ContainsXZ(new Vector3(2f, 0f, 2f))), Is.False);
            Assert.That(regions.Sum(region => region.Bounds.size.x * region.Bounds.size.z), Is.EqualTo(12f).Within(0.001f));
        }

        [Test]
        public void FastAreaDecompositionUsesOneBoundingRegionPerLayer()
        {
            HashSet<Vector3Int> cells = new()
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(1, 0, 0),
                new Vector3Int(0, 0, 1)
            };

            List<SurfaceRegion> regions = AreaDecomposer.CreateHorizontalRegions(
                cells,
                2f,
                AreaDecompositionMode.Fast,
                SurfaceKind.Floor);

            Assert.That(regions, Has.Count.EqualTo(1));
            Assert.That(regions[0].ContainsXZ(new Vector3(2f, 0f, 2f)), Is.True);
            Assert.That(regions[0].VoxelLayer, Is.EqualTo(0));
        }

        [Test]
        public void CeilingDecompositionUsesTopOfVoxelLayer()
        {
            List<SurfaceRegion> regions = AreaDecomposer.CreateHorizontalRegions(
                new HashSet<Vector3Int> { new(2, 3, 4) },
                2f,
                AreaDecompositionMode.Precise,
                SurfaceKind.Ceiling);

            Assert.That(regions, Has.Count.EqualTo(1));
            Assert.That(regions[0].SurfaceY, Is.EqualTo(7f));
            Assert.That(regions[0].Normal, Is.EqualTo(Vector3.down));
            Assert.That(regions[0].VoxelLayer, Is.EqualTo(3));
        }

        [Test]
        public void SubspaceCacheRoundTripsCellsAndHonorsMinimumCount()
        {
            SfsSubspaceCacheAsset cache = ScriptableObject.CreateInstance<SfsSubspaceCacheAsset>();
            _objects.Add(cache);
            HashSet<Vector3Int> cells = new()
            {
                new Vector3Int(-2, 1, 4),
                new Vector3Int(-1, 1, 4),
                new Vector3Int(0, 1, 4),
                new Vector3Int(5, 2, -3)
            };

            cache.Store("subspace", cells, maxEntries: 4, maxCells: 100);

            Assert.That(cache.TryGet("subspace", 4, out HashSet<Vector3Int> restored), Is.True);
            Assert.That(restored, Is.EquivalentTo(cells));
            Assert.That(cache.TryGet("subspace", 5, out _), Is.False);
        }

        [Test]
        public void SubspaceCacheAssetEvictsOldestEntryAtCapacity()
        {
            SfsSubspaceCacheAsset cache = ScriptableObject.CreateInstance<SfsSubspaceCacheAsset>();
            _objects.Add(cache);

            cache.Store("first", new HashSet<Vector3Int> { Vector3Int.zero }, 1, 10);
            cache.Store("second", new HashSet<Vector3Int> { Vector3Int.one }, 1, 10);

            Assert.That(cache.Contains("first", 1), Is.False);
            Assert.That(cache.Contains("second", 1), Is.True);
        }

        [Test]
        public void SubspaceCacheAssetHonorsCellBudgetAndClear()
        {
            SfsSubspaceCacheAsset cache = ScriptableObject.CreateInstance<SfsSubspaceCacheAsset>();
            _objects.Add(cache);
            HashSet<Vector3Int> cells = new() { Vector3Int.zero, Vector3Int.one };

            cache.Store("oversized", cells, maxEntries: 4, maxCells: 1);
            Assert.That(cache.Contains("oversized", 1), Is.False);

            cache.Store("kept", new HashSet<Vector3Int> { Vector3Int.zero }, 4, 1);
            cache.Clear();
            Assert.That(cache.Contains("kept", 1), Is.False);
        }

        [Test]
        public void InvalidPersistentCacheKeyIsRejectedWithoutCreatingAssets()
        {
            PersistentSubspaceCacheKey invalid = default;

            Assert.That(invalid.IsValid, Is.False);
            Assert.That(PersistentSubspaceCache.Contains(invalid, 1), Is.False);
            Assert.That(PersistentSubspaceCache.TryGet(invalid, 1, out _), Is.False);
            Assert.That(
                PersistentSubspaceCache.Store(invalid, new HashSet<Vector3Int> { Vector3Int.zero }),
                Is.EqualTo(PersistentSubspaceCacheStoreResult.NotStored));
        }

        [Test]
        public void AreaCacheAssetRoundTripsPlacementArea()
        {
            HashSet<Vector3Int> subspace = new() { Vector3Int.zero, Vector3Int.right };
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                ~0,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.SfsBoundaries);
            PlacementArea original = new(
                new SpatialSourceInfo("SFS", "Cached Area", "cached-area"),
                new Bounds(new Vector3(1f, 1f, 0.5f), new Vector3(4f, 2f, 2f)),
                new[] { SurfaceRegion.CreateFloor("Floor", 0f, 4f, 0f, 2f, 0f, 0) },
                new[]
                {
                    SurfaceRegion.CreateWall(
                        "Wall",
                        Vector3.zero,
                        new Vector3(4f, 0f, 0f),
                        2f,
                        Vector3.forward,
                        0)
                },
                new[] { Vector3Int.zero, Vector3Int.right },
                2f,
                settings,
                subspace,
                new[] { SurfaceRegion.CreateCeiling("Ceiling", 0f, 4f, 0f, 2f, 2f, 0) },
                new[] { Vector3Int.zero, Vector3Int.right },
                subspaceMask: new VoxelCellMask(subspace));
            SfsAreaCacheAsset cache = ScriptableObject.CreateInstance<SfsAreaCacheAsset>();
            _objects.Add(cache);

            cache.Store("area", original, maxEntries: 4, maxSurfaceCells: 100);
            bool found = cache.TryGet(
                "area",
                original.SourceInfo,
                settings,
                subspace,
                2f,
                _ => false,
                out PlacementArea restored);

            Assert.That(found, Is.True);
            Assert.That(restored.WorldBounds, Is.EqualTo(original.WorldBounds));
            Assert.That(restored.FloorRegions, Has.Count.EqualTo(1));
            Assert.That(restored.WallRegions, Has.Count.EqualTo(1));
            Assert.That(restored.CeilingRegions, Has.Count.EqualTo(1));
            Assert.That(restored.FloorCells, Is.EquivalentTo(original.FloorCells));
            Assert.That(restored.CeilingCells, Is.EquivalentTo(original.CeilingCells));
            Assert.That(restored.ContainsVolumePoint(Vector3.zero), Is.True);
        }

        [Test]
        public void AreaCacheAssetEvictsOldestEntryAtCapacity()
        {
            SfsAreaCacheAsset cache = ScriptableObject.CreateInstance<SfsAreaCacheAsset>();
            _objects.Add(cache);
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                ~0,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.SfsBoundaries);
            PlacementArea first = CreateSimpleArea("first", settings);
            PlacementArea second = CreateSimpleArea("second", settings);
            HashSet<Vector3Int> subspace = new() { Vector3Int.zero };

            cache.Store("first", first, maxEntries: 1, maxSurfaceCells: 10);
            cache.Store("second", second, maxEntries: 1, maxSurfaceCells: 10);

            Assert.That(cache.TryGet("first", first.SourceInfo, settings, subspace, 1f, _ => false, out _), Is.False);
            Assert.That(cache.TryGet("second", second.SourceInfo, settings, subspace, 1f, _ => false, out _), Is.True);
        }

        [Test]
        public void BoundsFallbackBuildsFloorWallsCeilingAndVolume()
        {
            GameObject source = CreateGameObject("Fallback Bounds");
            BoxCollider collider = source.AddComponent<BoxCollider>();
            collider.center = new Vector3(1f, 2f, 3f);
            collider.size = new Vector3(4f, 6f, 8f);
            Physics.SyncTransforms();
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                ~0,
                placementTargets: PlacementTarget.All,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.SfsBoundaries);

            bool built = BoundsAreaFallback.TryBuild(
                source,
                new SpatialSourceInfo("SFS", source.name, "fallback"),
                settings,
                value => value == collider,
                out PlacementArea area,
                out string error);

            Assert.That(built, Is.True, error);
            Assert.That(area.WorldBounds, Is.EqualTo(collider.bounds));
            Assert.That(area.FloorRegions, Has.Count.EqualTo(1));
            Assert.That(area.WallRegions, Has.Count.EqualTo(4));
            Assert.That(area.CeilingRegions, Has.Count.EqualTo(1));
            Assert.That(area.SupportsPlacementType(PlacementType.InsideSpace), Is.True);
        }

        [Test]
        public void BoundsFallbackReportsSourceWithoutGeometry()
        {
            GameObject source = CreateGameObject("No Bounds");
            AreaBuildSettings settings = new(AreaDecompositionMode.Precise, ~0);

            bool built = BoundsAreaFallback.TryBuild(
                source,
                new SpatialSourceInfo("SFS", source.name, "missing"),
                settings,
                _ => false,
                out PlacementArea area,
                out string error);

            Assert.That(built, Is.False);
            Assert.That(area, Is.Null);
            Assert.That(error, Does.Contain("neither persistent voxel data"));
        }

        [Test]
        public void SubspaceProviderUsesLiveCellsWithoutPersistentData()
        {
            GameObject source = CreateGameObject("Live SFS Anchor");
            SfsAnchor anchor = source.AddComponent<SfsAnchor>();
            anchor._subspacePositions.Add(Vector3Int.zero);
            anchor._subspacePositions.Add(Vector3Int.right);

            HashSet<Vector3Int> result = SfsSubspaceProvider.Resolve(null, anchor, out SfsSubspaceResolutionInfo info);

            Assert.That(result, Is.SameAs(anchor._subspacePositions));
            Assert.That(info.HasValue, Is.True);
            Assert.That(info.Source, Is.EqualTo(SfsSubspaceResolutionSource.Live));
            Assert.That(info.CellCount, Is.EqualTo(2));
            Assert.That(info.StoreResult, Is.EqualTo(PersistentSubspaceCacheStoreResult.NotStored));
        }

        [Test]
        public void SubspaceProviderReportsMissingPersistentData()
        {
            GameObject source = CreateGameObject("Empty SFS Anchor");
            SfsAnchor anchor = source.AddComponent<SfsAnchor>();

            HashSet<Vector3Int> result = SfsSubspaceProvider.Resolve(null, anchor, out SfsSubspaceResolutionInfo info);

            Assert.That(result, Is.Null);
            Assert.That(info.HasValue, Is.True);
            Assert.That(info.Source, Is.EqualTo(SfsSubspaceResolutionSource.MissingPersistentData));
            Assert.That(info.CellCount, Is.Zero);
        }

        [Test]
        public void SubspaceResolutionInfoMapsCacheSourceAndPreservesMetrics()
        {
            SfsSubspaceResolutionInfo memory = SfsSubspaceResolutionInfo.CacheHit(
                PersistentSubspaceCacheSource.Memory,
                12,
                20,
                3,
                5,
                "memory-key");
            SfsSubspaceResolutionInfo persistent = SfsSubspaceResolutionInfo.CacheHit(
                PersistentSubspaceCacheSource.Persistent,
                8,
                16,
                7,
                9,
                "persistent-key");
            SfsSubspaceResolutionInfo failed = SfsSubspaceResolutionInfo.Failed(
                SfsSubspaceResolutionSource.BoundsTooLarge,
                11,
                2_000_001);

            Assert.That(memory.Source, Is.EqualTo(SfsSubspaceResolutionSource.MemoryCache));
            Assert.That(memory.CacheMilliseconds, Is.EqualTo(3));
            Assert.That(memory.CacheKey, Is.EqualTo("memory-key"));
            Assert.That(persistent.Source, Is.EqualTo(SfsSubspaceResolutionSource.PersistentCache));
            Assert.That(persistent.CellCount, Is.EqualTo(8));
            Assert.That(failed.Source, Is.EqualTo(SfsSubspaceResolutionSource.BoundsTooLarge));
            Assert.That(failed.BoundsCellCount, Is.EqualTo(2_000_001));
        }

        [Test]
        public void AreaBuilderCreatesAllVoxelBoundarySurfaces()
        {
            SfsSpace space = CreateSpace(2f);
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                ~0,
                placementTargets: PlacementTarget.All,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.SfsBoundaries);

            bool built = SfsAreaBuilder.TryBuild(
                space,
                null,
                new SpatialSourceInfo("SFS", "Single Voxel", "single-voxel"),
                new HashSet<Vector3Int> { Vector3Int.zero },
                settings,
                _ => false,
                out PlacementArea area,
                out string error);

            Assert.That(built, Is.True, error);
            Assert.That(Vector3.Distance(area.WorldBounds.center, Vector3.zero), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(area.WorldBounds.size, Vector3.one * 2.02f), Is.LessThan(0.001f));
            Assert.That(area.FloorRegions, Has.Count.EqualTo(1));
            Assert.That(area.CeilingRegions, Has.Count.EqualTo(1));
            Assert.That(area.WallRegions, Has.Count.EqualTo(4));
            Assert.That(area.FloorRegions[0].SurfaceY, Is.EqualTo(-1f));
            Assert.That(area.CeilingRegions[0].SurfaceY, Is.EqualTo(1f));
            Assert.That(area.WallRegions.All(region =>
            {
                float normalAxis = Mathf.Abs(region.Normal.x) > 0.5f ? region.Normal.x : region.Normal.z;
                float plane = Mathf.Abs(region.Normal.x) > 0.5f ? region.WallStart.x : region.WallStart.z;
                return Mathf.Approximately(plane, -normalAxis);
            }), Is.True);
            Assert.That(area.SupportsPlacementType(PlacementType.InsideSpace), Is.True);
        }

        [Test]
        public void AllMatchingAreaBuilderUsesVolumeWithoutSurfaceRegions()
        {
            SfsSpace space = CreateSpace(2f);
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                ~0,
                placementTargets: PlacementTarget.Floor,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);

            bool built = SfsAreaBuilder.TryBuild(
                space,
                null,
                new SpatialSourceInfo("SFS", "Volume Search", "volume-search"),
                new HashSet<Vector3Int> { Vector3Int.zero },
                settings,
                _ => false,
                out PlacementArea area,
                out string error);

            Assert.That(built, Is.True, error);
            Assert.That(area.FloorRegions, Is.Empty);
            Assert.That(area.FloorCells, Is.Empty);
            Assert.That(area.SupportsPlacementType(PlacementType.Floor), Is.True);
            Assert.That(area.UsesAllMatchingSurfaceSearch, Is.True);
        }

        private SfsFoundation CreateFoundation()
        {
            GameObject value = CreateGameObject("SFS Cache Key Test");
            return value.AddComponent<SfsFoundation>();
        }

        private SfsSpace CreateSpace(float voxelSize)
        {
            SfsFoundation foundation = CreateFoundation();
            foundation.voxelSize = voxelSize;
            GameObject value = new("SFS Space Test");
            value.transform.SetParent(foundation.transform);
            value.AddComponent<MeshCollider>();
            _objects.Add(value);
            return value.AddComponent<SfsSpace>();
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject value = new(name);
            _objects.Add(value);
            return value;
        }

        private static PlacementArea CreateSimpleArea(string id, AreaBuildSettings settings)
        {
            return new PlacementArea(
                new SpatialSourceInfo("SFS", id, id),
                new Bounds(Vector3.zero, Vector3.one),
                new[] { SurfaceRegion.CreateFloor(id, -0.5f, 0.5f, -0.5f, 0.5f, -0.5f) },
                null,
                null,
                1f,
                settings);
        }

        private static PersistentSubspaceCacheKey CreateCacheKey(
            SfsFoundation foundation,
            Dictionary<Vector3Int, string> borderOwners)
        {
            List<Vector3Int> borders = borderOwners.Keys.ToList();
            VoxelBounds bounds = VoxelBounds.From(borders);
            PersistentSubspaceData data = new(
                foundation,
                "target",
                borderOwners,
                borders,
                borders[0],
                bounds);
            return PersistentSubspaceCacheKey.Create(data);
        }
    }
}
