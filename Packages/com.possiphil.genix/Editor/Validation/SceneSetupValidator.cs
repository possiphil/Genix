using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Generation;
using Genix.Editor.Assets;
using Genix.Extensions;
using Genix.Placement;
using UnityEngine;

namespace Genix.Editor.Validation
{
    /// <summary>Validates request assets, layers, integrations, targets, and scene prerequisites before generation.</summary>
    internal static class SceneSetupValidator
    {
        public static SceneSetupReport Validate(GenerationRequest request)
        {
            SceneSetupReport report = new();

            if (!GenerationPreflight.IsValid(request, out string preflightError))
            {
                report.AddError(preflightError);
                return report;
            }

            ValidateSurfaceLayers(request, report);
            ValidateArea(request, report);
            ValidateAssets(request, report);

            if (!report.HasIssues)
                report.AddInfo("Scene setup is valid for the current Genix settings.");

            return report;
        }

        private static void ValidateSurfaceLayers(GenerationRequest request, SceneSetupReport report)
        {
            if (!request.AreaBuildSettings.UsesPhysicsSurfaceProjection)
            {
                report.AddWarning("Surface source is set to SFS Boundaries. Genix will use SFS surface voxels and will ignore scene colliders as placement surfaces.");
                return;
            }

            ValidateSurfaceLayer(request, report, PlacementTarget.Floor, PlacementType.Floor);
            ValidateSurfaceLayer(request, report, PlacementTarget.Wall, PlacementType.Wall);
            ValidateSurfaceLayer(request, report, PlacementTarget.Ceiling, PlacementType.Ceiling);
        }

        private static void ValidateSurfaceLayer(
            GenerationRequest request,
            SceneSetupReport report,
            PlacementTarget target,
            PlacementType placementType)
        {
            if ((request.PlacementTargets & target) == 0)
                return;

            LayerMask mask = request.AreaBuildSettings.GetSurfaceLayers(placementType);

            if (mask.value == 0)
            {
                report.AddError($"{target.ToDisplayName()} is selected, but its surface layer mask is empty.");
                return;
            }

            int colliderCount = CountSceneColliders(mask);

            if (colliderCount == 0)
                report.AddWarning($"{target.ToDisplayName()} is selected, but no active scene colliders exist on its surface layers.");
        }

        private static void ValidateArea(GenerationRequest request, SceneSetupReport report)
        {
            if (!request.AreaSource.TryBuildArea(request.AreaBuildSettings, out PlacementArea area, out string error))
            {
                report.AddError($"Target Area could not be built: {error}");
                return;
            }

            bool usesAllSurfaceSearch = request.AreaBuildSettings.UsesAllMatchingSurfaceSearch;

            if (!usesAllSurfaceSearch &&
                (request.PlacementTargets & PlacementTarget.Floor) != 0 &&
                area.FloorRegions.Count == 0)
            {
                report.AddWarning("Floor is selected, but the target area has no detected floor regions.");
            }

            if ((request.PlacementTargets & PlacementTarget.Wall) != 0 && area.WallRegions.Count == 0)
                report.AddWarning("Wall is selected, but the target area has no detected wall regions.");

            if (!usesAllSurfaceSearch &&
                (request.PlacementTargets & PlacementTarget.Ceiling) != 0 &&
                area.CeilingRegions.Count == 0)
            {
                report.AddWarning("Ceiling is selected, but the target area has no detected ceiling regions.");
            }

            if (((request.PlacementTargets & PlacementTarget.InsideSpace) != 0 ||
                 usesAllSurfaceSearch &&
                 (request.PlacementTargets & (PlacementTarget.Floor | PlacementTarget.Ceiling)) != 0) &&
                area.WorldBounds.size == Vector3.zero)
            {
                report.AddWarning("The selected target area bounds are empty.");
            }
        }

        private static void ValidateAssets(GenerationRequest request, SceneSetupReport report)
        {
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            if (!GenerationAssetFilter.TryResolve(request, catalog, out List<AssetDefinition> assets, out string assetError))
            {
                report.AddError(assetError);
                return;
            }

            foreach (string warning in GenerationAssetFilter.GetUnavailableTargetWarnings(request, assets))
                report.AddWarning(warning);

            PlacementTarget usableTargets = TargetDistributionPolicy.GetUsableTargetsForValidation(request, assets);

            if (usableTargets == PlacementTarget.None)
            {
                report.AddError("No selected placement target has usable assets after prefab and semantic tag filtering.");
                return;
            }
        }

        private static int CountSceneColliders(LayerMask mask)
        {
            int count = 0;

            foreach (Collider collider in Resources.FindObjectsOfTypeAll<Collider>())
            {
                if (!collider ||
                    !collider.gameObject.scene.IsValid() ||
                    !collider.gameObject.activeInHierarchy ||
                    (mask.value & (1 << collider.gameObject.layer)) == 0)
                {
                    continue;
                }

                count++;
            }

            return count;
        }
    }

    /// <summary>Ordered collection of setup issues with aggregate error state.</summary>
    internal sealed class SceneSetupReport
    {
        private readonly List<SceneSetupIssue> _issues = new();

        public IReadOnlyList<SceneSetupIssue> Issues => _issues;
        public bool HasIssues => _issues.Count > 0;
        public bool HasErrors => _issues.Any(issue => issue.Severity == SceneSetupIssueSeverity.Error);

        public void AddInfo(string message) => Add(SceneSetupIssueSeverity.Info, message);
        public void AddWarning(string message) => Add(SceneSetupIssueSeverity.Warning, message);
        public void AddError(string message) => Add(SceneSetupIssueSeverity.Error, message);

        private void Add(SceneSetupIssueSeverity severity, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                _issues.Add(new SceneSetupIssue(severity, message));
        }
    }

    internal readonly struct SceneSetupIssue
    {
        public SceneSetupIssueSeverity Severity { get; }
        public string Message { get; }

        public SceneSetupIssue(SceneSetupIssueSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    internal enum SceneSetupIssueSeverity
    {
        Info,
        Warning,
        Error
    }
}
