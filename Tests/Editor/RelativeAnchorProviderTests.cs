using Genix.Assets;
using Genix.Core;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.PlacementArea)]
    public sealed class RelativeAnchorProviderTests
    {
        private const int AnchorLayer = 30;
        private GenerationTestScene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = new GenerationTestScene();
        }

        [TearDown]
        public void TearDown()
        {
            _scene.Dispose();
        }

        [Test]
        public void DisabledRelativePlacementAcceptsEveryCandidate()
        {
            GenerationContext context = CreateContext(RelativePlacementSettings.Disabled);

            bool accepted = RelativeAnchorProvider.IsCandidateInRange(
                new PlacementCandidate(Vector3.one * 100f, Quaternion.identity),
                context,
                out string relatedName);

            Assert.That(accepted, Is.True);
            Assert.That(relatedName, Is.Empty);
            Assert.That(RelativeAnchorProvider.HasAnyAnchor(context), Is.True);
        }

        [Test]
        public void SelectedAnchorUsesDistanceToItsBounds()
        {
            GameObject anchor = CreateAnchor("Selected Anchor", Vector3.zero, Vector3.one * 4f);
            GenerationContext context = CreateContext(new RelativePlacementSettings(
                RelativePlacementSource.SelectedObjects,
                1f,
                ~0,
                new[] { anchor.transform }));

            bool accepted = RelativeAnchorProvider.IsCandidateInRange(
                new PlacementCandidate(new Vector3(2.5f, 0f, 0f), Quaternion.identity),
                context,
                out string relatedName);

            Assert.That(accepted, Is.True);
            Assert.That(relatedName, Is.EqualTo(anchor.name));
        }

        [Test]
        public void RelativeDistanceIncludesVerticalAxis()
        {
            GameObject anchor = CreateAnchor("Vertical Anchor", Vector3.zero, Vector3.one * 2f);
            GenerationContext context = CreateContext(new RelativePlacementSettings(
                RelativePlacementSource.SelectedObjects,
                2f,
                ~0,
                new[] { anchor.transform }));

            bool accepted = RelativeAnchorProvider.IsCandidateInRange(
                new PlacementCandidate(new Vector3(0f, 5f, 0f), Quaternion.identity),
                context,
                out _);

            Assert.That(accepted, Is.False);
        }

        [Test]
        public void SceneAnchorsRespectLayerAndEnabledState()
        {
            GameObject included = CreateAnchor("Included Scene Anchor", Vector3.zero, Vector3.one);
            included.layer = AnchorLayer;
            GameObject wrongLayer = CreateAnchor("Wrong Layer", Vector3.right * 2f, Vector3.one);
            wrongLayer.layer = AnchorLayer - 1;
            GameObject disabled = CreateAnchor("Disabled Anchor", Vector3.left * 2f, Vector3.one);
            disabled.layer = AnchorLayer;
            disabled.GetComponent<BoxCollider>().enabled = false;
            Physics.SyncTransforms();

            GenerationContext context = CreateContext(new RelativePlacementSettings(
                RelativePlacementSource.SceneObjects,
                1f,
                1 << AnchorLayer,
                null));

            Assert.That(context.SceneRelativeAnchors, Has.Count.EqualTo(1));
            Assert.That(context.SceneRelativeAnchors[0].Name, Is.EqualTo(included.name));
        }

        [Test]
        public void GeneratedPlanObjectBecomesRelativeAnchor()
        {
            AssetDefinition asset = _scene.CreateAsset("Generated Anchor Asset");
            GenerationContext context = CreateContext(new RelativePlacementSettings(
                RelativePlacementSource.GeneratedObjects,
                1f,
                ~0,
                null));
            context.Plan.Add(
                asset,
                new PlacementCandidate(Vector3.zero, Quaternion.identity),
                "Planned Anchor");

            bool accepted = RelativeAnchorProvider.IsCandidateInRange(
                new PlacementCandidate(new Vector3(0.9f, 0f, 0f), Quaternion.identity),
                context,
                out string relatedName);

            Assert.That(accepted, Is.True);
            Assert.That(relatedName, Is.EqualTo("Planned Anchor"));
        }

        [Test]
        public void NearestAnchorReturnsClosestSelectedBounds()
        {
            GameObject first = CreateAnchor("First", Vector3.zero, Vector3.one);
            GameObject second = CreateAnchor("Second", Vector3.right * 10f, Vector3.one);
            GenerationContext context = CreateContext(new RelativePlacementSettings(
                RelativePlacementSource.SelectedObjects,
                20f,
                ~0,
                new[] { first.transform, second.transform }));

            bool found = RelativeAnchorProvider.TryFindNearestAnchor(
                context,
                new Vector3(9f, 0f, 0f),
                out RelativeAnchor nearest);

            Assert.That(found, Is.True);
            Assert.That(nearest.Name, Is.EqualTo(second.name));
        }

        private GenerationContext CreateContext(RelativePlacementSettings relativePlacement)
        {
            GenerationRequest source = _scene.CreateRequest();
            GenerationRequest request = new(
                source.AreaSource,
                source.AssetPool,
                source.ObjectCount,
                source.PlacementTargets,
                source.TargetDistributionMode,
                source.TargetDistributionWeights,
                source.StyleSettings,
                source.AreaBuildSettings,
                relativePlacement,
                useFixedSeed: true,
                randomSeed: 123);
            return _scene.CreateContext(request);
        }

        private GameObject CreateAnchor(string name, Vector3 position, Vector3 size)
        {
            GameObject anchor = _scene.CreateGameObject(name);
            anchor.transform.position = position;
            BoxCollider collider = anchor.AddComponent<BoxCollider>();
            collider.size = size;
            Physics.SyncTransforms();
            return anchor;
        }
    }
}
