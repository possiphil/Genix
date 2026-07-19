using System;

namespace Genix.Placement
{
    [Serializable]
    public struct PlacementSettings
    {
        public bool useFixedObjectClearance;
        public float fixedObjectDistance;

        public PlacementSettings(bool useFixedObjectClearance = false, float fixedObjectDistance = 0f)
        {
            this.useFixedObjectClearance = useFixedObjectClearance;
            this.fixedObjectDistance = fixedObjectDistance;
        }
    }
}