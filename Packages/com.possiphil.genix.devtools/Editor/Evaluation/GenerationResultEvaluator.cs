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
    /// <summary>Evaluates generated production metadata without changing generation decisions.</summary>
    internal static partial class GenerationResultEvaluator
    {
        private const float BoundsTolerance = 0.002f;
        private const float FacingTolerance = 1f;

        private sealed class GeneratedEntry
        {
            public Transform Root { get; set; }
            public GeneratedObjectMetadata Metadata { get; set; }
            public AssetDefinition Asset => Metadata ? Metadata.AssetDefinition : null;
            public PlacementSurfaceDescriptor Support => Metadata ? Metadata.SupportSurface : null;
            public OrientedBounds Bounds { get; set; }
            public OrientedBounds ContainmentBounds { get; set; }
            public Quaternion PlacementRotation { get; set; }
        }

        public static List<GenerationEvaluationCheckRecord> Evaluate(
            GenerationEvaluationScenario scenario,
            GenerationRequest request,
            GenerationDiagnostics diagnostics)
        {
            List<GenerationEvaluationCheckRecord> records = new();
            List<GeneratedEntry> entries = CollectEntries(request.AreaSource);
            EvaluationCheckSet enabled = scenario.Checks;

            AddIfEnabled(records, enabled, EvaluationCheckSet.Completion,
                EvaluateCompletion(scenario, request, diagnostics, entries.Count));
            AddIfEnabled(records, enabled, EvaluationCheckSet.Metadata,
                EvaluateMetadata(entries));

            if (!TryCreateContext(
                    request,
                    entries,
                    out GenerationContext context,
                    out List<AssetDefinition> resolvedAssets,
                    out string contextError))
            {
                AddUnavailable(records, enabled, EvaluationCheckSet.SpatialSourceIntegrity,
                    "Spatial Source Integrity", contextError);
                AddUnavailable(records, enabled, EvaluationCheckSet.TargetContainment, "Target Containment", contextError);
                AddUnavailable(records, enabled, EvaluationCheckSet.AssetRelations, "Asset Relations", contextError);
                AddUnavailable(records, enabled, EvaluationCheckSet.ExclusionRegions, "Exclusion Regions", contextError);
            }
            else
            {
                AddIfEnabled(records, enabled, EvaluationCheckSet.SpatialSourceIntegrity,
                    EvaluateSpatialSource(request.AreaSource));
                AddIfEnabled(records, enabled, EvaluationCheckSet.TargetContainment,
                    EvaluateContainment(context.Area, entries));
                AddIfEnabled(records, enabled, EvaluationCheckSet.AssetRelations,
                    EvaluateRelations(context, resolvedAssets, entries));
                AddIfEnabled(records, enabled, EvaluationCheckSet.ExclusionRegions,
                    EvaluateExclusions(context.Area, entries));
            }

            AddIfEnabled(records, enabled, EvaluationCheckSet.NonOverlap,
                EvaluateGeneratedOverlap(entries));
            AddIfEnabled(records, enabled, EvaluationCheckSet.SupportSemantics,
                EvaluateSupportSemantics(entries));
            AddIfEnabled(records, enabled, EvaluationCheckSet.PlacementLimits,
                EvaluateLimits(request.AssetPool, entries));
            AddIfEnabled(records, enabled, EvaluationCheckSet.SamplingSpacing,
                EvaluateSamplingSpacing(request, diagnostics));

            if (context != null)
            {
                AddIfEnabled(records, enabled, EvaluationCheckSet.RelativePlacement,
                    EvaluateRelativePlacement(context, entries));
            }
            else
            {
                AddUnavailable(records, enabled, EvaluationCheckSet.RelativePlacement,
                    "Relative Placement", contextError);
            }

            return records;
        }

        private static bool TryCreateContext(
            GenerationRequest request,
            IReadOnlyCollection<GeneratedEntry> entries,
            out GenerationContext context,
            out List<AssetDefinition> assets,
            out string error)
        {
            context = null;
            assets = null;
            error = string.Empty;

            if (!GenerationAssetFilter.TryResolve(
                    request,
                    AssetCatalogService.GetOrCreate(),
                    out assets,
                    out error))
            {
                return false;
            }

            try
            {
                Transform parent = entries.FirstOrDefault()?.Root?.parent;
                context = GenerationContextFactory.Create(request, parent, assets);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static List<GeneratedEntry> CollectEntries(IAreaSource areaSource)
        {
            List<GeneratedEntry> entries = new();
            if (!GeneratedHierarchy.TryGet(areaSource, out Transform parent))
                return entries;

            foreach (Transform child in parent)
            {
                GeneratedObjectMetadata metadata = child ? child.GetComponent<GeneratedObjectMetadata>() : null;
                AssetDefinition asset = metadata ? metadata.AssetDefinition : null;
                Quaternion placementRotation = asset
                    ? asset.RemovePrefabRotationOffset(child.rotation)
                    : child.rotation;
                OrientedBounds bounds = asset
                    ? new OrientedBounds(
                        child.position + placementRotation * asset.BoundsCenterOffset,
                        asset.BoundsSize,
                        placementRotation)
                    : new OrientedBounds(child.position, Vector3.one * 0.01f, placementRotation);
                OrientedBounds containmentBounds = asset
                    ? RemoveSurfaceSink(bounds, placementRotation, metadata.PlacementTarget, asset.SurfaceSinkOffset)
                    : bounds;
                entries.Add(new GeneratedEntry
                {
                    Root = child,
                    Metadata = metadata,
                    Bounds = bounds,
                    ContainmentBounds = containmentBounds,
                    PlacementRotation = placementRotation
                });
            }

            return entries;
        }

        private static void AddIfEnabled(
            ICollection<GenerationEvaluationCheckRecord> records,
            EvaluationCheckSet enabled,
            EvaluationCheckSet flag,
            GenerationEvaluationCheckRecord record)
        {
            if ((enabled & flag) != 0)
                records.Add(record);
        }

        private static void AddUnavailable(
            ICollection<GenerationEvaluationCheckRecord> records,
            EvaluationCheckSet enabled,
            EvaluationCheckSet flag,
            string name,
            string reason)
        {
            if ((enabled & flag) != 0)
                records.Add(Unavailable(name, reason));
        }

        private static GenerationEvaluationCheckRecord Record(
            string name,
            bool passed,
            int violations,
            string message) => new()
        {
            name = name,
            status = passed ? EvaluationCheckStatus.Passed : EvaluationCheckStatus.Failed,
            violations = Mathf.Max(0, violations),
            message = message ?? string.Empty
        };

        private static GenerationEvaluationCheckRecord Unavailable(string name, string reason) => new()
        {
            name = name,
            status = EvaluationCheckStatus.NotApplicable,
            message = reason ?? string.Empty
        };
    }
}
