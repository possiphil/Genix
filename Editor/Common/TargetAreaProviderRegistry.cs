using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Genix.Editor.TargetAreas
{
    public static class TargetAreaProviderRegistry
    {
        public static IReadOnlyList<ITargetAreaProvider> CreateProviders()
        {
            return TypeCache.GetTypesDerivedFrom<ITargetAreaProvider>()
                .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                .Select(CreateProvider)
                .Where(provider => provider != null)
                .OrderByDescending(provider => provider.Priority)
                .ThenBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static ITargetAreaProvider CreateProvider(Type type)
        {
            try
            {
                return Activator.CreateInstance(type) as ITargetAreaProvider;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"Genix could not initialize target area provider '{type.FullName}': {exception.Message}");
                return null;
            }
        }
    }
}
