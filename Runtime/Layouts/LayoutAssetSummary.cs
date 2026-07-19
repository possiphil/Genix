using System;
using UnityEngine;

namespace Genix.Layouts
{
    [Serializable]
    public sealed class LayoutAssetSummary
    {
        [SerializeField] private string assetName;
        [SerializeField] private int count;
        [SerializeField] private GameObject sourcePrefab;

        public string AssetName => assetName;
        public int Count => count;
        public GameObject SourcePrefab => sourcePrefab;

        public LayoutAssetSummary(string assetName, int count, GameObject sourcePrefab)
        {
            this.assetName = string.IsNullOrWhiteSpace(assetName) ? "Generated Object" : assetName;
            this.count = Mathf.Max(0, count);
            this.sourcePrefab = sourcePrefab;
        }
    }
}
