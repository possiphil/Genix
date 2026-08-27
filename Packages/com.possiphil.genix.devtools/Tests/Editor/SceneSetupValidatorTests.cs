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
        public void SceneSetupDiscoveryIncludesAssetRelationAnchorWithoutCollider()
        {
            AssetRelationAnchor anchor = CreateSceneObject("Fixed Desk Anchor")
                .AddComponent<AssetRelationAnchor>();

            List<SceneSetupObjectEntry> entries = SceneSetupObjectDiscovery.Collect(0);

            Assert.That(entries.Any(entry =>
                entry.Type == SceneSetupObjectType.RelationAnchor &&
                entry.RelationAnchor == anchor &&
                entry.DetailTarget == anchor), Is.True);
        }

        [Test]
        public void SceneSetupDiscoveryMergesSurfaceAndAnchorOnSameObject()
        {
            GameObject surface = CreateSceneObject("Desktop Anchor");
            surface.layer = 30;
            surface.AddComponent<BoxCollider>();
            PlacementSurfaceDescriptor descriptor = surface.AddComponent<PlacementSurfaceDescriptor>();
            AssetRelationAnchor anchor = surface.AddComponent<AssetRelationAnchor>();

            List<SceneSetupObjectEntry> entries = SceneSetupObjectDiscovery.Collect(1 << 30);
            List<SceneSetupObjectEntry> matchingEntries = entries
                .Where(entry => entry.GameObject == surface)
                .ToList();

            Assert.That(matchingEntries, Has.Count.EqualTo(1));
            Assert.That(matchingEntries[0].Type, Is.EqualTo(SceneSetupObjectType.Surface));
            Assert.That(matchingEntries[0].SurfaceDescriptor, Is.EqualTo(descriptor));
            Assert.That(matchingEntries[0].RelationAnchor, Is.EqualTo(anchor));
            Assert.That(matchingEntries[0].MatchesDetailTarget(descriptor), Is.True);
            Assert.That(matchingEntries[0].MatchesDetailTarget(anchor), Is.True);
        }

        [Test]
        public void PlacementSurfaceSettingsSnapshotCopiesCapacityRules()
        {
            _scene = new GenerationTestScene(CreateSettings(~0, PlacementTarget.Floor));
            AssetDefinition monitor = _scene.CreateAsset("Snapshot Monitor", PlacementType.Floor);
            PlacementSurfaceCapacityRule monitorLimit = new();
            monitorLimit.ConfigureAsset(monitor, 1);
            PlacementSurfaceDescriptor source = CreateSceneObject("Source Desktop")
                .AddComponent<PlacementSurfaceDescriptor>();
            PlacementSurfaceDescriptor target = CreateSceneObject("Target Desktop")
                .AddComponent<PlacementSurfaceDescriptor>();
            source.SetCapacity(true, 6);
            source.SetAssetCapacityRules(new[] { monitorLimit });

            PlacementSurfaceSettingsSnapshot.Capture(source).ApplyTo(target);

            Assert.That(target.LimitCapacity, Is.True);
            Assert.That(target.MaxCapacity, Is.EqualTo(6));
            Assert.That(target.AssetCapacityRules, Has.Count.EqualTo(1));
            Assert.That(target.AssetCapacityRules[0].Scope, Is.EqualTo(PlacementSurfaceCapacityRuleScope.Asset));
            Assert.That(target.AssetCapacityRules[0].Asset, Is.EqualTo(monitor));
            Assert.That(target.AssetCapacityRules[0].MaxCapacity, Is.EqualTo(1));
            Assert.That(target.AssetCapacityRules[0], Is.Not.SameAs(monitorLimit));
        }

        [Test]
        public void SceneSetupDiscoveryExcludesGeneratedObjects()
        {
            GameObject generated = CreateSceneObject("Generated Surface");
            generated.layer = 30;
            Collider collider = generated.AddComponent<BoxCollider>();
            generated.AddComponent<GeneratedObjectMetadata>();
            AssetRelationAnchor anchor = generated.AddComponent<AssetRelationAnchor>();

            List<SceneSetupObjectEntry> entries = SceneSetupObjectDiscovery.Collect(1 << 30);

            Assert.That(entries.Any(entry => entry.SurfaceCollider == collider), Is.False);
            Assert.That(entries.Any(entry => entry.RelationAnchor == anchor), Is.False);
        }

        [Test]
        public void SupportSurfaceAuthoringCreatesExplicitRegionWithInheritedDescriptor()
        {
            GameObject shelf = CreateSceneObject("Shelf");
            shelf.layer = 30;
            BoxCollider shelfCollider = shelf.AddComponent<BoxCollider>();
            shelfCollider.center = new Vector3(0f, 0.8f, 0f);
            shelfCollider.size = new Vector3(0.4f, 1.6f, 0.8f);
            PlacementSurfaceDescriptor descriptor = shelf.AddComponent<PlacementSurfaceDescriptor>();

            GameObject region = SupportSurfaceRegionAuthoring.Create(
                shelf,
                1 << 30,
                selectCreatedObject: false);
            _sceneObjects.Add(region);

            BoxCollider regionCollider = region.GetComponent<BoxCollider>();
            Assert.That(region.transform.parent, Is.EqualTo(shelf.transform));
            Assert.That(region.layer, Is.EqualTo(30));
            Assert.That(regionCollider, Is.Not.Null);
            Assert.That(regionCollider.isTrigger, Is.False);
            Assert.That(regionCollider.size.x, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(regionCollider.size.z, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(region.transform.localPosition.y + regionCollider.size.y * 0.5f,
                Is.EqualTo(1.6f).Within(0.0001f));
            Assert.That(region.GetComponentInParent<PlacementSurfaceDescriptor>(), Is.EqualTo(descriptor));
            Assert.That(region.GetComponent<PlacementSurfaceDescriptor>(), Is.Null);
        }

        [Test]
        public void SupportSurfaceAuthoringAddsDescriptorAndUniqueSiblingNames()
        {
            GameObject owner = CreateSceneObject("Unconfigured Shelf");

            GameObject first = SupportSurfaceRegionAuthoring.Create(
                owner,
                1 << 30,
                selectCreatedObject: false);
            GameObject second = SupportSurfaceRegionAuthoring.Create(
                first,
                1 << 30,
                selectCreatedObject: false);
            _sceneObjects.Add(first);
            _sceneObjects.Add(second);

            Assert.That(owner.GetComponent<PlacementSurfaceDescriptor>(), Is.Not.Null);
            Assert.That(first.transform.parent, Is.EqualTo(owner.transform));
            Assert.That(second.transform.parent, Is.EqualTo(owner.transform));
            Assert.That(first.name, Is.Not.EqualTo(second.name));
            Assert.That(first.layer, Is.EqualTo(30));
            Assert.That(second.layer, Is.EqualTo(30));
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
