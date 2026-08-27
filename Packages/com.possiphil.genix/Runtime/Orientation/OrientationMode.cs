using UnityEngine;

namespace Genix.Orientation
{
    /// <summary>Controls whether placement rotation reacts to a contextual target.</summary>
    public enum OrientationMode
    {
        /// <summary>Leaves contextual facing disabled.</summary>
        [InspectorName("None")] None,
        /// <summary>Rotates the asset horizontally toward the configured target point.</summary>
        [InspectorName("Face Target")] FaceTarget,
        /// <summary>Aligns the asset to the projected local Z or X direction of its sampled support surface.</summary>
        [InspectorName("Match Support Forward")] MatchSupportForward
    }
}
