using UnityEngine;

namespace Genix.Semantics
{
    public sealed class TagCategory : ScriptableObject
    {
        [SerializeField] private bool allowMultipleTags = true;

        public string DisplayName => name;
        public bool AllowMultipleTags => allowMultipleTags;

        public void Initialize(bool allowMultipleTags = true)
        {
            this.allowMultipleTags = allowMultipleTags;
        }
    }
}
