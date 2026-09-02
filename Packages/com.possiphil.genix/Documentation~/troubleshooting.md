# Troubleshooting

## No assets are placed

1. Confirm the asset pool contains assets with prefabs.
2. Confirm asset placement types match the selected placement targets.
3. Check semantic tags on both the target area and asset definitions.
4. For Floor, Wall, or Ceiling, confirm the collider layer is included in the matching surface-layer mask.
5. Inspect the top rejection reason in Diagnostics.

## Only boundary objects appear

Use **All Matching Surfaces** when placements should reach interior geometry or floors at arbitrary heights. Boundary modes intentionally restrict discovery to SFS-derived regions.

## Objects form an unexpected pattern

Check the style algorithm. Cluster sampling intentionally groups candidates. Grid and Jittered Grid preserve grid structure. Use Bridson Poisson Disk for even organic spacing and verify that its minimum distance is appropriate for the area size.

## A run is unexpectedly slow

1. Disable Detailed Diagnostics unless per-attempt data is required.
2. Compare the phase breakdown rather than total time alone.
3. Check for garbage collections and a large managed-memory delta.
4. Distinguish cold area construction from warm planning.
5. Review surface-fit calls and rejection counts; repeated support failures can dominate constrained floor placement.

## Results changed after a settings edit

Fixed seeds reproduce the random sequence for the same effective request. Changes to target distribution, area geometry, layers, styles, assets, or constraints legitimately change candidate generation or validation. Clearing caches is useful for benchmarking invalidation, not a requirement for normal editing.
