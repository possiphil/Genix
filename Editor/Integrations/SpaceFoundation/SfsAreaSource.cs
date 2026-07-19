using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Diagnostics;
using Genix.Semantics;
using UnityEngine;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    public sealed class SfsAreaSource : IAreaSource
    {
        private readonly SfsSpace _space;

        private SfsAnchor Anchor => _space ? _space.anchor : null;

        public SfsAreaSource(SfsSpace space)
        {
            _space = space;
        }

        public Transform ParentTransform => _space ? _space.transform : null;

        public SpatialSourceInfo SourceInfo => new(
            "Space Foundation location",
            _space ? AreaName.ToDesignerName(_space.name) : "Missing Location",
            Anchor ? Anchor.GetUniqueId() : string.Empty);

        public IReadOnlyList<SemanticTag> SemanticTags =>
            GetSemanticTagSet()?.SemanticTags ?? Array.Empty<SemanticTag>();

        public IReadOnlyList<TagCategory> AnyTagCategories =>
            GetSemanticTagSet()?.AnyTagCategories ?? Array.Empty<TagCategory>();

        public bool TryBuildArea(AreaBuildSettings settings, out PlacementArea area, out string error)
        {
            area = null;

            if (!_space)
            {
                error = "The selected Space Foundation location no longer exists.";
                return false;
            }

            if (!Anchor)
            {
                error = $"Location '{_space.name}' has no Space Foundation anchor.";
                return false;
            }

            HashSet<Vector3Int> subspace = SfsSubspaceProvider.Resolve(_space, Anchor);

            if (subspace != null && subspace.Count > 0)
            {
                return SfsAreaBuilder.TryBuild(
                    _space,
                    Anchor,
                    SourceInfo,
                    subspace,
                    settings,
                    IsSourceCollider,
                    out area,
                    out error);
            }

            return BoundsAreaFallback.TryBuild(
                _space.gameObject,
                SourceInfo,
                settings,
                IsSourceCollider,
                out area,
                out error);
        }

        public static void ClearPersistentSubspaceCache()
        {
            PersistentSubspaceCache.Clear();
        }

        private SemanticTagSet GetSemanticTagSet()
        {
            if (Anchor && Anchor.TryGetComponent(out SemanticTagSet anchorTags))
                return anchorTags;

            return _space && _space.TryGetComponent(out SemanticTagSet spaceTags)
                ? spaceTags
                : null;
        }

        public bool IsSourceCollider(Collider collider)
        {
            return collider && collider.GetComponentInParent<SfsSpace>() != null;
        }
    }
}
