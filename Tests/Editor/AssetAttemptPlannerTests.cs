using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests
{
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
        public void PruneDominatedPreservesAdaptiveSurfaceCandidateAfterStrictAreaFailure()
        {
            AssetDefinition failedStrict = CreateAsset("Strict Medium", new Vector3(2f, 1f, 2f), SurfaceFitMode.Strict);
            AssetDefinition adaptiveLarge = CreateAsset("Adaptive Large", new Vector3(4f, 1f, 4f), SurfaceFitMode.Adaptive);
            AssetDefinition strictLarge = CreateAsset("Strict Large", new Vector3(4f, 1f, 4f), SurfaceFitMode.Strict);
            AssetDefinition strictSmall = CreateAsset("Strict Small", new Vector3(1f, 1f, 1f), SurfaceFitMode.Strict);
            List<AssetDefinition> remaining = new()
            {
                adaptiveLarge,
                strictLarge,
                strictSmall
            };

            AssetAttemptPlanner.PruneDominated(
                remaining,
                PlacementType.Floor,
                failedStrict,
                RejectionReason.OutsideTargetArea);

            Assert.That(remaining, Does.Contain(adaptiveLarge));
            Assert.That(remaining, Does.Not.Contain(strictLarge));
            Assert.That(remaining, Does.Contain(strictSmall));
        }

        [Test]
        public void PruneDominatedRemovesRemainingAssetsWhenSeedIsTooCloseToGenerated()
        {
            AssetDefinition failed = CreateAsset("Failed", new Vector3(2f, 1f, 2f), SurfaceFitMode.Strict);
            AssetDefinition small = CreateAsset("Small", new Vector3(1f, 1f, 1f), SurfaceFitMode.Strict);
            AssetDefinition adaptive = CreateAsset("Adaptive", new Vector3(4f, 1f, 4f), SurfaceFitMode.Adaptive);
            List<AssetDefinition> remaining = new()
            {
                small,
                adaptive
            };

            AssetAttemptPlanner.PruneDominated(
                remaining,
                PlacementType.Floor,
                failed,
                RejectionReason.TooCloseToGenerated);

            Assert.That(remaining, Is.Empty);
        }

        private AssetDefinition CreateAsset(string assetName, Vector3 size, SurfaceFitMode surfaceFitMode)
        {
            GameObject prefab = new(assetName + " Prefab");
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.name = assetName;
            asset.Initialize(prefab, size);
            SetSurfaceFitMode(asset, surfaceFitMode);
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
