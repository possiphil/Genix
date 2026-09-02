using System.Collections.Generic;
using Genix.Assets;
using Genix.Editor.Generation;
using Genix.Editor.Assets;
using Genix.Editor.Infrastructure;
using Genix.Editor.State;
using Genix.Editor.Utilities;
using Genix.Editor.Validation;
using Genix.Geometry;
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

        [TestCase(5, 50f, 3)]
        [TestCase(5, 30f, 2)]
        [TestCase(5, 29.9f, 1)]
        [TestCase(5, -10f, 0)]
        [TestCase(5, 110f, 5)]
        [TestCase(0, 50f, 0)]
        public void PercentageCountsUseDesignerFriendlyHalfUpRounding(
            int totalCount,
            float percentage,
            int expectedCount)
        {
            Assert.That(
                EditorGui.RoundPercentageToCount(totalCount, percentage),
                Is.EqualTo(expectedCount));
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

        [Test]
        public void AssetDefinitionFactoryMeasuresBoundsBeforePrefabRootRotation()
        {
            GameObject value = CreateGameObject("Rotated Collider Prefab");
            BoxCollider collider = value.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 3f, 4f);
            collider.center = new Vector3(1f, 0f, -1f);
            value.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            Assert.That(AssetDefinitionFactory.TryGetPrefabBounds(value, out Vector3 size, out Vector3 offset), Is.True);
            Assert.That(size.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(size.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(size.z, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(offset.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(offset.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(offset.z, Is.EqualTo(-1f).Within(0.0001f));
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
        public void AssetFileServiceDoesNotSuffixCaseOnlyRename()
        {
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            string originalPath = AssetDatabase.GenerateUniqueAssetPath("Assets/genixcaserenametest.asset");
            AssetDatabase.CreateAsset(asset, originalPath);

            try
            {
                bool renamed = AssetFileService.Rename(
                    asset,
                    "GenixCaseRenameTest",
                    "Fallback",
                    out string error);

                Assert.That(renamed, Is.True, error);
                Assert.That(asset.name, Is.EqualTo("GenixCaseRenameTest"));
                Assert.That(asset.name, Does.Not.EndWith(" 1"));
            }
            finally
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.DeleteAsset(path);
            }
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

        [Test]
        public void MeasuredBoundsMatchScaledPrefabWithRotationOffsetAtRuntime()
        {
            GameObject source = CreateGameObject("Rotated Scaled Prefab");
            BoxCollider collider = source.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.8f, 2.4f, 4.6f);
            collider.center = new Vector3(0f, 0.7f, 0f);
            source.transform.localScale = Vector3.one * 0.3f;
            source.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Assert.That(
                AssetDefinitionFactory.TryGetPrefabBounds(source, out Vector3 size, out Vector3 center),
                Is.True);

            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            _objects.Add(asset);
            asset.Initialize(source, size, center);
            asset.SetPrefabRotationOffset(new Vector3(0f, 90f, 0f));

            for (int yaw = 0; yaw < 360; yaw += 15)
            {
                Quaternion placementRotation = Quaternion.Euler(0f, yaw, 0f);
                GameObject instance = Object.Instantiate(source);
                _objects.Add(instance);
                instance.transform.SetPositionAndRotation(
                    Vector3.zero,
                    asset.ApplyPrefabRotationOffset(placementRotation));
                Physics.SyncTransforms();

                Assert.That(BoundsUtility.TryGetColliderBounds(instance.transform, out Bounds actual, true, false), Is.True);
                OrientedBounds declared = new(
                    placementRotation * asset.BoundsCenterOffset,
                    asset.BoundsSize,
                    placementRotation);

                AssertBoundsContains(declared.ToAxisAlignedBounds(), actual, yaw, "declared bounds");
            }
        }

        private static void AssertBoundsContains(Bounds container, Bounds actual, int yaw, string label)
        {
            const float tolerance = 0.002f;
            Assert.That(actual.min.x, Is.GreaterThanOrEqualTo(container.min.x - tolerance), $"{label}, yaw {yaw}: min x");
            Assert.That(actual.min.y, Is.GreaterThanOrEqualTo(container.min.y - tolerance), $"{label}, yaw {yaw}: min y");
            Assert.That(actual.min.z, Is.GreaterThanOrEqualTo(container.min.z - tolerance), $"{label}, yaw {yaw}: min z");
            Assert.That(actual.max.x, Is.LessThanOrEqualTo(container.max.x + tolerance), $"{label}, yaw {yaw}: max x");
            Assert.That(actual.max.y, Is.LessThanOrEqualTo(container.max.y + tolerance), $"{label}, yaw {yaw}: max y");
            Assert.That(actual.max.z, Is.LessThanOrEqualTo(container.max.z + tolerance), $"{label}, yaw {yaw}: max z");
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
