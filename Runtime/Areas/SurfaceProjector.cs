using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Layouts;
using UnityEngine;

namespace Genix.Areas
{
    internal sealed class SurfaceProjector
    {
        private readonly Bounds _worldBounds;
        private readonly IReadOnlyList<SurfaceRegion> _floorRegions;
        private readonly IReadOnlyList<SurfaceRegion> _ceilingRegions;
        private readonly VoxelOccupancy _occupancy;
        private readonly AreaBuildSettings _settings;
        private readonly Predicate<Collider> _isSourceCollider;

        public SurfaceProjector(
            Bounds worldBounds,
            IReadOnlyList<SurfaceRegion> floorRegions,
            IReadOnlyList<SurfaceRegion> ceilingRegions,
            VoxelOccupancy occupancy,
            AreaBuildSettings settings,
            Predicate<Collider> isSourceCollider)
        {
            _worldBounds = worldBounds;
            _floorRegions = floorRegions;
            _ceilingRegions = ceilingRegions;
            _occupancy = occupancy;
            _settings = settings;
            _isSourceCollider = isSourceCollider;
        }

        public bool TryProjectToFloor(Vector3 position, SurfaceRegion targetRegion, out SurfacePoint point)
        {
            if (targetRegion != null && !targetRegion.ContainsXZ(position))
            {
                point = default;
                return false;
            }

            if (_settings.usePlacementSurfaceCheck)
            {
                return TryFindFloor(
                    position,
                    null,
                    targetRegion,
                    targetRegion?.VoxelLayer,
                    out point);
            }

            return TryProjectToRegion(position, targetRegion, _floorRegions, Vector3.up, out point);
        }

        public bool TryProjectToCeiling(Vector3 position, SurfaceRegion targetRegion, out SurfacePoint point)
        {
            if (targetRegion != null && !targetRegion.ContainsXZ(position))
            {
                point = default;
                return false;
            }

            if (_settings.usePlacementSurfaceCheck)
            {
                return TryFindCeiling(
                    position,
                    null,
                    targetRegion,
                    targetRegion?.VoxelLayer,
                    out point);
            }

            return TryProjectToRegion(position, targetRegion, _ceilingRegions, Vector3.down, out point);
        }

        public bool TryProjectToWall(
            Vector3 position,
            Vector3 inwardNormal,
            int? targetVoxelLayer,
            out SurfacePoint point)
        {
            inwardNormal = inwardNormal.sqrMagnitude > 0.001f
                ? inwardNormal.normalized
                : Vector3.forward;

            if (!_settings.usePlacementSurfaceCheck)
            {
                point = new SurfacePoint(position, inwardNormal, null, targetVoxelLayer);
                return true;
            }

            return TryFindWall(position, inwardNormal, targetVoxelLayer, out point);
        }

        public bool HasFloorSurfaceAt(Vector3 position) =>
            TryFindFloor(position, null, null, null, out _);

        public bool HasSurfaceAt(
            Vector3 position,
            PlacementType placementType,
            int? voxelLayer,
            Collider expectedSurfaceCollider)
        {
            return placementType == PlacementType.Ceiling
                ? TryFindCeiling(position, expectedSurfaceCollider, null, voxelLayer, out _)
                : TryFindFloor(position, expectedSurfaceCollider, null, voxelLayer, out _);
        }

        private static bool TryProjectToRegion(
            Vector3 position,
            SurfaceRegion targetRegion,
            IReadOnlyList<SurfaceRegion> regions,
            Vector3 normal,
            out SurfacePoint point)
        {
            if (targetRegion != null)
            {
                point = new SurfacePoint(
                    new Vector3(position.x, targetRegion.SurfaceY, position.z),
                    normal,
                    null,
                    targetRegion.VoxelLayer);
                return true;
            }

            foreach (SurfaceRegion region in regions)
            {
                if (!region.ContainsXZ(position))
                    continue;

                point = new SurfacePoint(
                    new Vector3(position.x, region.SurfaceY, position.z),
                    normal,
                    null,
                    region.VoxelLayer);
                return true;
            }

            point = default;
            return false;
        }

        private bool TryFindFloor(
            Vector3 position,
            Collider expectedCollider,
            SurfaceRegion targetRegion,
            int? targetVoxelLayer,
            out SurfacePoint point)
        {
            point = default;

            if (_settings.placementSurfaceLayers.value == 0 ||
                !HasMatchingRegion(position, targetRegion, targetVoxelLayer, _floorRegions))
            {
                return false;
            }

            float originY = _worldBounds.max.y + Mathf.Max(0.01f, _settings.surfaceRaycastHeight);
            Ray ray = new(new Vector3(position.x, originY, position.z), Vector3.down);

            foreach (RaycastHit hit in GetSortedHits(ray, _settings.surfaceRaycastDistance))
            {
                if (!IsUsableHit(hit, expectedCollider) ||
                    SurfaceClassifier.Classify(hit.normal, _settings) != PlacementType.Floor ||
                    hit.point.y < _worldBounds.min.y - 0.1f ||
                    hit.point.y > _worldBounds.max.y + _settings.surfaceRaycastHeight ||
                    !TryGetMatchingRegion(
                        hit.point,
                        targetRegion,
                        targetVoxelLayer,
                        _floorRegions,
                        out SurfaceRegion matchedRegion))
                {
                    continue;
                }

                point = new SurfacePoint(hit.point, hit.normal, hit.collider, matchedRegion?.VoxelLayer);
                return true;
            }

            return false;
        }

        private bool TryFindCeiling(
            Vector3 position,
            Collider expectedCollider,
            SurfaceRegion targetRegion,
            int? targetVoxelLayer,
            out SurfacePoint point)
        {
            point = default;

            if (_settings.placementSurfaceLayers.value == 0 ||
                !HasMatchingRegion(position, targetRegion, targetVoxelLayer, _ceilingRegions))
            {
                return false;
            }

            float originY = _worldBounds.min.y - Mathf.Max(0.01f, _settings.surfaceRaycastHeight);
            Ray ray = new(new Vector3(position.x, originY, position.z), Vector3.up);

            foreach (RaycastHit hit in GetSortedHits(ray, _settings.surfaceRaycastDistance))
            {
                if (!IsUsableHit(hit, expectedCollider) ||
                    SurfaceClassifier.Classify(hit.normal, _settings) != PlacementType.Ceiling ||
                    hit.point.y < _worldBounds.min.y - _settings.surfaceRaycastHeight ||
                    hit.point.y > _worldBounds.max.y + 0.1f ||
                    !TryGetMatchingRegion(
                        hit.point,
                        targetRegion,
                        targetVoxelLayer,
                        _ceilingRegions,
                        out SurfaceRegion matchedRegion))
                {
                    continue;
                }

                point = new SurfacePoint(hit.point, hit.normal, hit.collider, matchedRegion?.VoxelLayer);
                return true;
            }

            return false;
        }

        private bool TryFindWall(
            Vector3 position,
            Vector3 inwardNormal,
            int? targetVoxelLayer,
            out SurfacePoint point)
        {
            float offset = _occupancy.CellSize > 0f
                ? Mathf.Max(0.05f, _occupancy.CellSize * 0.5f)
                : 0.5f;
            float configuredDistance = Mathf.Max(0.01f, _settings.surfaceRaycastDistance);
            float desiredDistance = _occupancy.CellSize > 0f
                ? Mathf.Max(1f, _occupancy.CellSize * 4f)
                : Mathf.Min(configuredDistance, 5f);
            float distance = Mathf.Min(configuredDistance, desiredDistance);

            return TryFindWallAlongRay(
                       position + inwardNormal * offset,
                       -inwardNormal,
                       distance,
                       inwardNormal,
                       targetVoxelLayer,
                       out point) ||
                   TryFindWallAlongRay(
                       position - inwardNormal * offset,
                       inwardNormal,
                       distance,
                       inwardNormal,
                       targetVoxelLayer,
                       out point);
        }

        private bool TryFindWallAlongRay(
            Vector3 origin,
            Vector3 direction,
            float distance,
            Vector3 inwardNormal,
            int? targetVoxelLayer,
            out SurfacePoint point)
        {
            point = default;

            foreach (RaycastHit hit in GetSortedHits(new Ray(origin, direction), distance))
            {
                if (!IsUsableHit(hit, null) ||
                    SurfaceClassifier.Classify(hit.normal, _settings) != PlacementType.Wall)
                {
                    continue;
                }

                Vector3 surfaceNormal = hit.normal.normalized;

                if (Vector3.Dot(surfaceNormal, inwardNormal) < 0f)
                    surfaceNormal = -surfaceNormal;

                if (Vector3.Dot(surfaceNormal, inwardNormal) < 0.25f ||
                    hit.point.y < _worldBounds.min.y - 0.1f ||
                    hit.point.y > _worldBounds.max.y + 0.1f)
                {
                    continue;
                }

                point = new SurfacePoint(hit.point, surfaceNormal, hit.collider, targetVoxelLayer);
                return true;
            }

            return false;
        }

        private RaycastHit[] GetSortedHits(Ray ray, float distance)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                Mathf.Max(0.01f, distance),
                _settings.placementSurfaceLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            return hits;
        }

        private bool IsUsableHit(RaycastHit hit, Collider expectedCollider)
        {
            return hit.collider &&
                   (!expectedCollider || hit.collider == expectedCollider) &&
                   !ShouldIgnoreCollider(hit.collider);
        }

        private static bool HasMatchingRegion(
            Vector3 position,
            SurfaceRegion targetRegion,
            int? targetVoxelLayer,
            IReadOnlyList<SurfaceRegion> regions)
        {
            if (targetRegion != null)
                return targetRegion.ContainsXZ(position);

            foreach (SurfaceRegion region in regions)
            {
                if (targetVoxelLayer.HasValue && region.VoxelLayer != targetVoxelLayer)
                    continue;

                if (region.ContainsXZ(position))
                    return true;
            }

            return false;
        }

        private bool TryGetMatchingRegion(
            Vector3 hitPoint,
            SurfaceRegion targetRegion,
            int? targetVoxelLayer,
            IReadOnlyList<SurfaceRegion> regions,
            out SurfaceRegion matchedRegion)
        {
            matchedRegion = null;

            if (targetRegion != null)
            {
                if (!targetRegion.ContainsXZ(hitPoint) || !IsSurfaceYCompatible(hitPoint.y, targetRegion))
                    return false;

                matchedRegion = targetRegion;
                return true;
            }

            foreach (SurfaceRegion region in regions)
            {
                if (targetVoxelLayer.HasValue && region.VoxelLayer != targetVoxelLayer)
                    continue;

                if (!region.ContainsXZ(hitPoint) || !IsSurfaceYCompatible(hitPoint.y, region))
                    continue;

                matchedRegion = region;
                return true;
            }

            return false;
        }

        private bool IsSurfaceYCompatible(float surfaceY, SurfaceRegion region)
        {
            if (region == null || !region.VoxelLayer.HasValue && !_occupancy.HasSurfaceCells)
                return true;

            float tolerance = _occupancy.CellSize > 0f
                ? Mathf.Max(0.05f, _occupancy.CellSize * 0.75f)
                : 0.25f;
            return Mathf.Abs(surfaceY - region.SurfaceY) <= tolerance;
        }

        private bool ShouldIgnoreCollider(Collider collider)
        {
            if (!collider || HasDontSaveHideFlags(collider.transform))
                return true;

            if (collider.GetComponentInParent<GeneratedObjectMetadata>())
                return true;

            return _isSourceCollider?.Invoke(collider) == true;
        }

        private static bool HasDontSaveHideFlags(Transform transform)
        {
            while (transform)
            {
                if ((transform.gameObject.hideFlags & HideFlags.DontSave) != 0)
                    return true;

                transform = transform.parent;
            }

            return false;
        }
    }
}
