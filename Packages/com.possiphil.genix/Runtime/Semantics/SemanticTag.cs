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
