using System;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Integration)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class GenerationContextFactoryTests
    {
        private GenerationTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new GenerationTestScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        [Test]
        public void CreateLimitsAreaBuildToTargetsSupportedByResolvedAssets()
        {
            AssetDefinition floorAsset = _scene.CreateAsset("Floor", PlacementType.Floor);
            GenerationRequest request = _scene.CreateRequest(targets: PlacementTarget.All);

            GenerationContext context = GenerationContextFactory.Create(
                request,
                _scene.GeneratedRoot.transform,
                new[] { floorAsset });

            Assert.That(context.Area, Is.SameAs(_scene.Area));
            Assert.That(_scene.AreaSource.LastSettings.placementTargets, Is.EqualTo(PlacementTarget.Floor));
            Assert.That(_scene.AreaSource.LastSettings.profile, Is.SameAs(context.AreaBuildProfile));
        }

        [Test]
        public void CreateRemovesZeroWeightTargetsBeforeBuildingArea()
        {
            GenerationRequest request = _scene.CreateRequest(
                targets: PlacementTarget.Floor | PlacementTarget.Wall,
                distribution: TargetDistributionMode.Weighted,
                weights: new TargetDistributionWeights(1, 0, 0, 0));

            GenerationContextFactory.Create(request, _scene.GeneratedRoot.transform);

            Assert.That(_scene.AreaSource.LastSettings.placementTargets, Is.EqualTo(PlacementTarget.Floor));
        }

        [Test]
        public void CreateSurfacesAreaBuildFailureToCaller()
        {
            _scene.AreaSource.Error = "Synthetic area failure";

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                GenerationContextFactory.Create(_scene.CreateRequest(), _scene.GeneratedRoot.transform));

            Assert.That(exception.Message, Does.Contain("Synthetic area failure"));
        }

        [Test]
        public void CreateRejectsMissingRequestAndParent()
        {
            Assert.Throws<ArgumentNullException>(() =>
                GenerationContextFactory.Create(null, _scene.GeneratedRoot.transform));
            Assert.Throws<ArgumentException>(() =>
                GenerationContextFactory.Create(_scene.CreateRequest(), null));
        }
    }
}
