using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Orientation;
using Genix.Semantics;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SemanticsArea)]
    public sealed class AssetDefinitionTests
    {
        private GameObject _prefab;
        private AssetDefinition _asset;
        private TagCategory _category;
        private SemanticTag _tag;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("Prefab");
            _asset = ScriptableObject.CreateInstance<AssetDefinition>();
            _asset.Initialize(_prefab, new Vector3(2f, 3f, 4f), new Vector3(1f, 0f, -1f));
            _category = ScriptableObject.CreateInstance<TagCategory>();
            _category.Initialize();
            _tag = ScriptableObject.CreateInstance<SemanticTag>();
            _tag.Initialize(_category);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tag);
            Object.DestroyImmediate(_category);
            Object.DestroyImmediate(_asset);
            Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void InitializeStoresPrefabBoundsAndOffset()
        {
            Assert.That(_asset.Prefab, Is.SameAs(_prefab));
            Assert.That(_asset.BoundsSize, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(_asset.BoundsCenterOffset, Is.EqualTo(new Vector3(1f, 0f, -1f)));
            Assert.That(_asset.Footprint, Is.EqualTo(new Vector2(2f, 4f)));
        }

        [Test]
        public void SetBoundsSizeClampsEveryAxis()
        {
            _asset.SetBoundsSize(new Vector3(-1f, 0f, 0.005f));

            Assert.That(_asset.BoundsSize, Is.EqualTo(Vector3.one * 0.01f));
        }

        [Test]
        public void PrefabRotationOffsetCorrectsPlacementBoundsAndRotation()
        {
            Vector3 offset = new(0f, 90f, 0f);
            Quaternion placementRotation = Quaternion.Euler(0f, 35f, 0f);
            _asset.SetPrefabRotationOffset(offset);

            Quaternion prefabRotation = _asset.ApplyPrefabRotationOffset(placementRotation);

            Assert.That(_asset.PrefabRotationOffset, Is.EqualTo(offset));
            Assert.That(Vector3.Distance(_asset.BoundsSize, new Vector3(4f, 3f, 2f)), Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(
                    _asset.BoundsCenterOffset,
                    Quaternion.Euler(offset) * new Vector3(1f, 0f, -1f)),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(
                    _asset.RemovePrefabRotationOffset(prefabRotation),
                    placementRotation),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void BoundsCenterOffsetIncludesPrefabRootScaleExactlyOnce()
        {
            _prefab.transform.localScale = new Vector3(0.5f, 0.25f, 2f);
            _asset.SetBoundsCenterOffset(new Vector3(2f, 4f, 3f));

            Assert.That(
                Vector3.Distance(_asset.BoundsCenterOffset, new Vector3(1f, 1f, 6f)),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void PlacementLimitAndWallRelationshipExposeSanitizedValues()
        {
            _asset.SetPlacementLimit(true, -3);
            _asset.SetWallProximity(WallProximityMode.NearWall, -2f);

            Assert.That(_asset.LimitPlacements, Is.True);
            Assert.That(_asset.MaxPlacements, Is.EqualTo(1));
            Assert.That(_asset.HasReachedPlacementLimit(0), Is.False);
            Assert.That(_asset.HasReachedPlacementLimit(1), Is.True);
            Assert.That(_asset.WallProximityMode, Is.EqualTo(WallProximityMode.NearWall));
            Assert.That(_asset.WallDistance, Is.Zero);
        }

        [Test]
        public void BoundsCenterAndSurfaceLimitsExposeSanitizedValues()
        {
            _asset.SetBoundsCenterOffset(new Vector3(3f, 2f, 1f));
            SerializedObject serializedAsset = new(_asset);
            serializedAsset.FindProperty("maxSurfaceHeightDifference").floatValue = -2f;
            serializedAsset.FindProperty("minSurfaceSupport").floatValue = 2f;
            serializedAsset.FindProperty("surfaceSinkOffset").floatValue = -3f;
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_asset.BoundsCenterOffset, Is.EqualTo(new Vector3(3f, 2f, 1f)));
            Assert.That(_asset.MaxSurfaceHeightDifference, Is.Zero);
            Assert.That(_asset.MinSurfaceSupport, Is.EqualTo(1f));
            Assert.That(_asset.SurfaceSinkOffset, Is.Zero);
        }

        [Test]
        public void ClearanceUsesPrefabOriginRotationAndClampedSize()
        {
            _asset.SetClearance(true, new Vector3(-1f, 2f, 3f), new Vector3(1f, 0f, 0f));
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);

            Genix.Placement.OrientedBounds bounds = _asset.CreateClearanceBounds(
                new Vector3(2f, 3f, 4f),
                rotation);

            Assert.That(_asset.ReserveClearance, Is.True);
            Assert.That(bounds.Size, Is.EqualTo(new Vector3(0.01f, 2f, 3f)));
            Assert.That(Vector3.Distance(bounds.Center, new Vector3(2f, 3f, 3f)), Is.LessThan(0.0001f));
            Assert.That(bounds.Rotation, Is.EqualTo(rotation));
        }

        [Test]
        public void ClearanceCenterOffsetIncludesPrefabRootScaleExactlyOnce()
        {
            _prefab.transform.localScale = new Vector3(0.5f, 0.25f, 2f);
            _asset.SetClearance(true, Vector3.one, new Vector3(2f, 4f, 3f));

            Genix.Placement.OrientedBounds bounds = _asset.CreateClearanceBounds(
                Vector3.zero,
                Quaternion.identity);

            Assert.That(Vector3.Distance(bounds.Center, new Vector3(1f, 1f, 6f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void AssetSpacingUsesGreatestMatchingRule()
        {
            AssetDefinition other = ScriptableObject.CreateInstance<AssetDefinition>();
            AssetSpacingRule exact = new();
            AssetSpacingRule tagged = new();
            _asset.AddTag(_tag);
            other.AddTag(_tag);
            exact.ConfigureAsset(other, 2f);
            tagged.ConfigureTag(_tag, 4f);
            _asset.SetSpacingRules(new[] { exact, tagged });

            try
            {
                Assert.That(_asset.GetMinimumSpacingTo(other), Is.EqualTo(4f));
                Assert.That(_asset.MaxSpacingDistance, Is.EqualTo(4f));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void AssetRelativePlacementNormalizesDistancesAndRequiresAssetCompatibleTags()
        {
            AssetDefinition desk = ScriptableObject.CreateInstance<AssetDefinition>();
            TagCategory surfaceCategory = ScriptableObject.CreateInstance<TagCategory>();
            SemanticTag surfaceTag = ScriptableObject.CreateInstance<SemanticTag>();
            surfaceCategory.Initialize(true, TagCategoryUsage.Surface);
            surfaceTag.Initialize(surfaceCategory);

            try
            {
                _asset.AssetRelativePlacement.ConfigureAsset(
                    desk,
                    AssetRelativeAnchorSource.GeneratedObjects,
                    AssetRelativeSide.Front,
                    4f,
                    1f,
                    AssetRelativeFacing.Toward,
                    sameSupportSurface: true);

                Assert.That(_asset.AssetRelativePlacement.IsConfigured, Is.True);
                Assert.That(_asset.AssetRelativePlacement.MinimumDistance, Is.EqualTo(4f));
                Assert.That(_asset.AssetRelativePlacement.MaximumDistance, Is.EqualTo(4f));
                Assert.That(_asset.AssetRelativePlacement.RequireSameSupportSurface, Is.True);
                Assert.That(_asset.AssetRelativePlacement.Matches(desk, null), Is.True);
                _asset.AssetRelativePlacement.SetSides(new[]
                {
                    AssetRelativeSide.Left,
                    AssetRelativeSide.Right
                });
                Assert.That(_asset.AssetRelativePlacement.AllowsSide(AssetRelativeSide.Left), Is.True);
                Assert.That(_asset.AssetRelativePlacement.AllowsSide(AssetRelativeSide.Right), Is.True);
                Assert.That(_asset.AssetRelativePlacement.AllowsSide(AssetRelativeSide.Front), Is.False);
                _asset.AssetRelativePlacement.SetPerAnchorLimit(true, 0);
                Assert.That(_asset.AssetRelativePlacement.LimitPerAnchor, Is.True);
                Assert.That(_asset.AssetRelativePlacement.MaxPerAnchor, Is.EqualTo(1));
                Assert.That(
                    _asset.AssetRelativePlacement.CardinalityMode,
                    Is.EqualTo(AssetRelativeCardinalityMode.AtMost));
                _asset.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 2);
                Assert.That(_asset.AssetRelativePlacement.HasMinimumPerAnchor, Is.True);
                Assert.That(_asset.AssetRelativePlacement.HasMaximumPerAnchor, Is.True);
                Assert.That(_asset.AssetRelativePlacement.MinimumPerAnchor, Is.EqualTo(2));
                Assert.That(_asset.AssetRelativePlacement.CardinalityCount, Is.EqualTo(2));
                _asset.AssetRelativePlacement.SetCardinalityRange(1, 2);
                Assert.That(
                    _asset.AssetRelativePlacement.CardinalityMode,
                    Is.EqualTo(AssetRelativeCardinalityMode.Between));
                Assert.That(_asset.AssetRelativePlacement.MinimumPerAnchor, Is.EqualTo(1));
                Assert.That(_asset.AssetRelativePlacement.MaximumPerAnchor, Is.EqualTo(2));
                _asset.AssetRelativePlacement.SetFacingVariation(240f);
                Assert.That(_asset.AssetRelativePlacement.FacingVariationDegrees, Is.EqualTo(180f));
                _asset.AssetRelativePlacement.SetAlignment(AssetRelativeAlignment.Center);
                Assert.That(
                    _asset.AssetRelativePlacement.Alignment,
                    Is.EqualTo(AssetRelativeAlignment.Center));

                _asset.AssetRelativePlacement.ConfigureTag(
                    surfaceTag,
                    AssetRelativeAnchorSource.Any,
                    AssetRelativeSide.Any,
                    0f,
                    2f,
                    AssetRelativeFacing.Any);

                Assert.That(_asset.AssetRelativePlacement.IsConfigured, Is.False);

                _asset.AssetRelativePlacement.ConfigureTag(
                    _tag,
                    AssetRelativeAnchorSource.SceneAnchors,
                    AssetRelativeSide.Right,
                    -1f,
                    3f,
                    AssetRelativeFacing.MatchForward);

                Assert.That(_asset.AssetRelativePlacement.IsConfigured, Is.True);
                Assert.That(_asset.AssetRelativePlacement.MinimumDistance, Is.Zero);
                Assert.That(_asset.AssetRelativePlacement.Matches(null, new[] { _tag }), Is.True);
                Assert.That(_asset.AssetRelativePlacement.UsesFacing, Is.True);

                _asset.AssetRelativePlacement.Disable();
                Assert.That(_asset.AssetRelativePlacement.IsConfigured, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(surfaceTag);
                Object.DestroyImmediate(surfaceCategory);
                Object.DestroyImmediate(desk);
            }
        }

        [Test]
        public void AddTagIsIdempotentAndRemoveTagRestoresEmptyState()
        {
            _asset.AddTag(_tag);
            _asset.AddTag(_tag);

            Assert.That(_asset.SemanticTags, Is.EqualTo(new[] { _tag }));
            Assert.That(_asset.HasTag(_tag), Is.True);

            _asset.RemoveTag(_tag);
            Assert.That(_asset.HasTag(_tag), Is.False);
        }

        [Test]
        public void TagCategoryUsageSeparatesAssetAndSurfaceAssignments()
        {
            TagCategory surfaceCategory = ScriptableObject.CreateInstance<TagCategory>();
            TagCategory sharedCategory = ScriptableObject.CreateInstance<TagCategory>();
            SemanticTag surfaceTag = ScriptableObject.CreateInstance<SemanticTag>();
            SemanticTag sharedTag = ScriptableObject.CreateInstance<SemanticTag>();
            surfaceCategory.Initialize(true, TagCategoryUsage.Surface);
            sharedCategory.Initialize(true, TagCategoryUsage.AssetAndSurface);
            surfaceTag.Initialize(surfaceCategory);
            sharedTag.Initialize(sharedCategory);

            try
            {
                _asset.AddTag(surfaceTag);
                _asset.AddTag(sharedTag);
                _asset.SetRequiredSupportTags(new[] { _tag, surfaceTag, sharedTag });

                Assert.That(_category.Usage, Is.EqualTo(TagCategoryUsage.Asset));
                Assert.That(surfaceCategory.SupportsAssets, Is.False);
                Assert.That(surfaceCategory.SupportsSurfaces, Is.True);
                Assert.That(sharedCategory.SupportsAssets, Is.True);
                Assert.That(sharedCategory.SupportsSurfaces, Is.True);
                Assert.That(_asset.SemanticTags, Is.EqualTo(new[] { sharedTag }));
                Assert.That(_asset.RequiredSupportTags, Is.EqualTo(new[] { surfaceTag, sharedTag }));
            }
            finally
            {
                Object.DestroyImmediate(sharedTag);
                Object.DestroyImmediate(surfaceTag);
                Object.DestroyImmediate(sharedCategory);
                Object.DestroyImmediate(surfaceCategory);
            }
        }

        [Test]
        public void HasAnyTagTreatsEmptyRequirementsAsMatch()
        {
            Assert.That(_asset.HasAnyTag(null), Is.True);
            Assert.That(_asset.HasAnyTag(System.Array.Empty<SemanticTag>()), Is.True);
        }

        [Test]
        public void HasAnyTagRequiresAtLeastOneMatchingNonEmptyRequirement()
        {
            TagCategory otherCategory = ScriptableObject.CreateInstance<TagCategory>();
            SemanticTag otherTag = ScriptableObject.CreateInstance<SemanticTag>();
            otherCategory.Initialize();
            otherTag.Initialize(otherCategory);

            try
            {
                _asset.AddTag(_tag);
                Assert.That(_asset.HasAnyTag(new[] { otherTag, _tag }), Is.True);
                Assert.That(_asset.HasAnyTag(new[] { otherTag }), Is.False);
                Assert.That(_asset.HasAnyTagCategory(_category), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(otherTag);
                Object.DestroyImmediate(otherCategory);
            }
        }

        [Test]
        public void HasTagInCategoryUsesTagCategory()
        {
            _asset.AddTag(_tag);

            Assert.That(_asset.HasTagInCategory(_category), Is.True);
            Assert.That(_asset.HasTagInCategory(null), Is.False);
        }

        [Test]
        public void AssetCatalogKeepsDistinctValidEntries()
        {
            AssetCatalog catalog = ScriptableObject.CreateInstance<AssetCatalog>();
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();

            try
            {
                catalog.SetAssets(new[] { _asset, _asset, null });
                catalog.SetTags(new[] { _tag, _tag, null });
                catalog.SetCategories(new[] { _category, _category, null });
                catalog.SetAssetPools(new[] { pool, pool, null });

                Assert.That(catalog.Assets, Is.EqualTo(new[] { _asset }));
                Assert.That(catalog.Tags, Is.EqualTo(new[] { _tag }));
                Assert.That(catalog.Categories, Is.EqualTo(new[] { _category }));
                Assert.That(catalog.AssetPools, Is.EqualTo(new[] { pool }));
            }
            finally
            {
                Object.DestroyImmediate(pool);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void AssetCatalogAddMethodsAreIdempotent()
        {
            AssetCatalog catalog = ScriptableObject.CreateInstance<AssetCatalog>();
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();

            try
            {
                catalog.AddAsset(_asset);
                catalog.AddAsset(_asset);
                catalog.AddTag(_tag);
                catalog.AddTag(_tag);
                catalog.AddCategory(_category);
                catalog.AddCategory(_category);
                catalog.AddAssetPool(pool);
                catalog.AddAssetPool(pool);

                Assert.That(catalog.Assets, Has.Count.EqualTo(1));
                Assert.That(catalog.Tags, Has.Count.EqualTo(1));
                Assert.That(catalog.Categories, Has.Count.EqualTo(1));
                Assert.That(catalog.AssetPools, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(pool);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void StaticPoolReturnsDistinctValidAssetsRegardlessOfCatalog()
        {
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            pool.Initialize("Static", AssetPoolMode.Static);

            try
            {
                pool.AddStaticAssets(new[] { _asset, _asset, null });

                Assert.That(pool.HasValidStaticAssets, Is.True);
                Assert.That(pool.ResolveAssets((IEnumerable<AssetDefinition>)null), Is.EqualTo(new[] { _asset }));

                pool.RemoveStaticAsset(_asset);
                Assert.That(pool.HasValidStaticAssets, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(pool);
            }
        }

        [Test]
        public void DynamicPoolAppliesPlacementAndOrientationFilters()
        {
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            AssetDefinition wallAsset = CreateTemporaryAsset("Wall", PlacementType.Wall, OrientationMode.FaceTarget);
            AssetDefinition wrongOrientation = CreateTemporaryAsset("Wrong Orientation", PlacementType.Wall, OrientationMode.None);
            AssetDefinition floorAsset = CreateTemporaryAsset("Floor", PlacementType.Floor, OrientationMode.FaceTarget);

            try
            {
                ConfigureDynamicPool(pool, PlacementType.Wall, OrientationMode.FaceTarget);

                IReadOnlyList<AssetDefinition> resolved = pool.ResolveAssets(new[]
                {
                    wallAsset,
                    wallAsset,
                    wrongOrientation,
                    floorAsset,
                    null
                });

                Assert.That(resolved, Is.EqualTo(new[] { wallAsset }));
                Assert.That(pool.MatchesAsset(null), Is.False);
            }
            finally
            {
                DestroyTemporaryAsset(wallAsset);
                DestroyTemporaryAsset(wrongOrientation);
                DestroyTemporaryAsset(floorAsset);
                Object.DestroyImmediate(pool);
            }
        }

        [Test]
        public void DynamicPoolRequiresEveryActiveSemanticFilter()
        {
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            _asset.AddTag(_tag);

            try
            {
                SerializedObject serializedPool = new(pool);
                serializedPool.FindProperty("mode").enumValueIndex = (int)AssetPoolMode.Dynamic;
                SerializedProperty filters = serializedPool.FindProperty("categoryFilters");
                filters.InsertArrayElementAtIndex(0);
                SerializedProperty filter = filters.GetArrayElementAtIndex(0);
                filter.FindPropertyRelative("category").objectReferenceValue = _category;
                SerializedProperty tags = filter.FindPropertyRelative("tags");
                tags.InsertArrayElementAtIndex(0);
                tags.GetArrayElementAtIndex(0).objectReferenceValue = _tag;
                serializedPool.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(pool.ResolveAssets(new[] { _asset }), Is.EqualTo(new[] { _asset }));

                _asset.RemoveTag(_tag);
                Assert.That(pool.ResolveAssets(new[] { _asset }), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(pool);
            }
        }

        [Test]
        public void PoolCleanupDropsDestroyedAssetsAndInactiveFilters()
        {
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            AssetDefinition temporary = CreateTemporaryAsset("Temporary", PlacementType.Floor, OrientationMode.None);
            pool.AddStaticAsset(temporary);

            SerializedObject serializedPool = new(pool);
            SerializedProperty filters = serializedPool.FindProperty("categoryFilters");
            filters.InsertArrayElementAtIndex(0);
            serializedPool.ApplyModifiedPropertiesWithoutUndo();

            DestroyTemporaryAsset(temporary);
            pool.RemoveMissingReferences();

            Assert.That(pool.StaticAssets, Is.Empty);
            Assert.That(pool.CategoryFilters, Is.Empty);
            Object.DestroyImmediate(pool);
        }

        [Test]
        public void PoolRemovesSemanticTagAndCategoryFilters()
        {
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            SerializedObject serializedPool = new(pool);
            SerializedProperty filters = serializedPool.FindProperty("categoryFilters");
            filters.InsertArrayElementAtIndex(0);
            SerializedProperty filter = filters.GetArrayElementAtIndex(0);
            filter.FindPropertyRelative("category").objectReferenceValue = _category;
            SerializedProperty tags = filter.FindPropertyRelative("tags");
            tags.InsertArrayElementAtIndex(0);
            tags.GetArrayElementAtIndex(0).objectReferenceValue = _tag;
            serializedPool.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                pool.RemoveTag(_tag);
                Assert.That(pool.CategoryFilters.Single().IsActive, Is.False);

                pool.RemoveCategory(_category);
                Assert.That(pool.CategoryFilters, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(pool);
            }
        }

        [Test]
        public void CatalogCleanupRemovesDestroyedReferencesFromNestedAssetsAndPools()
        {
            AssetCatalog catalog = ScriptableObject.CreateInstance<AssetCatalog>();
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            AssetDefinition temporary = CreateTemporaryAsset("Temporary", PlacementType.Floor, OrientationMode.None);
            SemanticTag temporaryTag = ScriptableObject.CreateInstance<SemanticTag>();
            temporaryTag.Initialize(_category);
            temporary.AddTag(temporaryTag);
            pool.AddStaticAsset(temporary);
            catalog.SetAssets(new[] { temporary });
            catalog.SetTags(new[] { temporaryTag });
            catalog.SetCategories(new[] { _category });
            catalog.SetAssetPools(new[] { pool });

            Object.DestroyImmediate(temporaryTag);
            DestroyTemporaryAsset(temporary);
            catalog.RemoveMissingReferences();

            Assert.That(catalog.Assets, Is.Empty);
            Assert.That(catalog.Tags, Is.Empty);
            Assert.That(pool.StaticAssets, Is.Empty);
            Object.DestroyImmediate(pool);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void SemanticTagCanMoveBetweenCategories()
        {
            TagCategory other = ScriptableObject.CreateInstance<TagCategory>();
            other.Initialize();

            try
            {
                _tag.name = "Forest";
                _tag.SetCategory(other);

                Assert.That(_tag.DisplayName, Is.EqualTo("Forest"));
                Assert.That(_tag.Category, Is.SameAs(other));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        private static void ConfigureDynamicPool(
            AssetPool pool,
            PlacementType placementType,
            OrientationMode orientationMode)
        {
            SerializedObject serializedPool = new(pool);
            serializedPool.FindProperty("mode").enumValueIndex = (int)AssetPoolMode.Dynamic;
            serializedPool.FindProperty("filterByPlacementType").boolValue = true;
            serializedPool.FindProperty("placementType").enumValueIndex = (int)placementType;
            serializedPool.FindProperty("filterByOrientationMode").boolValue = true;
            serializedPool.FindProperty("orientationMode").enumValueIndex = (int)orientationMode;
            serializedPool.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AssetDefinition CreateTemporaryAsset(
            string name,
            PlacementType placementType,
            OrientationMode orientationMode)
        {
            GameObject prefab = new(name + " Prefab");
            AssetDefinition asset = ScriptableObject.CreateInstance<AssetDefinition>();
            asset.name = name;
            asset.Initialize(prefab, Vector3.one);

            SerializedObject serializedAsset = new(asset);
            serializedAsset.FindProperty("placementType").enumValueIndex = (int)placementType;
            serializedAsset.FindProperty("orientationMode").enumValueIndex = (int)orientationMode;
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void DestroyTemporaryAsset(AssetDefinition asset)
        {
            if (!asset)
                return;

            GameObject prefab = asset.Prefab;
            Object.DestroyImmediate(asset);
            if (prefab)
                Object.DestroyImmediate(prefab);
        }
    }
}
