using System;
using UnityEngine;

namespace Genix.Areas
{
    [Serializable]
    public struct AreaBuildSettings
    {
        public AreaDecompositionMode decompositionMode;
        public bool usePlacementSurfaceCheck;
        public LayerMask placementSurfaceLayers;
        public float surfaceRaycastHeight;
        public float surfaceRaycastDistance;
        public float minSurfaceNormalY;
        public float floorNormalYThreshold;
        public float ceilingNormalYThreshold;

        public AreaBuildSettings(
            AreaDecompositionMode decompositionMode,
            bool usePlacementSurfaceCheck,
            LayerMask placementSurfaceLayers,
            float surfaceRaycastHeight = 100f,
            float surfaceRaycastDistance = 250f,
            float minSurfaceNormalY = 0.65f,
            float floorNormalYThreshold = 0.5f,
            float ceilingNormalYThreshold = -0.5f)
        {
            this.decompositionMode = decompositionMode;
            this.usePlacementSurfaceCheck = usePlacementSurfaceCheck;
            this.placementSurfaceLayers = placementSurfaceLayers;
            this.surfaceRaycastHeight = surfaceRaycastHeight;
            this.surfaceRaycastDistance = surfaceRaycastDistance;
            this.minSurfaceNormalY = minSurfaceNormalY;
            this.floorNormalYThreshold = Mathf.Clamp(floorNormalYThreshold, -1f, 1f);
            this.ceilingNormalYThreshold = Mathf.Clamp(ceilingNormalYThreshold, -1f, 1f);
        }
    }
}
