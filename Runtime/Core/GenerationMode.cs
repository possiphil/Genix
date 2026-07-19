using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genix.Core
{
    public enum GenerationMode
    {
        [InspectorName("Target Placement")] TargetPlacement
    }

    [Flags]
    public enum PlacementTarget
    {
        [InspectorName("None")] None = 0,
        [InspectorName("Floor")] Floor = 1 << 0,
        [InspectorName("Wall")] Wall = 1 << 1,
        [InspectorName("Ceiling")] Ceiling = 1 << 2,
        [InspectorName("Inside Space")] InsideSpace = 1 << 3,
        [InspectorName("Any")] All = Floor | Wall | Ceiling | InsideSpace
    }

    public enum TargetDistributionMode
    {
        [InspectorName("Random")] Random,
        [InspectorName("Balanced")] Balanced,
        [InspectorName("Weighted")] Weighted
    }

    [Serializable]
    public struct TargetDistributionWeights
    {
        [SerializeField] private int floor;
        [SerializeField] private int wall;
        [SerializeField] private int ceiling;
        [SerializeField] private int insideSpace;

        public int Floor => Mathf.Max(0, floor);
        public int Wall => Mathf.Max(0, wall);
        public int Ceiling => Mathf.Max(0, ceiling);
        public int InsideSpace => Mathf.Max(0, insideSpace);

        public static TargetDistributionWeights Default => new(1, 1, 1, 1);

        public TargetDistributionWeights(int floor, int wall, int ceiling, int insideSpace)
        {
            this.floor = Mathf.Max(0, floor);
            this.wall = Mathf.Max(0, wall);
            this.ceiling = Mathf.Max(0, ceiling);
            this.insideSpace = Mathf.Max(0, insideSpace);
        }

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

    public enum RelativePlacementSource
    {
        [InspectorName("None")] None,
        [InspectorName("Generated Objects")] GeneratedObjects,
        [InspectorName("Scene Objects")] SceneObjects,
        [InspectorName("Any")] Any,
        [InspectorName("Selected Objects")] SelectedObjects
    }

    public sealed class RelativePlacementSettings
    {
        public static RelativePlacementSettings Disabled { get; } = new(
            RelativePlacementSource.None,
            2f,
            ~0,
            Array.Empty<Transform>());

        public RelativePlacementSource Source { get; }
        public float Radius { get; }
        public LayerMask SceneLayers { get; }
        public IReadOnlyList<Transform> SelectedTransforms { get; }

        public bool IsEnabled => Source != RelativePlacementSource.None;
        public bool UsesGeneratedObjects => Source == RelativePlacementSource.GeneratedObjects || Source == RelativePlacementSource.Any;
        public bool UsesSceneObjects => Source == RelativePlacementSource.SceneObjects || Source == RelativePlacementSource.Any;
        public bool UsesSelectedObjects => Source == RelativePlacementSource.SelectedObjects;

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
