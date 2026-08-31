using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Generation;
using Genix.Editor.Infrastructure;
using Genix.Editor.Profiling;
using Genix.Layouts;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Genix.Tests.Integration
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Integration)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class GenerationWorkflowTests
    {
        private const string PrefabPath = "Assets/__GenixGenerationWorkflowTest.prefab";
        private GenerationTestScene _scene;
        private bool _catalogExisted;
        private bool _profilingWasEnabled;
        private bool _rootExisted;

        [SetUp]
        public void SetUp()
        {
            GenerationWorkflow.ClearPreviewPlan();
            _catalogExisted = AssetDatabase.LoadAssetAtPath<AssetCatalog>(ProjectContentPaths.AssetCatalog);
            _profilingWasEnabled = GenerationProfilerService.ProfilingEnabled;
            _rootExisted = GameObject.Find("Genix");
            GenerationProfilerService.SetProfilingEnabled(false);
            _scene = new GenerationTestScene(sourceName: "Workflow Test " + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            GenerationWorkflow.ClearPreviewPlan();

            if (_scene != null)
            {
                GeneratedHierarchy.Clear(_scene.AreaSource);
                _scene.Dispose();
            }

            AssetDatabase.DeleteAsset(PrefabPath);

            if (!_catalogExisted)
                AssetDatabase.DeleteAsset(ProjectContentPaths.AssetCatalog);

            if (!_rootExisted)
            {
                GameObject root = GameObject.Find("Genix");

                if (root && root.transform.childCount == 0)
                    Object.DestroyImmediate(root);
            }

            GenerationProfilerService.SetProfilingEnabled(_profilingWasEnabled);
        }

        [Test]
        public void PreviewCanBeAppliedAndThenCleared()
        {
            AssetDatabase.DeleteAsset(PrefabPath);
            GameObject source = new("Workflow Prefab Source");
            source.AddComponent<BoxCollider>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            Object.DestroyImmediate(source);
            AssetDefinition asset = _scene.CreateAsset("Workflow Asset", prefab: prefab);
            GenerationRequest request = _scene.CreateRequest(count: 3);

            GenerationWorkflow.Preview(request);

            Assert.That(GenerationWorkflow.HasPreviewPlan, Is.True);
            Assert.That(GeneratedHierarchy.TryGet(_scene.AreaSource, out _), Is.False);
            Assert.That(GenerationWorkflow.ApplyPreview(), Is.True);
            Assert.That(GenerationWorkflow.HasPreviewPlan, Is.False);
            Assert.That(GeneratedHierarchy.TryGet(_scene.AreaSource, out Transform group), Is.True);
            Assert.That(group.childCount, Is.EqualTo(3));
            Assert.That(group.GetChild(0).GetComponent<GeneratedObjectMetadata>(), Is.Not.Null);

            GenerationWorkflow.Clear(_scene.AreaSource);

            Assert.That(GeneratedHierarchy.TryGet(_scene.AreaSource, out _), Is.False);
            Assert.That(asset, Is.Not.Null);
        }

        [Test]
        public void ApplyPreviewReturnsFalseWithoutRetainedPlan()
        {
            LogAssert.Expect(LogType.Warning, "No Genix preview is available to apply. Run Preview first.");

            Assert.That(GenerationWorkflow.ApplyPreview(), Is.False);
        }

        [Test]
        public void GenerateCreatesObjectsAndClearRemovesThem()
        {
            GenerationRequest request = CreatePrefabBackedRequest(2);
            Assert.That(GeneratedHierarchy.HasObjects(_scene.AreaSource), Is.False);

            GenerationWorkflow.Generate(request);

            Assert.That(GeneratedHierarchy.TryGet(_scene.AreaSource, out Transform group), Is.True);
            Assert.That(group.childCount, Is.EqualTo(2));
            Assert.That(GeneratedHierarchy.HasObjects(_scene.AreaSource), Is.True);

            GenerationWorkflow.Clear(_scene.AreaSource);

            Assert.That(GeneratedHierarchy.TryGet(_scene.AreaSource, out _), Is.False);
            Assert.That(GeneratedHierarchy.HasObjects(_scene.AreaSource), Is.False);
        }

        [Test]
        public void GenerateAppendsToExistingResultInSameArea()
        {
            CreatePrefabBackedRequest(2);

            GenerationWorkflow.Generate(_scene.CreateRequest(count: 2, seed: 123));
            GenerationWorkflow.Generate(_scene.CreateRequest(count: 3, seed: 456));

            Assert.That(GeneratedHierarchy.TryGet(_scene.AreaSource, out Transform group), Is.True);
            Assert.That(group.childCount, Is.EqualTo(5));
        }

        [Test]
        public void RegenerateReplacesExistingResultInsteadOfAppending()
        {
            GenerationWorkflow.Generate(CreatePrefabBackedRequest(2));

            GenerationWorkflow.Regenerate(_scene.CreateRequest(count: 3));

            Assert.That(GeneratedHierarchy.TryGet(_scene.AreaSource, out Transform group), Is.True);
            Assert.That(group.childCount, Is.EqualTo(3));
        }

        [Test]
        public void InvalidRequestDoesNotCreateGeneratedHierarchy()
        {
            _scene.CreateAsset("Preflight Asset");
            GenerationRequest invalid = _scene.CreateRequest(count: 0);
            LogAssert.Expect(
                LogType.Warning,
                "Generation could not start because Object Count must be greater than zero.");

            GenerationWorkflow.Generate(invalid);

            Assert.That(GeneratedHierarchy.TryGet(_scene.AreaSource, out _), Is.False);
        }

        [Test]
        public void DestroyedPreviewAreaCannotBeAppliedAndClearsRetainedPlan()
        {
            GenerationWorkflow.Preview(CreatePrefabBackedRequest(1));
            Object.DestroyImmediate(_scene.AreaRoot);
            LogAssert.Expect(
                LogType.Warning,
                "The last Genix preview can no longer be applied because its target area is no longer available.");

            Assert.That(GenerationWorkflow.ApplyPreview(), Is.False);
            Assert.That(GenerationWorkflow.HasPreviewPlan, Is.False);
        }

        [Test]
        public void ClearWithoutAreaReportsActionableWarning()
        {
            LogAssert.Expect(LogType.Warning, "No location is selected. Choose a Target Area before clearing generated objects.");

            GenerationWorkflow.Clear(null);
        }

        private GenerationRequest CreatePrefabBackedRequest(int count)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
            {
                AssetDatabase.DeleteAsset(PrefabPath);
                GameObject source = new("Workflow Prefab Source");
                source.AddComponent<BoxCollider>();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
                Object.DestroyImmediate(source);
                _scene.CreateAsset("Workflow Asset", prefab: prefab);
            }

            return _scene.CreateRequest(count: count);
        }
    }
}
