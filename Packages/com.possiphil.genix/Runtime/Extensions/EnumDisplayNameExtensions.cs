using System.Globalization;
using System.Text;
using Genix.Placement;

namespace Genix.Extensions
{
    /// <summary>Provides extension methods for enum display name.</summary>
    public static class EnumDisplayNameExtensions
    {
        /// <summary>Converts a placement rejection reason to a concise designer-facing label.</summary>
        public static string ToDisplayName(this RejectionReason value) =>
            value switch
            {
                RejectionReason.OutsideTargetArea => "Outside Target Surface",
                RejectionReason.OverlapsGenerated => "Overlaps Generated Object",
                RejectionReason.OverlapsFixed => "Overlaps Scene Object",
                RejectionReason.TooCloseToGenerated => "Too Close to Generated Object",
                RejectionReason.TooCloseToFixed => "Too Close to Scene Object",
                RejectionReason.OutsideRelativeRadius => "Outside Proximity Range",
                RejectionReason.UnsupportedSupportSurface => "Support Tags Do Not Match",
                RejectionReason.SurfaceRejectsAsset => "Surface Does Not Allow Asset",
                RejectionReason.SupportCapacityReached => "Surface Capacity Reached",
                RejectionReason.SupportAssetCapacityReached => "Surface Asset Limit Reached",
                RejectionReason.MissingSupportDirection => "Support Direction Missing",
                RejectionReason.TooFarFromWall => "Too Far from Wall",
                RejectionReason.TooCloseToWall => "Too Close to Wall",
                RejectionReason.MissingWallReference => "Wall Reference Missing",
                RejectionReason.AssetSpacingViolation => "Asset Spacing Conflict",
                RejectionReason.MissingAssetRelationAnchor => "Relation Anchor Missing",
                RejectionReason.OutsideAssetRelationRange => "Outside Relation Range",
                RejectionReason.WrongAssetRelationSide => "Wrong Side of Relation Anchor",
                RejectionReason.DifferentAssetRelationSupportSurface => "Different Relation Support Surface",
                RejectionReason.AssetRelationAnchorCapacityReached => "Anchor Capacity Reached",
                RejectionReason.AssetRelationGroupCapacityReached => "Relation Group Capacity Reached",
                RejectionReason.OutsideAssetRelationBounds => "Outside Relation Anchor Bounds",
                RejectionReason.MissingPathReference => "Path Reference Missing",
                RejectionReason.OutsidePathDistance => "Outside Path Distance Range",
                RejectionReason.WrongPathSide => "Wrong Side of Path",
                RejectionReason.TooCloseToPathEndpoint => "Too Close to Path End",
                _ => ToDisplayName((System.Enum)value)
            };

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
