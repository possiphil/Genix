using System.Globalization;
using System.Text;

namespace Genix.Extensions
{
    /// <summary>Provides extension methods for enum display name.</summary>
    public static class EnumDisplayNameExtensions
    {
        /// <summary>Converts an enum value to a human-readable label.</summary>
        public static string ToDisplayName(this System.Enum value)
        {
            string raw = value.ToString();
            StringBuilder words = new(raw.Length + 4);

            for (int i = 0; i < raw.Length; i++)
            {
                char current = raw[i];

                if (current == '_')
                {
                    if (words.Length > 0 && words[^1] != ' ')
                        words.Append(' ');

                    continue;
                }

                bool startsWord = i > 0 &&
                                  char.IsUpper(current) &&
                                  (char.IsLower(raw[i - 1]) ||
                                   char.IsDigit(raw[i - 1]) ||
                                   i + 1 < raw.Length && char.IsLower(raw[i + 1]) && char.IsUpper(raw[i - 1]));

                if (startsWord && words.Length > 0 && words[^1] != ' ')
                    words.Append(' ');

                words.Append(current);
            }

            string text = words.ToString().Trim().ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
        }
    }
}
