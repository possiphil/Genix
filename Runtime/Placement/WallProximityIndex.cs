using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using UnityEngine;

namespace Genix.Placement
{
    /// <summary>Provides opt-in horizontal distance checks against detected scene and terrain walls.</summary>
    internal sealed class WallProximityIndex
    {
        private const int MaximumTerrainSamplesPerAxis = 512;
        private static readonly ConditionalWeakTable<PlacementArea, WallProximityIndex> AreaCache = new();

        private readonly List<WallReference> _references;
        private readonly List<TerrainWallGrid> _terrainGrids;

        public bool HasReferences => _references.Count > 0 || _terrainGrids.Count > 0;

        private WallProximityIndex(
            List<WallReference> references,
            List<TerrainWallGrid> terrainGrids)
        {
            _references = references;
            _terrainGrids = terrainGrids;
        }

        public static WallProximityIndex Create(PlacementArea area)
        {
            return area == null
                ? new WallProximityIndex(new List<WallReference>(), new List<TerrainWallGrid>())
                : AreaCache.GetValue(area, Build);
        }

        private static WallProximityIndex Build(PlacementArea area)
        {
            List<WallReference> references = new();
            List<TerrainWallGrid> terrainGrids = new();

            foreach (SurfaceRegion region in area.WallRegions)
            {
                if (region == null || region.Kind != SurfaceKind.Wall)
                    continue;

                references.Add(WallReference.FromSegment(region.Name, region.WallStart, region.WallEnd));
            }

            bool hasRegionReferences = references.Count > 0;

            HashSet<Collider> seen = new();

            foreach (WallSurfaceSource source in area.WallSurfaceSources)
            {
                if (!source.Collider || !seen.Add(source.Collider))
                    continue;

                if (source.IsTerrain && source.Collider is TerrainCollider terrainCollider)
                {
                    TerrainWallGrid terrainGrid = TerrainWallGrid.Create(
                        terrainCollider,
                        area.WorldBounds,
                        area.Settings,
                        MaximumTerrainSamplesPerAxis);
                    if (terrainGrid != null && terrainGrid.HasWalls)
                        terrainGrids.Add(terrainGrid);
                }
                else if (!hasRegionReferences)
                {
                    references.Add(WallReference.FromBounds(source.Collider.name, source.Bounds));
                }
            }

            return new WallProximityIndex(references, terrainGrids);
        }

        public bool TryGetNearestGap(
            OrientedBounds bounds,
            float relevantDistance,
            out float gap,
            out string wallName)
        {
            gap = float.PositiveInfinity;
            wallName = string.Empty;

            for (int i = 0; i < _references.Count; i++)
            {
                WallReference reference = _references[i];
                Vector2 center = new(bounds.Center.x, bounds.Center.z);
                Vector2 nearest = reference.GetNearestPoint(center);
                Vector2 direction = nearest - center;
                float centerDistance = direction.magnitude;
                float candidateRadius = centerDistance > 0.0001f
                    ? GetHorizontalRadius(bounds, direction / centerDistance)
                    : 0f;
                float candidateGap = Mathf.Max(0f, centerDistance - candidateRadius);

                if (candidateGap >= gap)
                    continue;

                gap = candidateGap;
                wallName = reference.Name;
            }

            float candidateSearchRadius = Mathf.Max(0f, relevantDistance) +
                                          GetMaximumHorizontalRadius(bounds);
            foreach (TerrainWallGrid terrainGrid in _terrainGrids)
            {
                if (!terrainGrid.TryGetNearestPoint(
                        new Vector2(bounds.Center.x, bounds.Center.z),
                        candidateSearchRadius,
                        out Vector2 nearest,
                        out float coverageRadius))
                {
                    continue;
                }

                Vector2 direction = nearest - new Vector2(bounds.Center.x, bounds.Center.z);
                float centerDistance = direction.magnitude;
                float candidateRadius = centerDistance > 0.0001f
                    ? GetHorizontalRadius(bounds, direction / centerDistance)
                    : 0f;
                float candidateGap = Mathf.Max(0f, centerDistance - candidateRadius - coverageRadius);
                if (candidateGap >= gap)
                    continue;

                gap = candidateGap;
                wallName = terrainGrid.Name;
            }

            return HasReferences;
        }

        private static float GetMaximumHorizontalRadius(OrientedBounds bounds)
        {
            Vector3 extents = bounds.Extents;
            float x = Mathf.Abs(bounds.Right.x) * extents.x +
                      Mathf.Abs(bounds.Up.x) * extents.y +
                      Mathf.Abs(bounds.Forward.x) * extents.z;
            float z = Mathf.Abs(bounds.Right.z) * extents.x +
                      Mathf.Abs(bounds.Up.z) * extents.y +
                      Mathf.Abs(bounds.Forward.z) * extents.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        private static float GetHorizontalRadius(OrientedBounds bounds, Vector2 direction)
        {
            Vector3 worldDirection = new(direction.x, 0f, direction.y);
            Vector3 extents = bounds.Extents;
            return Mathf.Abs(Vector3.Dot(bounds.Right, worldDirection)) * extents.x +
                   Mathf.Abs(Vector3.Dot(bounds.Up, worldDirection)) * extents.y +
                   Mathf.Abs(Vector3.Dot(bounds.Forward, worldDirection)) * extents.z;
        }

        private readonly struct WallReference
        {
            private readonly Vector2 _start;
            private readonly Vector2 _end;
            private readonly Rect _rect;
            private readonly bool _usesBounds;

            public string Name { get; }

            private WallReference(
                string name,
                Vector2 start,
                Vector2 end,
                Rect rect,
                bool usesBounds)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Wall" : name;
                _start = start;
                _end = end;
                _rect = rect;
                _usesBounds = usesBounds;
            }

            public static WallReference FromSegment(string name, Vector3 start, Vector3 end) =>
                new(name, new Vector2(start.x, start.z), new Vector2(end.x, end.z), default, false);

            public static WallReference FromBounds(string name, Bounds bounds) =>
                new(
                    name,
                    default,
                    default,
                    Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z),
                    true);

            public Vector2 GetNearestPoint(Vector2 point)
            {
                if (_usesBounds)
                {
                    return new Vector2(
                        Mathf.Clamp(point.x, _rect.xMin, _rect.xMax),
                        Mathf.Clamp(point.y, _rect.yMin, _rect.yMax));
                }

                Vector2 segment = _end - _start;
                float lengthSquared = segment.sqrMagnitude;

                if (lengthSquared <= 0.0001f)
                    return _start;

                float t = Mathf.Clamp01(Vector2.Dot(point - _start, segment) / lengthSquared);
                return _start + segment * t;
            }
        }

        private sealed class TerrainWallGrid
        {
            private readonly bool[] _walls;
            private readonly int _countX;
            private readonly int _countZ;
            private readonly float _minimumX;
            private readonly float _minimumZ;
            private readonly float _stepX;
            private readonly float _stepZ;
            private readonly Matrix4x4 _localToWorld;
            private readonly Matrix4x4 _worldToLocal;
            private readonly float _minimumHorizontalScale;

            public string Name { get; }
            public bool HasWalls { get; }
            public float CoverageRadius { get; }

            private TerrainWallGrid(
                string name,
                bool[] walls,
                int countX,
                int countZ,
                float minimumX,
                float minimumZ,
                float stepX,
                float stepZ,
                Matrix4x4 localToWorld,
                Matrix4x4 worldToLocal,
                float minimumHorizontalScale,
                bool hasWalls)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Terrain Wall" : name;
                _walls = walls;
                _countX = countX;
                _countZ = countZ;
                _minimumX = minimumX;
                _minimumZ = minimumZ;
                _stepX = stepX;
                _stepZ = stepZ;
                _localToWorld = localToWorld;
                _worldToLocal = worldToLocal;
                _minimumHorizontalScale = Mathf.Max(0.0001f, minimumHorizontalScale);
                HasWalls = hasWalls;

                Vector3 worldX = localToWorld.MultiplyVector(new Vector3(stepX, 0f, 0f));
                Vector3 worldZ = localToWorld.MultiplyVector(new Vector3(0f, 0f, stepZ));
                CoverageRadius = 0.5f * Mathf.Sqrt(
                    new Vector2(worldX.x, worldX.z).sqrMagnitude +
                    new Vector2(worldZ.x, worldZ.z).sqrMagnitude);
            }

            public static TerrainWallGrid Create(
                TerrainCollider collider,
                Bounds areaBounds,
                AreaBuildSettings settings,
                int maximumSamplesPerAxis)
            {
                TerrainData data = collider ? collider.terrainData : null;
                if (!data || data.size.x <= 0f || data.size.z <= 0f)
                    return null;

                Transform transform = collider.transform;
                Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
                Matrix4x4 localToWorld = transform.localToWorldMatrix;
                Vector3[] corners =
                {
                    new(areaBounds.min.x, areaBounds.center.y, areaBounds.min.z),
                    new(areaBounds.min.x, areaBounds.center.y, areaBounds.max.z),
                    new(areaBounds.max.x, areaBounds.center.y, areaBounds.min.z),
                    new(areaBounds.max.x, areaBounds.center.y, areaBounds.max.z)
                };

                float minX = float.PositiveInfinity;
                float minZ = float.PositiveInfinity;
                float maxX = float.NegativeInfinity;
                float maxZ = float.NegativeInfinity;
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = worldToLocal.MultiplyPoint3x4(corner);
                    minX = Mathf.Min(minX, local.x);
                    minZ = Mathf.Min(minZ, local.z);
                    maxX = Mathf.Max(maxX, local.x);
                    maxZ = Mathf.Max(maxZ, local.z);
                }

                minX = Mathf.Clamp(minX, 0f, data.size.x);
                maxX = Mathf.Clamp(maxX, 0f, data.size.x);
                minZ = Mathf.Clamp(minZ, 0f, data.size.z);
                maxZ = Mathf.Clamp(maxZ, 0f, data.size.z);
                if (maxX <= minX || maxZ <= minZ)
                    return null;

                int nativeIntervals = Mathf.Max(1, data.heightmapResolution - 1);
                float nativeStepX = data.size.x / nativeIntervals;
                float nativeStepZ = data.size.z / nativeIntervals;
                int countX = Mathf.Clamp(
                    Mathf.CeilToInt((maxX - minX) / nativeStepX) + 1,
                    2,
                    maximumSamplesPerAxis);
                int countZ = Mathf.Clamp(
                    Mathf.CeilToInt((maxZ - minZ) / nativeStepZ) + 1,
                    2,
                    maximumSamplesPerAxis);
                float stepX = (maxX - minX) / (countX - 1);
                float stepZ = (maxZ - minZ) / (countZ - 1);
                bool[] walls = new bool[countX * countZ];
                bool hasWalls = false;

                for (int z = 0; z < countZ; z++)
                {
                    float localZ = minZ + z * stepZ;
                    float normalizedZ = Mathf.Clamp01(localZ / data.size.z);
                    for (int x = 0; x < countX; x++)
                    {
                        float localX = minX + x * stepX;
                        float normalizedX = Mathf.Clamp01(localX / data.size.x);
                        float localY = data.GetInterpolatedHeight(normalizedX, normalizedZ);
                        Vector3 worldPosition = localToWorld.MultiplyPoint3x4(
                            new Vector3(localX, localY, localZ));
                        if (worldPosition.y < areaBounds.min.y || worldPosition.y > areaBounds.max.y)
                            continue;

                        Vector3 localNormal = data.GetInterpolatedNormal(normalizedX, normalizedZ);
                        Vector3 worldNormal = localToWorld.MultiplyVector(localNormal).normalized;
                        if (SurfaceClassifier.Classify(worldNormal, settings) != PlacementType.Wall)
                            continue;

                        walls[z * countX + x] = true;
                        hasWalls = true;
                    }
                }

                Vector3 scale = transform.lossyScale;
                float minimumScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                return new TerrainWallGrid(
                    collider.name,
                    walls,
                    countX,
                    countZ,
                    minX,
                    minZ,
                    stepX,
                    stepZ,
                    localToWorld,
                    worldToLocal,
                    minimumScale,
                    hasWalls);
            }

            public bool TryGetNearestPoint(
                Vector2 worldCenter,
                float worldSearchRadius,
                out Vector2 nearest,
                out float coverageRadius)
            {
                nearest = default;
                coverageRadius = CoverageRadius;
                if (!HasWalls)
                    return false;

                Vector3 localCenter = _worldToLocal.MultiplyPoint3x4(
                    new Vector3(worldCenter.x, 0f, worldCenter.y));
                float localRadius = (Mathf.Max(0f, worldSearchRadius) + CoverageRadius) /
                                    _minimumHorizontalScale;
                int minX = Mathf.Clamp(
                    Mathf.FloorToInt((localCenter.x - localRadius - _minimumX) / _stepX),
                    0,
                    _countX - 1);
                int maxX = Mathf.Clamp(
                    Mathf.CeilToInt((localCenter.x + localRadius - _minimumX) / _stepX),
                    0,
                    _countX - 1);
                int minZ = Mathf.Clamp(
                    Mathf.FloorToInt((localCenter.z - localRadius - _minimumZ) / _stepZ),
                    0,
                    _countZ - 1);
                int maxZ = Mathf.Clamp(
                    Mathf.CeilToInt((localCenter.z + localRadius - _minimumZ) / _stepZ),
                    0,
                    _countZ - 1);

                float nearestSquared = float.PositiveInfinity;
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (!_walls[z * _countX + x])
                            continue;

                        Vector3 world = _localToWorld.MultiplyPoint3x4(new Vector3(
                            _minimumX + x * _stepX,
                            0f,
                            _minimumZ + z * _stepZ));
                        Vector2 point = new(world.x, world.z);
                        float squared = (point - worldCenter).sqrMagnitude;
                        if (squared >= nearestSquared)
                            continue;

                        nearestSquared = squared;
                        nearest = point;
                    }
                }

                return !float.IsPositiveInfinity(nearestSquared);
            }
        }
    }

    /// <summary>Evaluates optional asset-to-wall distance constraints.</summary>
    internal static class WallProximityRules
    {
        public static bool TryValidate(
            AssetDefinition asset,
            OrientedBounds bounds,
            GenerationContext context,
            out RejectionReason rejectionReason,
            out string relatedObjectName)
        {
            rejectionReason = RejectionReason.None;
            relatedObjectName = string.Empty;

            if (!asset || asset.WallProximityMode == WallProximityMode.AnyDistance)
                return true;

            WallProximityIndex index = context.WallProximity;

            if (!index.HasReferences)
            {
                rejectionReason = RejectionReason.MissingWallReference;
                return false;
            }

            index.TryGetNearestGap(bounds, asset.WallDistance, out float gap, out relatedObjectName);

            if (asset.WallProximityMode == WallProximityMode.NearWall && gap > asset.WallDistance)
            {
                rejectionReason = RejectionReason.TooFarFromWall;
                return false;
            }

            if (asset.WallProximityMode == WallProximityMode.AwayFromWall && gap < asset.WallDistance)
            {
                rejectionReason = RejectionReason.TooCloseToWall;
                return false;
            }

            return true;
        }
    }
}
