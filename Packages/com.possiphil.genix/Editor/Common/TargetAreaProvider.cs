using Genix.Areas;
using Genix.Assets;
using UnityEngine;

namespace Genix.Editor.TargetAreas
{
    /// <summary>Describes an editor integration that exposes target areas and location metadata to Genix.</summary>
    public interface ITargetAreaProvider
    {
        /// <summary>Gets the stable identifier used to preserve the selected provider.</summary>
        string Id { get; }
        /// <summary>Gets the designer-facing provider name.</summary>
        string DisplayName { get; }
        /// <summary>Gets the ordering priority; higher-priority providers appear first.</summary>
        int Priority { get; }

        /// <summary>Creates the stateful selector drawn in the generator window.</summary>
        ITargetAreaSelector CreateSelector();
        /// <summary>Creates the optional content-window panel used to edit location semantics.</summary>
        ILocationPanel CreateLocationPanel();
    }

    /// <summary>Maintains a target-area selection and converts it into a runtime area source.</summary>
    public interface ITargetAreaSelector
    {
        /// <summary>Refreshes available targets while preserving the current stable selection when possible.</summary>
        void Refresh();
        /// <summary>Draws the provider-specific target field with the supplied label and tooltip.</summary>
        void Draw(GUIContent label);
        /// <summary>Creates an area source for the current selection, or returns null when none is valid.</summary>
        IAreaSource CreateAreaSource();
    }

    /// <summary>Optionally separates target-area feedback from the selector field.</summary>
    public interface ITargetAreaSelectorStatus
    {
        /// <summary>Draws warnings or guidance for the current target-area state.</summary>
        void DrawStatus();
    }

    /// <summary>Draws integration-specific semantic controls in the Genix content window.</summary>
    public interface ILocationPanel
    {
        /// <summary>Gets the panel heading.</summary>
        string Title { get; }
        /// <summary>Draws location controls using the current asset catalog.</summary>
        void Draw(AssetCatalog catalog);
    }
}
