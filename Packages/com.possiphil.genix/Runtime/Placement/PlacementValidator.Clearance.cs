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
        private static bool TryFindClearanceViolation(
            PlacementCandidate candidate,
            OrientedBounds candidateBounds,
            AssetDefinition asset,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;
            bool hasCandidateClearance = asset && asset.ReserveClearance;
            bool hasExistingClearance = context.Plan.HasClearanceBounds ||
                                        context.GeneratedSceneObjects?.HasClearanceBounds == true;

            if (!hasCandidateClearance && !hasExistingClearance)
                return false;

            OrientedBounds candidateClearance = hasCandidateClearance
                ? asset.CreateClearanceBounds(candidate)
                : default;
            Bounds candidateVisualAabb = candidateBounds.ToAxisAlignedBounds();

            if (hasCandidateClearance)
            {
                if (TryFindExclusionRegion(
                        candidateClearance,
                        candidate.PlacementType,
                        context,
                        asset,
                        out relatedObjectName))
                {
                    return true;
                }

                Bounds clearanceAabb = candidateClearance.ToAxisAlignedBounds();

                foreach (PlannedObject plannedObject in context.Plan.Query(clearanceAabb))
                {
                    if (!candidateClearance.Intersects(plannedObject.Bounds))
                        continue;

                    relatedObjectName = plannedObject.ObjectName;
                    return true;
                }

                foreach (PlannedObject plannedObject in context.Plan.QueryClearance(clearanceAabb))
                {
                    OrientedBounds existingClearance = plannedObject.Asset.CreateClearanceBounds(
                        plannedObject.Candidate);

                    if (!candidateClearance.Intersects(existingClearance))
                        continue;

                    relatedObjectName = plannedObject.ObjectName;
                    return true;
                }

                SceneObjectIndex generatedSceneObjects = context.GeneratedSceneObjects;

                if (generatedSceneObjects != null)
                {
                    foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.Query(clearanceAabb))
                    {
                        if (IsSupportingGeneratedObject(sceneObject, candidate.SurfaceCollider) ||
                            !candidateClearance.Intersects(sceneObject.Bounds))
                        {
                            continue;
                        }

                        relatedObjectName = sceneObject.ObjectName;
                        return true;
                    }

                    foreach (SceneObjectIndex.Entry sceneObject in generatedSceneObjects.QueryClearance(clearanceAabb))
                    {
                        if (!sceneObject.AssetDefinition || !sceneObject.Root ||
                            IsSupportingGeneratedObject(sceneObject, candidate.SurfaceCollider))
                        {
                            continue;
                        }

                        OrientedBounds existingClearance = sceneObject.AssetDefinition.CreateClearanceBounds(
                            sceneObject.Root.position,
                            sceneObject.Root.rotation);

                        if (!candidateClearance.Intersects(existingClearance))
                            continue;

                        relatedObjectName = sceneObject.ObjectName;
                        return true;
                    }
                }

                if (TryFindFixedClearanceBlocker(candidate, candidateClearance, context, out relatedObjectName))
                    return true;
            }

            foreach (PlannedObject plannedObject in context.Plan.QueryClearance(candidateVisualAabb))
            {
                OrientedBounds existingClearance = plannedObject.Asset.CreateClearanceBounds(
                    plannedObject.Candidate);

                if (!existingClearance.Intersects(candidateBounds))
                    continue;

                relatedObjectName = plannedObject.ObjectName;
                return true;
            }

            SceneObjectIndex existingSceneObjects = context.GeneratedSceneObjects;

            if (existingSceneObjects == null)
                return false;

            foreach (SceneObjectIndex.Entry sceneObject in existingSceneObjects.QueryClearance(candidateVisualAabb))
            {
                if (!sceneObject.AssetDefinition || !sceneObject.Root ||
                    IsSupportingGeneratedObject(sceneObject, candidate.SurfaceCollider))
                {
                    continue;
                }

                OrientedBounds existingClearance = sceneObject.AssetDefinition.CreateClearanceBounds(
                    sceneObject.Root.position,
                    sceneObject.Root.rotation);

                if (!existingClearance.Intersects(candidateBounds))
                    continue;

                relatedObjectName = sceneObject.ObjectName;
                return true;
            }

            return false;
        }

        private static bool TryFindFixedClearanceBlocker(
            PlacementCandidate candidate,
            OrientedBounds clearanceBounds,
            GenerationContext context,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (!HasPotentialFixedObstacle(candidate, clearanceBounds, context))
                return false;

            int hitCount = OverlapBox(clearanceBounds, out Collider[] hits);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = hits[i];

                if (ShouldIgnoreFixedCollider(hit, candidate, context))
                    continue;

                relatedObjectName = hit.name;
                return true;
            }

            return false;
        }

        private static Bounds ExpandBounds(Bounds bounds, float radius)
        {
            bounds.Expand(Mathf.Max(0f, radius) * 2f);
            return bounds;
        }

        private static bool TryFindExclusionRegion(
            OrientedBounds candidateBounds,
            PlacementType placementType,
            GenerationContext context,
            AssetDefinition asset,
            out string relatedObjectName)
        {
            relatedObjectName = string.Empty;

            if (context?.ExclusionRegions == null || context.ExclusionRegions.Count == 0)
                return false;

            foreach (PlacementExclusionRegion region in context.ExclusionRegions)
            {
                if (!region || !region.Intersects(candidateBounds, placementType, asset))
                    continue;

                relatedObjectName = region.name;
                return true;
            }

            return false;
        }
    }
}
