using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Editor.TargetAreas;
using UnityEngine;
using UnityEngine.SceneManagement;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Resolves Space Foundation locations for scene-based Genix benchmarks.</summary>
    public sealed class SfsBenchmarkAreaResolver : IBenchmarkAreaResolver
    {
        /// <inheritdoc />
        public string ProviderId => "space-foundation";
        /// <inheritdoc />
        public string DisplayName => "Space Foundation";

        /// <inheritdoc />
        public IReadOnlyList<BenchmarkAreaTarget> FindTargets(Scene scene) =>
            FindSpaces(scene)
                .Select(space => new BenchmarkAreaTarget(
                    space.anchor.GetUniqueId(),
                    AreaName.ToDesignerName(space.name)))
                .OrderBy(target => target.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        /// <inheritdoc />
        public IAreaSource Resolve(Scene scene, string targetId)
        {
            SfsSpace[] spaces = FindSpaces(scene);
            SfsSpace selected = string.IsNullOrWhiteSpace(targetId) && spaces.Length == 1
                ? spaces[0]
                : spaces.FirstOrDefault(space =>
                    string.Equals(space.anchor.GetUniqueId(), targetId, StringComparison.Ordinal));
            return selected ? new SfsAreaSource(selected) : null;
        }

        private static SfsSpace[] FindSpaces(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return Array.Empty<SfsSpace>();

            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SfsSpace>(true))
                .Where(space => space && space.anchor)
                .Distinct()
                .ToArray();
        }
    }
}
