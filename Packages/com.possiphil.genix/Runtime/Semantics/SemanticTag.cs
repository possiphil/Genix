using UnityEngine;

namespace Genix.Semantics
{
    /// <summary>Defines a reusable semantic label belonging to an optional category.</summary>
    public sealed class SemanticTag : ScriptableObject
    {
        [SerializeField] private TagCategory category;

        /// <summary>Gets display name.</summary>
        public string DisplayName => name;
        /// <summary>Gets category.</summary>
        public TagCategory Category => category;
        /// <summary>Indicates whether this tag may describe assets and asset relationships.</summary>
        public bool SupportsAssets => category && category.SupportsAssets;
        /// <summary>Indicates whether this tag may describe placement support surfaces.</summary>
        public bool SupportsSurfaces => category && category.SupportsSurfaces;

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(TagCategory category)
        {
            this.category = category;
        }

        /// <summary>Sets category.</summary>
        public void SetCategory(TagCategory category)
        {
            this.category = category;
        }
    }
}
