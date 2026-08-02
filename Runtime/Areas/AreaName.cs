using System;

namespace Genix.Areas
{
    /// <summary>Converts internal spatial-area names into labels suitable for designer-facing UI.</summary>
    public static class AreaName
    {
        private const string SubspacePrefix = "Subspace of ";

        /// <summary>Converts an internal area name to a designer-facing label.</summary>
        public static string ToDesignerName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "Target Area";

            string name = rawName.Trim();

            if (name.StartsWith(SubspacePrefix, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(SubspacePrefix.Length).Trim();

            return string.IsNullOrWhiteSpace(name) ? "Target Area" : name;
        }

        /// <summary>Converts a name to a Unity-safe hierarchy label.</summary>
        public static string ToUnitySafeDisplayName(string rawName)
        {
            return ToDesignerName(rawName).Replace("/", "\u2215");
        }
    }
}
