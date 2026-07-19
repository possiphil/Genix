using System.Collections.Generic;
using Genix.Orientation;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
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
        [SerializeField] private bool randomYawRotation = true;
        [SerializeField] private bool randomPitchRotation;
        [SerializeField] private bool randomRollRotation;

        public string AssetName => name;
        public GameObject Prefab => prefab;
        public IReadOnlyList<SemanticTag> SemanticTags => semanticTags;
        public IReadOnlyList<TagCategory> AnyTagCategories => anyTagCategories;

        public PlacementType PlacementType => placementType;
        public float PlacementHeight => placementHeight;
        public bool UseHeightOffset => useHeightOffset;
        public float MaxHeightOffset => maxHeightOffset;

        public Vector3 BoundsSize => boundsSize;
        public Vector3 BoundsCenterOffset => boundsCenterOffset;
        public Vector2 Footprint => new(boundsSize.x, boundsSize.z);
        public float Width => boundsSize.x;
        public float Height => boundsSize.y;
        public float Depth => boundsSize.z;

        public OrientationMode OrientationMode => orientationMode;
        public bool RandomYawRotation => randomYawRotation;
        public bool RandomPitchRotation => randomPitchRotation;
        public bool RandomRollRotation => randomRollRotation;

        public void Initialize(GameObject sourcePrefab, Vector3 generatedBoundsSize, Vector3 generatedBoundsCenterOffset = default)
        {
            prefab = sourcePrefab;
            boundsSize = generatedBoundsSize;
            boundsCenterOffset = generatedBoundsCenterOffset;
        }

        public void SetBoundsSize(Vector3 value)
        {
            boundsSize = new Vector3(
                Mathf.Max(0.01f, value.x),
                Mathf.Max(0.01f, value.y),
                Mathf.Max(0.01f, value.z));
        }

        public void SetBoundsCenterOffset(Vector3 value)
        {
            boundsCenterOffset = value;
        }

        public bool HasTag(SemanticTag tag)
        {
            return tag && semanticTags.Contains(tag);
        }

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

        public bool HasAnyTagCategory(TagCategory category)
        {
            return category && anyTagCategories.Contains(category);
        }

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

        public void AddTag(SemanticTag tag)
        {
            if (!tag || semanticTags.Contains(tag))
                return;

            semanticTags.Add(tag);
        }

        public void RemoveTag(SemanticTag tag)
        {
            semanticTags.Remove(tag);
        }

        public void RemoveMissingTags()
        {
            semanticTags.RemoveAll(tag => !tag);
            anyTagCategories.RemoveAll(category => !category);
        }
    }
}
