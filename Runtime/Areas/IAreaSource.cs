using System.Collections.Generic;
using Genix.Diagnostics;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Areas
{
    public interface IAreaSource
    {
        SpatialSourceInfo SourceInfo { get; }
        Transform ParentTransform { get; }
        IReadOnlyList<SemanticTag> SemanticTags { get; }
        IReadOnlyList<TagCategory> AnyTagCategories { get; }
        bool IsSourceCollider(Collider collider);

        bool TryBuildArea(
            AreaBuildSettings settings,
            out PlacementArea area,
            out string error);
    }
}
