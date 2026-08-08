using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Infrastructure;
using Genix.Editor.SceneConfiguration;
using Genix.Editor.Validation;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Integration)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class SceneSetupValidatorTests
    {
        private GenerationTestScene _scene;
        private bool _catalogExisted;
        private readonly List<GameObject> _sceneObjects = new();

        [SetUp]
        public void SetUp()
        {
            _catalogExisted = AssetDatabase.LoadAssetAtPath<AssetCatalog>(ProjectContentPaths.AssetCatalog);
        }

        [TearDown]
        public void TearDown()
        {
            _scene?.Dispose();

            foreach (GameObject sceneObject in _sceneObjects.Where(sceneObject => sceneObject))
                Object.DestroyImmediate(sceneObject);

            _sceneObjects.Clear();

            if (!_catalogExisted)
                AssetDatabase.DeleteAsset(ProjectContentPaths.AssetCatalog);
        }

        [Test]
        public void ValidInsideSpaceRequestProducesInformationalResult()
        {
            AreaBuildSettings settings = CreateSettings(~0, PlacementTarget.InsideSpace);
            _scene = new GenerationTestScene(settings);
            _scene.CreateAsset("Inside Asset", PlacementType.InsideSpace);

            SceneSetupReport report = SceneSetupValidator.Validate(
                _scene.CreateRequest(
                    targets: PlacementTarget.InsideSpace,
                    areaSettings: settings));

            Assert.That(report.HasErrors, Is.False);
            Assert.That(report.Issues, Has.Count.EqualTo(1));
            Assert.That(report.Issues[0].Severity, Is.EqualTo(SceneSetupIssueSeverity.Info));
            Assert.That(report.Issues[0].Message, Does.Contain("valid"));
        }

        [Test]
        public void EmptySelectedSurfaceLayerIsReportedAsError()
        {
            AreaBuildSettings settings = CreateSettings(0, PlacementTarget.Floor);
            _scene = new GenerationTestScene(settings);
            _scene.CreateAsset("Floor Asset");

            SceneSetupReport report = SceneSetupValidator.Validate(
                _scene.CreateRequest(areaSettings: settings));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues.Any(issue => issue.Message.Contains("surface layer mask is empty")), Is.True);
        }

        [Test]
        public void MissingColliderOnSelectedLayerProducesWarning()
        {
            AreaBuildSettings settings = CreateSettings(1 << 30, PlacementTarget.Floor);
            _scene = new GenerationTestScene(settings);
            _scene.CreateAsset("Floor Asset");

            SceneSetupReport report = SceneSetupValidator.Validate(
                _scene.CreateRequest(areaSettings: settings));

            Assert.That(report.HasErrors, Is.False);
            Assert.That(report.Issues.Any(issue => issue.Message.Contains("no active scene colliders")), Is.True);
        }

        [Test]
        public void SfsBoundaryModeExplainsThatPhysicsSurfacesAreIgnored()
        {
            AreaBuildSettings settings = CreateSettings(
                ~0,
                PlacementTarget.Floor,
                SurfaceDiscoveryMode.SfsBoundaries);
            _scene = new GenerationTestScene(settings);
            _scene.CreateAsset("Floor Asset");

            SceneSetupReport report = SceneSetupValidator.Validate(
                _scene.CreateRequest(areaSettings: settings));

            Assert.That(report.HasErrors, Is.False);
            Assert.That(report.Issues.Any(issue => issue.Message.Contains("ignore scene colliders")), Is.True);
        }

        [Test]
        public void AreaBuildFailureIsReportedWithProviderMessage()
        {
            AreaBuildSettings settings = CreateSettings(~0, PlacementTarget.InsideSpace);
            _scene = new GenerationTestScene(settings);
            _scene.CreateAsset("Inside Asset", PlacementType.InsideSpace);
            _scene.AreaSource.Error = "broken spatial source";

            SceneSetupReport report = SceneSetupValidator.Validate(
                _scene.CreateRequest(
                    targets: PlacementTarget.InsideSpace,
                    areaSettings: settings));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues.Any(issue => issue.Message.Contains("broken spatial source")), Is.True);
        }

        [Test]
        public void AssetsForDifferentTargetAreReportedAsUnusable()
        {
            AreaBuildSettings settings = CreateSettings(
                ~0,
                PlacementTarget.Wall,
                SurfaceDiscoveryMode.SfsBoundaries);
            _scene = new GenerationTestScene(settings);
            _scene.CreateAsset("Floor Asset", PlacementType.Floor);

            SceneSetupReport report = SceneSetupValidator.Validate(
                _scene.CreateRequest(
                    targets: PlacementTarget.Wall,
                    areaSettings: settings));

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues.Any(issue => issue.Message.Contains("none can be used")), Is.True);
        }

        [Test]
        public void SceneSetupDiscoveryIncludesConfiguredColliderAndExclusionRegion()
        {
            GameObject surface = CreateSceneObject("Configured Surface");
            surface.layer = 30;
            Collider collider = surface.AddComponent<BoxCollider>();
            PlacementExclusionRegion region = CreateSceneObject("Exclusion")
                .AddComponent<PlacementExclusionRegion>();

            List<SceneSetupObjectEntry> entries = SceneSetupObjectDiscovery.Collect(1 << 30);

            Assert.That(entries.Any(entry => entry.SurfaceCollider == collider), Is.True);
            Assert.That(entries.Any(entry => entry.ExclusionRegion == region), Is.True);
        }

        [Test]
        public void SceneSetupDiscoveryIncludesDescriptorOutsideConfiguredLayers()
        {
            GameObject surface = CreateSceneObject("Semantic Surface");
            Collider collider = surface.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = surface.AddComponent<PlacementSurfaceDescriptor>();

            List<SceneSetupObjectEntry> entries = SceneSetupObjectDiscovery.Collect(0);

            Assert.That(entries.Any(entry =>
                entry.SurfaceCollider == collider && entry.SurfaceDescriptor == descriptor), Is.True);
        }

        [Test]
        public void SceneSetupDiscoveryExcludesGeneratedObjects()
        {
            GameObject generated = CreateSceneObject("Generated Surface");
            generated.layer = 30;
            Collider collider = generated.AddComponent<BoxCollider>();
            generated.AddComponent<GeneratedObjectMetadata>();

            List<SceneSetupObjectEntry> entries = SceneSetupObjectDiscovery.Collect(1 << 30);

            Assert.That(entries.Any(entry => entry.SurfaceCollider == collider), Is.False);
        }

        private GameObject CreateSceneObject(string name)
        {
            GameObject sceneObject = new(name);
            _sceneObjects.Add(sceneObject);
            return sceneObject;
        }

        private static AreaBuildSettings CreateSettings(
            int layers,
            PlacementTarget targets,
            SurfaceDiscoveryMode discovery = SurfaceDiscoveryMode.AllMatchingSurfacesInVolume) =>
            new(
                AreaDecompositionMode.Precise,
                layers,
                placementTargets: targets,
                surfaceDiscoveryMode: discovery);
    }
}
