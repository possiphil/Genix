using System.Collections.Generic;
using Genix.Assets;
using Genix.Editor.Generation;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.Infrastructure;
using Genix.Editor.State;
using Genix.Editor.Validation;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class EditorServiceTests
    {
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _objects)
            {
                if (value)
                    Object.DestroyImmediate(value);
            }

            _objects.Clear();
        }

        [Test]
        public void GeneratedObjectNamerCleansAssetNamesAndSkipsExistingChildren()
        {
            GameObject parent = CreateGameObject("Parent");
            GameObject existing = CreateGameObject("GenixGeneratedRockAsset1");
            existing.transform.SetParent(parent.transform);
            AssetDefinition asset = CreateAsset("Genix_rock-asset");
            GeneratedObjectNamer namer = new(parent.transform);

            Assert.That(namer.Next(asset), Is.EqualTo("GenixGeneratedRockAsset2"));
            Assert.That(namer.Next(asset), Is.EqualTo("GenixGeneratedRockAsset3"));
        }

        [Test]
        public void GeneratedObjectNamerFallsBackForMissingAsset()
        {
            GeneratedObjectNamer namer = new(null);

            Assert.That(namer.Next(null), Is.EqualTo("GenixGeneratedObject1"));
        }

        [Test]
        public void TargetFormattingIncludesPlacedAndRequestedCounts()
        {
            Dictionary<PlacementType, int> targets = new()
            {
                [PlacementType.Floor] = 7,
                [PlacementType.Wall] = 3
            };
            Dictionary<PlacementType, int> placed = new()
            {
                [PlacementType.Floor] = 5
            };

            string result = TargetDistributionPolicy.FormatTargets(targets, placed);

            Assert.That(result, Does.Contain("Floor 5/7"));
            Assert.That(result, Does.Contain("Wall 0/3"));
        }

        [Test]
        public void HasAssetsRequiresMatchingTypeAndUsablePrefab()
        {
            AssetDefinition floor = CreateAsset("Floor");

            Assert.That(TargetDistributionPolicy.HasAssets(new[] { floor }, PlacementType.Floor), Is.True);
            Assert.That(TargetDistributionPolicy.HasAssets(new[] { floor }, PlacementType.Wall), Is.False);
            Assert.That(TargetDistributionPolicy.HasAssets(new AssetDefinition[] { null }, PlacementType.Floor), Is.False);
        }

        [Test]
        public void SceneSetupReportIgnoresEmptyMessagesAndTracksErrors()
        {
            SceneSetupReport report = new();
            report.AddInfo(string.Empty);
            report.AddWarning("Warning");
            report.AddError("Error");

            Assert.That(report.Issues, Has.Count.EqualTo(2));
            Assert.That(report.HasIssues, Is.True);
            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues[0].Severity, Is.EqualTo(SceneSetupIssueSeverity.Warning));
        }

        [Test]
        public void TimedMessageCanBeShownAndCleared()
        {
            TimedMessage message = new();

            message.Show("Saved", MessageType.Info, 10d);
            Assert.That(message.IsVisible, Is.True);
            Assert.That(message.Update(), Is.True);

            message.Clear();
            Assert.That(message.IsVisible, Is.False);
            Assert.That(message.Type, Is.EqualTo(MessageType.None));
            Assert.That(message.Update(), Is.False);
        }

        [Test]
        public void TimedMessageWithExpiredDurationClearsOnUpdate()
        {
            TimedMessage message = new();
            message.Show("Expired", durationSeconds: -1d);

            Assert.That(message.Update(), Is.False);
            Assert.That(message.IsVisible, Is.False);
        }

        [Test]
        public void AssetDefinitionFactoryReadsColliderBoundsFromSceneObject()
        {
            GameObject value = CreateGameObject("Collider Prefab");
            BoxCollider collider = value.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 3f, 4f);
            collider.center = new Vector3(1f, 0f, -1f);

            Assert.That(AssetDefinitionFactory.TryGetPrefabBounds(value, out Vector3 size, out Vector3 offset), Is.True);
            Assert.That(size, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(offset, Is.EqualTo(new Vector3(1f, 0f, -1f)));
            Assert.That(AssetDefinitionFactory.IsPrefabAsset(value), Is.False);
        }

        [TestCase(null, "Fallback", "Fallback")]
        [TestCase("   ", "Fallback", "Fallback")]
        [TestCase("  Clean Name  ", "Fallback", "Clean Name")]
        public void AssetFileServiceCleansDisplayNames(string value, string fallback, string expected)
        {
            Assert.That(AssetFileService.CleanName(value, fallback), Is.EqualTo(expected));
        }

        [Test]
        public void AssetFileServiceSanitizesInvalidFileNameCharacters()
        {
            string sanitized = AssetFileService.SanitizeName("  Folder/Asset\0  ", "Fallback");

            Assert.That(sanitized, Is.EqualTo("FolderAsset"));
            Assert.That(AssetFileService.SanitizeName("/", "Fallback"), Is.EqualTo("Fallback"));
        }

        [Test]
        public void AssetFileServiceRejectsMoveAndRenameForNonAssetObjects()
        {
            GameObject sceneObject = CreateGameObject("Scene Object");

            Assert.That(AssetFileService.Move(sceneObject, "Assets", "Moved", out string moveError), Is.False);
            Assert.That(moveError, Is.Null);
            Assert.That(AssetFileService.Rename(sceneObject, "Renamed", "Fallback", out string renameError), Is.False);
            Assert.That(renameError, Is.Null);
            Assert.DoesNotThrow(() => AssetFileService.Delete(sceneObject));
            Assert.DoesNotThrow(() => AssetFileService.SetDirty(null));
        }

        [Test]
        public void AssetDefinitionFactoryRejectsMissingOrUnboundedObjects()
        {
            GameObject unbounded = CreateGameObject("Unbounded");

            Assert.That(AssetDefinitionFactory.TryGetPrefabBounds(null, out _, out _), Is.False);
            Assert.That(AssetDefinitionFactory.TryGetPrefabBounds(unbounded, out _, out _), Is.False);
            Assert.That(AssetDefinitionFactory.IsPrefabAsset(null), Is.False);
        }

        [Test]
        public void AssetDefinitionFactoryHandlesMissingPrefabSequence()
        {
            Assert.That(AssetDefinitionFactory.CreateAssetsFromPrefabs(null), Is.Empty);
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject value = new(name);
            _objects.Add(value);
            return value;
        }

        private AssetDefinition CreateAsset(string name)
        {
            GameObject prefab = CreateGameObject(name + " Prefab");
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.name = name;
            asset.Initialize(prefab, Vector3.one);
            _objects.Add(asset);
            return asset;
        }
    }
}
