using System.Globalization;

namespace Genix.Extensions
{
    public static class EnumDisplayNameExtensions
    {
        public static string ToDisplayName(this System.Enum value)
        {
            string text = value.ToString().ToLowerInvariant().Replace("_", " ");
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
        }
    }
}
