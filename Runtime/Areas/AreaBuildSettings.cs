using System;
using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Areas
{
    public enum SurfaceDiscoveryMode
    {
        SfsBoundaries,
        NearSfsBoundaries,
        AllMatchingSurfacesInVolume
    }

    [Serializable]
    public struct AreaBuildSettings
    {
        public AreaDecompositionMode decompositionMode;
        public bool usePlacementSurfaceCheck;
        public SurfaceDiscoveryMode surfaceDiscoveryMode;
        public LayerMask placementSurfaceLayers;
        public LayerMask floorSurfaceLayers;
        public LayerMask wallSurfaceLayers;
        public LayerMask ceilingSurfaceLayers;
        public float surfaceRaycastHeight;
        public float surfaceRaycastDistance;
        public float minSurfaceNormalY;
        public float floorNormalYThreshold;
        public float ceilingNormalYThreshold;
        [NonSerialized] public PlacementTarget placementTargets;
        [NonSerialized] public AreaBuildProfile profile;

        public AreaBuildSettings(
            AreaDecompositionMode decompositionMode,
            bool usePlacementSurfaceCheck,
            LayerMask placementSurfaceLayers,
            LayerMask floorSurfaceLayers = default,
            LayerMask wallSurfaceLayers = default,
            LayerMask ceilingSurfaceLayers = default,
            float surfaceRaycastHeight = 100f,
            float surfaceRaycastDistance = 250f,
            float minSurfaceNormalY = 0.65f,
            float floorNormalYThreshold = 0.5f,
            float ceilingNormalYThreshold = -0.5f,
            PlacementTarget placementTargets = PlacementTarget.All,
            AreaBuildProfile profile = null,
            SurfaceDiscoveryMode surfaceDiscoveryMode = SurfaceDiscoveryMode.AllMatchingSurfacesInVolume)
        {
            this.decompositionMode = decompositionMode;
            this.surfaceDiscoveryMode = usePlacementSurfaceCheck
                ? NormalizeSurfaceDiscoveryMode(surfaceDiscoveryMode)
                : SurfaceDiscoveryMode.SfsBoundaries;
            this.usePlacementSurfaceCheck = this.surfaceDiscoveryMode != SurfaceDiscoveryMode.SfsBoundaries;
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
            this.minSurfaceNormalY = minSurfaceNormalY;
            this.floorNormalYThreshold = Mathf.Clamp(floorNormalYThreshold, -1f, 1f);
            this.ceilingNormalYThreshold = Mathf.Clamp(ceilingNormalYThreshold, -1f, 1f);
            this.placementTargets = placementTargets == PlacementTarget.None
                ? PlacementTarget.All
                : placementTargets & PlacementTarget.All;
            this.profile = profile;
        }

        public readonly SurfaceDiscoveryMode EffectiveSurfaceDiscoveryMode =>
            usePlacementSurfaceCheck
                ? NormalizeEnabledSurfaceDiscoveryMode(surfaceDiscoveryMode)
                : SurfaceDiscoveryMode.SfsBoundaries;

        public readonly bool UsesPhysicsSurfaceProjection =>
            EffectiveSurfaceDiscoveryMode != SurfaceDiscoveryMode.SfsBoundaries;

        public readonly bool UsesBoundarySurfaceProjection =>
            EffectiveSurfaceDiscoveryMode == SurfaceDiscoveryMode.NearSfsBoundaries;

        public readonly bool UsesAllMatchingSurfaceSearch =>
            EffectiveSurfaceDiscoveryMode == SurfaceDiscoveryMode.AllMatchingSurfacesInVolume;

        public readonly LayerMask GetSurfaceLayers(PlacementType placementType)
        {
            return placementType switch
            {
                PlacementType.Wall => wallSurfaceLayers,
                PlacementType.Ceiling => ceilingSurfaceLayers,
                _ => floorSurfaceLayers
            };
        }

        public readonly AreaBuildSettings WithPlacementTargets(PlacementTarget targets)
        {
            AreaBuildSettings copy = this;
            copy.placementTargets = targets == PlacementTarget.None
                ? PlacementTarget.All
                : targets & PlacementTarget.All;
            return copy;
        }

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

        private static SurfaceDiscoveryMode NormalizeEnabledSurfaceDiscoveryMode(SurfaceDiscoveryMode mode)
        {
            mode = NormalizeSurfaceDiscoveryMode(mode);
            return mode == SurfaceDiscoveryMode.SfsBoundaries
                ? SurfaceDiscoveryMode.AllMatchingSurfacesInVolume
                : mode;
        }
    }
}
