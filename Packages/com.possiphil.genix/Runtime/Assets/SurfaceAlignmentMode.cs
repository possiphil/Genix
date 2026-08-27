using UnityEngine;

namespace Genix.Assets
{
    /// <summary>Controls whether adaptive surface fit changes asset tilt.</summary>
    public enum SurfaceAlignmentMode
    {
        /// <summary>Aligns the asset up direction with the fitted surface normal.</summary>
        [InspectorName("Align To Surface")] AlignToSurface,
        /// <summary>Uses fitted support height while keeping the asset upright.</summary>
        [InspectorName("Keep Upright")] KeepUpright
    }
}
