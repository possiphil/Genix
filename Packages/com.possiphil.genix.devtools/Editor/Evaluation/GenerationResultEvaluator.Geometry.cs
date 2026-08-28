using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Editor.Generation;
using Genix.Editor.Assets;
using Genix.Layouts;
using Genix.Placement;
using Genix.Semantics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Evaluation
{
    internal static partial class GenerationResultEvaluator
    {
        private static GenerationEvaluationCheckRecord EvaluateSamplingSpacing(
            GenerationRequest request,
            GenerationDiagnostics diagnostics)
        {
            float minimum = Mathf.Max(0f, request.StyleSettings.poisson.minDistance);
            if (minimum <= 0f || diagnostics == null)
                return Unavailable("Sampling Spacing", "No Poisson placement observations are available.");

            int violations = 0;
            IReadOnlyList<PlacementDiagnostic> placements = diagnostics.Placements;
            float thresholdSquared = Mathf.Max(0f, minimum - 0.001f);
            thresholdSquared *= thresholdSquared;

            for (int i = 0; i < placements.Count - 1; i++)
            for (int j = i + 1; j < placements.Count; j++)
            {
                if ((placements[i].Position - placements[j].Position).sqrMagnitude < thresholdSquared)
                    violations++;
            }

            return Record(
                "Sampling Spacing",
                violations == 0,
                violations,
                violations == 0
                    ? $"All accepted sample positions preserve the {minimum:F2} unit minimum distance."
                    : $"{violations} accepted sample pairs are closer than {minimum:F2} units.");
        }

        private static GenerationEvaluationCheckRecord EvaluateCompletion(
            GenerationEvaluationScenario scenario,
            GenerationRequest request,
            GenerationDiagnostics diagnostics,
            int generatedCount)
        {
            int placed = diagnostics?.PlacedObjectCount ?? generatedCount;
            float ratio = request.ObjectCount > 0 ? placed / (float)request.ObjectCount : 0f;
            bool passed = ratio + 0.0001f >= scenario.MinimumCompletionRatio &&
                          ratio - 0.0001f <= scenario.MaximumCompletionRatio;
            return Record(
                "Completion",
                passed,
                passed ? 0 : 1,
                $"Placed {placed} of {request.ObjectCount} ({ratio:P1}); accepted interval " +
                $"{scenario.MinimumCompletionRatio:P0}-{scenario.MaximumCompletionRatio:P0}.");
        }

        private static GenerationEvaluationCheckRecord EvaluateMetadata(IReadOnlyList<GeneratedEntry> entries)
        {
            int violations = entries.Count(entry => !entry.Metadata || !entry.Asset || !entry.Asset.Prefab);
            return Record(
                "Generated Metadata",
                violations == 0,
                violations,
                violations == 0
                    ? $"All {entries.Count} generated roots retain asset and placement metadata."
                    : $"{violations} generated roots have incomplete metadata or asset references.");
        }

        private static GenerationEvaluationCheckRecord EvaluateContainment(
            PlacementArea area,
            IReadOnlyList<GeneratedEntry> entries)
        {
            int violations = entries.Count(entry => entry.Asset && !area.ContainsPlacementVolume(entry.ContainmentBounds));
            return Record(
                "Target Containment",
                violations == 0,
                violations,
                violations == 0
                    ? "Every generated placement bound remains inside the target volume."
                    : $"{violations} generated placement bounds extend outside the target volume.");
        }

        private static GenerationEvaluationCheckRecord EvaluateGeneratedOverlap(
            IReadOnlyList<GeneratedEntry> entries)
        {
            SpatialBoundsIndex index = new(capacity: entries.Count);
            int violations = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                GeneratedEntry entry = entries[i];
                Bounds query = Shrink(entry.Bounds).ToAxisAlignedBounds();

                foreach (int otherIndex in index.Query(query))
                {
                    if (HasGeneratedConflict(entry, entries[otherIndex]))
                        violations++;
                }

                index.Add(query, i);
            }

            return Record(
                "Generated Overlap",
                violations == 0,
                violations,
                violations == 0
                    ? "No generated placement bounds overlap."
                    : $"{violations} generated placement-bound pairs overlap.");
        }

        private static bool HasGeneratedConflict(GeneratedEntry first, GeneratedEntry second)
        {
            OrientedBounds firstBounds = Shrink(first.Bounds);
            OrientedBounds secondBounds = Shrink(second.Bounds);

            if (firstBounds.Intersects(secondBounds))
                return true;

            bool firstHasClearance = first.Asset && first.Asset.ReserveClearance;
            bool secondHasClearance = second.Asset && second.Asset.ReserveClearance;

            if (!firstHasClearance && !secondHasClearance)
                return false;

            OrientedBounds firstClearance = firstHasClearance
                ? Shrink(first.Asset.CreateClearanceBounds(first.Root.position, first.Root.rotation))
                : default;
            OrientedBounds secondClearance = secondHasClearance
                ? Shrink(second.Asset.CreateClearanceBounds(second.Root.position, second.Root.rotation))
                : default;

            return firstHasClearance && firstClearance.Intersects(secondBounds) ||
                   secondHasClearance && secondClearance.Intersects(firstBounds) ||
                   firstHasClearance && secondHasClearance && firstClearance.Intersects(secondClearance);
        }

        private static OrientedBounds RemoveSurfaceSink(
            OrientedBounds visualBounds,
            Quaternion placementRotation,
            PlacementTarget placementTarget,
            float sinkOffset)
        {
            if (sinkOffset <= 0f || placementTarget == PlacementTarget.InsideSpace)
                return visualBounds;

            Vector3 normal = placementTarget == PlacementTarget.Wall
                ? placementRotation * Vector3.forward
                : placementRotation * Vector3.up;
            return new OrientedBounds(
                visualBounds.Center + normal.normalized * sinkOffset,
                visualBounds.Size,
                visualBounds.Rotation);
        }

        private static OrientedBounds Shrink(OrientedBounds bounds) => new(
            bounds.Center,
            new Vector3(
                Mathf.Max(0.01f, bounds.Size.x - BoundsTolerance * 2f),
                Mathf.Max(0.01f, bounds.Size.y - BoundsTolerance * 2f),
                Mathf.Max(0.01f, bounds.Size.z - BoundsTolerance * 2f)),
            bounds.Rotation);
    }
}

