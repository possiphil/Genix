## Diagnostics

Diagnostics use progressive disclosure so the same report serves designers and developers without
mixing their terminology. By default, the report shows the generation result, requested and placed
objects, and the **Main Placement Issue** in designer-facing language. **Technical Details** adds
the recorded configuration and aggregate placement-search measurements.
Reports captured with **Detailed Diagnostics** additionally expose individual positions,
asset attempts, bounds, rejection reasons, related objects, and Scene-view overlays.

### Terminology

| Term | Meaning |
|---|---|
| Object | One accepted prefab instance in the generation result. |
| Candidate Position | A sampled location that may be evaluated for one or more assets. |
| Evaluated Position | A candidate position that the solver reached during the run. |
| Asset Attempt | One concrete asset evaluated at a candidate position, including its rotation and bounds. |
| Accepted Position | An evaluated position that produced an accepted asset attempt. |
| Rejected Position | An evaluated position where every recorded asset attempt was rejected. Available in detailed reports. |
| Unused Position | A candidate position that was generated but not evaluated before the run stopped. |
| Attempt Skipped by Support Rules | An asset-position pairing eliminated by immutable support compatibility before full placement validation. |
| Primary Rejection Reason | The most frequent reason an asset attempt was rejected. The default report view calls this the **Main Placement Issue**. |

Counts grouped under **Objects by Placement Target** or **Objects by Support Surface** use the
format `placed or planned / target`. Summary diagnostics retain aggregate counts and rejection
reasons. Detailed diagnostics retain per-attempt geometry and therefore consume more memory.

Common rejection reasons include:

- **Outside Target Volume**: the complete asset bounds leave the valid SFS volume.
- **Outside Target Surface**: a strict surface footprint does not fit the discovered region.
- **Insufficient Surface Support**: adaptive probes do not meet support or height requirements.
- **Overlaps Generated Object / Scene Object**: the oriented bounds intersect a planned or existing object.
- **Too Close to Generated Object / Scene Object**: style spacing or scene clearance is violated.
- **Support Tags Do Not Match**: the sampled support surface does not satisfy the asset's support tags.
- **Relation Anchor Missing**: an asset relation requires a matching generated object or authored anchor.
- **Exceeds Target Height**: the full asset bounds leave the target's vertical extent.

Enable detailed capture only while investigating a run. The optional **Genix DevTools** package
provides separate profiling and benchmark workflows under **Tools > Genix Developer**; their
measurement controls are deliberately not part of the designer Generator.

The Generator's **Save Report** action stores one report for the last run. Summary runs produce a
compact report; runs recorded with **Detailed Diagnostics** produce a detailed report that
already contains the summary result. The Diagnostics window lists both together, newest first.
