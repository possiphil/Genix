# Diagnostics and profiling

## Diagnostics

Summary diagnostics record requested and accepted counts, candidate totals, target budgets, and rejection reasons. Detailed diagnostics additionally retain per-attempt positions, bounds, asset identifiers, and related objects for Scene-view inspection.

Common rejection reasons include:

- **Outside Target Volume**: the oriented asset bounds leave the valid SFS volume.
- **Outside Target Area**: a strict surface footprint does not fit the discovered region.
- **Insufficient Surface Support**: adaptive probes do not meet support or height requirements.
- **Overlaps Generated/Fixed**: the oriented bounds intersect a planned or existing object.
- **Too Close To Generated/Fixed**: style spacing or fixed clearance is violated.
- **Exceeds Target Height**: the full asset bounds leave the target's vertical extent.

## Profiling

Enable **Profile Run** only for measured runs. The profiler reports total time and major phases: asset filtering, area build, candidate generation, planning, and scene application. Planning is further split into candidate iteration, asset ordering, candidate construction, validation, recording, and naming.

For comparisons, use the same scene, object count, style, targets, seed policy, Unity version, and profiling state. Separate:

- cold runs after relevant cache invalidation;
- warm runs with unchanged spatial data;
- first managed-runtime runs after reload;
- repeated steady-state runs.

Garbage collection and managed-memory deltas explain isolated slow runs. A fixed seed controls generation randomness, but editor scheduling, JIT compilation, and garbage collection can still change wall-clock time.
