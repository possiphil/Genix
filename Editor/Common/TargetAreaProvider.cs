using Genix.Areas;
using Genix.Assets;

namespace Genix.Editor.TargetAreas
{
    public interface ITargetAreaProvider
    {
        string Id { get; }
        string DisplayName { get; }
        int Priority { get; }

        ITargetAreaSelector CreateSelector();
        ILocationPanel CreateLocationPanel();
    }

    public interface ITargetAreaSelector
    {
        void Refresh();
        void Draw(string label);
        IAreaSource CreateAreaSource();
    }

    public interface ILocationPanel
    {
        string Title { get; }
        void Draw(AssetCatalog catalog);
    }
}
