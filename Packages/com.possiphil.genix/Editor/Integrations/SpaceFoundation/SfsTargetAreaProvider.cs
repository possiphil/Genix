using Genix.Editor.TargetAreas;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Registers Space Foundation System spaces as selectable Genix target areas.</summary>
    public sealed class SfsTargetAreaProvider : ITargetAreaProvider
    {
        /// <summary>Gets id.</summary>
        public string Id => "space-foundation";
        /// <summary>Gets display name.</summary>
        public string DisplayName => "Space Foundation";
        /// <summary>Gets priority.</summary>
        public int Priority => 100;

        /// <summary>Creates selector.</summary>
        public ITargetAreaSelector CreateSelector() => new SfsLocationSelector();

        /// <summary>Creates location panel.</summary>
        public ILocationPanel CreateLocationPanel() => new LocationPanel();
    }
}
