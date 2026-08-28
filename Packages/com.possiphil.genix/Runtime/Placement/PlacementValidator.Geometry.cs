using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Core;
using Genix.Geometry;
using Genix.Profiling;
using Genix.Sampling;
using Genix.Semantics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Placement
{
    public static partial class PlacementValidator
    {
        private static bool IsSupportingGeneratedObject(
            SceneObjectIndex.Entry sceneObject,
            Collider surfaceCollider)
        {
            if (!surfaceCollider || !sceneObject.Root)
                return false;

            PlacementSurfaceDescriptor descriptor = PlacementSupportRules.GetDescriptor(surfaceCollider);
            return descriptor && descriptor.transform.IsChildOf(sceneObject.Root);
        }

        private static Bounds CreateHorizontalSpacingQueryBounds(
            Bounds candidateBounds,
            float minDistance,
            Bounds verticalBounds)
        {
            float expansion = minDistance * 2f;
            Vector3 min = candidateBounds.min;
            Vector3 max = candidateBounds.max;

            min.x -= expansion;
            min.z -= expansion;
            max.x += expansion;
            max.z += expansion;

            if (verticalBounds.size.y > 0f)
            {
                min.y = Mathf.Min(min.y, verticalBounds.min.y);
                max.y = Mathf.Max(max.y, verticalBounds.max.y);
            }

            Bounds queryBounds = default;
            queryBounds.SetMinMax(min, max);
            return queryBounds;
        }

        private static bool IsCloserThanMinDistance(
            Vector3 a,
            Vector3 b,
            float minDistance,
            bool includeHeight)
        {
            float minDistanceSquared = minDistance * minDistance;

            float dx = a.x - b.x;
            float dy = includeHeight ? a.y - b.y : 0f;
            float dz = a.z - b.z;

            return dx * dx + dy * dy + dz * dz < minDistanceSquared;
        }

        private static bool UsesThreeDimensionalSpacing(PlacementType placementType) =>
            placementType is PlacementType.Wall or PlacementType.InsideSpace;

        private static bool BoundsOverlap(OrientedBounds a, Bounds b)
        {
            return a.Intersects(b);
        }
    }
}
