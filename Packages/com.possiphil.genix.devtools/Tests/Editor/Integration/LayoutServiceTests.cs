using System.Collections.Generic;
using Genix.Core;
using Genix.Editor.Generation;
using Genix.Editor.Layouts;
using Genix.Layouts;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Genix.Tests.Integration
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Integration)]
    [Category(GenixTestCategories.LayoutsArea)]
    public sealed class LayoutServiceTests
    {
        private readonly List<string> _assetPaths = new();
        private GenerationTestScene _scene;
        private bool _rootExisted;

        [SetUp]
        public void SetUp()
        {
            _rootExisted = GameObject.Find("Genix");
            _scene = new GenerationTestScene(sourceName: "Layout Test " + System.Guid.NewGuid().ToString("N"));
            LayoutPreviewService.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            LayoutPreviewService.Clear();
            GeneratedHierarchy.Clear(_scene.AreaSource);
            _scene.Dispose();

            foreach (string path in _assetPaths)
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.SaveAssets();

            if (!_rootExisted)
            {
                GameObject root = GameObject.Find("Genix");

                if (root && root.transform.childCount == 0)
                    Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyRejectsMissingLayoutAndTargetArea()
        {
            Assert.That(LayoutApplyService.Apply(null, _scene.AreaSource, out string noLayout), Is.False);
            Assert.That(noLayout, Does.Contain("No layout"));

            SavedLayout layout = CreateLayout(CreateLayoutPrefab());

            Assert.That(LayoutApplyService.Apply(layout, null, out string noArea), Is.False);
            Assert.That(noArea, Does.Contain("Target Area"));
        }

        [Test]
        public void ApplyMovesSavedChildrenIntoGeneratedHierarchy()
        {
            SavedLayout layout = CreateLayout(CreateLayoutPrefab());

            bool applied = LayoutApplyService.Apply(layout, _scene.AreaSource, out string error);

            Assert.That(applied, Is.True, error);
            Assert.That(GeneratedHierarchy.TryGet(_scene.AreaSource, out Transform group), Is.True);
            Assert.That(group.childCount, Is.EqualTo(2));
            Assert.That(group.Find("First Saved Object"), Is.Not.Null);
            Assert.That(group.Find("Second Saved Object"), Is.Not.Null);
        }

        [Test]
        public void PreviewCreatesNonPersistentScenePreviewAndDisablesPhysics()
        {
            GameObject prefab = CreateLayoutPrefab();
            prefab.transform.GetChild(0).gameObject.AddComponent<BoxCollider>();
            SavedLayout layout = CreateLayout(prefab);
            Object[] previousSelection = { _scene.AreaRoot };
            Selection.objects = previousSelection;

            bool shown = LayoutPreviewService.Show(layout, out string error);

            Assert.That(shown, Is.True, error);
            GameObject root = GameObject.Find("Genix Layout Preview");
            Assert.That(root, Is.Not.Null);
            Assert.That((root.hideFlags & HideFlags.DontSave) != 0, Is.True);
            Assert.That(root.GetComponentInChildren<Collider>(true).enabled, Is.False);
            Assert.That(Selection.objects, Is.EqualTo(previousSelection));
            Assert.That(LayoutPreviewService.IsShowing(layout), Is.True);

            LayoutPreviewService.Clear();
            Assert.That(GameObject.Find("Genix Layout Preview"), Is.Null);
            Assert.That(LayoutPreviewService.IsShowing(layout), Is.False);
        }

        [Test]
        public void RepositoryMatchesAreaByStableIdAndCurrentScene()
        {
            SavedLayout layout = CreateLayout(CreateLayoutPrefab());

            Assert.That(LayoutRepository.MatchesCurrentScene(layout), Is.True);
            Assert.That(LayoutRepository.MatchesArea(layout, _scene.AreaSource), Is.True);
            Assert.That(LayoutRepository.MatchesArea(layout, null), Is.False);
        }

        [Test]
        public void BrowserIndexMatchesSceneWithoutLoadingUnrelatedLayouts()
        {
            SavedLayout layout = CreateLayout(CreateLayoutPrefab());
            LayoutBrowserIndexEntry entry = LayoutBrowserIndexEntry.FromLayout(layout, "Assets/Test Layout.asset");

            Assert.That(entry.MatchesScene(layout.ScenePath), Is.True);
            Assert.That(entry.MatchesScene("Assets/Scenes/Another Scene.unity"), Is.False);
        }

        [Test]
        public void BrowserIndexPrefersStableAreaIdAndFallsBackToAreaName()
        {
            SavedLayout layout = CreateLayout(CreateLayoutPrefab());
            LayoutBrowserIndexEntry entry = LayoutBrowserIndexEntry.FromLayout(layout, "Assets/Test Layout.asset");

            Assert.That(
                entry.MatchesArea(layout.ScenePath, layout.TargetAreaId, "Different Display Name"),
                Is.True);
            Assert.That(
                entry.MatchesArea(layout.ScenePath, "different-id", layout.TargetAreaName),
                Is.False);
            Assert.That(
                entry.MatchesArea(layout.ScenePath, string.Empty, layout.TargetAreaName.ToUpperInvariant()),
                Is.True);
        }

        [Test]
        public void BrowserIndexCopiesListMetadataWithoutKeepingLayoutReference()
        {
            SavedLayout layout = CreateLayout(CreateLayoutPrefab());
            layout.SetDesignerMetadata("Readable Layout", "Searchable notes", true, true);

            LayoutBrowserIndexEntry entry = LayoutBrowserIndexEntry.FromLayout(
                layout,
                "Assets/Test Layout.asset");

            Assert.That(entry.DisplayName, Is.EqualTo("Readable Layout"));
            Assert.That(entry.Notes, Is.EqualTo("Searchable notes"));
            Assert.That(entry.Favorite, Is.True);
            Assert.That(entry.Locked, Is.True);
            Assert.That(entry.SceneName, Is.EqualTo(layout.SceneName));
            Assert.That(entry.TargetAreaName, Is.EqualTo(layout.TargetAreaName));
            Assert.That(entry.StyleName, Is.EqualTo(layout.StyleName));
            Assert.That(entry.ObjectCount, Is.EqualTo(layout.ObjectCount));
            Assert.That(entry.CreatedAt, Is.EqualTo(layout.CreatedAt));
        }

        [Test]
        public void CaptureRejectsAreaWithoutGeneratedObjects()
        {
            bool saved = LayoutCaptureService.Save(
                _scene.AreaSource,
                PlacementTarget.Floor,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                _scene.Pool,
                "Natural",
                out _,
                out string error);

            Assert.That(saved, Is.False);
            Assert.That(error, Does.Contain("No generated objects"));
        }

        [Test]
        public void CapturePersistsMetadataAndRepositoryDeleteRemovesOwnedAssets()
        {
            Transform generatedParent = GeneratedHierarchy.GetOrCreate(_scene.AreaSource);
            GameObject first = _scene.CreateGameObject("Captured Floor Object");
            first.transform.SetParent(generatedParent, false);
            first.AddComponent<BoxCollider>();
            first.AddComponent<GeneratedObjectMetadata>().Initialize(Genix.Assets.PlacementType.Floor);
            GameObject second = _scene.CreateGameObject("Captured Wall Object");
            second.transform.SetParent(generatedParent, false);
            second.transform.localPosition = Vector3.right * 2f;
            second.AddComponent<BoxCollider>();
            second.AddComponent<GeneratedObjectMetadata>().Initialize(Genix.Assets.PlacementType.Wall);

            bool ignoreFailingMessages = LogAssert.ignoreFailingMessages;
            bool saved;
            SavedLayout layout;
            string error;

            try
            {
                // AssetDatabase.Refresh may surface warnings from unrelated immutable packages.
                LogAssert.ignoreFailingMessages = true;
                saved = LayoutCaptureService.Save(
                    _scene.AreaSource,
                    PlacementTarget.All,
                    TargetDistributionMode.Balanced,
                    TargetDistributionWeights.Default,
                    _scene.Pool,
                    "Natural",
                    out layout,
                    out error);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoreFailingMessages;
            }

            Assert.That(saved, Is.True, error);
            Assert.That(layout.ObjectCount, Is.EqualTo(2));
            Assert.That(layout.PlacementTargets, Is.EqualTo(PlacementTarget.Floor | PlacementTarget.Wall));
            Assert.That(layout.TargetAreaId, Is.EqualTo(_scene.Area.SourceInfo.SourceId));
            string layoutPath = AssetDatabase.GetAssetPath(layout);
            string prefabPath = AssetDatabase.GetAssetPath(layout.Prefab);
            string layoutFolder = layoutPath.Substring(0, layoutPath.LastIndexOf('/'));

            bool deleted;

            try
            {
                // Repository cleanup refreshes the same AssetDatabase and can emit the package warning again.
                LogAssert.ignoreFailingMessages = true;
                deleted = LayoutRepository.Delete(layout, out error);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = ignoreFailingMessages;
            }

            Assert.That(deleted, Is.True, error);
            Assert.That(AssetDatabase.LoadAssetAtPath<SavedLayout>(layoutPath), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(layoutFolder), Is.False);
        }

        [Test]
        public void LockedLayoutCannotBeDeleted()
        {
            SavedLayout layout = CreateLayout(CreateLayoutPrefab());
            layout.SetDesignerMetadata("Locked Layout", string.Empty, false, true);

            bool deleted = LayoutRepository.Delete(layout, out string error);

            Assert.That(deleted, Is.False);
            Assert.That(error, Does.Contain("locked"));
        }

        private GameObject CreateLayoutPrefab()
        {
            GameObject root = _scene.CreateGameObject("Saved Layout Root");
            GameObject first = _scene.CreateGameObject("First Saved Object");
            first.transform.SetParent(root.transform, false);
            GameObject second = _scene.CreateGameObject("Second Saved Object");
            second.transform.SetParent(root.transform, false);
            second.transform.localPosition = Vector3.right;
            string path = $"Assets/__GenixLayoutServiceTest_{System.Guid.NewGuid():N}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            _assetPaths.Add(path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private SavedLayout CreateLayout(GameObject prefab)
        {
            Scene scene = SceneManager.GetActiveScene();
            SavedLayout layout = _scene.Track(ScriptableObject.CreateInstance<SavedLayout>());
            layout.Initialize(
                "Test Layout",
                prefab,
                scene.name,
                scene.path,
                _scene.Area.SourceInfo.SourceName,
                _scene.Area.SourceInfo.SourceId,
                _scene.Area.SourceInfo.SourceType,
                PlacementTarget.Floor,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                _scene.Pool,
                "Natural",
                2,
                _scene.Area.WorldBounds,
                "2026-08-02 20:00",
                null);
            return layout;
        }
    }
}
