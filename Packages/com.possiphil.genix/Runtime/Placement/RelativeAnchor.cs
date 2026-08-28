using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Geometry;
using Genix.Layouts;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Placement
{
    internal readonly struct RelativeAnchor
    {
        public Vector3 Position { get; }
        public Bounds Bounds { get; }
        public string Name { get; }
        public Vector3 Forward { get; }
        public Vector3 Right { get; }
        public AssetDefinition Asset { get; }
        public IReadOnlyList<Genix.Semantics.SemanticTag> AssetTags { get; }
        public PlacementSurfaceDescriptor SupportSurface { get; }
        public object Identity { get; }
        public string PersistentIdentityKey { get; }
        public AssetRelativeAnchorSource Source { get; }

        public RelativeAnchor(
            Vector3 position,
            Bounds bounds,
            string name,
            Vector3 forward = default,
            Vector3 right = default,
            AssetDefinition asset = null,
            IReadOnlyList<Genix.Semantics.SemanticTag> assetTags = null,
            PlacementSurfaceDescriptor supportSurface = null,
            object identity = null,
            AssetRelativeAnchorSource source = AssetRelativeAnchorSource.Any)
        {
            Position = position;
            Bounds = bounds;
            Name = string.IsNullOrWhiteSpace(name) ? "Relative Anchor" : name;
            Forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            Right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
            Asset = asset;
            AssetTags = assetTags ?? System.Array.Empty<Genix.Semantics.SemanticTag>();
            SupportSurface = supportSurface;
            Identity = identity;
            PersistentIdentityKey = RelativeAnchorProvider.GetPersistentIdentityKey(identity);
            Source = source;
        }

        public bool Matches(AssetRelativePlacementRule rule) =>
            rule != null && rule.Matches(Asset, AssetTags);
    }

    internal sealed class AssetRelationAnchorIndex
    {
        private readonly List<RelativeAnchor> _anchors = new();
        private readonly SpatialBoundsIndex _spatialIndex = new();
        private readonly HashSet<AssetDefinition> _assets = new();
        private readonly HashSet<Genix.Semantics.SemanticTag> _assetTags = new();

        public int Count => _anchors.Count;
        public IReadOnlyList<RelativeAnchor> Anchors => _anchors;

        public void Add(RelativeAnchor anchor)
        {
            _anchors.Add(anchor);
            _spatialIndex.Add(anchor.Bounds, _anchors.Count - 1);

            if (anchor.Asset)
            {
                _assets.Add(anchor.Asset);

                foreach (Genix.Semantics.SemanticTag tag in anchor.Asset.SemanticTags)
                {
                    if (tag && tag.SupportsAssets)
                        _assetTags.Add(tag);
                }
            }

            foreach (Genix.Semantics.SemanticTag tag in anchor.AssetTags)
            {
                if (tag && tag.SupportsAssets)
                    _assetTags.Add(tag);
            }
        }

        public IEnumerable<RelativeAnchor> Query(Bounds bounds)
        {
            foreach (int index in _spatialIndex.Query(bounds))
                yield return _anchors[index];
        }

        public bool HasMatch(AssetRelativePlacementRule rule) => rule.TargetScope switch
        {
            AssetRelativeTargetScope.Asset => rule.TargetAsset && _assets.Contains(rule.TargetAsset),
            AssetRelativeTargetScope.AssetTag => rule.TargetTag && _assetTags.Contains(rule.TargetTag),
            _ => false
        };
    }
}
