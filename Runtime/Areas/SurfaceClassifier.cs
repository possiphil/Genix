using Genix.Assets;
using UnityEngine;

namespace Genix.Areas
{
    public static class SurfaceClassifier
    {
        public static PlacementType Classify(Vector3 normal, AreaBuildSettings settings)
        {
            if (normal.sqrMagnitude <= 0.001f)
                return PlacementType.Wall;

            float normalY = normal.normalized.y;
            float floorThreshold = Mathf.Clamp(settings.floorNormalYThreshold, -1f, 1f);
            float ceilingThreshold = Mathf.Clamp(settings.ceilingNormalYThreshold, -1f, 1f);

            if (ceilingThreshold > floorThreshold)
                (floorThreshold, ceilingThreshold) = (ceilingThreshold, floorThreshold);

            if (normalY >= floorThreshold)
                return PlacementType.Floor;

            return normalY <= ceilingThreshold ? PlacementType.Ceiling : PlacementType.Wall;
        }
    }
}
