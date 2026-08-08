# Getting started

## Prerequisites

1. Open a Unity project that contains Genix and Space Foundation System.
2. Create or compute an SFS space and expose it through a target-area anchor.
3. Put physical placement surfaces on layers that can be selected in Genix.
4. Create Genix asset definitions and assign them to an asset pool.
5. Create or select a style preset.

## Create a first preview

1. Open **Tools > Genix > Generator**.
2. Select the SFS **Target Area**.
3. Keep **Surface Source** set to **All Matching Surfaces** for the general case.
4. Choose Floor, Wall, Ceiling, or Inside Space under **Placement Targets**.
5. Select an **Asset Pool** and **Style Preset**.
6. Set **Object Count** and choose **Preview Run**.
7. Inspect accepted positions and rejection summaries in Genix Diagnostics.
8. Choose **Apply Preview** to instantiate the exact accepted plan.

## Surface setup

Floor, wall, and ceiling assets require matching physical surfaces unless **SFS Boundaries** is selected. Assign those colliders to the corresponding Floor, Wall, or Ceiling layer masks. A surface is classified by its normal and the Floor/Ceiling Angle thresholds.

Inside Space assets use valid volume cells and do not require a surface collider. They still undergo height, volume, overlap, clearance, and relative-placement checks.

## Reproducible runs

Enable **Use Seed** to repeat the same random sequence while the request, scene, area data, and assets remain unchanged. A fixed seed also permits candidate-cache reuse. Changing geometry or relevant settings may invalidate other caches even when the seed remains fixed.

## Reuse a generation configuration

Use **Generation Preset > Save New** to capture the current asset pool, style, object count,
placement targets, target distribution, surface settings, relative-placement settings, seed, and
Best Effort choice. Selecting a preset immediately restores those values. **Update** overwrites the
selected preset, while **Reload** discards local changes.

Enable **Default on Startup** to load the selected preset whenever the Generator window is created
after a Unity or domain reload. The default is stored per project and per editor user.

Target Area is intentionally not captured, so one preset can be reused across evaluation scenes.
Profile Run and Detailed Diagnostics are also excluded because they are measurement and debugging
controls that can add runtime or memory overhead.
