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
        private static bool TryFindOverlappingGeneratedObject(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            Bounds axisAlignedBounds = candidateBounds.ToAxisAlignedBounds();

            foreach (PlannedObject plannedObject in context.Plan.Query(axisAlignedBounds))
            {
                if (!candidateBounds.Intersects(plannedObject.Bounds))
                    continue;

                relatedObjectName = plannedObject.ObjectName;
                return true;
            }

            SceneObjectIndex generatedSceneObjects = context.GeneratedSceneObjects;

            if (generatedSceneObjects == null || generatedSceneObjects.Count == 0)
                return false;

            foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.Query(axisAlignedBounds))
            {
                if (IsSupportingGeneratedObject(sceneObject, candidate.SurfaceCollider))
                    continue;

                if (!BoundsOverlap(candidateBounds, sceneObject.Bounds))
                    continue;

                relatedObjectName = sceneObject.ObjectName;
                return true;
            }

            return false;
        }

        private static bool TryFindTooClosePlannedObject(
            Bounds candidateBounds,
            PlacementType placementType,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (context.StyleSettings.algorithm != SamplingAlgorithm.BridsonPoissonDisk)
                return false;

            float minDistance = context.StyleSettings.poisson.minDistance;

            if (minDistance <= 0f)
                return false;

            bool includeHeight = UsesThreeDimensionalSpacing(placementType);
            IEnumerable<PlannedObject> nearbyObjects = includeHeight
                ? context.Plan.QuerySpatialSpacing(candidateBounds, minDistance)
                : context.Plan.QueryHorizontalSpacing(candidateBounds, minDistance);

            foreach (PlannedObject plannedObject in nearbyObjects)
            {
                if (!IsCloserThanMinDistance(
                        candidateBounds.center,
                        plannedObject.Bounds.Center,
                        minDistance,
                        includeHeight))
                {
                    continue;
                }

                relatedObjectName = plannedObject.ObjectName;
                return true;
            }

            return false;
        }

        private static bool TryFindTooCloseGeneratedSceneObject(
            Bounds candidateBounds,
            PlacementType placementType,
            Collider surfaceCollider,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (context.StyleSettings.algorithm != SamplingAlgorithm.BridsonPoissonDisk)
                return false;

            float minDistance = context.StyleSettings.poisson.minDistance;

            if (minDistance <= 0f)
                return false;

            SceneObjectIndex generatedSceneObjects = context.GeneratedSceneObjects;

            if (generatedSceneObjects == null || generatedSceneObjects.Count == 0)
                return false;

            Bounds queryBounds;

            bool includeHeight = UsesThreeDimensionalSpacing(placementType);

            if (includeHeight)
            {
                queryBounds = candidateBounds;
                queryBounds.Expand(minDistance * 2f);
            }
            else
            {
                Bounds verticalBounds = generatedSceneObjects.HasBounds
                    ? generatedSceneObjects.Bounds
                    : context.TargetBounds;
                queryBounds = CreateHorizontalSpacingQueryBounds(
                    candidateBounds,
                    minDistance,
                    verticalBounds);
            }

            foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.Query(queryBounds))
            {
                if (IsSupportingGeneratedObject(sceneObject, surfaceCollider))
                    continue;

                if (!IsCloserThanMinDistance(
                        candidateBounds.center,
                        sceneObject.Bounds.center,
                        minDistance,
                        includeHeight))
                    continue;

                relatedObjectName = sceneObject.ObjectName;
                return true;
            }

            return false;
        }

        private static bool TryFindAssetSpacingViolation(
            Bounds candidateBounds,
            PlacementType placementType,
            Collider surfaceCollider,
            AssetDefinition asset,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (!asset)
                return false;

            SceneObjectIndex generatedSceneObjects = context.GeneratedSceneObjects;
            float searchRadius = Mathf.Max(
                asset.MaxSpacingDistance,
                context.Plan.MaxAssetSpacingDistance,
                generatedSceneObjects?.MaxAssetSpacingDistance ?? 0f);

            if (searchRadius <= 0f)
                return false;

            bool includeHeight = UsesThreeDimensionalSpacing(placementType);
            IEnumerable<PlannedObject> plannedObjects = includeHeight
                ? context.Plan.QuerySpatialSpacing(candidateBounds, searchRadius)
                : context.Plan.QueryHorizontalSpacing(candidateBounds, searchRadius);

            foreach (PlannedObject plannedObject in plannedObjects)
            {
                float requiredDistance = GetRequiredAssetSpacing(asset, plannedObject.Asset);

                if (requiredDistance <= 0f ||
                    !IsCloserThanMinDistance(
                        candidateBounds.center,
                        plannedObject.Bounds.Center,
                        requiredDistance,
                        includeHeight))
                {
                    continue;
                }

                relatedObjectName = plannedObject.ObjectName;
                return true;
            }

            if (generatedSceneObjects == null || generatedSceneObjects.Count == 0)
                return false;

            Bounds queryBounds = includeHeight
                ? ExpandBounds(candidateBounds, searchRadius)
                : CreateHorizontalSpacingQueryBounds(
                    candidateBounds,
                    searchRadius,
                    generatedSceneObjects.HasBounds ? generatedSceneObjects.Bounds : context.TargetBounds);

            foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.Query(queryBounds))
            {
                if (!sceneObject.AssetDefinition || IsSupportingGeneratedObject(sceneObject, surfaceCollider))
                    continue;

                float requiredDistance = GetRequiredAssetSpacing(asset, sceneObject.AssetDefinition);

                if (requiredDistance <= 0f ||
                    !IsCloserThanMinDistance(
                        candidateBounds.center,
                        sceneObject.Bounds.center,
                        requiredDistance,
                        includeHeight))
                {
                    continue;
                }

                relatedObjectName = sceneObject.ObjectName;
                return true;
            }

            return false;
        }

        private static float GetRequiredAssetSpacing(AssetDefinition first, AssetDefinition second) =>
            first && second
                ? Mathf.Max(first.GetMinimumSpacingTo(second), second.GetMinimumSpacingTo(first))
                : 0f;
    }
}
