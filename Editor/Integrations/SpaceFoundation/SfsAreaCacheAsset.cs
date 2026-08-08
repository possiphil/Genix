using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Core;
using Genix.Diagnostics;
using UnityEngine;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Serialized bounded store from stable area keys to reconstructable placement-area data.</summary>
    public sealed class SfsAreaCacheAsset : ScriptableObject
    {
        [SerializeField] private List<PersistentEntry> entries = new();

        public bool TryGet(
            string key,
            SpatialSourceInfo sourceInfo,
            AreaBuildSettings settings,
            HashSet<Vector3Int> subspace,
            float voxelSize,
            Predicate<Collider> isSourceCollider,
            out PlacementArea area)
        {
            area = null;
            PersistentEntry entry = entries.Find(item => item.Key == key);

            if (entry == null)
                return false;

            return entry.TryCreateArea(
                sourceInfo,
                settings,
                subspace,
                voxelSize,
                isSourceCollider,
                out area);
        }

        public void Store(
            string key,
            PlacementArea area,
            int maxEntries,
            int maxSurfaceCells)
        {
            entries.RemoveAll(entry => entry.Key == key || !entry.IsValid);
            entries.Insert(0, new PersistentEntry(key, area));
            Trim(maxEntries, maxSurfaceCells);
        }

        public void Clear() => entries.Clear();

        private void Trim(int maxEntries, int maxSurfaceCells)
        {
            while (entries.Count > Mathf.Max(1, maxEntries))
                entries.RemoveAt(entries.Count - 1);

            while (GetSurfaceCellCount() > maxSurfaceCells && entries.Count > 0)
                entries.RemoveAt(entries.Count - 1);
        }

        private int GetSurfaceCellCount()
        {
            int count = 0;

            foreach (PersistentEntry entry in entries)
                count += entry.SurfaceCellCount;

            return count;
        }

        [Serializable]
        private sealed class PersistentEntry
        {
            [SerializeField] private string key;
            [SerializeField] private Bounds bounds;
            [SerializeField] private List<SurfaceEntry> floors = new();
            [SerializeField] private List<SurfaceEntry> walls = new();
            [SerializeField] private List<SurfaceEntry> ceilings = new();
            [SerializeField] private List<Vector3Int> floorCells = new();
            [SerializeField] private List<Vector3Int> ceilingCells = new();

            public string Key => key;
            public bool IsValid => !string.IsNullOrWhiteSpace(key);
            public int SurfaceCellCount => (floorCells?.Count ?? 0) + (ceilingCells?.Count ?? 0);

            public PersistentEntry()
            {
            }

            public PersistentEntry(string key, PlacementArea area)
            {
                this.key = key;
                bounds = area.WorldBounds;
                floors = CreateSurfaceEntries(area.FloorRegions);
                walls = CreateSurfaceEntries(area.WallRegions);
                ceilings = CreateSurfaceEntries(area.CeilingRegions);
                floorCells = area.FloorCells != null ? new List<Vector3Int>(area.FloorCells) : new List<Vector3Int>();
                ceilingCells = area.CeilingCells != null ? new List<Vector3Int>(area.CeilingCells) : new List<Vector3Int>();
            }

            public bool TryCreateArea(
                SpatialSourceInfo sourceInfo,
                AreaBuildSettings settings,
                HashSet<Vector3Int> subspace,
                float voxelSize,
                Predicate<Collider> isSourceCollider,
                out PlacementArea area)
            {
                area = null;

                if (!IsValid || subspace == null || subspace.Count == 0)
                    return false;

                List<SurfaceRegion> floorRegions = CreateRegions(floors);
                List<SurfaceRegion> wallRegions = CreateRegions(walls);
                List<SurfaceRegion> ceilingRegions = CreateRegions(ceilings);
                VoxelCellMask subspaceMask = new(subspace);

                area = new PlacementArea(
                    sourceInfo,
                    bounds,
                    floorRegions,
                    wallRegions,
                    floorCells,
                    voxelSize,
                    settings,
                    subspace,
                    ceilingRegions,
                    ceilingCells,
                    isSourceCollider,
                    subspaceMask);
                return true;
            }

            private static List<SurfaceEntry> CreateSurfaceEntries(IReadOnlyList<SurfaceRegion> regions)
            {
                List<SurfaceEntry> result = new();

                if (regions == null)
                    return result;

                foreach (SurfaceRegion region in regions)
                    result.Add(new SurfaceEntry(region));

                return result;
            }

            private static List<SurfaceRegion> CreateRegions(IEnumerable<SurfaceEntry> entries)
            {
                List<SurfaceRegion> regions = new();

                if (entries == null)
                    return regions;

                foreach (SurfaceEntry entry in entries)
                {
                    if (entry.TryCreateRegion(out SurfaceRegion region))
                        regions.Add(region);
                }

                return regions;
            }
        }

        [Serializable]
        private sealed class SurfaceEntry
        {
            [SerializeField] private string name;
            [SerializeField] private SurfaceKind kind;
            [SerializeField] private Bounds bounds;
            [SerializeField] private Vector3 normal;
            [SerializeField] private Vector3 wallStart;
            [SerializeField] private Vector3 wallEnd;
            [SerializeField] private float surfaceY;
            [SerializeField] private bool hasVoxelLayer;
            [SerializeField] private int voxelLayer;

            public SurfaceEntry()
            {
            }

            public SurfaceEntry(SurfaceRegion region)
            {
                name = region.Name;
                kind = region.Kind;
                bounds = region.Bounds;
                normal = region.Normal;
                wallStart = region.WallStart;
                wallEnd = region.WallEnd;
                surfaceY = region.SurfaceY;
                hasVoxelLayer = region.VoxelLayer.HasValue;
                voxelLayer = region.VoxelLayer.GetValueOrDefault();
            }

            public bool TryCreateRegion(out SurfaceRegion region)
            {
                int? layer = hasVoxelLayer ? voxelLayer : null;

                region = kind switch
                {
                    SurfaceKind.Floor => SurfaceRegion.CreateFloor(
                        name,
                        bounds.min.x,
                        bounds.max.x,
                        bounds.min.z,
                        bounds.max.z,
                        surfaceY,
                        layer),
                    SurfaceKind.Ceiling => SurfaceRegion.CreateCeiling(
                        name,
                        bounds.min.x,
                        bounds.max.x,
                        bounds.min.z,
                        bounds.max.z,
                        surfaceY,
                        layer),
                    SurfaceKind.Wall => SurfaceRegion.CreateWall(
                        name,
                        wallStart,
                        wallEnd,
                        bounds.max.y,
                        normal,
                        layer),
                    _ => null
                };

                return region != null;
            }
        }
    }
}
