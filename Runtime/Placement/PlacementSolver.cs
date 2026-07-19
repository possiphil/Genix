using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;

namespace Genix.Placement
{
    public static class PlacementSolver
    {
        public static void ClearCandidateCache()
        {
            CandidateSeedCache.Clear();
        }

        public static CandidatePool CreateCandidatePool(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null,
            PlacementTarget? targets = null)
        {
            diagnostics ??= NullDiagnosticsSink.Instance;
            return new CandidatePool(CandidateSeedFactory.Create(context, diagnostics, targets));
        }

        public static Dictionary<PlacementType, CandidatePool> CreateCandidatePoolsByPlacementType(
            GenerationContext context,
            IDiagnosticsSink diagnostics = null,
            PlacementTarget? targets = null)
        {
            diagnostics ??= NullDiagnosticsSink.Instance;
            return CandidateSeedFactory.Create(context, diagnostics, targets)
                .GroupBy(seed => seed.PlacementType)
                .ToDictionary(group => group.Key, group => new CandidatePool(group.ToList()));
        }

        public static bool TryGetValidCandidate(
            GenerationContext context,
            AssetDefinition asset,
            CandidatePool candidates,
            out PlacementCandidate candidate,
            IDiagnosticsSink diagnostics = null,
            string generatedObjectName = "")
        {
            diagnostics ??= NullDiagnosticsSink.Instance;

            while (candidates.TryTakeNext(out CandidateSeed seed))
            {
                diagnostics.RecordTestedCandidateSeed(seed.Position);

                if (asset && asset.PlacementType != seed.PlacementType)
                    continue;

                if (TryEvaluateAsset(
                        context,
                        asset,
                        seed,
                        generatedObjectName,
                        diagnostics,
                        out candidate,
                        out _))
                {
                    return true;
                }
            }

            candidate = default;
            return false;
        }

        public static bool TryGetValidCandidateForAnyAsset(
            GenerationContext context,
            IReadOnlyList<AssetDefinition> assets,
            CandidatePool candidates,
            Func<AssetDefinition, string> createObjectName,
            out AssetDefinition selectedAsset,
            out PlacementCandidate candidate,
            out string generatedObjectName,
            IDiagnosticsSink diagnostics = null)
        {
            diagnostics ??= NullDiagnosticsSink.Instance;
            selectedAsset = null;
            candidate = default;
            generatedObjectName = string.Empty;

            if (context == null || assets == null || assets.Count == 0 || candidates == null)
                return false;

            while (candidates.TryTakeNext(out CandidateSeed seed))
            {
                diagnostics.RecordTestedCandidateSeed(seed.Position);
                List<AssetDefinition> remaining = AssetAttemptPlanner.CreateOrder(
                    assets,
                    seed.PlacementType,
                    context.Random);

                while (remaining.Count > 0)
                {
                    AssetDefinition asset = remaining[0];
                    remaining.RemoveAt(0);
                    string objectName = createObjectName?.Invoke(asset) ?? asset.AssetName;

                    if (TryEvaluateAsset(
                            context,
                            asset,
                            seed,
                            objectName,
                            diagnostics,
                            out PlacementCandidate attempt,
                            out RejectionReason rejection))
                    {
                        selectedAsset = asset;
                        candidate = attempt;
                        generatedObjectName = objectName;
                        return true;
                    }

                    AssetAttemptPlanner.PruneDominated(
                        remaining,
                        seed.PlacementType,
                        asset,
                        rejection);
                }
            }

            return false;
        }

        private static bool TryEvaluateAsset(
            GenerationContext context,
            AssetDefinition asset,
            CandidateSeed seed,
            string generatedObjectName,
            IDiagnosticsSink diagnostics,
            out PlacementCandidate candidate,
            out RejectionReason pruningReason)
        {
            candidate = default;
            pruningReason = RejectionReason.None;

            if (!asset || !asset.Prefab)
                return false;

            string objectName = string.IsNullOrWhiteSpace(generatedObjectName)
                ? asset.AssetName
                : generatedObjectName;
            int rotationCount = CandidateFactory.GetRotationAttemptCount(context, asset, seed.PlacementType);
            float yawBase = CandidateFactory.UsesRandomYaw(context, asset, seed.PlacementType)
                ? context.Random.Range(0f, 360f)
                : 0f;

            for (int rotationIndex = 0; rotationIndex < rotationCount; rotationIndex++)
            {
                PlacementCandidate attempt = CandidateFactory.Create(
                    seed,
                    context,
                    asset,
                    rotationIndex,
                    rotationCount,
                    yawBase);
                OrientedBounds bounds = CandidateFactory.GetBounds(attempt, asset);

                if (PlacementValidator.TryValidateCandidate(
                        attempt,
                        bounds,
                        context,
                        asset,
                        out RejectionReason rejection,
                        out string relatedObjectName))
                {
                    diagnostics.RecordCandidate(
                        asset.AssetName,
                        objectName,
                        attempt,
                        bounds.ToLocalBounds(),
                        true,
                        RejectionReason.None);
                    diagnostics.RecordPlacement(asset, objectName, attempt);
                    candidate = attempt;
                    return true;
                }

                diagnostics.RecordCandidate(
                    asset.AssetName,
                    objectName,
                    attempt,
                    bounds.ToLocalBounds(),
                    false,
                    rejection,
                    relatedObjectName);

                if (pruningReason == RejectionReason.None)
                    pruningReason = rejection;
            }

            return false;
        }
    }
}
