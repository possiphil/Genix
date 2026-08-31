using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SpatialArea)]
    public sealed class SceneObjectIndexTests
    {
        private readonly List<UnityEngine.Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            SceneObjectIndex.ClearCache();

            foreach (UnityEngine.Object value in _objects)
            {
                if (value)
                    UnityEngine.Object.DestroyImmediate(value);
            }

            _objects.Clear();
        }

        [Test]
        public void GeneratedIndexCollectsRenderableChildrenAndQueriesBounds()
        {
            GameObject parent = CreateGameObject("Generated");
            GameObject child = CreatePrimitive("Placed", new Vector3(3f, 0f, 0f));
            child.transform.SetParent(parent.transform);

            SceneObjectIndex index = SceneObjectIndex.CollectGenerated(parent.transform);
            List<SceneObjectIndex.Entry> hits = index.Query(new Bounds(new Vector3(3f, 0f, 0f), Vector3.one * 2f)).ToList();

            Assert.That(index.Count, Is.EqualTo(1));
            Assert.That(index.HasBounds, Is.True);
            Assert.That(hits, Has.Count.EqualTo(1));
            Assert.That(hits[0].ObjectName, Is.EqualTo("Placed"));
        }

        [Test]
        public void GeneratedIndexIgnoresChildrenWithoutRendererBounds()
        {
            GameObject parent = CreateGameObject("Generated");
            CreateGameObject("Empty").transform.SetParent(parent.transform);

            SceneObjectIndex index = SceneObjectIndex.CollectGenerated(parent.transform);

            Assert.That(index.Count, Is.Zero);
            Assert.That(index.HasBounds, Is.False);
        }

        [Test]
        public void GeneratedIndexUsesPlacementBoundsInsteadOfRendererBounds()
        {
            GameObject parent = CreateGameObject("Generated");
            GameObject child = CreatePrimitive("Placed", new Vector3(3f, 0f, 0f));
            child.transform.localScale = Vector3.one * 10f;
            child.transform.SetParent(parent.transform);
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            _objects.Add(asset);
            asset.Initialize(child, new Vector3(0.5f, 1f, 1.5f));
            child.AddComponent<GeneratedObjectMetadata>().Initialize(
                PlacementType.Floor,
                sourceAsset: asset);

            SceneObjectIndex.Entry entry = SceneObjectIndex
                .CollectGenerated(parent.transform)
                .Entries
                .Single();

            Assert.That(Vector3.Distance(entry.Bounds.center, new Vector3(3f, 0f, 0f)), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(entry.Bounds.size, new Vector3(0.5f, 1f, 1.5f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void GeneratedCacheReusesIndexUntilExplicitlyCleared()
        {
            GameObject parent = CreateGameObject("Generated");
            CreatePrimitive("Placed", Vector3.zero).transform.SetParent(parent.transform);

            SceneObjectIndex first = SceneObjectIndex.CollectGeneratedCached(parent.transform);
            SceneObjectIndex second = SceneObjectIndex.CollectGeneratedCached(parent.transform);
            SceneObjectIndex.ClearCache();
            SceneObjectIndex third = SceneObjectIndex.CollectGeneratedCached(parent.transform);

            Assert.That(second, Is.SameAs(first));
            Assert.That(third, Is.Not.SameAs(first));
        }

        [Test]
        public void FixedIndexExcludesSourceGeneratedAndTriggerColliders()
        {
            GameObject areaRoot = CreateGameObject("Area");
            GameObject generatedRoot = CreateGameObject("Generated");
            Collider source = CreatePrimitive("Source", Vector3.zero).GetComponent<Collider>();
            source.transform.SetParent(areaRoot.transform);
            CreatePrimitive("Generated Child", Vector3.zero).transform.SetParent(generatedRoot.transform);
            Collider trigger = CreatePrimitive("Trigger", Vector3.zero).GetComponent<Collider>();
            trigger.isTrigger = true;
            GameObject fixedObject = CreatePrimitive("Fixed", new Vector3(2f, 0f, 0f));
            StubAreaSource areaSource = new(areaRoot.transform, source);

            SceneObjectIndex index = SceneObjectIndex.CollectFixed(
                areaSource,
                generatedRoot.transform,
                new Bounds(Vector3.zero, Vector3.one * 20f),
                0f);

            Assert.That(index.Count, Is.EqualTo(1));
            Assert.That(index.Query(new Bounds(new Vector3(2f, 0f, 0f), Vector3.one * 2f)).Single().ObjectName, Is.EqualTo(fixedObject.name));
        }

        [Test]
        public void FixedIndexRestrictsCollectionToExpandedTargetBounds()
        {
            GameObject areaRoot = CreateGameObject("Area");
            GameObject generatedRoot = CreateGameObject("Generated");
            CreatePrimitive("Near", new Vector3(4f, 0f, 0f));
            CreatePrimitive("Far", new Vector3(20f, 0f, 0f));
            Physics.SyncTransforms();

            SceneObjectIndex index = SceneObjectIndex.CollectFixed(
                new StubAreaSource(areaRoot.transform),
                generatedRoot.transform,
                new Bounds(Vector3.zero, Vector3.one * 4f),
                3f);

            Assert.That(index.Count, Is.EqualTo(1));
            Assert.That(index.Query(new Bounds(new Vector3(4f, 0f, 0f), Vector3.one * 2f)).Single().ObjectName, Is.EqualTo("Near"));
        }

        private GameObject CreatePrimitive(string name, Vector3 position)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.position = position;
            _objects.Add(value);
            return value;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject value = new(name);
            _objects.Add(value);
            return value;
        }

        private sealed class StubAreaSource : IAreaSource
        {
            private readonly Collider _sourceCollider;

            public SpatialSourceInfo SourceInfo { get; } = new("Test", "Area", "scene-index-tests");
            public Transform ParentTransform { get; }
            public IReadOnlyList<SemanticTag> SemanticTags => Array.Empty<SemanticTag>();
            public IReadOnlyList<TagCategory> AnyTagCategories => Array.Empty<TagCategory>();

            public StubAreaSource(Transform parentTransform, Collider sourceCollider = null)
            {
                ParentTransform = parentTransform;
                _sourceCollider = sourceCollider;
            }

            public bool IsSourceCollider(Collider collider) => collider == _sourceCollider;

            public bool TryBuildArea(AreaBuildSettings settings, out PlacementArea area, out string error)
            {
                area = null;
                error = "Not used.";
                return false;
            }
        }
    }
}
