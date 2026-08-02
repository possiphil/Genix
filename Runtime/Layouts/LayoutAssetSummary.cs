using System;
using UnityEngine;

namespace Genix.Layouts
{
    /// <summary>Stores the prefab and instance count for one asset type in a saved layout.</summary>
    [Serializable]
    public sealed class LayoutAssetSummary
    {
        [SerializeField] private string assetName;
        [SerializeField] private int count;
        [SerializeField] private GameObject sourcePrefab;

        /// <summary>Gets asset name.</summary>
        public string AssetName => assetName;
        /// <summary>Gets the number of stored items.</summary>
        public int Count => count;
        /// <summary>Gets source prefab.</summary>
        public GameObject SourcePrefab => sourcePrefab;

        /// <summary>Initializes a new instance of layout asset summary.</summary>
        public LayoutAssetSummary(string assetName, int count, GameObject sourcePrefab)
        {
            this.assetName = string.IsNullOrWhiteSpace(assetName) ? "Generated Object" : assetName;
            this.count = Mathf.Max(0, count);
            this.sourcePrefab = sourcePrefab;
        }
    }
}
