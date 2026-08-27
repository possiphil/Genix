# Diagnostics

## Diagnostics

Summary diagnostics record requested and accepted counts, candidate totals, target budgets, and rejection reasons. Detailed diagnostics additionally retain per-attempt positions, bounds, asset identifiers, and related objects for Scene-view inspection.

Common rejection reasons include:

- **Outside Target Volume**: the oriented asset bounds leave the valid SFS volume.
- **Outside Target Area**: a strict surface footprint does not fit the discovered region.
- **Insufficient Surface Support**: adaptive probes do not meet support or height requirements.
- **Overlaps Generated/Fixed**: the oriented bounds intersect a planned or existing object.
- **Too Close To Generated/Fixed**: style spacing or fixed clearance is violated.
- **Exceeds Target Height**: the full asset bounds leave the target's vertical extent.

Detailed diagnostics retain per-attempt geometry and therefore consume more memory than summaries. Enable them only while investigating a run. The optional **Genix DevTools** package provides separate profiling and benchmark workflows under **Tools > Genix Developer**; their measurement controls are deliberately not part of the designer Generator.
