using System;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Identifies whether an asset spacing rule targets one definition or an asset-tag group.</summary>
    public enum AssetSpacingRuleScope
    {
        /// <summary>The rule applies to one concrete neighboring asset definition.</summary>
        Asset,
        /// <summary>The rule applies to neighboring assets carrying the selected semantic tag.</summary>
        AssetTag
    }

    /// <summary>Defines a minimum center distance from matching generated assets.</summary>
    [Serializable]
    public sealed class AssetSpacingRule
    {
        [SerializeField] private AssetSpacingRuleScope scope = AssetSpacingRuleScope.AssetTag;
        [SerializeField] private AssetDefinition asset;
        [SerializeField] private SemanticTag assetTag;
        [SerializeField, Min(0f)] private float minimumDistance = 1f;

        /// <summary>Gets whether this rule matches one asset or an asset tag.</summary>
        public AssetSpacingRuleScope Scope => scope;
        /// <summary>Gets the concrete neighboring asset selected by this rule.</summary>
        public AssetDefinition Asset => asset;
        /// <summary>Gets the neighboring asset tag selected by this rule.</summary>
        public SemanticTag AssetTag => assetTag;
        /// <summary>Gets the required center-to-center distance in world units.</summary>
        public float MinimumDistance => Mathf.Max(0f, minimumDistance);
        /// <summary>Indicates whether the rule has a valid target and positive distance.</summary>
        public bool IsConfigured => MinimumDistance > 0f && (scope switch
        {
            AssetSpacingRuleScope.Asset => asset,
            AssetSpacingRuleScope.AssetTag => IsAssetTag(assetTag),
            _ => false
        });
        /// <summary>Gets a concise target name suitable for diagnostics.</summary>
        public string DisplayName => scope switch
        {
            AssetSpacingRuleScope.Asset when asset => asset.AssetName,
            AssetSpacingRuleScope.AssetTag when assetTag => assetTag.DisplayName,
            _ => "Unconfigured Rule"
        };

        /// <summary>Configures spacing from one concrete asset definition.</summary>
        public void ConfigureAsset(AssetDefinition targetAsset, float distance)
        {
            scope = AssetSpacingRuleScope.Asset;
            asset = targetAsset;
            assetTag = null;
            minimumDistance = Mathf.Max(0f, distance);
        }

        /// <summary>Configures spacing from every asset carrying an asset-compatible tag.</summary>
        public void ConfigureTag(SemanticTag targetTag, float distance)
        {
            scope = AssetSpacingRuleScope.AssetTag;
            asset = null;
            assetTag = IsAssetTag(targetTag) ? targetTag : null;
            minimumDistance = Mathf.Max(0f, distance);
        }

        /// <summary>Determines whether the supplied neighboring asset is constrained by this rule.</summary>
        public bool Matches(AssetDefinition other) => other && IsConfigured && (scope switch
        {
            AssetSpacingRuleScope.Asset => other == asset,
            AssetSpacingRuleScope.AssetTag => other.HasTag(assetTag),
            _ => false
        });

        internal void Normalize()
        {
            minimumDistance = Mathf.Max(0f, minimumDistance);

            if (scope == AssetSpacingRuleScope.Asset)
                assetTag = null;
            else
            {
                asset = null;

                if (!IsAssetTag(assetTag))
                    assetTag = null;
            }
        }

        private static bool IsAssetTag(SemanticTag tag) =>
            tag && tag.SupportsAssets;
    }
}
