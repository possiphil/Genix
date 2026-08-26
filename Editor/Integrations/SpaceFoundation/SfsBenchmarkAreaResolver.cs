using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Editor.TargetAreas;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SfsFoundation = SpaceFoundationSystem.SpaceFoundation;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Resolves Space Foundation locations for scene-based Genix benchmarks.</summary>
    public sealed class SfsBenchmarkAreaResolver : IBenchmarkAreaResolver, IBenchmarkAreaPreparer
    {
        /// <inheritdoc />
        public string ProviderId => "space-foundation";
        /// <inheritdoc />
        public string DisplayName => "Space Foundation";

        /// <inheritdoc />
        public bool Prepare(Scene scene, out string error)
        {
            error = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "The evaluation scene is not loaded.";
                return false;
            }

            SfsFoundation[] foundations = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SfsFoundation>(true))
                .Where(foundation => foundation)
                .Distinct()
                .ToArray();
            if (foundations.Length != 1)
            {
                error = $"Expected exactly one Space Foundation in scene '{scene.name}', found {foundations.Length}.";
                return false;
            }

            SfsAreaCache.Clear();
            PersistentSubspaceCache.Clear();

            try
            {
                SpaceFoundationSystem.SpaceFoundationBackend.ClearData();
                SpaceFoundationSystem.SpaceFoundationBackend.Compute();
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                error = $"Space Foundation preparation failed: {exception.Message}";
                return false;
            }

            if (FindSpaces(scene).Length == 0)
            {
                error = "Space Foundation compute completed without producing a target space.";
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public IReadOnlyList<BenchmarkAreaTarget> FindTargets(Scene scene) =>
            FindSpaces(scene)
                .Select(space => new BenchmarkAreaTarget(
                    GetStableTargetId(space),
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
                    string.Equals(GetStableTargetId(space), targetId, StringComparison.Ordinal));
            return selected ? new SfsAreaSource(selected) : null;
        }

        private static string GetStableTargetId(SfsSpace space)
        {
            if (!space || !space.anchor)
                return string.Empty;

            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(space.anchor);
            return id.identifierType != 0 && id.targetObjectId != 0
                ? id.ToString()
                : space.anchor.GetUniqueId();
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
