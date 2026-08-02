using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genix.Core
{
    /// <summary>Bit mask of surface and volume targets allowed by a generation request.</summary>
    [Flags]
    public enum PlacementTarget
    {
        /// <summary>Disables every placement target.</summary>
        [InspectorName("None")] None = 0,
        /// <summary>Allows upward-facing floor surfaces.</summary>
        [InspectorName("Floor")] Floor = 1 << 0,
        /// <summary>Allows near-vertical wall surfaces.</summary>
        [InspectorName("Wall")] Wall = 1 << 1,
        /// <summary>Allows downward-facing ceiling surfaces.</summary>
        [InspectorName("Ceiling")] Ceiling = 1 << 2,
        /// <summary>Allows free placement within valid volume cells.</summary>
        [InspectorName("Inside Space")] InsideSpace = 1 << 3,
        /// <summary>Allows all supported surface and volume targets.</summary>
        [InspectorName("Any")] All = Floor | Wall | Ceiling | InsideSpace
    }

    /// <summary>Controls how a requested object count is shared among selected placement targets.</summary>
    public enum TargetDistributionMode
    {
        /// <summary>Chooses freely from available target candidates.</summary>
        [InspectorName("Random")] Random,
        /// <summary>Aims for equal accepted counts on each selected target.</summary>
        [InspectorName("Balanced")] Balanced,
        /// <summary>Uses relative target weights to allocate accepted counts.</summary>
        [InspectorName("Weighted")] Weighted
    }

    /// <summary>Non-negative relative weights for weighted target distribution.</summary>
    [Serializable]
    public struct TargetDistributionWeights
    {
        [SerializeField] private int floor;
        [SerializeField] private int wall;
        [SerializeField] private int ceiling;
        [SerializeField] private int insideSpace;

        /// <summary>Gets floor.</summary>
        public int Floor => Mathf.Max(0, floor);
        /// <summary>Gets wall.</summary>
        public int Wall => Mathf.Max(0, wall);
        /// <summary>Gets ceiling.</summary>
        public int Ceiling => Mathf.Max(0, ceiling);
        /// <summary>Gets inside space.</summary>
        public int InsideSpace => Mathf.Max(0, insideSpace);

        /// <summary>Gets default.</summary>
        public static TargetDistributionWeights Default => new(1, 1, 1, 1);

        /// <summary>Initializes a new instance of target distribution weights.</summary>
        public TargetDistributionWeights(int floor, int wall, int ceiling, int insideSpace)
        {
            this.floor = Mathf.Max(0, floor);
            this.wall = Mathf.Max(0, wall);
            this.ceiling = Mathf.Max(0, ceiling);
            this.insideSpace = Mathf.Max(0, insideSpace);
        }

        /// <summary>Returns weight.</summary>
        public int GetWeight(PlacementTarget target)
        {
            return target switch
            {
                PlacementTarget.Floor => Floor,
                PlacementTarget.Wall => Wall,
                PlacementTarget.Ceiling => Ceiling,
                PlacementTarget.InsideSpace => InsideSpace,
                _ => 0
            };
        }
    }

    /// <summary>Selects the anchor set used by relative placement and target-facing orientation.</summary>
    public enum RelativePlacementSource
    {
        /// <summary>Disables relative-placement constraints.</summary>
        [InspectorName("None")] None,
        /// <summary>Uses objects accepted earlier in the current plan as anchors.</summary>
        [InspectorName("Generated Objects")] GeneratedObjects,
        /// <summary>Uses existing scene objects on the configured layers as anchors.</summary>
        [InspectorName("Scene Objects")] SceneObjects,
        /// <summary>Uses generated and matching existing scene objects as anchors.</summary>
        [InspectorName("Any")] Any,
        /// <summary>Uses the transforms selected when the request is created as anchors.</summary>
        [InspectorName("Selected Objects")] SelectedObjects
    }

    /// <summary>Constrains placements to a three-dimensional radius around one or more anchor sources.</summary>
    public sealed class RelativePlacementSettings
    {
        /// <summary>Gets a reusable disabled relative-placement configuration.</summary>
        public static RelativePlacementSettings Disabled { get; } = new(
            RelativePlacementSource.None,
            2f,
            ~0,
            Array.Empty<Transform>());

        /// <summary>Gets the anchor source.</summary>
        public RelativePlacementSource Source { get; }
        /// <summary>Gets the maximum world-space distance from the bounds of a matching anchor.</summary>
        public float Radius { get; }
        /// <summary>Gets the layers searched by scene-object anchoring.</summary>
        public LayerMask SceneLayers { get; }
        /// <summary>Gets the immutable selection snapshot used by selected-object anchoring.</summary>
        public IReadOnlyList<Transform> SelectedTransforms { get; }

        /// <summary>Indicates whether relative placement is enabled.</summary>
        public bool IsEnabled => Source != RelativePlacementSource.None;
        /// <summary>Indicates whether generated objects are valid anchors.</summary>
        public bool UsesGeneratedObjects => Source == RelativePlacementSource.GeneratedObjects || Source == RelativePlacementSource.Any;
        /// <summary>Indicates whether existing scene objects are valid anchors.</summary>
        public bool UsesSceneObjects => Source == RelativePlacementSource.SceneObjects || Source == RelativePlacementSource.Any;
        /// <summary>Indicates whether selected transforms are valid anchors.</summary>
        public bool UsesSelectedObjects => Source == RelativePlacementSource.SelectedObjects;

        /// <summary>Creates relative-placement settings.</summary>
        /// <param name="source">Anchor source, or <see cref="RelativePlacementSource.None"/> to disable the constraint.</param>
        /// <param name="radius">Maximum world-space distance from the bounds of a matching anchor.</param>
        /// <param name="sceneLayers">Scene layers considered by scene-object sources.</param>
        /// <param name="selectedTransforms">Snapshot of selected transforms for the selected-object source.</param>
        public RelativePlacementSettings(
            RelativePlacementSource source,
            float radius,
            LayerMask sceneLayers,
            IReadOnlyList<Transform> selectedTransforms)
        {
            Source = source;
            Radius = Mathf.Max(0.01f, radius);
            SceneLayers = sceneLayers;
            SelectedTransforms = selectedTransforms ?? Array.Empty<Transform>();
        }
    }
}
