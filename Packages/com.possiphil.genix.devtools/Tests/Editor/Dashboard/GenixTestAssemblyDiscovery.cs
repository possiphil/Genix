using System;
using System.Linq;

namespace Genix.Tests.Dashboard
{
    internal static class GenixTestAssemblyDiscovery
    {
        private const string AssemblyPrefix = "Genix.Tests.";

        public static string[] GetLoadedAssemblyNames() => AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetName().Name)
            .Where(name => !string.IsNullOrWhiteSpace(name) &&
                           name.StartsWith(AssemblyPrefix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }
}
