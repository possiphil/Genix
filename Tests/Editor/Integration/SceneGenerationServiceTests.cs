using Genix.Assets;
using Genix.Core;
using Genix.Editor.Generation;
using Genix.Layouts;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests.Integration
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Integration)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class SceneGenerationServiceTests
    {
        private const string PrefabPath = "Assets/__GenixSceneGenerationServiceTest.prefab";
        private GameObject _parent;
        private AssetDefinition _asset;

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(PrefabPath);

            if (_asset)
                Object.DestroyImmediate(_asset);

            if (_parent)
                Object.DestroyImmediate(_parent);
        }

        [Test]
        public void ApplyCreatesEditablePrefabMetadataAndSupportsUndo()
        {
            AssetDatabase.DeleteAsset(PrefabPath);
            GameObject source = new("Source Prefab");
            source.AddComponent<BoxCollider>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            Object.DestroyImmediate(source);
            _parent = new GameObject("Generated Parent");
            _asset = ScriptableObject.CreateInstance<AssetDefinition>();
            _asset.Initialize(prefab, Vector3.one);
            PlacementCandidate candidate = new(
                new Vector3(2f, 3f, 4f),
                Quaternion.Euler(0f, 90f, 0f),
                placementType: PlacementType.Floor);
            GenerationPlan plan = new(1);
            plan.Add(_asset, candidate, "Generated Test Object");
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            bool applied = SceneGenerationService.Apply(plan, _parent.transform, out string error);

            Assert.That(applied, Is.True, error);
            Assert.That(_parent.transform.childCount, Is.EqualTo(1));
            GameObject instance = _parent.transform.GetChild(0).gameObject;
            Assert.That(instance.name, Is.EqualTo("Generated Test Object"));
            Assert.That(instance.transform.position, Is.EqualTo(candidate.Position));
            Assert.That(
                Quaternion.Angle(instance.transform.rotation, candidate.Rotation),
                Is.LessThan(0.001f));
            Assert.That(instance.GetComponent<GeneratedObjectMetadata>(), Is.Not.Null);
            Assert.That(
                instance.GetComponent<GeneratedObjectMetadata>().PlacementTarget,
                Is.EqualTo(PlacementTarget.Floor));

            Undo.CollapseUndoOperations(undoGroup);
            Undo.PerformUndo();

            Assert.That(_parent.transform.childCount, Is.Zero);
        }
    }
}
