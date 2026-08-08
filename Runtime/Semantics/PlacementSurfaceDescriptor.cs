using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Genix.Semantics
{
    /// <summary>
    /// Describes the semantic role, preferred facing direction, and optional capacity of one placement surface.
    /// </summary>
    /// <remarks>
    /// Add the component to the collider itself or one of its parents. All descendant colliders share the same
    /// descriptor and capacity. The component does not change Unity physics behavior.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlacementSurfaceDescriptor : MonoBehaviour
    {
        [SerializeField] private List<SemanticTag> surfaceTags = new();
        [SerializeField] private List<TagCategory> noneTagCategories = new();
        [SerializeField] private bool usePreferredForward;
        [SerializeField] private bool limitCapacity;
        [SerializeField, Min(0)] private int maxCapacity = 1;

        /// <summary>Gets the semantic tags exposed by this surface.</summary>
        public IReadOnlyList<SemanticTag> SurfaceTags => surfaceTags;
        /// <summary>Gets categories explicitly configured as None instead of their default Any state.</summary>
        public IReadOnlyList<TagCategory> NoneTagCategories => noneTagCategories;
        /// <summary>Indicates whether assets may align to this object's forward direction.</summary>
        public bool UsePreferredForward => usePreferredForward;
        /// <summary>Gets the preferred world-space forward direction.</summary>
        public Vector3 PreferredForward => transform.forward;
        /// <summary>Indicates whether the number of supported generated objects is limited.</summary>
        public bool LimitCapacity => limitCapacity;
        /// <summary>Gets the maximum supported object count when <see cref="LimitCapacity"/> is enabled.</summary>
        public int MaxCapacity => Mathf.Max(0, maxCapacity);

        /// <summary>Determines whether the descriptor contains the supplied semantic tag.</summary>
        public bool HasTag(SemanticTag tag)
        {
            if (!tag || !tag.Category || !tag.Category.SupportsSurfaces || noneTagCategories.Contains(tag.Category))
                return false;

            bool hasSpecificSelection = surfaceTags.Any(existing =>
                existing && existing.Category == tag.Category);
            return !hasSpecificSelection || surfaceTags.Contains(tag);
        }

        /// <summary>Indicates whether the category uses its default Any state.</summary>
        public bool AcceptsAnyTag(TagCategory category) =>
            category &&
            category.SupportsSurfaces &&
            !noneTagCategories.Contains(category) &&
            surfaceTags.All(tag => !tag || tag.Category != category);

        /// <summary>Indicates whether the category explicitly accepts no tags.</summary>
        public bool AcceptsNoTag(TagCategory category) =>
            category && category.SupportsSurfaces && noneTagCategories.Contains(category);

        /// <summary>Replaces the semantic tags with a normalized, duplicate-free collection.</summary>
        public void SetSurfaceTags(IEnumerable<SemanticTag> tags)
        {
            surfaceTags = tags?
                .Where(tag => tag && tag.Category && tag.Category.SupportsSurfaces)
                .Distinct()
                .ToList() ?? new List<SemanticTag>();
            noneTagCategories.RemoveAll(category =>
                surfaceTags.Any(tag => tag && tag.Category == category));
        }

        /// <summary>Sets one category to Any, None, or a concrete tag selection.</summary>
        public void SetCategorySelection(
            TagCategory category,
            IEnumerable<SemanticTag> selectedTags,
            bool selectNone)
        {
            if (!category || !category.SupportsSurfaces)
                return;

            surfaceTags.RemoveAll(tag => !tag || tag.Category == category);
            noneTagCategories.RemoveAll(existing => !existing || existing == category);

            if (selectNone)
            {
                noneTagCategories.Add(category);
                return;
            }

            surfaceTags.AddRange(selectedTags?
                .Where(tag => tag && tag.Category == category)
                .Distinct() ?? Enumerable.Empty<SemanticTag>());
        }

        /// <summary>Resets every surface-tag category to its default Any state.</summary>
        public void ResetTagSelections()
        {
            surfaceTags.Clear();
            noneTagCategories.Clear();
        }

        /// <summary>Configures whether this transform provides a preferred forward direction.</summary>
        public void SetPreferredForwardEnabled(bool enabled) => usePreferredForward = enabled;

        /// <summary>Configures the optional placement capacity. A limited capacity of zero rejects all placements.</summary>
        public void SetCapacity(bool limited, int capacity)
        {
            limitCapacity = limited;
            maxCapacity = Mathf.Max(0, capacity);
        }

        private void OnValidate()
        {
            maxCapacity = Mathf.Max(0, maxCapacity);
            surfaceTags.RemoveAll(tag => !tag || !tag.Category || !tag.Category.SupportsSurfaces);
            noneTagCategories.RemoveAll(category => !category || !category.SupportsSurfaces);

            for (int i = surfaceTags.Count - 1; i >= 0; i--)
            {
                if (surfaceTags.IndexOf(surfaceTags[i]) != i)
                    surfaceTags.RemoveAt(i);
            }

            for (int i = noneTagCategories.Count - 1; i >= 0; i--)
            {
                TagCategory category = noneTagCategories[i];

                if (noneTagCategories.IndexOf(category) != i ||
                    surfaceTags.Any(tag => tag && tag.Category == category))
                {
                    noneTagCategories.RemoveAt(i);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!usePreferredForward)
                return;

            Vector3 origin = transform.position;
            Vector3 direction = PreferredForward.normalized;
            float length = 1.25f;
            Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.95f);
            Gizmos.DrawLine(origin, origin + direction * length);
            Gizmos.DrawSphere(origin + direction * length, 0.06f);
        }
    }
}
