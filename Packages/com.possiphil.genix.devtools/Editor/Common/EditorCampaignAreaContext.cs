using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Editor.TargetAreas;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Common
{
    /// <summary>
    /// Resolves and retains the target-area state shared by automated editor campaigns.
    /// </summary>
    /// <remarks>
    /// Provider preparation is performed at most once for each loaded scene, while target resolution
    /// can be repeated between runs after generated content has been cleared.
    /// </remarks>
    internal sealed class EditorCampaignAreaContext
    {
        private bool _scenePrepared;

        /// <summary>Gets the currently resolved runtime area.</summary>
        public IAreaSource AreaSource { get; private set; }

        /// <summary>Gets the provider-specific identifier of the resolved target.</summary>
        public string TargetId { get; private set; } = string.Empty;

        /// <summary>Starts tracking a newly loaded scene.</summary>
        public void BeginScene()
        {
            _scenePrepared = false;
            ClearTarget();
        }

        /// <summary>Clears the current target while retaining prepared scene data.</summary>
        public void ClearTarget()
        {
            AreaSource = null;
            TargetId = string.Empty;
        }

        /// <summary>Prepares the provider when needed and resolves one configured target.</summary>
        public void Resolve(
            Scene scene,
            string providerId,
            string requestedTargetId,
            string scenarioName,
            Action<string> reportStatus = null)
        {
            IBenchmarkAreaResolver resolver = BenchmarkAreaResolverRegistry.CreateResolvers()
                .FirstOrDefault(candidate => candidate.ProviderId == providerId);

            if (!_scenePrepared && resolver is IBenchmarkAreaPreparer preparer)
            {
                reportStatus?.Invoke($"Preparing authoritative spatial data for {scenarioName}");
                if (!preparer.Prepare(scene, out string preparationError))
                    throw new InvalidOperationException(preparationError);

                _scenePrepared = true;
            }

            IReadOnlyList<BenchmarkAreaTarget> targets = resolver?.FindTargets(scene) ??
                Array.Empty<BenchmarkAreaTarget>();

            if (targets.Count == 0)
                throw new InvalidOperationException(
                    $"Scene '{scene.name}' has no targets for provider '{providerId}'.");

            TargetId = string.IsNullOrWhiteSpace(requestedTargetId) && targets.Count == 1
                ? targets[0].Id
                : requestedTargetId;
            AreaSource = resolver.Resolve(scene, TargetId);

            if (AreaSource != null)
                return;

            string available = string.Join(", ", targets.Select(target =>
                $"{target.DisplayName} [{target.Id}]"));
            throw new InvalidOperationException(
                $"Target '{requestedTargetId}' could not be resolved in scene '{scene.name}'. " +
                $"Available targets: {available}.");
        }
    }
}
