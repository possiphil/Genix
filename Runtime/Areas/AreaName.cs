using System;

namespace Genix.Areas
{
    public static class AreaName
    {
        private const string SubspacePrefix = "Subspace of ";

        public static string ToDesignerName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "Target Area";

            string name = rawName.Trim();

            if (name.StartsWith(SubspacePrefix, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(SubspacePrefix.Length).Trim();

            return string.IsNullOrWhiteSpace(name) ? "Target Area" : name;
        }

        public static string ToUnitySafeDisplayName(string rawName)
        {
            return ToDesignerName(rawName).Replace("/", "\u2215");
        }
    }
}