using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Layouts;

namespace Genix.Editor.Layouts
{
    public static class LayoutWorkflow
    {
        public static SavedLayout[] LoadLayouts() => LayoutRepository.LoadAll();

        public static SavedLayout[] LoadLayoutsForArea(IAreaSource areaSource) =>
            LayoutRepository.LoadForArea(areaSource);

        public static bool MatchesArea(SavedLayout layout, IAreaSource areaSource) =>
            LayoutRepository.MatchesArea(layout, areaSource);

        public static bool SaveCurrentLayout(
            IAreaSource areaSource,
            GenerationMode generationMode,
            PlacementTarget placementTargets,
            TargetDistributionMode distributionMode,
            TargetDistributionWeights distributionWeights,
            AssetPool assetPool,
            string styleName,
            out SavedLayout layout,
            out string error) =>
            LayoutCaptureService.Save(
                areaSource,
                generationMode,
                placementTargets,
                distributionMode,
                distributionWeights,
                assetPool,
                styleName,
                out layout,
                out error);

        public static bool ApplyLayout(SavedLayout layout, IAreaSource areaSource, out string error) =>
            LayoutApplyService.Apply(layout, areaSource, out error);

        public static bool PreviewLayout(SavedLayout layout, LayoutPreviewSlot slot, out string error) =>
            LayoutPreviewService.Show(layout, slot, out error);

        public static void ClearPreview() => LayoutPreviewService.ClearAll();

        public static void ClearPreview(LayoutPreviewSlot slot) => LayoutPreviewService.Clear(slot);

        public static bool DeleteLayout(SavedLayout layout, out string error) =>
            LayoutRepository.Delete(layout, out error);

        public static bool ClearLayouts(out int deletedCount, out string error) =>
            LayoutRepository.ClearUnlocked(out deletedCount, out error);
    }
}
