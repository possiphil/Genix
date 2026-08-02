using System.Collections.Generic;
using Genix.Orientation;
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
        [SerializeField] private PlacementType placementType = PlacementType.Floor;
        [SerializeField] private float placementHeight;
        [SerializeField] private bool useHeightOffset;
        [SerializeField] private float maxHeightOffset = 0.25f;
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

        /// <summary>Gets asset name.</summary>
        public string AssetName => name;
        /// <summary>Gets prefab.</summary>
        public GameObject Prefab => prefab;
        /// <summary>Gets semantic tags.</summary>
        public IReadOnlyList<SemanticTag> SemanticTags => semanticTags;
        /// <summary>Gets any tag categories.</summary>
        public IReadOnlyList<TagCategory> AnyTagCategories => anyTagCategories;

        /// <summary>Gets placement type.</summary>
        public PlacementType PlacementType => placementType;
        /// <summary>Gets placement height.</summary>
        public float PlacementHeight => placementHeight;
        /// <summary>Indicates whether height offset.</summary>
        public bool UseHeightOffset => useHeightOffset;
        /// <summary>Gets max height offset.</summary>
        public float MaxHeightOffset => maxHeightOffset;

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
        /// <summary>Gets max surface height difference.</summary>
        public float MaxSurfaceHeightDifference => Mathf.Max(0f, maxSurfaceHeightDifference);
        /// <summary>Gets min surface support.</summary>
        public float MinSurfaceSupport => Mathf.Clamp01(minSurfaceSupport);
        /// <summary>Gets surface sink offset.</summary>
        public float SurfaceSinkOffset => Mathf.Max(0f, surfaceSinkOffset);
        /// <summary>Indicates whether random yaw rotation.</summary>
        public bool RandomYawRotation => randomYawRotation;
        /// <summary>Indicates whether random pitch rotation.</summary>
        public bool RandomPitchRotation => randomPitchRotation;
        /// <summary>Indicates whether random roll rotation.</summary>
        public bool RandomRollRotation => randomRollRotation;

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
            return tag && semanticTags.Contains(tag);
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
            return category && anyTagCategories.Contains(category);
        }

        /// <summary>Determines whether tag in category.</summary>
        public bool HasTagInCategory(TagCategory category)
        {
            if (!category)
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
            if (!tag || semanticTags.Contains(tag))
                return;

            semanticTags.Add(tag);
        }

        /// <summary>Removes tag.</summary>
        public void RemoveTag(SemanticTag tag)
        {
            semanticTags.Remove(tag);
        }

        /// <summary>Removes missing tags.</summary>
        public void RemoveMissingTags()
        {
            semanticTags.RemoveAll(tag => !tag);
            anyTagCategories.RemoveAll(category => !category);
        }
    }
}
