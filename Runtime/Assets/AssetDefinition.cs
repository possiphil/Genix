using System.Collections.Generic;
using System.Linq;
using Genix.Orientation;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Controls how a wall asset chooses its vertical position within the target area.</summary>
    public enum WallVerticalPlacementMode
    {
        /// <summary>Uses wall samples across the complete target height.</summary>
        [InspectorName("Full Wall")] FullWall,
        /// <summary>Places the asset's lower bound at one height above the target area's lower bound.</summary>
        [InspectorName("Fixed Height")] FixedHeight,
        /// <summary>Distributes the asset's lower bound between two heights above the target area's lower bound.</summary>
        [InspectorName("Height Range")] HeightRange
    }

    /// <summary>Optional horizontal relationship between a floor or ceiling asset and detected walls.</summary>
    public enum WallProximityMode
    {
        /// <summary>Does not constrain wall distance.</summary>
        [InspectorName("Any Distance")] AnyDistance,
        /// <summary>Requires the asset bounds to lie within a maximum wall distance.</summary>
        [InspectorName("Near Wall")] NearWall,
        /// <summary>Requires at least a minimum clearance from every detected wall.</summary>
        [InspectorName("Away From Wall")] AwayFromWall
    }

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
        [SerializeField] private PlacementType placementType = PlacementType.Floor;
        [SerializeField] private WallVerticalPlacementMode wallVerticalPlacementMode = WallVerticalPlacementMode.FullWall;
        [SerializeField] private float placementHeight;
        [SerializeField, Min(0f)] private float wallMinHeight;
        [SerializeField, Min(0f)] private float wallMaxHeight = 2f;
        [SerializeField] private Vector3 boundsSize = new(1f, 1f, 1f);
        [SerializeField] private Vector3 boundsCenterOffset;
        [SerializeField] private OrientationMode orientationMode = OrientationMode.None;
        [SerializeField] private SurfaceFitMode surfaceFitMode = SurfaceFitMode.Strict;
        [SerializeField] private SurfaceAlignmentMode surfaceAlignmentMode = SurfaceAlignmentMode.AlignToSurface;
        [SerializeField] private SurfaceHeightMode surfaceHeightMode = SurfaceHeightMode.Average;
        [SerializeField, Min(0f)] private float maxSurfaceHeightDifference = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float minSurfaceSupport = 0.75f;
        [SerializeField, Min(0f)] private float surfaceSinkOffset;
        [SerializeField] private bool randomYawRotation = true;
        [SerializeField] private bool randomPitchRotation;
        [SerializeField] private bool randomRollRotation;
        [SerializeField] private WallProximityMode wallProximityMode = WallProximityMode.AnyDistance;
        [SerializeField, Min(0f)] private float wallDistance = 1f;

        /// <summary>Gets asset name.</summary>
        public string AssetName => name;
        /// <summary>Gets prefab.</summary>
        public GameObject Prefab => prefab;
        /// <summary>Gets semantic tags.</summary>
        public IReadOnlyList<SemanticTag> SemanticTags => semanticTags;
        /// <summary>Gets any tag categories.</summary>
        public IReadOnlyList<TagCategory> AnyTagCategories => anyTagCategories;
        /// <summary>Gets support tags of which at least one must be present when the list is not empty.</summary>
        public IReadOnlyList<SemanticTag> RequiredSupportTags => requiredSupportTags;
        /// <summary>Gets support tags that always reject the asset; forbidden tags take precedence over required tags.</summary>
        public IReadOnlyList<SemanticTag> ForbiddenSupportTags => forbiddenSupportTags;
        /// <summary>Gets support categories explicitly configured to accept no surface in Required Tags.</summary>
        public IReadOnlyList<TagCategory> RequiredSupportNoneCategories => requiredSupportNoneCategories;
        /// <summary>Gets support categories for which every surface is forbidden.</summary>
        public IReadOnlyList<TagCategory> ForbiddenSupportAnyCategories => forbiddenSupportAnyCategories;
        /// <summary>Indicates whether this asset has a per-generation-run placement limit.</summary>
        public bool LimitPlacements => limitPlacements;
        /// <summary>Gets the maximum accepted instances in one generation run.</summary>
        public int MaxPlacements => Mathf.Max(1, maxPlacements);

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

        /// <summary>Gets bounds size.</summary>
        public Vector3 BoundsSize => boundsSize;
        /// <summary>Gets bounds center offset.</summary>
        public Vector3 BoundsCenterOffset => boundsCenterOffset;
        /// <summary>Gets footprint.</summary>
        public Vector2 Footprint => new(boundsSize.x, boundsSize.z);
        /// <summary>Gets width.</summary>
        public float Width => boundsSize.x;
        /// <summary>Gets height.</summary>
        public float Height => boundsSize.y;
        /// <summary>Gets depth.</summary>
        public float Depth => boundsSize.z;

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
        }

        /// <summary>Sets placement-bound dimensions with a positive minimum on every axis.</summary>
        public void SetBoundsSize(Vector3 value)
        {
            boundsSize = new Vector3(
                Mathf.Max(0.01f, value.x),
                Mathf.Max(0.01f, value.y),
                Mathf.Max(0.01f, value.z));
        }

        /// <summary>Sets the offset from the prefab origin to the placement-bound center.</summary>
        public void SetBoundsCenterOffset(Vector3 value)
        {
            boundsCenterOffset = value;
        }

        /// <summary>Determines whether tag.</summary>
        public bool HasTag(SemanticTag tag)
        {
            return tag && tag.Category && tag.Category.SupportsAssets && semanticTags.Contains(tag);
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

        /// <summary>Replaces the required support-tag alternatives used for surface compatibility.</summary>
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

        /// <summary>Configures the maximum number of this asset accepted in one generation run.</summary>
        public void SetPlacementLimit(bool limited, int maximum)
        {
            limitPlacements = limited;
            maxPlacements = Mathf.Max(1, maximum);
        }

        /// <summary>Determines whether the supplied planned count has reached this asset's run limit.</summary>
        public bool HasReachedPlacementLimit(int plannedCount) =>
            limitPlacements && Mathf.Max(0, plannedCount) >= MaxPlacements;

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
        }

        private void OnValidate()
        {
            maxPlacements = Mathf.Max(1, maxPlacements);
            wallDistance = Mathf.Max(0f, wallDistance);

            if (placementType is PlacementType.Wall or PlacementType.InsideSpace)
                wallProximityMode = WallProximityMode.AnyDistance;

            RemoveMissingTags();
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
