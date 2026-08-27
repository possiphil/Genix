using UnityEngine;

namespace Genix.Areas
{
    /// <summary>Describes a bounded floor, wall, or ceiling region available for placement.</summary>
    public sealed class SurfaceRegion
    {
        private const float Thickness = 0.02f;

        /// <summary>Gets name.</summary>
        public string Name { get; }
        /// <summary>Gets kind.</summary>
        public SurfaceKind Kind { get; }
        /// <summary>Gets bounds.</summary>
        public Bounds Bounds { get; }
        /// <summary>Gets normal.</summary>
        public Vector3 Normal { get; }
        /// <summary>Gets wall start.</summary>
        public Vector3 WallStart { get; }
        /// <summary>Gets wall end.</summary>
        public Vector3 WallEnd { get; }
        /// <summary>Gets voxel layer.</summary>
        public int? VoxelLayer { get; }
        /// <summary>Gets surface y.</summary>
        public float SurfaceY { get; }

        private SurfaceRegion(
            string name,
            SurfaceKind kind,
            Bounds bounds,
            Vector3 normal,
            Vector3 wallStart,
            Vector3 wallEnd,
            float surfaceY,
            int? voxelLayer = null)
        {
            Name = name;
            Kind = kind;
            Bounds = bounds;
            Normal = normal;
            WallStart = wallStart;
            WallEnd = wallEnd;
            SurfaceY = surfaceY;
            VoxelLayer = voxelLayer;
        }

        /// <summary>Creates floor.</summary>
        public static SurfaceRegion CreateFloor(
            string name,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float y,
            int? voxelLayer = null)
        {
            Vector3 center = new((minX + maxX) * 0.5f, y + Thickness * 0.5f, (minZ + maxZ) * 0.5f);
            Vector3 size = new(Mathf.Max(Thickness, maxX - minX), Thickness, Mathf.Max(Thickness, maxZ - minZ));

            return new SurfaceRegion(name, SurfaceKind.Floor, new Bounds(center, size), Vector3.up, default, default, y, voxelLayer);
        }

        /// <summary>Creates ceiling.</summary>
        public static SurfaceRegion CreateCeiling(
            string name,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float y,
            int? voxelLayer = null)
        {
            Vector3 center = new((minX + maxX) * 0.5f, y - Thickness * 0.5f, (minZ + maxZ) * 0.5f);
            Vector3 size = new(Mathf.Max(Thickness, maxX - minX), Thickness, Mathf.Max(Thickness, maxZ - minZ));

            return new SurfaceRegion(name, SurfaceKind.Ceiling, new Bounds(center, size), Vector3.down, default, default, y, voxelLayer);
        }

        /// <summary>Creates wall.</summary>
        public static SurfaceRegion CreateWall(
            string name,
            Vector3 start,
            Vector3 end,
            float maxY,
            Vector3 inwardNormal,
            int? voxelLayer = null)
        {
            Bounds bounds = new(start, Vector3.zero);
            bounds.Encapsulate(end);
            bounds.Encapsulate(new Vector3(start.x, maxY, start.z));
            bounds.Encapsulate(new Vector3(end.x, maxY, end.z));
            bounds.Expand(Thickness);

            return new SurfaceRegion(name, SurfaceKind.Wall, bounds, inwardNormal.normalized, start, end, start.y, voxelLayer);
        }

        /// <summary>Determines whether a world position lies inside the region footprint.</summary>
        public bool ContainsXZ(Vector3 position, float padding = 0.001f)
        {
            return position.x >= Bounds.min.x - padding &&
                   position.x <= Bounds.max.x + padding &&
                   position.z >= Bounds.min.z - padding &&
                   position.z <= Bounds.max.z + padding;
        }

        /// <summary>Determines whether the bounds footprint lies inside the region.</summary>
        public bool ContainsBoundsXZ(Bounds bounds)
        {
            return ContainsXZ(new Vector3(bounds.min.x, SurfaceY, bounds.min.z)) &&
                   ContainsXZ(new Vector3(bounds.min.x, SurfaceY, bounds.max.z)) &&
                   ContainsXZ(new Vector3(bounds.max.x, SurfaceY, bounds.min.z)) &&
                   ContainsXZ(new Vector3(bounds.max.x, SurfaceY, bounds.max.z)) &&
                   ContainsXZ(new Vector3(bounds.center.x, SurfaceY, bounds.center.z));
        }
    }
}
