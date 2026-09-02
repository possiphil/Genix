using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Genix.Editor.TargetAreas
{
    /// <summary>Stable target identifier and display name exposed to automated benchmark runs.</summary>
    public readonly struct BenchmarkAreaTarget
    {
        /// <summary>Gets the provider-specific stable target identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the target name shown in benchmark configuration.</summary>
        public string DisplayName { get; }

        /// <summary>Creates a benchmark target descriptor.</summary>
        public BenchmarkAreaTarget(string id, string displayName)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }
    }

    /// <summary>Resolves stable benchmark target identifiers after a benchmark scene has been opened.</summary>
    public interface IBenchmarkAreaResolver
    {
        /// <summary>Gets the identifier shared with the corresponding target-area provider.</summary>
        string ProviderId { get; }
        /// <summary>Gets the provider name shown in benchmark configuration.</summary>
        string DisplayName { get; }
        /// <summary>Returns all valid targets owned by the supplied loaded scene.</summary>
        IReadOnlyList<BenchmarkAreaTarget> FindTargets(Scene scene);
        /// <summary>Resolves one stable target identifier into a runtime area source.</summary>
        IAreaSource Resolve(Scene scene, string targetId);
    }

    /// <summary>
    /// Optionally prepares authoritative provider data after a scene load and before benchmark or evaluation runs.
    /// Preparation is deliberately outside the measured generation operation.
    /// </summary>
    public interface IBenchmarkAreaPreparer
    {
        /// <summary>
        /// Ensures that the loaded scene contains authoritative spatial data for subsequent target resolution.
        /// </summary>
        bool Prepare(Scene scene, out string error);
    }

    /// <summary>Discovers installed benchmark target resolvers without coupling the runner to one spatial system.</summary>
    public static class BenchmarkAreaResolverRegistry
    {
        /// <summary>Creates all available resolvers in stable display order.</summary>
        public static IReadOnlyList<IBenchmarkAreaResolver> CreateResolvers()
        {
            List<IBenchmarkAreaResolver> resolvers = new();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<IBenchmarkAreaResolver>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                try
                {
                    if (Activator.CreateInstance(type) is IBenchmarkAreaResolver resolver)
                        resolvers.Add(resolver);
                }
                catch
                {
                    // A broken optional integration must not prevent other benchmark providers from loading.
                }
            }

            return resolvers
                .GroupBy(resolver => resolver.ProviderId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(resolver => resolver.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
