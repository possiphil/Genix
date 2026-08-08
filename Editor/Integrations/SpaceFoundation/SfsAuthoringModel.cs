using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Genix.SpaceFoundation.Editor
{
    internal enum SfsAuthoringLayoutType
    {
        BoundedLocation,
        LocationGrid,
        FootprintLocation
    }

    internal enum SfsAuthoringSizeMode
    {
        WorldUnits,
        VoxelCounts,
        FitSelection
    }

    internal enum SfsAuthoringCenterMode
    {
        Manual,
        SceneViewPivot,
        SelectionBounds
    }

    internal enum SfsFootprintTemplate
    {
        Rectangle,
        LShape,
        UShape,
        TShape,
        Courtyard,
        Custom
    }

    internal sealed class SfsAuthoringRequest
    {
        public string Name = "Location";
        public SfsAuthoringLayoutType LayoutType = SfsAuthoringLayoutType.BoundedLocation;
        public SfsAuthoringSizeMode SizeMode = SfsAuthoringSizeMode.WorldUnits;
        public Vector3 Center = Vector3.zero;
        public Vector3 WorldSize = new(10f, 4f, 10f);
        public Vector3Int VoxelCounts = new(10, 4, 10);
        public Vector3Int GridCounts = Vector3Int.one;
        public Vector3Int UniformRoomCells = new(10, 4, 10);
        public Vector3Int SeparatorCells = Vector3Int.one;
        public bool UsePerAxisRoomSizes;
        public readonly List<int> XRoomCells = new() { 10 };
        public readonly List<int> YRoomCells = new() { 4 };
        public readonly List<int> ZRoomCells = new() { 10 };
        public SfsFootprintTemplate FootprintTemplate = SfsFootprintTemplate.Rectangle;
        public Vector2Int FootprintDimensions = new(4, 4);
        public Vector2Int FootprintTileCells = new(4, 4);
        public int FootprintHeightCells = 4;
        public readonly HashSet<Vector2Int> CustomFootprint = new();
        public bool AutomaticAnchorRange = true;
        public float AnchorRangeOverride = 40f;
    }

    internal readonly struct SfsAuthoringCellVolume
    {
        public SfsAuthoringCellVolume(string name, Vector3Int min, Vector3Int size)
        {
            Name = name;
            Min = min;
            Size = size;
        }

        public string Name { get; }
        public Vector3Int Min { get; }
        public Vector3Int Size { get; }

        public Bounds ToWorldBounds(float voxelSize)
        {
            Vector3 center = (Vector3)Min * voxelSize + (Vector3)(Size - Vector3Int.one) * (voxelSize * 0.5f);
            return new Bounds(center, (Vector3)Size * voxelSize);
        }
    }

    internal readonly struct SfsAuthoringAnchorPlan
    {
        public SfsAuthoringAnchorPlan(string name, Vector3Int cell, float range)
        {
            Name = name;
            Cell = cell;
            Range = range;
        }

        public string Name { get; }
        public Vector3Int Cell { get; }
        public float Range { get; }

        public Vector3 ToWorldPosition(float voxelSize) => (Vector3)Cell * voxelSize;
    }

    internal sealed class SfsAuthoringPlan
    {
        public string Name { get; set; }
        public SfsAuthoringLayoutType LayoutType { get; set; }
        public float VoxelSize { get; set; }
        public Vector3 RequestedCenter { get; set; }
        public Vector3 ActualCenter { get; set; }
        public Vector3 RequestedSize { get; set; }
        public Vector3 ActualSize { get; set; }
        public int LocationCount { get; set; }
        public int SeparatorCellCount { get; set; }
        public readonly List<SfsAuthoringCellVolume> Delimiters = new();
        public readonly List<SfsAuthoringCellVolume> InteriorVolumes = new();
        public readonly List<SfsAuthoringAnchorPlan> Anchors = new();
    }

    internal readonly struct SfsAuthoringValidationMessage
    {
        public SfsAuthoringValidationMessage(string text, MessageType type)
        {
            Text = text;
            Type = type;
        }

        public string Text { get; }
        public MessageType Type { get; }
    }
}
