# Settings reference

## Generation presets

Generation presets are reusable assets stored under `Assets/Genix/Generation Presets` by default.
They capture the settings that define a generation request:

- Asset Pool, Generation Style, and Object Count
- Placement Targets, target distribution, and weighted shares
- Surface discovery, boundary decomposition, surface layers, and classification angles
- Relative-placement source, radius, and scene layers
- Use Seed, Seed, and Best Effort

**Save New** creates a preset from the current fields. **Update** writes current fields back to the
selected preset. **Reload** restores the asset. The `Custom` entry keeps the current fields without
binding them to an asset. A notice appears when bound settings differ from the selected preset.

**Default on Startup** stores the preset asset GUID per project and editor user, so moving or
renaming the asset does not break startup loading. Target Area, Profile Run, and Detailed
Diagnostics are not captured because they are scene- or run-specific.

## Surface discovery

| Setting | Behavior | Recommended use |
|---|---|---|
| All Matching Surfaces | Searches matching physics layers throughout the SFS volume. | Default; interior floors, terrain, and surfaces at arbitrary heights. |
| Near SFS Boundaries | Searches matching physics surfaces near voxel-derived boundary regions. | Spaces where only boundary-adjacent geometry should receive objects. |
| SFS Boundaries | Uses voxel-derived SFS regions without physics projection. | Fully voxel-defined spaces or scenes without suitable surface colliders. |

**Boundary Regions** appears only for boundary-based floor or ceiling generation. **Layer Bounds** is faster but approximates irregular layers. **Cell-Preserving** keeps holes and outlines more accurately.

Floor and Ceiling Angle define the maximum slope from upward and downward horizontal. Normals between the thresholds are walls.

## Placement targets

- **Floor** projects onto upward-facing surfaces.
- **Wall** projects onto near-vertical surfaces or wall boundary regions.
- **Ceiling** projects onto downward-facing surfaces.
- **Inside Space** samples valid volume cells without requiring a support surface.

When multiple targets are selected, **Random** uses any available target, **Balanced** aims for equal counts, and **Weighted** uses relative target weights. A zero weight disables that target for the weighted run.

## Sampling styles

| Algorithm | Pattern | Main controls |
|---|---|---|
| Random | Independent random candidates. | Candidate multiplier, minimum candidates, shuffle. |
| Grid | Regular rows and columns. | Cell size. |
| Jittered Grid | Grid with bounded random offsets. | Cell size, jitter amount. |
| Cluster | Candidates grouped around centers. | Cluster count, radius, optional center spacing. |
| Bridson Poisson Disk | Even organic spacing with a minimum distance. | Minimum distance, attempts. |

Candidate multiplier and minimum candidate count trade additional search coverage for generation time. Poisson attempts control how thoroughly the sampler fills difficult regions; they do not replace the minimum-distance validation.

## Asset placement

| Setting | Behavior |
|---|---|
| Placement Type | Restricts the asset to Floor, Wall, Ceiling, or Inside Space seeds. |
| Strict Surface Fit | Requires the asset footprint to fit the discovered surface directly. |
| Adaptive Surface Fit | Probes the footprint and derives a supported height and normal. |
| Align To Surface | Tilts the asset with the fitted surface normal. |
| Keep Upright | Uses surface height but preserves an upright orientation. |
| Average/Lowest/Highest Height | Chooses how adaptive support heights produce the final placement height. |
| Minimum Support | Required supported fraction of adaptive footprint probes. |
| Maximum Height Difference | Maximum allowed height range among support probes. |
| Sink Offset | Moves a fitted asset into the support surface by a small distance. |
| Wall Placement Height | Adds clearance between a wall asset's rotated lower bound and the wall baseline. Zero places it flush. |
| Wall Random Roll | Tries deterministic rotations around the wall normal without tilting the asset away from the wall. |
| Face Target | Rotates the asset toward the nearest active relative-placement anchor. |

## Asset pools and semantics

- **Static** pools contain an explicit curated asset list. Use them when membership must remain stable.
- **Dynamic** pools resolve current catalog assets through placement, orientation, and semantic-tag filters. Use them for reusable content rules.
- Concrete target-area tags require an asset to match at least one tag in each tagged category. **Any** on an asset accepts every target tag in that category; **Any** on the area removes the concrete requirement for that category.

## Relative placement

- **None** disables anchor proximity.
- **Generated Objects** uses placements already accepted by Genix.
- **Scene Objects** uses existing objects on the selected layers.
- **Any** combines generated and matching scene objects.
- **Selected Objects** uses the current editor selection captured when the run starts.

Radius is the maximum three-dimensional world-space distance from the nearest point on an
anchor's bounds. This prevents objects on different floors from qualifying only because their
horizontal positions overlap. `Face Target` uses the same active anchor source for orientation.

## Workflow controls

- **Best Effort** keeps a valid partial plan when the requested count cannot be reached.
- **Use Seed** makes random decisions reproducible and enables candidate-cache reuse.
- **Detailed Diagnostics** stores per-attempt geometry and should be enabled only while investigating a run.
- **Profile Run** records timing, memory, cache, and rejection counters and adds measurement overhead.
