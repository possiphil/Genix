# Getting started

## Set up the editable starter room

In a project without Genix assets, pools, or styles, open **Tools > Genix > Generator** or
**Tools > Genix > Content** and choose **Set Up Starter Content**. Genix creates an editable SFS room,
a compact semantic taxonomy, five general-purpose styles, neutral example prefabs, helper pools, and
the **Starter Room** generation preset under `Assets/Genix`.

The room demonstrates a fixed desk as both a semantic anchor and a Desktop support surface. The
generated monitor, keyboard, mouse, mug, and chair exercise asset relations; a cargo box, warning
sign, and ceiling light cover independent floor, wall, and ceiling placement. The preset deliberately
uses a new seed for each run. Enable **Fixed Seed** only when a reproducible comparison is required.

Starter Content is regular project content rather than a read-only package sample. Designers can
inspect, rename, replace, or delete every generated asset after setup.

## Prerequisites

1. Open a Unity project that contains Genix and Space Foundation System. In a new project, open
   **Window > Asset Management > Addressables > Groups** once so SFS can resolve its Addressables settings.
2. Create or compute an SFS space and expose it through a target-area anchor.
3. Put physical placement surfaces on layers that can be selected in Genix.
4. Create Genix asset definitions and assign them to an asset pool.
5. Create or select a style preset.

## Create a first preview

1. Open **Tools > Genix > Generator**.
2. Leave the shared **Advanced** toggle disabled for the first run.
3. Select the SFS **Target Area**.
4. Choose Floor, Wall, Ceiling, or Inside Space under **Placement Targets**.
5. Select an **Asset Pool** and **Generation Style**.
6. Set **Object Count** and choose **Preview**.
7. Inspect accepted positions and rejection summaries in Genix Diagnostics.
8. Choose **Apply Preview** to instantiate the exact accepted plan.

Enable **Advanced** when the scene needs a non-default surface source, target or support distribution, relative placement, deterministic seed, search-budget tuning, or detailed diagnostics. The toggle is shared by the designer windows and never changes stored settings.

## Surface setup

Floor, wall, and ceiling assets require matching physical surfaces unless **SFS Boundaries** is selected. Assign those colliders to the corresponding Floor, Wall, or Ceiling layer masks. A surface is classified by its normal and the **Floor Slope Limit** and **Ceiling Slope Limit** thresholds.

Inside Space assets use valid volume cells and do not require a surface collider. They still undergo height, volume, overlap, clearance, and relative-placement checks.

## Reproducible runs

Enable **Fixed Seed** to repeat the same random sequence while the request, scene, area data, and assets remain unchanged. A fixed seed also permits candidate-cache reuse. Changing geometry or relevant settings may invalidate other caches even when the seed remains fixed.

## Reuse a generation configuration

Use **Generation Preset > Save as New** in the Advanced view to capture the current asset pool, style, object count,
placement targets, target distribution, surface settings, relative-placement settings, seed, and
Allow Partial Results choice. Selecting a preset immediately restores those values. **Update** overwrites the
selected preset, while **Revert** discards local changes.

The Generator remembers the last selected preset per project and editor user. Choosing `Custom`
clears that remembered selection.

Target Area is intentionally not captured, so one preset can be reused across evaluation scenes.
Detailed Diagnostics is also excluded because it is a run-specific debugging control that can add
memory overhead.
