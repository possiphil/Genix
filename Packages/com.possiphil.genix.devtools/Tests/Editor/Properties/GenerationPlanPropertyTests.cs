using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Fluent;
using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests.Properties
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SpatialArea)]
    public sealed class GenerationPlanPropertyTests
    {
        private AssetDefinition _asset;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<AssetDefinition>();
            _asset.SetBoundsSize(new Vector3(2f, 2f, 2f));
        }

        [TearDown]
        public void TearDown()
        {
            if (_asset)
                Object.DestroyImmediate(_asset);
        }

        [Test]
        [Category(GenixTestCategories.Property)]
        public void BroadPhaseNeverOmitsAnActuallyIntersectingObject()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-100_000, 100_000));

            GenixProperty.Check(
                nameof(BroadPhaseNeverOmitsAnActuallyIntersectingObject),
                Prop.ForAll(seeds, seed =>
                {
                    GenerationRandom random = new(seed);
                    GenerationPlan plan = new(96);

                    for (int i = 0; i < 96; i++)
                    {
                        Vector3 position = new(
                            random.Range(-50f, 50f),
                            random.Range(-20f, 20f),
                            random.Range(-50f, 50f));
                        plan.Add(_asset, new PlacementCandidate(position, Quaternion.identity), i.ToString());
                    }

                    Bounds query = new(
                        new Vector3(
                            random.Range(-40f, 40f),
                            random.Range(-15f, 15f),
                            random.Range(-40f, 40f)),
                        new Vector3(
                            random.Range(0.1f, 20f),
                            random.Range(0.1f, 20f),
                            random.Range(0.1f, 20f)));
                    HashSet<string> returned = plan.Query(query)
                        .Select(item => item.ObjectName)
                        .ToHashSet();

                    if (returned.Count != plan.Query(query).Count())
                        return false;

                    foreach (PlannedObject item in plan.Objects)
                    {
                        if (item.Bounds.ToAxisAlignedBounds().Intersects(query) && !returned.Contains(item.ObjectName))
                            return false;
                    }

                    return true;
                }));
        }

        [Test]
        public void OversizedObjectsRemainQueryableThroughOverflowStorage()
        {
            _asset.SetBoundsSize(Vector3.one * 10_000f);
            GenerationPlan plan = new();
            plan.Add(_asset, new PlacementCandidate(Vector3.zero, Quaternion.identity), "Huge");

            Assert.That(
                plan.Query(new Bounds(new Vector3(4_999f, 0f, 0f), Vector3.one))
                    .Select(item => item.ObjectName),
                Does.Contain("Huge"));
        }
    }
}
