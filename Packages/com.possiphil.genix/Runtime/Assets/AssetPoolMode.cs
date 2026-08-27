namespace Genix.Assets
{
    /// <summary>Determines whether a pool stores assets or resolves them from catalog filters.</summary>
    public enum AssetPoolMode
    {
        /// <summary>Uses an explicit curated list.</summary>
        Static,
        /// <summary>Resolves current catalog assets using placement, orientation, and semantic filters.</summary>
        Dynamic
    }
}
