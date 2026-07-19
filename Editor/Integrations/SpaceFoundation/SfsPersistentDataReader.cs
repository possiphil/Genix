using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsChunkData = SpaceFoundationSystem.SpaceFoundationChunkData;
using SfsData = SpaceFoundationSystem.SpaceFoundationData;
using SfsFoundation = SpaceFoundationSystem.SpaceFoundation;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    internal static class SfsPersistentDataReader
    {
        public static bool TryRead(
            SfsSpace space,
            SfsAnchor anchor,
            out PersistentSubspaceData result)
        {
            result = default;

            if (!anchor)
                return false;

            string anchorId = anchor.GetUniqueId();
            SfsFoundation foundation = SfsFoundationUtility.Find(space, anchor);

            if (string.IsNullOrWhiteSpace(anchorId) || !foundation ||
                !TryReadBorderOwners(foundation, out Dictionary<Vector3Int, string> borderOwners))
            {
                return false;
            }

            List<Vector3Int> anchorBorders = GetAnchorBorders(borderOwners, anchorId);

            if (anchorBorders.Count == 0)
                return false;

            VoxelBounds bounds = VoxelBounds.From(anchorBorders);
            Vector3Int seed = ResolveSeed(anchor, foundation, anchorId, borderOwners, anchorBorders, bounds);
            bounds = bounds.Encapsulate(seed).Expand(1);
            result = new PersistentSubspaceData(
                foundation,
                anchorId,
                borderOwners,
                anchorBorders,
                seed,
                bounds);
            return true;
        }

        private static bool TryReadBorderOwners(
            SfsFoundation foundation,
            out Dictionary<Vector3Int, string> borderOwners)
        {
            if (foundation.data && foundation.data.borders != null)
            {
                borderOwners = foundation.data.borders.ToDictionary();

                if (borderOwners.Count > 0)
                    return true;
            }

            return TryReadChunkedBorders(foundation, out borderOwners);
        }

        private static bool TryReadChunkedBorders(
            SfsFoundation foundation,
            out Dictionary<Vector3Int, string> borderOwners)
        {
            borderOwners = new Dictionary<Vector3Int, string>();
            string folder = $"Assets/SpaceFoundationData/{foundation.assetName}_chunked";

            if (!AssetDatabase.IsValidFolder(folder))
                return false;

            string[] chunkGuids = AssetDatabase.FindAssets("t:SpaceFoundationChunkData", new[] { folder });

            foreach (string guid in chunkGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!TryParseChunkPosition(Path.GetFileNameWithoutExtension(path), out Vector3Int chunkPosition))
                    continue;

                SfsChunkData chunk = AssetDatabase.LoadAssetAtPath<SfsChunkData>(path);

                if (!chunk || chunk.borders == null)
                    continue;

                foreach (KeyValuePair<int, string> entry in chunk.borders.ToDictionary())
                {
                    Vector3Int local = ReverseChunkIndex(entry.Key, foundation.chunkSize);
                    Vector3Int offset = new(
                        chunkPosition.x * foundation.chunkSize,
                        chunkPosition.y * foundation.chunkSize,
                        chunkPosition.z * foundation.chunkSize);
                    borderOwners[local + offset] = entry.Value;
                }
            }

            return borderOwners.Count > 0;
        }

        private static Vector3Int ResolveSeed(
            SfsAnchor anchor,
            SfsFoundation foundation,
            string anchorId,
            Dictionary<Vector3Int, string> borderOwners,
            IReadOnlyList<Vector3Int> anchorBorders,
            VoxelBounds bounds)
        {
            Vector3Int seed = foundation.DetermineVoxelGridPosition(anchor.transform.position);

            if (VoxelFloodFill.IsValidSeed(seed, anchorId, borderOwners, bounds))
                return seed;

            if (TryReadSerializedAnchorPosition(foundation, anchorId, out seed) &&
                VoxelFloodFill.IsValidSeed(seed, anchorId, borderOwners, bounds))
            {
                return seed;
            }

            seed = Average(anchorBorders);

            if (VoxelFloodFill.IsValidSeed(seed, anchorId, borderOwners, bounds))
                return seed;

            return VoxelFloodFill.TryFindInteriorSeed(
                anchorBorders,
                anchorId,
                borderOwners,
                bounds,
                out seed)
                ? seed
                : anchorBorders[0];
        }

        private static bool TryReadSerializedAnchorPosition(
            SfsFoundation foundation,
            string anchorId,
            out Vector3Int position)
        {
            return TryReadSerializedAnchorPosition(foundation.data, anchorId, out position) ||
                   TryReadSerializedAnchorPosition(foundation.chunkMainData, anchorId, out position);
        }

        private static bool TryReadSerializedAnchorPosition(
            SfsData data,
            string anchorId,
            out Vector3Int position)
        {
            position = default;

            if (!data || data.anchorToVoxelPositionDict == null)
                return false;

            return data.anchorToVoxelPositionDict.ToDictionary().TryGetValue(anchorId, out position);
        }

        private static List<Vector3Int> GetAnchorBorders(
            Dictionary<Vector3Int, string> borderOwners,
            string anchorId)
        {
            List<Vector3Int> result = new();

            foreach (KeyValuePair<Vector3Int, string> entry in borderOwners)
            {
                if (entry.Value == anchorId)
                    result.Add(entry.Key);
            }

            return result;
        }

        private static Vector3Int Average(IReadOnlyList<Vector3Int> cells)
        {
            Vector3 sum = Vector3.zero;

            foreach (Vector3Int cell in cells)
                sum += (Vector3)cell;

            return Vector3Int.RoundToInt(sum / Mathf.Max(1, cells.Count));
        }

        private static bool TryParseChunkPosition(string value, out Vector3Int position)
        {
            position = default;
            string[] parts = value?.Split('_');

            if (parts == null || parts.Length != 3 ||
                !int.TryParse(parts[0], out int x) ||
                !int.TryParse(parts[1], out int y) ||
                !int.TryParse(parts[2], out int z))
            {
                return false;
            }

            position = new Vector3Int(x, y, z);
            return true;
        }

        private static Vector3Int ReverseChunkIndex(int index, int chunkSize)
        {
            return new Vector3Int(
                index % chunkSize,
                index % (chunkSize * chunkSize) / chunkSize,
                index / (chunkSize * chunkSize));
        }
    }

    internal readonly struct PersistentSubspaceData
    {
        public SfsFoundation Foundation { get; }
        public string AnchorId { get; }
        public Dictionary<Vector3Int, string> BorderOwners { get; }
        public IReadOnlyList<Vector3Int> AnchorBorders { get; }
        public Vector3Int Seed { get; }
        public VoxelBounds Bounds { get; }

        public PersistentSubspaceData(
            SfsFoundation foundation,
            string anchorId,
            Dictionary<Vector3Int, string> borderOwners,
            IReadOnlyList<Vector3Int> anchorBorders,
            Vector3Int seed,
            VoxelBounds bounds)
        {
            Foundation = foundation;
            AnchorId = anchorId;
            BorderOwners = borderOwners;
            AnchorBorders = anchorBorders;
            Seed = seed;
            Bounds = bounds;
        }
    }
}
