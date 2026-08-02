using System;
using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Areas
{
    /// <summary>Controls which geometry is exposed as floor, wall, and ceiling placement surfaces.</summary>
    public enum SurfaceDiscoveryMode
    {
        /// <summary>Uses voxel-derived SFS boundary regions without physics-surface projection.</summary>
        SfsBoundaries,
        /// <summary>Projects matching physics surfaces only near voxel-derived SFS boundaries.</summary>
        NearSfsBoundaries,
        /// <summary>Searches matching physics surfaces throughout the complete target volume.</summary>
        AllMatchingSurfacesInVolume
    }

    /// <summary>Configures target-specific spatial data construction for one generation request.</summary>
    [Serializable]
    public struct AreaBuildSettings
    {
        /// <summary>Stores decomposition mode.</summary>
        public AreaDecompositionMode decompositionMode;
        /// <summary>Stores surface discovery mode.</summary>
        public SurfaceDiscoveryMode surfaceDiscoveryMode;
        /// <summary>Stores placement surface layers.</summary>
        public LayerMask placementSurfaceLayers;
        /// <summary>Stores floor surface layers.</summary>
        public LayerMask floorSurfaceLayers;
        /// <summary>Stores wall surface layers.</summary>
        public LayerMask wallSurfaceLayers;
        /// <summary>Stores ceiling surface layers.</summary>
        public LayerMask ceilingSurfaceLayers;
        /// <summary>Stores surface raycast height.</summary>
        public float surfaceRaycastHeight;
        /// <summary>Stores surface raycast distance.</summary>
        public float surfaceRaycastDistance;
        /// <summary>Stores floor normal y threshold.</summary>
        public float floorNormalYThreshold;
        /// <summary>Stores ceiling normal y threshold.</summary>
        public float ceilingNormalYThreshold;
        /// <summary>Targets for which spatial data should be built; not serialized with presets.</summary>
        [NonSerialized] public PlacementTarget placementTargets;
        /// <summary>Optional timing sink populated while constructing the area; not serialized.</summary>
        [NonSerialized] public AreaBuildProfile profile;

        /// <summary>Initializes a new instance of area build settings.</summary>
        public AreaBuildSettings(
            AreaDecompositionMode decompositionMode,
            LayerMask placementSurfaceLayers,
            LayerMask floorSurfaceLayers = default,
            LayerMask wallSurfaceLayers = default,
            LayerMask ceilingSurfaceLayers = default,
            float surfaceRaycastHeight = 100f,
            float surfaceRaycastDistance = 250f,
            float floorNormalYThreshold = 0.5f,
            float ceilingNormalYThreshold = -0.5f,
            PlacementTarget placementTargets = PlacementTarget.All,
            AreaBuildProfile profile = null,
            SurfaceDiscoveryMode surfaceDiscoveryMode = SurfaceDiscoveryMode.AllMatchingSurfacesInVolume)
        {
            this.decompositionMode = decompositionMode;
            this.surfaceDiscoveryMode = NormalizeSurfaceDiscoveryMode(surfaceDiscoveryMode);
            this.placementSurfaceLayers = placementSurfaceLayers;

            bool hasSpecificSurfaceLayers =
                floorSurfaceLayers.value != 0 ||
                wallSurfaceLayers.value != 0 ||
                ceilingSurfaceLayers.value != 0;

            this.floorSurfaceLayers = hasSpecificSurfaceLayers ? floorSurfaceLayers : placementSurfaceLayers;
            this.wallSurfaceLayers = hasSpecificSurfaceLayers ? wallSurfaceLayers : placementSurfaceLayers;
            this.ceilingSurfaceLayers = hasSpecificSurfaceLayers ? ceilingSurfaceLayers : placementSurfaceLayers;
            this.surfaceRaycastHeight = surfaceRaycastHeight;
            this.surfaceRaycastDistance = surfaceRaycastDistance;
            this.floorNormalYThreshold = Mathf.Clamp(floorNormalYThreshold, -1f, 1f);
            this.ceilingNormalYThreshold = Mathf.Clamp(ceilingNormalYThreshold, -1f, 1f);
            this.placementTargets = placementTargets == PlacementTarget.None
                ? PlacementTarget.All
                : placementTargets & PlacementTarget.All;
            this.profile = profile;
        }

        /// <summary>Gets effective surface discovery mode.</summary>
        public readonly SurfaceDiscoveryMode EffectiveSurfaceDiscoveryMode =>
            NormalizeSurfaceDiscoveryMode(surfaceDiscoveryMode);

        /// <summary>Indicates whether collider-based surface projection is enabled.</summary>
        public readonly bool UsesPhysicsSurfaceProjection =>
            EffectiveSurfaceDiscoveryMode != SurfaceDiscoveryMode.SfsBoundaries;

        /// <summary>Indicates whether collider projection is limited to SFS boundary regions.</summary>
        public readonly bool UsesBoundarySurfaceProjection =>
            EffectiveSurfaceDiscoveryMode == SurfaceDiscoveryMode.NearSfsBoundaries;

        /// <summary>Indicates whether matching colliders are searched throughout the target volume.</summary>
        public readonly bool UsesAllMatchingSurfaceSearch =>
            EffectiveSurfaceDiscoveryMode == SurfaceDiscoveryMode.AllMatchingSurfacesInVolume;

        /// <summary>Gets the physics layer mask for a placement type.</summary>
        public readonly LayerMask GetSurfaceLayers(PlacementType placementType)
        {
            return placementType switch
            {
                PlacementType.Wall => wallSurfaceLayers,
                PlacementType.Ceiling => ceilingSurfaceLayers,
                _ => floorSurfaceLayers
            };
        }

        /// <summary>Returns a copy limited to the supplied effective placement targets.</summary>
        public readonly AreaBuildSettings WithPlacementTargets(PlacementTarget targets)
        {
            AreaBuildSettings copy = this;
            copy.placementTargets = targets == PlacementTarget.None
                ? PlacementTarget.All
                : targets & PlacementTarget.All;
            return copy;
        }

        /// <summary>Returns a copy that records area-construction steps into the supplied profile.</summary>
        public readonly AreaBuildSettings WithProfile(AreaBuildProfile areaBuildProfile)
        {
            AreaBuildSettings copy = this;
            copy.profile = areaBuildProfile;
            return copy;
        }

        private static SurfaceDiscoveryMode NormalizeSurfaceDiscoveryMode(SurfaceDiscoveryMode mode)
        {
            return Enum.IsDefined(typeof(SurfaceDiscoveryMode), mode)
                ? mode
                : SurfaceDiscoveryMode.AllMatchingSurfacesInVolume;
        }

    }
}
