using System.Collections.Generic;
using System.Linq;
using Genix.Authoring;
using Genix.SpaceFoundation.Editor;
using NUnit.Framework;
using SpaceFoundationSystem;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests.SpaceFoundation
{
    [Category("Genix.Preset.Quick")]
    [Category("Genix.Preset.Full")]
    [Category("Genix.Area.Spatial")]
    public sealed class SfsAuthoringTests
    {
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _objects)
            {
                if (value)
                    Object.DestroyImmediate(value);
            }

            _objects.Clear();
        }

        [Test]
        public void WorldSizesRoundUpWithoutShrinkingRequestedInterior()
        {
            Vector3Int cells = SfsAuthoringPlanner.WorldSizeToCells(new Vector3(10f, 4f, 11f), 3f);

            Assert.That(cells, Is.EqualTo(new Vector3Int(4, 2, 4)));
            Assert.That((Vector3)cells * 3f, Is.EqualTo(new Vector3(12f, 6f, 12f)));
        }

        [Test]
        public void BoundedLocationUsesExactVoxelCountsAndSixBoundaryVolumes()
        {
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.BoundedLocation);
            request.SizeMode = SfsAuthoringSizeMode.VoxelCounts;
            request.VoxelCounts = new Vector3Int(10, 5, 8);

            bool created = SfsAuthoringPlanner.TryCreate(request, 2f, out SfsAuthoringPlan plan, out string error);

            Assert.That(created, Is.True, error);
            Assert.That(plan.ActualSize, Is.EqualTo(new Vector3(20f, 10f, 16f)));
            Assert.That(plan.InteriorVolumes, Has.Count.EqualTo(1));
            Assert.That(plan.Delimiters, Has.Count.EqualTo(6));
            Assert.That(plan.Anchors, Has.Count.EqualTo(1));
            Assert.That(plan.InteriorVolumes[0].Size, Is.EqualTo(request.VoxelCounts));
            Assert.That(VolumesOverlap(plan.InteriorVolumes[0], plan.Delimiters[0]), Is.False);
        }

        [Test]
        public void BoundedWorldSizeReportsSnappedActualCenterAndRoundedSize()
        {
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.BoundedLocation);
            request.SizeMode = SfsAuthoringSizeMode.WorldUnits;
            request.Center = new Vector3(0.6f, 0f, -0.6f);
            request.WorldSize = new Vector3(10f, 10f, 10f);

            bool created = SfsAuthoringPlanner.TryCreate(request, 3f, out SfsAuthoringPlan plan, out string error);

            Assert.That(created, Is.True, error);
            Assert.That(plan.ActualSize, Is.EqualTo(Vector3.one * 12f));
            Assert.That(Vector3.Distance(plan.RequestedCenter, plan.ActualCenter), Is.LessThanOrEqualTo(3f * 0.8661f));
        }

        [Test]
        public void StackedGridCreatesOneSharedSeparatorAndTwoAnchors()
        {
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.LocationGrid);
            request.GridCounts = new Vector3Int(1, 2, 1);
            request.UniformRoomCells = new Vector3Int(4, 3, 5);
            request.SeparatorCells = Vector3Int.one;

            bool created = SfsAuthoringPlanner.TryCreate(request, 1f, out SfsAuthoringPlan plan, out string error);

            Assert.That(created, Is.True, error);
            Assert.That(plan.LocationCount, Is.EqualTo(2));
            Assert.That(plan.Anchors, Has.Count.EqualTo(2));
            Assert.That(plan.Delimiters, Has.Count.EqualTo(7));
            Assert.That(plan.ActualSize, Is.EqualTo(new Vector3(4f, 7f, 5f)));
            Assert.That(plan.Delimiters.Count(value => value.Name.StartsWith("Separator Y")), Is.EqualTo(1));
            Assert.That(plan.Anchors[1].Cell.y, Is.GreaterThan(plan.Anchors[0].Cell.y));
        }

        [Test]
        public void LargeGridUsesSharedAxisSlabsInsteadOfPerRoomWalls()
        {
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.LocationGrid);
            request.GridCounts = new Vector3Int(3, 4, 6);
            request.UniformRoomCells = new Vector3Int(2, 3, 4);
            request.SeparatorCells = Vector3Int.one;

            bool created = SfsAuthoringPlanner.TryCreate(request, 1f, out SfsAuthoringPlan plan, out string error);

            Assert.That(created, Is.True, error);
            Assert.That(plan.LocationCount, Is.EqualTo(72));
            Assert.That(plan.Delimiters, Has.Count.EqualTo(6 + 2 + 3 + 5));
            Assert.That(plan.Anchors, Has.Count.EqualTo(72));
            Assert.That(plan.ActualSize, Is.EqualTo(new Vector3(8f, 15f, 29f)));
        }

        [Test]
        public void PerAxisGridSizesPreserveAlignedRowsColumnsAndLevels()
        {
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.LocationGrid);
            request.GridCounts = new Vector3Int(3, 2, 2);
            request.UsePerAxisRoomSizes = true;
            Set(request.XRoomCells, 2, 3, 4);
            Set(request.YRoomCells, 5, 7);
            Set(request.ZRoomCells, 6, 8);
            request.SeparatorCells = new Vector3Int(1, 2, 3);

            bool created = SfsAuthoringPlanner.TryCreate(request, 1f, out SfsAuthoringPlan plan, out string error);

            Assert.That(created, Is.True, error);
            Assert.That(plan.LocationCount, Is.EqualTo(12));
            Assert.That(plan.ActualSize, Is.EqualTo(new Vector3(11f, 14f, 17f)));
            Assert.That(plan.InteriorVolumes.Select(value => value.Size.x).Distinct(), Is.EquivalentTo(new[] { 2, 3, 4 }));
            Assert.That(plan.InteriorVolumes.Select(value => value.Size.y).Distinct(), Is.EquivalentTo(new[] { 5, 7 }));
            Assert.That(plan.InteriorVolumes.Select(value => value.Size.z).Distinct(), Is.EquivalentTo(new[] { 6, 8 }));
        }

        [Test]
        public void GridInteriorNeverOccupiesDelimiterCells()
        {
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.LocationGrid);
            request.GridCounts = new Vector3Int(2, 2, 2);
            request.UniformRoomCells = new Vector3Int(3, 3, 3);
            request.SeparatorCells = Vector3Int.one;

            SfsAuthoringPlanner.TryCreate(request, 1f, out SfsAuthoringPlan plan, out string error);

            Assert.That(plan, Is.Not.Null, error);
            foreach (SfsAuthoringCellVolume interior in plan.InteriorVolumes)
            foreach (SfsAuthoringCellVolume delimiter in plan.Delimiters)
                Assert.That(VolumesOverlap(interior, delimiter), Is.False, $"{interior.Name} overlaps {delimiter.Name}");
        }

        [TestCase((int)SfsFootprintTemplate.Rectangle)]
        [TestCase((int)SfsFootprintTemplate.LShape)]
        [TestCase((int)SfsFootprintTemplate.UShape)]
        [TestCase((int)SfsFootprintTemplate.TShape)]
        [TestCase((int)SfsFootprintTemplate.Courtyard)]
        public void BuiltInFootprintTemplatesAreConnected(int templateValue)
        {
            HashSet<Vector2Int> mask = SfsAuthoringPlanner.CreateFootprintMask(
                (SfsFootprintTemplate)templateValue,
                new Vector2Int(5, 5),
                null);

            Assert.That(mask, Is.Not.Empty);
            Assert.That(SfsAuthoringPlanner.IsConnected(mask), Is.True);
        }

        [Test]
        public void CustomFootprintRejectsDisconnectedModules()
        {
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.FootprintLocation);
            request.FootprintTemplate = SfsFootprintTemplate.Custom;
            request.CustomFootprint.Add(Vector2Int.zero);
            request.CustomFootprint.Add(new Vector2Int(2, 0));

            bool created = SfsAuthoringPlanner.TryCreate(request, 1f, out _, out string error);

            Assert.That(created, Is.False);
            Assert.That(error, Does.Contain("connected"));
        }

        [Test]
        public void LFootprintCreatesOneLocationAndOnlyOccupiedInteriorModules()
        {
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.FootprintLocation);
            request.FootprintTemplate = SfsFootprintTemplate.LShape;
            request.FootprintDimensions = new Vector2Int(4, 4);
            request.FootprintTileCells = new Vector2Int(2, 3);
            request.FootprintHeightCells = 5;

            bool created = SfsAuthoringPlanner.TryCreate(request, 2f, out SfsAuthoringPlan plan, out string error);

            Assert.That(created, Is.True, error);
            Assert.That(plan.LocationCount, Is.EqualTo(1));
            Assert.That(plan.InteriorVolumes, Has.Count.EqualTo(7));
            Assert.That(plan.Anchors, Has.Count.EqualTo(1));
            Assert.That(plan.Delimiters, Is.Not.Empty);
            Assert.That(plan.ActualSize, Is.EqualTo(new Vector3(16f, 10f, 24f)));
        }

        [Test]
        public void SceneBuilderCreatesExpectedComponentsLayersAndAnchorRanges()
        {
            SpaceFoundationSystem.SpaceFoundation foundation = CreateFoundation(2f);
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.LocationGrid);
            request.GridCounts = new Vector3Int(2, 1, 1);
            request.UniformRoomCells = new Vector3Int(3, 3, 3);
            SfsAuthoringPlanner.TryCreate(request, foundation.voxelSize, out SfsAuthoringPlan plan, out string planError);

            GameObject root = SfsAuthoringSceneBuilder.CreateLayout(plan, foundation, out string error);
            _objects.Add(root);

            Assert.That(root, Is.Not.Null, error + planError);
            Assert.That(root.transform.position, Is.EqualTo(plan.ActualCenter));
            Assert.That(root.GetComponentsInChildren<Delimiter>(), Has.Length.EqualTo(plan.Delimiters.Count));
            Assert.That(root.GetComponentsInChildren<Anchor>(), Has.Length.EqualTo(plan.Anchors.Count));
            Assert.That(root.GetComponentsInChildren<BoxCollider>(), Has.Length.EqualTo(plan.Delimiters.Count));

            SfsAuthoringLayoutDisplay display = root.GetComponent<SfsAuthoringLayoutDisplay>();
            Assert.That(display, Is.Not.Null);
            Assert.That(display.AlwaysShowFreeSpace, Is.False);
            Assert.That(display.LocalVolumes, Has.Count.EqualTo(plan.InteriorVolumes.Count));

            for (int i = 0; i < plan.InteriorVolumes.Count; i++)
            {
                Bounds expected = plan.InteriorVolumes[i].ToWorldBounds(plan.VoxelSize);
                Assert.That(display.LocalVolumes[i].center, Is.EqualTo(expected.center - plan.ActualCenter));
                Assert.That(display.LocalVolumes[i].size, Is.EqualTo(expected.size));
            }

            int layer = LayerMask.NameToLayer(SfsAuthoringSceneBuilder.DelimiterLayerName);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0));
            Assert.That(root.GetComponentsInChildren<Delimiter>().All(value => value.gameObject.layer == layer), Is.True);
            Assert.That((foundation.delimitingLayerMask.value & (1 << layer)) != 0, Is.True);

            Anchor[] anchors = root.GetComponentsInChildren<Anchor>();
            Assert.That(anchors.All(value => value.correspondingSpaceFoundation == foundation), Is.True);
            Assert.That(anchors.Select(value => value.GetMaxDistance()), Is.All.GreaterThan(0f));
            Assert.That(anchors.Select(value => value.name), Is.EquivalentTo(new[] { "Test Layout 1", "Test Layout 2" }));

            for (int i = 0; i < anchors.Length; i++)
            {
                SfsAuthoringAnchorPlan anchorPlan = plan.Anchors.Single(value => value.Name == anchors[i].name);
                Vector3 expectedWorldPosition = anchorPlan.ToWorldPosition(plan.VoxelSize);
                Assert.That(anchors[i].transform.position, Is.EqualTo(expectedWorldPosition));
                Assert.That(anchors[i].transform.localPosition, Is.EqualTo(expectedWorldPosition - plan.ActualCenter));
            }
        }

        [Test]
        public void BoundedLocationAnchorUsesLayoutName()
        {
            AssertSingleLocationAnchorUsesLayoutName(SfsAuthoringLayoutType.BoundedLocation);
        }

        [Test]
        public void ExistingBoundedLayoutCanRecoverFreeSpaceDisplay()
        {
            SpaceFoundationSystem.SpaceFoundation foundation = CreateFoundation(1f);
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.BoundedLocation);
            request.SizeMode = SfsAuthoringSizeMode.VoxelCounts;
            request.VoxelCounts = new Vector3Int(6, 4, 8);
            SfsAuthoringPlanner.TryCreate(request, foundation.voxelSize, out SfsAuthoringPlan plan, out _);
            GameObject root = SfsAuthoringSceneBuilder.CreateLayout(plan, foundation, out string createError);
            _objects.Add(root);
            Object.DestroyImmediate(root.GetComponent<SfsAuthoringLayoutDisplay>());

            bool added = SfsAuthoringSceneBuilder.TryAddFreeSpaceDisplay(root, out string error);
            SfsAuthoringLayoutDisplay display = root.GetComponent<SfsAuthoringLayoutDisplay>();

            Assert.That(root, Is.Not.Null, createError);
            Assert.That(added, Is.True, error);
            Assert.That(display, Is.Not.Null);
            Assert.That(display.LocalVolumes, Has.Count.EqualTo(1));
            Assert.That(display.LocalVolumes[0].center, Is.EqualTo(Vector3.zero));
            Assert.That(display.LocalVolumes[0].size, Is.EqualTo(plan.ActualSize));
        }

        [Test]
        public void FootprintLocationAnchorUsesLayoutName()
        {
            AssertSingleLocationAnchorUsesLayoutName(SfsAuthoringLayoutType.FootprintLocation);
        }

        private static void AssertSingleLocationAnchorUsesLayoutName(SfsAuthoringLayoutType layoutType)
        {
            SfsAuthoringRequest request = CreateRequest(layoutType);

            bool created = SfsAuthoringPlanner.TryCreate(request, 1f, out SfsAuthoringPlan plan, out string error);

            Assert.That(created, Is.True, error);
            Assert.That(plan.Anchors, Has.Count.EqualTo(1));
            Assert.That(plan.Anchors[0].Name, Is.EqualTo("Test Layout"));
        }

        [Test]
        public void GeneratedColliderCoversOnlyPlannedCellCenters()
        {
            SpaceFoundationSystem.SpaceFoundation foundation = CreateFoundation(2f);
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.BoundedLocation);
            request.SizeMode = SfsAuthoringSizeMode.VoxelCounts;
            request.VoxelCounts = new Vector3Int(2, 2, 2);
            SfsAuthoringPlanner.TryCreate(request, foundation.voxelSize, out SfsAuthoringPlan plan, out _);
            GameObject root = SfsAuthoringSceneBuilder.CreateLayout(plan, foundation, out string error);
            _objects.Add(root);

            SfsAuthoringCellVolume leftPlan = plan.Delimiters.Single(value => value.Name.EndsWith("Left"));
            BoxCollider left = root.GetComponentsInChildren<BoxCollider>().Single(value => value.name.EndsWith("Left"));
            Bounds plannedBounds = leftPlan.ToWorldBounds(foundation.voxelSize);

            Assert.That(root, Is.Not.Null, error);
            Assert.That(left.bounds.center, Is.EqualTo(plannedBounds.center));
            Assert.That(left.bounds.Contains((Vector3)leftPlan.Min * foundation.voxelSize), Is.True);
            Vector3 adjacentInteriorCenter = (Vector3)(leftPlan.Min + Vector3Int.right) * foundation.voxelSize;
            Assert.That(left.bounds.Contains(adjacentInteriorCenter), Is.False);
        }

        [Test]
        public void QuickAddDelimiterSnapsToGridAndCoversRequestedCells()
        {
            SpaceFoundationSystem.SpaceFoundation foundation = CreateFoundation(2f);
            Delimiter delimiter = SfsAuthoringSceneBuilder.CreateGridAlignedBoxDelimiter(
                new Vector3(0.9f, 1.1f, 0.8f),
                new Vector3Int(4, 4, 1),
                foundation);
            _objects.Add(delimiter.gameObject);
            Physics.SyncTransforms();

            BoxCollider collider = delimiter.GetComponent<BoxCollider>();
            Assert.That(delimiter.transform.position, Is.EqualTo(new Vector3(1f, 1f, 0f)));
            Assert.That(collider.size, Is.EqualTo(new Vector3(7.84f, 7.84f, 1.84f)));

            int mask = 1 << LayerMask.NameToLayer(SfsAuthoringSceneBuilder.DelimiterLayerName);
            Vector3 halfVoxel = Vector3.one * foundation.voxelSize * 0.5f;
            Collider[] buffer = new Collider[4];
            for (int y = -1; y <= 2; y++)
            for (int x = -1; x <= 2; x++)
            {
                int hits = Physics.OverlapBoxNonAlloc(
                    new Vector3(x * 2f, y * 2f, 0f),
                    halfVoxel,
                    buffer,
                    Quaternion.identity,
                    mask);
                Assert.That(hits, Is.GreaterThan(0), $"Expected delimiter cell ({x}, {y}, 0) was not blocked.");
            }

            int adjacentHits = Physics.OverlapBoxNonAlloc(
                new Vector3(0f, 0f, 2f),
                halfVoxel,
                buffer,
                Quaternion.identity,
                mask);
            Assert.That(adjacentHits, Is.Zero, "The one-cell wall also blocked an adjacent depth layer.");
        }

        [Test]
        public void AdjacentGridMatchesSfsVoxelOverlapContract()
        {
            SpaceFoundationSystem.SpaceFoundation foundation = CreateFoundation(2f);
            SfsAuthoringRequest request = CreateRequest(SfsAuthoringLayoutType.LocationGrid);
            request.Center = Vector3.one * 100000f;
            request.GridCounts = new Vector3Int(2, 1, 1);
            request.UniformRoomCells = new Vector3Int(2, 2, 2);
            request.SeparatorCells = Vector3Int.one;
            SfsAuthoringPlanner.TryCreate(request, foundation.voxelSize, out SfsAuthoringPlan plan, out _);
            GameObject root = SfsAuthoringSceneBuilder.CreateLayout(plan, foundation, out string error);
            _objects.Add(root);
            Physics.SyncTransforms();

            int layer = LayerMask.NameToLayer(SfsAuthoringSceneBuilder.DelimiterLayerName);
            int mask = 1 << layer;
            Vector3 halfVoxel = Vector3.one * (foundation.voxelSize * 0.5f);
            Collider[] buffer = new Collider[32];

            Assert.That(root, Is.Not.Null, error);
            foreach (Vector3Int cell in EnumerateCells(plan.InteriorVolumes))
            {
                int hits = Physics.OverlapBoxNonAlloc(
                    (Vector3)cell * foundation.voxelSize,
                    halfVoxel,
                    buffer,
                    Quaternion.identity,
                    mask);
                Assert.That(hits, Is.Zero, $"Free cell {cell} was classified as a delimiter cell.");
            }

            foreach (Vector3Int cell in EnumerateCells(plan.Delimiters))
            {
                int hits = Physics.OverlapBoxNonAlloc(
                    (Vector3)cell * foundation.voxelSize,
                    halfVoxel,
                    buffer,
                    Quaternion.identity,
                    mask);
                Assert.That(hits, Is.GreaterThan(0), $"Planned delimiter cell {cell} was not blocked.");
            }
        }

        private SpaceFoundationSystem.SpaceFoundation CreateFoundation(float voxelSize)
        {
            GameObject gameObject = new("SFS Authoring Test Foundation");
            _objects.Add(gameObject);
            SpaceFoundationSystem.SpaceFoundation foundation = gameObject.AddComponent<SpaceFoundationSystem.SpaceFoundation>();
            foundation.voxelSize = voxelSize;
            return foundation;
        }

        private static SfsAuthoringRequest CreateRequest(SfsAuthoringLayoutType layoutType)
        {
            return new SfsAuthoringRequest
            {
                Name = "Test Layout",
                LayoutType = layoutType,
                Center = Vector3.zero,
                WorldSize = new Vector3(10f, 4f, 10f),
                VoxelCounts = new Vector3Int(10, 4, 10),
                UniformRoomCells = new Vector3Int(10, 4, 10),
                GridCounts = Vector3Int.one,
                SeparatorCells = Vector3Int.one,
                FootprintDimensions = new Vector2Int(4, 4),
                FootprintTileCells = new Vector2Int(2, 2),
                FootprintHeightCells = 4
            };
        }

        private static void Set(List<int> target, params int[] values)
        {
            target.Clear();
            target.AddRange(values);
        }

        private static bool VolumesOverlap(SfsAuthoringCellVolume first, SfsAuthoringCellVolume second)
        {
            Vector3Int firstMax = first.Min + first.Size - Vector3Int.one;
            Vector3Int secondMax = second.Min + second.Size - Vector3Int.one;
            return first.Min.x <= secondMax.x && firstMax.x >= second.Min.x &&
                   first.Min.y <= secondMax.y && firstMax.y >= second.Min.y &&
                   first.Min.z <= secondMax.z && firstMax.z >= second.Min.z;
        }

        private static IEnumerable<Vector3Int> EnumerateCells(IEnumerable<SfsAuthoringCellVolume> volumes)
        {
            HashSet<Vector3Int> cells = new();
            foreach (SfsAuthoringCellVolume volume in volumes)
            for (int z = 0; z < volume.Size.z; z++)
            for (int y = 0; y < volume.Size.y; y++)
            for (int x = 0; x < volume.Size.x; x++)
                cells.Add(volume.Min + new Vector3Int(x, y, z));
            return cells;
        }
    }
}
