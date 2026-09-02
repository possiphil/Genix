using System.Collections.Generic;
using Genix.Diagnostics;
using Genix.Semantics;
using UnityEngine;

namespace Genix.Areas
{
    /// <summary>Adapts an external spatial system into the representation consumed by Genix.</summary>
    public interface IAreaSource
    {
        /// <summary>Gets stable source metadata recorded in diagnostics.</summary>
        SpatialSourceInfo SourceInfo { get; }
        /// <summary>Gets the transform that owns the target area and its fixed scene objects.</summary>
        Transform ParentTransform { get; }
        /// <summary>Gets semantic tags required when filtering assets for this area.</summary>
        IReadOnlyList<SemanticTag> SemanticTags { get; }
        /// <summary>Gets categories for which any asset tag in that category is accepted.</summary>
        IReadOnlyList<TagCategory> AnyTagCategories { get; }
        /// <summary>Determines whether a collider belongs to the source representation itself.</summary>
        /// <param name="collider">Collider to classify.</param>
        /// <returns><see langword="true"/> when the collider must be excluded from fixed-object checks.</returns>
        bool IsSourceCollider(Collider collider);

        /// <summary>Builds or retrieves a placement area for the requested targets and surface policy.</summary>
        /// <param name="settings">Area-construction settings.</param>
        /// <param name="area">Resulting area when successful.</param>
        /// <param name="error">Actionable failure description when unsuccessful.</param>
        /// <returns><see langword="true"/> when <paramref name="area"/> is valid.</returns>
        bool TryBuildArea(
            AreaBuildSettings settings,
            out PlacementArea area,
            out string error);
    }

    /// <summary>Optional capability exposed by area sources with manually invalidatable caches.</summary>
    public interface IAreaCacheControl
    {
        /// <summary>Invalidates all spatial data owned by this area source.</summary>
        void ClearCache();
    }

    /// <summary>
    /// Exposes whether an area build used its authoritative spatial representation or a degraded fallback.
    /// Diagnostics and validation tools can use this capability without coupling to a specific spatial-system
    /// integration.
    /// </summary>
    public interface IAreaSourceIntegrityStatus
    {
        /// <summary>Gets whether the most recent successful area build used authoritative spatial data.</summary>
        bool UsedAuthoritativeSpatialData { get; }
        /// <summary>Gets a concise explanation of the spatial source used by the most recent area build.</summary>
        string SpatialDataStatusMessage { get; }
    }
}
