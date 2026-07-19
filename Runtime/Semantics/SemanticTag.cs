using UnityEngine;

namespace Genix.Semantics
{
    public sealed class SemanticTag : ScriptableObject
    {
        [SerializeField] private TagCategory category;

        public string DisplayName => name;
        public TagCategory Category => category;

        public void Initialize(TagCategory category)
        {
            this.category = category;
        }

        public void SetCategory(TagCategory category)
        {
            this.category = category;
        }
    }
}
