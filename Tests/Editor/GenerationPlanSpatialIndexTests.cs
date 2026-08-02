using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SpatialArea)]
    public sealed class GenerationPlanSpatialIndexTests
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
        public void QueryReturnsNearbyPlannedObjects()
        {
            AssetDefinition asset = CreateAsset(Vector3.one);
            GenerationPlan plan = new();

            plan.Add(asset, new PlacementCandidate(Vector3.zero, Quaternion.identity), "Near");
            plan.Add(asset, new PlacementCandidate(new Vector3(20f, 0f, 20f), Quaternion.identity), "Far");

            List<string> names = plan.Query(new Bounds(Vector3.zero, Vector3.one * 3f))
                .Select(plannedObject => plannedObject.ObjectName)
                .ToList();

            Assert.That(names.Contains("Near"), Is.True);
            Assert.That(names.Contains("Far"), Is.False);
        }

        [Test]
        public void QueryIgnoresObjectsOutsideVerticalBounds()
        {
            AssetDefinition asset = CreateAsset(Vector3.one);
            GenerationPlan plan = new();

            plan.Add(asset, new PlacementCandidate(new Vector3(0f, 25f, 0f), Quaternion.identity), "Above");

            List<string> names = plan.Query(new Bounds(Vector3.zero, Vector3.one * 3f))
                .Select(plannedObject => plannedObject.ObjectName)
                .ToList();

            Assert.That(names.Contains("Above"), Is.False);
        }

        [Test]
        public void ClearRemovesIndexedPlannedObjects()
        {
            AssetDefinition asset = CreateAsset(Vector3.one);
            GenerationPlan plan = new();

            plan.Add(asset, new PlacementCandidate(Vector3.zero, Quaternion.identity), "Near");
            plan.Clear();

            Assert.That(plan.Query(new Bounds(Vector3.zero, Vector3.one * 3f)).ToList().Count, Is.EqualTo(0));
        }

        private AssetDefinition CreateAsset(Vector3 size)
        {
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.SetBoundsSize(size);
            _createdObjects.Add(asset);
            return asset;
        }
    }
}
