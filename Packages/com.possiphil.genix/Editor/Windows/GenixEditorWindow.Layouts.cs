using Genix.Areas;
using Genix.Editor.Layouts;
using Genix.Layouts;
using UnityEngine;

namespace Genix.Editor.Windows
{
    public sealed partial class GenixEditorWindow
    {
        private void SaveCurrentLayout()
        {
            IAreaSource areaSource = CreateAreaSource();

            if (!LayoutWorkflow.SaveCurrentLayout(
                    areaSource,
                    GetEffectivePlacementTargets(),
                    GetEffectiveTargetDistributionMode(),
                    GetEffectiveTargetDistributionWeights(),
                    _assetPool,
                    _selectedStylePreset ? _selectedStylePreset.name : string.Empty,
                    out SavedLayout layout,
                    out string error))
            {
                Debug.LogWarning(error);
                return;
            }

            Debug.Log($"Saved Genix layout '{layout.DisplayName}'.");
        }
    }
}
