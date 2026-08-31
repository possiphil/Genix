using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.PlacementArea)]
    public sealed class AssetAttemptPlannerTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in _createdObjects)
            {
                if (createdObject)
                    Object.DestroyImmediate(createdObject);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void CreateOrderKeepsAllSizesAvailableAfterRandomStart()
        {
            AssetDefinition small = CreateAsset("Small", new Vector3(1f, 1f, 1f), SurfaceFitMode.Strict);
            AssetDefinition medium = CreateAsset("Medium", new Vector3(2f, 1f, 2f), SurfaceFitMode.Strict);
            AssetDefinition large = CreateAsset("Large", new Vector3(4f, 1f, 4f), SurfaceFitMode.Strict);
            AssetDefinition[] assets = { large, medium, small };

            for (int seed = 0; seed < 32; seed++)
            {
                List<AssetDefinition> order = AssetAttemptPlanner.CreateOrder(
                    assets,
                    PlacementType.Floor,
                    new GenerationRandom(seed));

                Assert.That(order, Has.Count.EqualTo(assets.Length));
                Assert.That(order, Is.EquivalentTo(assets));
            }
        }

        [Test]
        public void CreateOrderPromotesReadyDependenciesWithoutDroppingAssets()
        {
            AssetDefinition root = CreateAsset("Root", Vector3.one, SurfaceFitMode.Strict);
            AssetDefinition dependent = CreateAsset("Dependent", Vector3.one, SurfaceFitMode.Strict);
            AssetDefinition other = CreateAsset("Other", Vector3.one, SurfaceFitMode.Strict);
            AssetAttemptPlanner.Catalog catalog = AssetAttemptPlanner.CreateCatalog(
                new[] { root, dependent, other });
            List<AssetDefinition> order = new();

            catalog.CreateOrder(
                PlacementType.Floor,
                new GenerationRandom(17),
                order,
                _ => true,
                asset => asset == dependent);

            Assert.That(order, Has.Count.EqualTo(3));
            Assert.That(order[0], Is.SameAs(dependent));
            Assert.That(order, Is.EquivalentTo(new[] { root, dependent, other }));
        }

        [Test]
        public void PruneRemainingPreservesAllValidAssetsAfterGeometryFailure()
        {
            AssetDefinition adaptiveLarge = CreateAsset("Adaptive Large", new Vector3(4f, 1f, 4f), SurfaceFitMode.Adaptive);
            AssetDefinition strictLarge = CreateAsset("Strict Large", new Vector3(4f, 1f, 4f), SurfaceFitMode.Strict);
            AssetDefinition strictSmall = CreateAsset("Strict Small", new Vector3(1f, 1f, 1f), SurfaceFitMode.Strict);
            List<AssetDefinition> remaining = new()
            {
                adaptiveLarge,
                strictLarge,
                strictSmall
            };

            AssetAttemptPlanner.PruneRemaining(
                remaining,
                0,
                RejectionReason.OutsideTargetArea);

            Assert.That(remaining, Does.Contain(adaptiveLarge));
            Assert.That(remaining, Does.Contain(strictLarge));
            Assert.That(remaining, Does.Contain(strictSmall));
        }

        [Test]
        public void PruneRemainingDropsAssetsWhenSeedIsTooClose()
        {
            AssetDefinition small = CreateAsset("Small", new Vector3(1f, 1f, 1f), SurfaceFitMode.Strict);
            AssetDefinition adaptive = CreateAsset("Adaptive", new Vector3(4f, 1f, 4f), SurfaceFitMode.Adaptive);
            List<AssetDefinition> remaining = new()
            {
                small,
                adaptive
            };

            AssetAttemptPlanner.PruneRemaining(
                remaining,
                0,
                RejectionReason.TooCloseToGenerated);

            Assert.That(remaining, Is.Empty);
        }

        [Test]
        public void CatalogHandlesMissingInputsAndClearsOrderForMissingType()
        {
            AssetDefinition floor = CreateAsset("Floor", Vector3.one, SurfaceFitMode.Strict);
            AssetAttemptPlanner.Catalog catalog = AssetAttemptPlanner.CreateCatalog(null);
            List<AssetDefinition> reusableOrder = new() { floor };

            catalog.CreateOrder(PlacementType.Wall, new GenerationRandom(1), reusableOrder);

            Assert.That(reusableOrder, Is.Empty);
            Assert.That(catalog.CreateOrder(PlacementType.InsideSpace, new GenerationRandom(1)), Is.Empty);
            Assert.That(() => catalog.CreateOrder(
                PlacementType.Floor,
                new GenerationRandom(1),
                null), Throws.ArgumentNullException);
        }

        [Test]
        public void PruneRemainingHandlesMissingListsAndRemovesInvalidTail()
        {
            AssetDefinition first = CreateAsset("First", Vector3.one, SurfaceFitMode.Strict);
            AssetDefinition last = CreateAsset("Last", Vector3.one, SurfaceFitMode.Strict);
            List<AssetDefinition> remaining = new() { first, null, last };

            Assert.DoesNotThrow(() => AssetAttemptPlanner.PruneRemaining(null, 0, RejectionReason.OutsideTargetArea));
            AssetAttemptPlanner.PruneRemaining(remaining, 1, RejectionReason.OutsideTargetArea);

            Assert.That(remaining, Is.EqualTo(new[] { first, last }));
        }

        [TestCase(PlacementType.Wall)]
        [TestCase(PlacementType.InsideSpace)]
        public void CreateOrderSupportsNonFloorFootprintPolicies(PlacementType placementType)
        {
            AssetDefinition first = CreateAsset("First", new Vector3(1f, 4f, 2f), SurfaceFitMode.Strict, placementType);
            AssetDefinition second = CreateAsset("Second", new Vector3(3f, 2f, 5f), SurfaceFitMode.Strict, placementType);

            List<AssetDefinition> order = AssetAttemptPlanner.CreateOrder(
                new[] { first, second },
                placementType,
                new GenerationRandom(7));

            Assert.That(order, Is.EquivalentTo(new[] { first, second }));
        }

        private AssetDefinition CreateAsset(
            string assetName,
            Vector3 size,
            SurfaceFitMode surfaceFitMode,
            PlacementType placementType = PlacementType.Floor)
        {
            GameObject prefab = new(assetName + " Prefab");
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.name = assetName;
            asset.Initialize(prefab, size);
            SetSurfaceFitMode(asset, surfaceFitMode);
            SerializedObject serializedAsset = new(asset);
            serializedAsset.FindProperty("placementType").enumValueIndex = (int)placementType;
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
            _createdObjects.Add(prefab);
            _createdObjects.Add(asset);
            return asset;
        }

        private static void SetSurfaceFitMode(AssetDefinition asset, SurfaceFitMode surfaceFitMode)
        {
            SerializedObject serializedAsset = new(asset);
            SerializedProperty surfaceFitModeProperty = serializedAsset.FindProperty("surfaceFitMode");
            surfaceFitModeProperty.enumValueIndex = (int)surfaceFitMode;
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
