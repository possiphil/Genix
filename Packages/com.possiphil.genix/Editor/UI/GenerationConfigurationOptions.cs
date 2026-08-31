using Genix.Areas;
using Genix.Core;
using UnityEngine;

namespace Genix.Editor.UI
{
    /// <summary>Shared values and labels for generation settings shown in editor workflows.</summary>
    internal static class GenerationConfigurationOptions
    {
        internal static readonly TargetDistributionMode[] TargetDistributionModes =
        {
            TargetDistributionMode.Random,
            TargetDistributionMode.Balanced,
            TargetDistributionMode.Weighted
        };

        internal static readonly GUIContent[] TargetDistributionLabels =
        {
            new("Random", "Choose placement targets freely from the available candidates. Best when no fixed target ratio is required."),
            new("Balanced", "Aim for an equal object count on every selected placement target."),
            new("Weighted", "Distribute objects according to the relative weights shown below.")
        };

        internal static readonly SurfaceDiscoveryMode[] SurfaceDiscoveryModes =
        {
            SurfaceDiscoveryMode.AllMatchingSurfacesInVolume,
            SurfaceDiscoveryMode.NearSfsBoundaries,
            SurfaceDiscoveryMode.SfsBoundaries
        };

        internal static readonly GUIContent[] SurfaceDiscoveryLabels =
        {
            new("All Matching Surfaces", "Search all colliders on the configured layers throughout the SFS volume. Recommended for most scenes and for interior floors at arbitrary heights."),
            new("Near SFS Boundaries", "Project onto matching colliders only near SFS boundary regions. Use when interior surfaces should be ignored and a smaller search area is preferable."),
            new("SFS Boundaries", "Use only voxel-derived SFS boundary regions without physics surface projection. Best for fully voxel-defined spaces.")
        };

        internal static readonly AreaDecompositionMode[] AreaDecompositionModes =
        {
            AreaDecompositionMode.Fast,
            AreaDecompositionMode.Precise
        };

        internal static readonly GUIContent[] AreaDecompositionLabels =
        {
            new("Layer Bounds", "Merge each voxel layer into broad rectangular regions. Faster, but holes and irregular outlines may be approximated."),
            new("Cell-Preserving", "Decompose occupied cells into tighter rectangles. Use for irregular SFS boundaries where preserving holes and outlines matters.")
        };
    }
}
