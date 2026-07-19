using Genix.Editor.TargetAreas;

namespace Genix.SpaceFoundation.Editor
{
    public sealed class SfsTargetAreaProvider : ITargetAreaProvider
    {
        public string Id => "space-foundation";
        public string DisplayName => "Space Foundation";
        public int Priority => 100;

        public ITargetAreaSelector CreateSelector() => new SfsLocationSelector();

        public ILocationPanel CreateLocationPanel() => new LocationPanel();
    }
}
