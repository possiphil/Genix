using System;

namespace Genix.Placement
{
    /// <summary>Constraints applied while validating generated objects against fixed scene objects.</summary>
    [Serializable]
    public struct PlacementSettings
    {
        /// <summary>Whether fixed scene objects require an additional clearance radius.</summary>
        public bool useFixedObjectClearance;
        /// <summary>Required clearance from fixed objects in world units when enabled.</summary>
        public float fixedObjectDistance;

        /// <summary>Creates fixed-object placement constraints.</summary>
        /// <param name="useFixedObjectClearance">Whether to enforce additional clearance.</param>
        /// <param name="fixedObjectDistance">Clearance distance in world units.</param>
        public PlacementSettings(bool useFixedObjectClearance = false, float fixedObjectDistance = 0f)
        {
            this.useFixedObjectClearance = useFixedObjectClearance;
            this.fixedObjectDistance = fixedObjectDistance;
        }
    }
}
