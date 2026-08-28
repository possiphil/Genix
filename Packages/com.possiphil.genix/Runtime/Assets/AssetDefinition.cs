using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Orientation;
using Genix.Placement;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    /// <summary>
    /// Defines the prefab, semantic identity, placement target, bounds, rotation, and surface-fit policy of one placeable asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Genix/Assets/Asset Definition")]
    public sealed class AssetDefinition : ScriptableObject
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private List<SemanticTag> semanticTags = new();
        [SerializeField] private List<TagCategory> anyTagCategories = new();
        [SerializeField] private List<SemanticTag> requiredSupportTags = new();
        [SerializeField] private List<SemanticTag> forbiddenSupportTags = new();
        [SerializeField] private List<TagCategory> requiredSupportNoneCategories = new();
        [SerializeField] private List<TagCategory> forbiddenSupportAnyCategories = new();
        [SerializeField] private bool limitPlacements;
        [SerializeField, Min(1)] private int maxPlacements = 1;
        [SerializeField] private List<AssetSpacingRule> spacingRules = new();
        [SerializeField] private AssetRelativePlacementRule assetRelativePlacement = new();
        [SerializeField] private PathPlacementRule pathPlacement = new();
        [SerializeField] private PlacementType placementType = PlacementType.Floor;
        [SerializeField] private WallVerticalPlacementMode wallVerticalPlacementMode = WallVerticalPlacementMode.FullWall;
        [SerializeField] private float placementHeight = 0f;
        [SerializeField, Min(0f)] private float wallMinHeight = 0f;
        [SerializeField, Min(0f)] private float wallMaxHeight = 2f;
        [SerializeField] private Vector3 prefabRotationOffset;
        [SerializeField] private Vector3 boundsSize = new(1f, 1f, 1f);
        [SerializeField] private Vector3 boundsCenterOffset;
        [SerializeField] private bool reserveClearance;
        [SerializeField] private Vector3 clearanceSize = Vector3.one;
        [SerializeField] private Vector3 clearanceCenterOffset;
        [SerializeField] private OrientationMode orientationMode = OrientationMode.None;
        [SerializeField] private SurfaceFitMode surfaceFitMode = SurfaceFitMode.Strict;
        [SerializeField] private SurfaceAlignmentMode surfaceAlignmentMode = SurfaceAlignmentMode.AlignToSurface;
        [SerializeField] private SurfaceHeightMode surfaceHeightMode = SurfaceHeightMode.Average;
        [SerializeField, Min(0f)] private float maxSurfaceHeightDifference = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float minSurfaceSupport = 0.75f;
        [SerializeField, Min(0f)] private float surfaceSinkOffset = 0f;
        [SerializeField] private bool randomYawRotation = true;
        [SerializeField] private bool randomPitchRotation = false;
        [SerializeField] private bool randomRollRotation = false;
        [SerializeField] private WallProximityMode wallProximityMode = WallProximityMode.AnyDistance;
        [SerializeField, Min(0f)] private float wallDistance = 1f;

        [NonSerialized] private bool placementGeometryCacheValid;
        [NonSerialized] private Quaternion cachedPrefabRotationOffset = Quaternion.identity;
        [NonSerialized] private Vector3 cachedBoundsSize = Vector3.one;
        [NonSerialized] private Vector3 cachedBoundsCenterOffset;
        [NonSerialized] private Vector3 cachedClearanceSize = Vector3.one;
        [NonSerialized] private Vector3 cachedClearanceCenterOffset;

        /// <summary>Gets asset name.</summary>
        public string AssetName => name;
        /// <summary>Gets prefab.</summary>
        public GameObject Prefab => prefab;
        /// <summary>Gets semantic tags.</summary>
        public IReadOnlyList<SemanticTag> SemanticTags => semanticTags;
        /// <summary>Gets any tag categories.</summary>
        public IReadOnlyList<TagCategory> AnyTagCategories => anyTagCategories;
        /// <summary>Gets support-tag alternatives; one tag from every represented category must match.</summary>
        public IReadOnlyList<SemanticTag> RequiredSupportTags => requiredSupportTags;
        /// <summary>Gets support tags that always reject the asset; forbidden tags take precedence over required tags.</summary>
        public IReadOnlyList<SemanticTag> ForbiddenSupportTags => forbiddenSupportTags;
        /// <summary>Gets support categories explicitly configured to accept no surface in Required Tags.</summary>
        public IReadOnlyList<TagCategory> RequiredSupportNoneCategories => requiredSupportNoneCategories;
        /// <summary>Gets support categories for which every surface is forbidden.</summary>
        public IReadOnlyList<TagCategory> ForbiddenSupportAnyCategories => forbiddenSupportAnyCategories;
        /// <summary>Indicates whether this asset has a per-generation-run placement limit.</summary>
        public bool LimitPlacements => limitPlacements;
        /// <summary>Gets the maximum accepted instances across existing generated output and the current plan.</summary>
        public int MaxPlacements => Mathf.Max(1, maxPlacements);
        /// <summary>Gets optional minimum-distance rules for neighboring generated assets.</summary>
        public IReadOnlyList<AssetSpacingRule> SpacingRules => spacingRules;
        /// <summary>Gets the largest active asset-specific spacing distance.</summary>
        public float MaxSpacingDistance
        {
            get
            {
                float maximum = 0f;

                foreach (AssetSpacingRule rule in spacingRules)
                {
                    if (rule?.IsConfigured == true)
                        maximum = Mathf.Max(maximum, rule.MinimumDistance);
                }

                return maximum;
            }
        }
        /// <summary>Gets the optional semantic relationship this asset requires from another asset or scene anchor.</summary>
        public AssetRelativePlacementRule AssetRelativePlacement =>
            assetRelativePlacement ??= new AssetRelativePlacementRule();
        /// <summary>Gets the optional distance, side, and facing constraint relative to a semantic scene path.</summary>
        public PathPlacementRule PathPlacement => pathPlacement ??= new PathPlacementRule();

        /// <summary>Gets placement type.</summary>
        public PlacementType PlacementType => placementType;
        /// <summary>Gets the policy used to choose a wall asset's vertical position.</summary>
        public WallVerticalPlacementMode WallVerticalPlacementMode => wallVerticalPlacementMode;
        /// <summary>Gets the sampled-baseline offset or fixed asset-bottom height, depending on the wall placement mode.</summary>
        public float PlacementHeight => placementHeight;
        /// <summary>Gets the lower wall-height limit measured from the target area's lower bound.</summary>
        public float WallMinHeight => Mathf.Min(Mathf.Max(0f, wallMinHeight), Mathf.Max(0f, wallMaxHeight));
        /// <summary>Gets the upper wall-height limit measured from the target area's lower bound.</summary>
        public float WallMaxHeight => Mathf.Max(Mathf.Max(0f, wallMinHeight), Mathf.Max(0f, wallMaxHeight));

        /// <summary>Gets the Euler correction applied to the prefab after Genix determines its placement orientation.</summary>
        public Vector3 PrefabRotationOffset => prefabRotationOffset;
        /// <summary>Gets placement-bound dimensions after applying the prefab rotation correction.</summary>
        public Vector3 BoundsSize
        {
            get
            {
                EnsurePlacementGeometryCache();
                return cachedBoundsSize;
            }
        }
        /// <summary>Gets the corrected offset from the prefab origin to the placement-bound center.</summary>
        public Vector3 BoundsCenterOffset
        {
            get
            {
                EnsurePlacementGeometryCache();
                return cachedBoundsCenterOffset;
            }
        }
        /// <summary>Gets footprint.</summary>
        public Vector2 Footprint => new(BoundsSize.x, BoundsSize.z);
        /// <summary>Gets width.</summary>
        public float Width => BoundsSize.x;
        /// <summary>Gets height.</summary>
        public float Height => BoundsSize.y;
        /// <summary>Gets depth.</summary>
        public float Depth => BoundsSize.z;
        /// <summary>Indicates whether this asset reserves an additional collider-free volume.</summary>
        public bool ReserveClearance => reserveClearance;
        /// <summary>Gets the full local-axis dimensions of the reserved clearance volume.</summary>
        public Vector3 ClearanceSize
        {
            get
            {
                EnsurePlacementGeometryCache();
                return cachedClearanceSize;
            }
        }
        /// <summary>Gets the clearance center relative to the prefab origin.</summary>
        public Vector3 ClearanceCenterOffset
        {
            get
            {
                EnsurePlacementGeometryCache();
                return cachedClearanceCenterOffset;
            }
        }

        /// <summary>Gets orientation mode.</summary>
        public OrientationMode OrientationMode => orientationMode;
        /// <summary>Gets surface fit mode.</summary>
        public SurfaceFitMode SurfaceFitMode => surfaceFitMode;
        /// <summary>Gets surface alignment mode.</summary>
        public SurfaceAlignmentMode SurfaceAlignmentMode => surfaceAlignmentMode;
        /// <summary>Gets surface height mode.</summary>
        public SurfaceHeightMode SurfaceHeightMode => surfaceHeightMode;
        /// <summary>Gets the maximum supported height or wall-depth variation.</summary>
        public float MaxSurfaceHeightDifference => Mathf.Max(0f, maxSurfaceHeightDifference);
        /// <summary>Gets min surface support.</summary>
        public float MinSurfaceSupport => Mathf.Clamp01(minSurfaceSupport);
        /// <summary>Gets surface sink offset.</summary>
        public float SurfaceSinkOffset => Mathf.Max(0f, surfaceSinkOffset);
        /// <summary>Indicates whether random yaw rotation.</summary>
        public bool RandomYawRotation => randomYawRotation;
        /// <summary>Indicates whether random pitch rotation.</summary>
        public bool RandomPitchRotation => randomPitchRotation;
        /// <summary>Indicates whether random roll is enabled around the forward axis or wall normal.</summary>
        public bool RandomRollRotation => randomRollRotation;
        /// <summary>Gets the optional relationship to detected walls.</summary>
        public WallProximityMode WallProximityMode => wallProximityMode;
        /// <summary>Gets the maximum near-wall distance or minimum away-from-wall clearance.</summary>
        public float WallDistance => Mathf.Max(0f, wallDistance);

        /// <summary>Initializes the prefab and placement bounds of a newly created definition.</summary>
        public void Initialize(GameObject sourcePrefab, Vector3 generatedBoundsSize, Vector3 generatedBoundsCenterOffset = default)
        {
            prefab = sourcePrefab;
            boundsSize = generatedBoundsSize;
            boundsCenterOffset = generatedBoundsCenterOffset;
            InvalidatePlacementGeometryCache();
        }

        /// <summary>Sets placement-bound dimensions with a positive minimum on every axis.</summary>
        public void SetBoundsSize(Vector3 value)
        {
            boundsSize = new Vector3(
                Mathf.Max(0.01f, value.x),
                Mathf.Max(0.01f, value.y),
                Mathf.Max(0.01f, value.z));
            InvalidatePlacementGeometryCache();
        }

        /// <summary>Sets the offset from the prefab origin to the placement-bound center.</summary>
        public void SetBoundsCenterOffset(Vector3 value)
        {
            boundsCenterOffset = value;
            InvalidatePlacementGeometryCache();
        }

        /// <summary>Sets the prefab-local Euler correction applied after Genix computes placement orientation.</summary>
        public void SetPrefabRotationOffset(Vector3 value)
        {
            prefabRotationOffset = value;
            InvalidatePlacementGeometryCache();
        }

        /// <summary>Combines a logical Genix placement orientation with this prefab's import-axis correction.</summary>
        public Quaternion ApplyPrefabRotationOffset(Quaternion placementRotation)
        {
            EnsurePlacementGeometryCache();
            return placementRotation * cachedPrefabRotationOffset;
        }

        /// <summary>Recovers the logical Genix placement orientation from an instantiated prefab root.</summary>
        public Quaternion RemovePrefabRotationOffset(Quaternion prefabRotation)
        {
            EnsurePlacementGeometryCache();
            return prefabRotation * Quaternion.Inverse(cachedPrefabRotationOffset);
        }

        /// <summary>Configures an optional local-space volume that other generated and fixed geometry must leave empty.</summary>
        public void SetClearance(bool enabled, Vector3 size, Vector3 centerOffset)
        {
            reserveClearance = enabled;
            clearanceSize = ClampSize(size);
            clearanceCenterOffset = centerOffset;
            InvalidatePlacementGeometryCache();
        }

        /// <summary>Creates this asset's world-space clearance volume for a planned placement.</summary>
        public OrientedBounds CreateClearanceBounds(PlacementCandidate candidate)
        {
            Vector3 objectOrigin = candidate.Position - candidate.Rotation * BoundsCenterOffset;
            return CreateCorrectedClearanceBounds(objectOrigin, candidate.Rotation);
        }

        /// <summary>Creates this asset's world-space clearance volume for an instantiated prefab root.</summary>
        public OrientedBounds CreateClearanceBounds(Vector3 objectOrigin, Quaternion prefabRotation) =>
            CreateCorrectedClearanceBounds(objectOrigin, RemovePrefabRotationOffset(prefabRotation));

        /// <summary>Determines whether tag.</summary>
        public bool HasTag(SemanticTag tag)
        {
            return tag && tag.SupportsAssets && semanticTags.Contains(tag);
        }

        /// <summary>Determines whether any tag.</summary>
        public bool HasAnyTag(IReadOnlyList<SemanticTag> tags)
        {
            if (tags == null || tags.Count == 0)
                return true;

            foreach (SemanticTag tag in tags)
            {
                if (HasTag(tag))
                    return true;
            }

            return false;
        }

        /// <summary>Determines whether any tag category.</summary>
        public bool HasAnyTagCategory(TagCategory category)
        {
            return category && category.SupportsAssets && anyTagCategories.Contains(category);
        }

        /// <summary>Determines whether tag in category.</summary>
        public bool HasTagInCategory(TagCategory category)
        {
            if (!category || !category.SupportsAssets)
                return false;

            foreach (SemanticTag tag in semanticTags)
            {
                if (tag && tag.Category == category)
                    return true;
            }

            return false;
        }

        /// <summary>Adds tag.</summary>
        public void AddTag(SemanticTag tag)
        {
            if (!tag || !tag.Category || !tag.Category.SupportsAssets || semanticTags.Contains(tag))
                return;

            semanticTags.Add(tag);
        }

        /// <summary>Removes tag.</summary>
        public void RemoveTag(SemanticTag tag)
        {
            semanticTags.Remove(tag);
        }

        /// <summary>
        /// Replaces required support tags. Tags in one category are alternatives; represented categories combine conjunctively.
        /// </summary>
        public void SetRequiredSupportTags(IEnumerable<SemanticTag> tags)
        {
            requiredSupportTags = NormalizeTags(tags, requireSurfaceUsage: true);
        }

        /// <summary>Replaces the forbidden support tags, which take precedence over required tags.</summary>
        public void SetForbiddenSupportTags(IEnumerable<SemanticTag> tags)
        {
            forbiddenSupportTags = NormalizeTags(tags, requireSurfaceUsage: true);
        }

        /// <summary>Replaces categories whose Required selection is explicitly None.</summary>
        public void SetRequiredSupportNoneCategories(IEnumerable<TagCategory> categories)
        {
            requiredSupportNoneCategories = NormalizeSurfaceCategories(categories);
        }

        /// <summary>Replaces categories whose Forbidden selection is explicitly Any.</summary>
        public void SetForbiddenSupportAnyCategories(IEnumerable<TagCategory> categories)
        {
            forbiddenSupportAnyCategories = NormalizeSurfaceCategories(categories);
        }

        /// <summary>Configures the maximum number of this asset accepted in generated output.</summary>
        public void SetPlacementLimit(bool limited, int maximum)
        {
            limitPlacements = limited;
            maxPlacements = Mathf.Max(1, maximum);
        }

        /// <summary>Determines whether the supplied generated count has reached this asset's limit.</summary>
        public bool HasReachedPlacementLimit(int generatedCount) =>
            limitPlacements && Mathf.Max(0, generatedCount) >= MaxPlacements;

        /// <summary>Returns the greatest configured minimum distance matching another asset.</summary>
        public float GetMinimumSpacingTo(AssetDefinition other)
        {
            float minimum = 0f;

            foreach (AssetSpacingRule rule in spacingRules)
            {
                if (rule?.Matches(other) == true)
                    minimum = Mathf.Max(minimum, rule.MinimumDistance);
            }

            return minimum;
        }

        /// <summary>Replaces asset-specific spacing rules with normalized entries.</summary>
        public void SetSpacingRules(IEnumerable<AssetSpacingRule> rules)
        {
            spacingRules = rules?.Where(rule => rule != null).ToList() ?? new List<AssetSpacingRule>();
            NormalizeSpacingRules();
        }

        /// <summary>Configures the optional distance relationship to detected walls.</summary>
        public void SetWallProximity(WallProximityMode mode, float distance)
        {
            wallProximityMode = placementType is PlacementType.Floor or PlacementType.Ceiling
                ? mode
                : WallProximityMode.AnyDistance;
            wallDistance = Mathf.Max(0f, distance);
        }

        /// <summary>Removes missing tags.</summary>
        public void RemoveMissingTags()
        {
            semanticTags.RemoveAll(tag => !tag || !tag.Category || !tag.Category.SupportsAssets);
            anyTagCategories.RemoveAll(category => !category || !category.SupportsAssets);
            requiredSupportTags.RemoveAll(tag => !tag || !tag.Category || !tag.Category.SupportsSurfaces);
            forbiddenSupportTags.RemoveAll(tag => !tag || !tag.Category || !tag.Category.SupportsSurfaces);
            requiredSupportNoneCategories.RemoveAll(category => !category || !category.SupportsSurfaces);
            forbiddenSupportAnyCategories.RemoveAll(category => !category || !category.SupportsSurfaces);
            NormalizeSpacingRules();
            assetRelativePlacement ??= new AssetRelativePlacementRule();
            assetRelativePlacement.Normalize();
            pathPlacement ??= new PathPlacementRule();
            pathPlacement.Normalize();
        }

        private void OnValidate()
        {
            InvalidatePlacementGeometryCache();
            maxPlacements = Mathf.Max(1, maxPlacements);
            wallDistance = Mathf.Max(0f, wallDistance);
            clearanceSize = ClampSize(clearanceSize);

            if (placementType is PlacementType.Wall or PlacementType.InsideSpace)
                wallProximityMode = WallProximityMode.AnyDistance;

            RemoveMissingTags();
        }

        private static Vector3 ClampSize(Vector3 size) => new(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y),
            Mathf.Max(0.01f, size.z));

        private OrientedBounds CreateCorrectedClearanceBounds(
            Vector3 objectOrigin,
            Quaternion placementRotation)
        {
            EnsurePlacementGeometryCache();
            return new OrientedBounds(
                objectOrigin + placementRotation * cachedClearanceCenterOffset,
                cachedClearanceSize,
                placementRotation);
        }

        private void EnsurePlacementGeometryCache()
        {
            if (placementGeometryCacheValid)
                return;

            cachedPrefabRotationOffset = Quaternion.Euler(prefabRotationOffset);
            Vector3 prefabScale = prefab ? prefab.transform.localScale : Vector3.one;
            Vector3 scaledBoundsCenterOffset = Vector3.Scale(boundsCenterOffset, prefabScale);
            Vector3 scaledClearanceCenterOffset = Vector3.Scale(clearanceCenterOffset, prefabScale);
            cachedBoundsSize = RotateAxisAlignedSize(ClampSize(boundsSize), cachedPrefabRotationOffset);
            cachedBoundsCenterOffset = cachedPrefabRotationOffset * scaledBoundsCenterOffset;
            cachedClearanceSize = RotateAxisAlignedSize(ClampSize(clearanceSize), cachedPrefabRotationOffset);
            cachedClearanceCenterOffset = cachedPrefabRotationOffset * scaledClearanceCenterOffset;
            placementGeometryCacheValid = true;
        }

        private void InvalidatePlacementGeometryCache() => placementGeometryCacheValid = false;

        private static Vector3 RotateAxisAlignedSize(Vector3 size, Quaternion rotation)
        {
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            return new Vector3(
                Mathf.Abs(right.x) * size.x + Mathf.Abs(up.x) * size.y + Mathf.Abs(forward.x) * size.z,
                Mathf.Abs(right.y) * size.x + Mathf.Abs(up.y) * size.y + Mathf.Abs(forward.y) * size.z,
                Mathf.Abs(right.z) * size.x + Mathf.Abs(up.z) * size.y + Mathf.Abs(forward.z) * size.z);
        }

        private void NormalizeSpacingRules()
        {
            spacingRules ??= new List<AssetSpacingRule>();
            spacingRules.RemoveAll(rule => rule == null);

            foreach (AssetSpacingRule rule in spacingRules)
                rule.Normalize();
        }

        private static List<SemanticTag> NormalizeTags(
            IEnumerable<SemanticTag> tags,
            bool requireSurfaceUsage = false) =>
            tags?
                .Where(tag => tag && tag.Category && (!requireSurfaceUsage || tag.Category.SupportsSurfaces))
                .Distinct()
                .ToList() ?? new List<SemanticTag>();

        private static List<TagCategory> NormalizeSurfaceCategories(
            IEnumerable<TagCategory> categories) =>
            categories?
                .Where(category => category && category.SupportsSurfaces)
                .Distinct()
                .ToList() ?? new List<TagCategory>();
    }
}
