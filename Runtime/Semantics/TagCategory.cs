using UnityEngine;

namespace Genix.Semantics
{
    /// <summary>Defines a semantic-tag category and whether objects may use multiple tags from it.</summary>
    public sealed class TagCategory : ScriptableObject
    {
        [SerializeField] private bool allowMultipleTags = true;

        /// <summary>Gets display name.</summary>
        public string DisplayName => name;
        /// <summary>Indicates whether allow multiple tags.</summary>
        public bool AllowMultipleTags => allowMultipleTags;

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(bool allowMultipleTags = true)
        {
            this.allowMultipleTags = allowMultipleTags;
        }
    }
}
