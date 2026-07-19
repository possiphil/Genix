using System;
using System.Collections.Generic;
using System.Text;
using Genix.Assets;
using UnityEngine;

namespace Genix.Editor.Generation
{
    internal sealed class GeneratedObjectNamer
    {
        private const string Prefix = "GenixGenerated";
        private readonly HashSet<string> _usedNames = new(StringComparer.Ordinal);

        public GeneratedObjectNamer(Transform parent)
        {
            if (!parent)
                return;

            foreach (Transform child in parent)
                _usedNames.Add(child.name);
        }

        public string Next(AssetDefinition asset)
        {
            string baseName = ToPascalCase(asset ? asset.AssetName : null);
            int index = 1;
            string candidate;

            do
            {
                candidate = $"{Prefix}{baseName}{index++}";
            }
            while (!_usedNames.Add(candidate));

            return candidate;
        }

        private static string ToPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Object";

            string clean = value.Trim();

            if (clean.StartsWith("Genix_", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring("Genix_".Length);

            string[] parts = clean.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return "Object";

            StringBuilder result = new();

            foreach (string part in parts)
            {
                result.Append(char.ToUpperInvariant(part[0]));

                if (part.Length > 1)
                    result.Append(part, 1, part.Length - 1);
            }

            return result.ToString();
        }
    }
}
