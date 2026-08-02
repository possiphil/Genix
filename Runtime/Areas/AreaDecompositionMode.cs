namespace Genix.Areas
{
    /// <summary>Controls how voxel boundary layers become rectangular surface regions.</summary>
    public enum AreaDecompositionMode
    {
        /// <summary>Merges each layer into broad bounds, favoring build speed over irregular outlines.</summary>
        Fast,
        /// <summary>Preserves occupied-cell outlines and holes through tighter rectangular regions.</summary>
        Precise
    }
}
