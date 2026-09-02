# Designer interface guide

Genix keeps the normal authoring path visible and moves specialist controls behind contextual foldouts. The windows share Unity's selection, Inspector, Undo, object picker, and docking conventions, so a designer can move between Genix and the rest of the Editor without learning a separate interaction model.

## Common interaction model

- Open designer tools through **Tools > Genix**. Genix docks beside related tabs when possible and falls back to a consistent Genix tab group.
- **Advanced** is a shared presentation preference. It reveals specialist controls in Generator, Space Setup, and relevant Content tabs without changing or resetting hidden values.
- Bold section headers identify a responsibility, not a separate asset. Foldout headers reveal related advanced controls across the available width.
- A control's tooltip explains its effect, units, and important trade-offs. Validation messages appear near the affected workflow and state what needs to change.
- Names that cannot fit are shortened with an ellipsis. Hover the label or list row to read the complete value.
- Disabled actions remain visible when their position helps explain the workflow. Their tooltip states what is missing.
- Buttons ending in an ellipsis open a confirmation or another decision. Destructive actions require confirmation.
- Authoring operations participate in Unity Undo. One **Generate**, **Regenerate**, layout application, or multi-object authoring command is one Undo step.

## Generator

Open **Tools > Genix > Generator** for the main placement workflow.

1. Select a **Generation Preset** or keep **Custom**.
2. Select a **Target Area**, **Asset Pool**, object count, placement targets, and generation style.
3. Choose **Preview** to inspect a plan without instantiating prefabs, then **Apply Preview** to apply that exact plan. Choose **Generate** to add a new batch immediately.
4. Use **Regenerate** to replace all generated objects owned by the selected target area. It remains disabled until that area contains generated output.

The four primary actions use a stable two-column layout: Preview and Apply Preview form one workflow; Generate and Regenerate form the other. **More** contains infrequent commands such as saving a layout, clearing the last run, and deleting generated objects from the selected area.

With **Advanced** enabled, preset editing actions appear in a reserved second row so the content below does not move when the preference changes. Advanced Settings are grouped as follows:

- **Surface Search** controls the spatial source, physical layers, voxel-boundary detail, and slope classification.
- **Distribution** controls allocation across placement targets and semantic support surfaces.
- **Global Proximity** keeps every placement near generated, selected, or scene objects.
- **Run and Reproducibility** contains partial-result policy, fixed seeds, and detailed diagnostics.

Support-distribution rules express either a whole-object count or its equivalent share. Editing one updates the other using nearest-integer rounding with halves rounded up. **All Unlisted Surfaces** is the remaining budget and therefore has no independent input.

## Content

Open **Tools > Genix > Content** to author and browse reusable data. Wide windows show tabs; narrow windows replace only the tabs with one compact dropdown.

### Tags

Categories define semantic dimensions and whether one or several values may be selected. Selecting a category filters the tag list. **Show All** clears that filter; it remains visible but disabled while every category is already shown. **New** creates an editable item with a unique default name and selects it immediately, avoiding a detached setup dialog.

### Pools

Manual pools contain an explicit asset list. Rule-based pools resolve catalog assets from placement, orientation, and semantic filters. Pool-level count rules and per-anchor groups appear when **Advanced** is enabled. Membership is edited here; the Assets tab focuses on the selected asset itself.

### Target Areas

This tab delegates to the installed spatial integration. With SFS, it exposes location names and semantic tags for the selected area while retaining the provider's own validation and selection behavior.

### Assets

The list supports search, placement-type and orientation filters, and sorting. The selected Asset Definition is edited in grouped sections ordered from identity and placement behavior to fit, bounds, support semantics, and optional relationships. Controls that apply only to Wall, Floor, Ceiling, or Inside Space appear only for the relevant placement type.

Advanced groups collect geometry overrides, support restrictions, spacing and capacity, object relationships, and path relationships. **Update from Prefab** recomputes placement bounds; **Start from Placement Bounds** initializes reserved clearance from those bounds before further editing.

### Layouts

Layouts are indexed by lightweight metadata and loaded only when selected, which keeps the first visit and subsequent filtering responsive even with many saved layouts. Search can match name, notes, scene, or target area. Scope narrows results to the current scene, current target area, or the complete project.

The selected layout shows its summary and an asset-composition table below the preview image. **Preview Layout** creates a temporary scene visualization; **Apply Layout** instantiates the saved arrangement. **Delete** remains visible but disabled for protected layouts, with the reason in its tooltip. Names, notes, favorite state, and deletion protection are edited in the details panel.

### Scene Setup

Scene Setup inventories placement surfaces, fixed relation anchors, paths, and exclusion regions in loaded scenes. Search, type, and **Needs Attention** filters support focused cleanup. Selecting a complete row selects its scene object and opens the appropriate editor below the list.

Use the row-level add actions for the normal workflow. **Actions** contains batch configuration, authoring-guide visibility, and multi-selection commands. Validation status appears in its own compact column; hover it or select the row for the complete message.

## Space Setup

Open **Tools > Genix > Space Setup** to create voxel-aligned SFS inputs.

- **Space Foundation** selects or creates the owning grid and keeps **Check Scene** close to that context.
- **Quick Add** creates an individual anchor or box delimiter.
- **Location Setup** creates a bounded location, aligned location grid, or extruded footprint.
- **Create Location** leaves editable scene objects. **Create and Compute** also validates and invokes SFS graph computation.

With **Advanced** disabled, Space Setup uses world units or fitted selection bounds. Enabling it adds exact voxel counts, aligned actual position and size, per-axis grid sizing, anchor-range overrides, and the Scene preview. Requested values remain visible directly beside their voxel-aligned result so rounding is easy to understand.

## Diagnostics

Open **Tools > Genix > Diagnostics** to inspect saved generation reports. Select anywhere on a report row to open it. The default report view prioritizes completion, placed objects, and the main placement issue in designer-facing language.

Enable **Technical Details** in the Report Details header for recorded configuration, candidate and asset-attempt counts, exact rejection reasons, bounds, and Scene view overlays. The amount of available information depends on whether **Detailed Diagnostics** was enabled for the generation run.

**Delete Selected** removes the current report. Its adjacent menu contains deletion by report type and deletion of all reports. Summary and detailed reports use the same list because a detailed report already includes its summary.

## Context commands

Genix adds focused commands where Unity users expect them:

- **Assets > Genix > Create Asset Definition From Prefab** creates definitions from selected prefab assets.
- **GameObject > Genix** creates relation anchors, placement surfaces, support surfaces, exclusion regions, and Space Foundation elements from the current scene selection.
- Custom Inspectors expose the same terminology and validation rules as the Content and Generator windows.

The [settings reference](reference/settings.md) defines individual fields and algorithms. The [troubleshooting guide](troubleshooting.md) starts from observable symptoms when a configured run does not behave as expected.
