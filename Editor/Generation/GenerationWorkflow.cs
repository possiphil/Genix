using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Editor.Diagnostics;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Editor.Genix.Editor.Common;
using Genix.Editor.Utilities;
using Genix.Extensions;
using Genix.Placement;
using UnityEngine;

namespace Genix.Editor.Generation
{
    public static class GenerationWorkflow
    {
        private const string GenerateUndoName = "Generated Genix Objects";
        private const string RegenerateUndoName = "Regenerated Genix Objects";
        private const string ClearUndoName = "Cleared Genix Objects";

        public static void Generate(GenerationRequest request)
        {
            if (!Validate(request))
                return;

            UndoStep.ExecuteAsSingleStep(GenerateUndoName, () => GenerateInternal(request));
        }

        public static void Regenerate(GenerationRequest request)
        {
            if (!Validate(request))
                return;

            UndoStep.ExecuteAsSingleStep(RegenerateUndoName, () => RegenerateInternal(request));
        }

        public static void Clear(IAreaSource areaSource)
        {
            if (areaSource == null || !areaSource.ParentTransform)
            {
                Debug.LogWarning("No location is selected. Choose a Target Area before clearing generated objects.");
                return;
            }

            UndoStep.ExecuteAsSingleStep(ClearUndoName, () =>
            {
                if (!SceneGenerationService.Clear(areaSource))
                    Debug.Log("No generated Genix objects were found for the selected location.");
            });
        }

        private static bool GenerateInternal(GenerationRequest request)
        {
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            if (!GenerationAssetFilter.TryResolve(request, catalog, out List<AssetDefinition> assets, out string assetError))
            {
                Debug.LogWarning(assetError);
                return false;
            }

            foreach (string warning in GenerationAssetFilter.GetUnavailableTargetWarnings(request, assets))
                Debug.LogWarning(warning);

            bool parentExisted = GeneratedHierarchy.TryGet(request.AreaSource, out _);
            Transform generatedParent = GeneratedHierarchy.GetOrCreate(request.AreaSource);

            if (!TryCreateContext(request, generatedParent, out GenerationContext context))
            {
                SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);
                return false;
            }

            if (!RelativeAnchorProvider.HasAnyAnchor(context))
            {
                SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);
                Debug.LogWarning(
                    $"Relative placement could not start because '{request.RelativePlacement.Source.ToDisplayName()}' has no usable anchor objects. " +
                    "Choose another Relative To source, select scene objects, generate anchor objects first, or adjust the relative scene layers.");
                return false;
            }

            DiagnosticsRecorder recorder = new(context, DiagnosticsMode.Detailed, request.StyleName);
            GenerationOutcome outcome = GenerationEngine.BuildPlan(context, assets, recorder);

            if (!outcome.ShouldApply)
            {
                context.Plan.Clear();
                recorder.Diagnostics.Placements.Clear();
                recorder.RecordStopReason(outcome.Message);
                DiagnosticsStore.SetLast(recorder.Diagnostics);
                SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);

                string rollbackText = context.BestEffort
                    ? string.Empty
                    : " Best Effort is disabled, so the complete plan was discarded and nothing was placed.";
                Debug.LogWarning(
                    $"Genix found {outcome.PlacedCount} of {context.Count} requested placements. " +
                    $"{outcome.Message}{rollbackText} Open Genix Diagnostics for rejection details.");
                return false;
            }

            if (!SceneGenerationService.Apply(context.Plan, generatedParent, out string applyError))
            {
                recorder.Diagnostics.Placements.Clear();
                recorder.RecordStopReason(applyError);
                DiagnosticsStore.SetLast(recorder.Diagnostics);
                SceneGenerationService.RemoveEmptyParent(generatedParent, parentExisted);
                Debug.LogWarning(applyError);
                return false;
            }

            if (!outcome.IsComplete)
            {
                recorder.RecordStopReason(outcome.Message);
                Debug.LogWarning(outcome.Message);
            }

            DiagnosticsStore.SetLast(recorder.Diagnostics);
            return true;
        }

        private static void RegenerateInternal(GenerationRequest request)
        {
            GameObject snapshot = SceneGenerationService.CreateSnapshot(request.AreaSource);

            try
            {
                SceneGenerationService.Clear(request.AreaSource);

                if (!GenerateInternal(request))
                    SceneGenerationService.RestoreSnapshot(request.AreaSource, snapshot);
            }
            finally
            {
                if (snapshot)
                    UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        private static bool TryCreateContext(
            GenerationRequest request,
            Transform generatedParent,
            out GenerationContext context)
        {
            try
            {
                context = GenerationContextFactory.Create(request, generatedParent);
                return true;
            }
            catch (Exception exception)
            {
                context = null;
                Debug.LogWarning(exception.Message);
                return false;
            }
        }

        private static bool Validate(GenerationRequest request)
        {
            if (GenerationPreflight.IsValid(request, out string error))
                return true;

            Debug.LogWarning(error);
            return false;
        }
    }
}
