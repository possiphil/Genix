using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Layouts;

namespace Genix.Editor.Layouts
{
    /// <summary>Coordinates loading, saving, applying, and deleting generated layouts in the editor.</summary>
    public static class LayoutWorkflow
    {
        /// <summary>Loads all saved layouts in deterministic display order.</summary>
        public static SavedLayout[] LoadLayouts() => LayoutRepository.LoadAll();

        /// <summary>Loads layouts associated with the specified target area.</summary>
        public static SavedLayout[] LoadLayoutsForArea(IAreaSource areaSource) =>
            LayoutRepository.LoadForArea(areaSource);

        /// <summary>Loads layouts associated with the current scene.</summary>
        public static SavedLayout[] LoadLayoutsForCurrentScene() =>
            LayoutRepository.LoadForCurrentScene();

        /// <summary>Determines whether a layout was captured for the specified target area.</summary>
        public static bool MatchesArea(SavedLayout layout, IAreaSource areaSource) =>
            LayoutRepository.MatchesArea(layout, areaSource);

        /// <summary>Determines whether a layout belongs to the current scene.</summary>
        public static bool MatchesCurrentScene(SavedLayout layout) =>
            LayoutRepository.MatchesCurrentScene(layout);

        /// <summary>Captures and persists the currently generated hierarchy as a layout.</summary>
        public static bool SaveCurrentLayout(
            IAreaSource areaSource,
            PlacementTarget placementTargets,
            TargetDistributionMode distributionMode,
            TargetDistributionWeights distributionWeights,
            AssetPool assetPool,
            string styleName,
            out SavedLayout layout,
            out string error) =>
            LayoutCaptureService.Save(
                areaSource,
                placementTargets,
                distributionMode,
                distributionWeights,
                assetPool,
                styleName,
                out layout,
                out error);

        /// <summary>Instantiates the selected saved layout in the scene.</summary>
        public static bool ApplyLayout(SavedLayout layout, IAreaSource areaSource, out string error) =>
            LayoutApplyService.Apply(layout, areaSource, out error);

        /// <summary>Loads a saved layout into the selected preview slot.</summary>
        public static bool PreviewLayout(SavedLayout layout, LayoutPreviewSlot slot, out string error) =>
            LayoutPreviewService.Show(layout, slot, out error);

        /// <summary>Clears preview.</summary>
        public static void ClearPreview() => LayoutPreviewService.ClearAll();

        /// <summary>Clears preview.</summary>
        public static void ClearPreview(LayoutPreviewSlot slot) => LayoutPreviewService.Clear(slot);

        /// <summary>Deletes a saved layout from the project.</summary>
        public static bool DeleteLayout(SavedLayout layout, out string error) =>
            LayoutRepository.Delete(layout, out error);

        /// <summary>Clears layouts.</summary>
        public static bool ClearLayouts(out int deletedCount, out string error) =>
            LayoutRepository.ClearUnlocked(out deletedCount, out error);

        /// <summary>Clears layouts for area.</summary>
        public static bool ClearLayoutsForArea(IAreaSource areaSource, out int deletedCount, out string error) =>
            LayoutRepository.ClearUnlockedForArea(areaSource, out deletedCount, out error);
    }
}
