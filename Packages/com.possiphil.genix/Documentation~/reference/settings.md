# Settings reference

The shared **Basic / Advanced** selector controls presentation, not behavior. Basic shows the common designer workflow. Advanced adds surface discovery and classification, deterministic seeds, distribution and relationship rules, fit and bounds overrides, candidate budgets, and other specialist controls. Values hidden by Basic remain active. A settings icon with a tooltip identifies configurations that contain hidden advanced values.

## Generation presets

Generation presets are reusable assets stored under `Assets/Genix/Generation Presets` by default.
They capture the settings that define a generation request:

- Asset Pool, Generation Style, and Object Count
- Placement Targets, target distribution, weighted shares, and optional support-surface distribution
- Surface discovery, boundary decomposition, surface layers, and classification angles
- Relative-placement source, radius, and scene layers
- Use Seed, Seed, and Best Effort

**Save New** creates a preset from the current fields. **Update** writes current fields back to the
selected preset. **Reload** restores the asset. The `Custom` entry keeps the current fields without
binding them to an asset. A notice appears when bound settings differ from the selected preset.

**Default on Startup** stores the preset asset GUID per project and editor user, so moving or
renaming the asset does not break startup loading. Target Area and Detailed Diagnostics are not
captured because they are scene- or run-specific.

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

### Support distribution

**Support Distribution** optionally controls how many accepted objects land on semantic support
types such as Desktop, Shelf, Path, or Rock. Add rules only for the support tags that need explicit
control. Every surface that matches no listed rule belongs to **Default / Other Surfaces**, so an
office setup can list only Desktop and Shelf without maintaining zero-weight entries for unrelated
environment tags.

- **Exact Count** reserves a concrete number of accepted placements for the selected support tag.
- **Weight** assigns a relative share of the object count remaining after exact rules.
- **Default / Other Surfaces Weight** assigns the corresponding share to all unlisted surfaces.

Exact rules are allocated first. Weighted rules and the default group then divide the remainder by
their relative weights; the displayed percentages update from the current weight sum. A weight of
zero excludes that group from the weighted remainder, while other groups can still receive their
exact counts. If one surface descriptor carries several explicitly configured tags, the first
matching rule in the list is used. Diagnostics report requested and achieved counts for every rule
and the default group.

## Sampling styles

| Algorithm | Pattern | Main controls |
|---|---|---|
| Random | Independent random candidates. | Candidate multiplier, minimum candidates, shuffle. |
| Grid | Regular rows and columns. | Cell size. |
| Jittered Grid | Grid with bounded random offsets. | Cell size, jitter amount. |
| Cluster | Candidates grouped around centers. | Cluster count, radius, optional center spacing. |
| Bridson Poisson Disk | Even organic spacing with a minimum distance. | Minimum distance, attempts. |

Candidate multiplier and minimum candidate count trade additional search coverage for generation time. The hard candidate budget is `max(Object Count x Candidate Multiplier, Minimum Candidates)` and is shown as **Maximum Candidates** in the generator. Candidates are created lazily, so successful runs stop before reaching that maximum. If an impossible request consumes the complete budget, diagnostics identify budget exhaustion explicitly instead of allowing a provider-specific oversampling path to continue beyond it. Poisson attempts control how thoroughly the sampler fills difficult regions; they do not replace the minimum-distance validation. Minimum distance is measured horizontally for floor and ceiling placement, and in full 3D for wall and inside-space placement.

## Asset placement

| Setting | Behavior |
|---|---|
| Placement Type | Restricts the asset to Floor, Wall, Ceiling, or Inside Space seeds. |
| Rotation Offset | Corrects prefab-local import axes after Genix computes surface alignment. Use this instead of rotating the prefab root; wall-asset fronts should resolve to Genix local +Z. |
| Strict Surface Fit | Requires the asset footprint to fit the discovered surface directly. |
| Adaptive Surface Fit | Probes the footprint and derives a supported height and normal. |
| Align To Surface | Tilts the asset with the fitted surface normal. |
| Keep Upright | Uses surface height but preserves an upright orientation. |
| Average/Lowest/Highest Height | Chooses how adaptive support heights produce the final floor or ceiling placement height. |
| Average Depth/Deepest/Outermost | Chooses whether adaptive wall placement follows the mean, most recessed, or most protruding supported wall probe. Deepest is useful for embedded natural assets; Outermost minimizes penetration for mounted fixtures. |
| Minimum Support | Required supported fraction of adaptive footprint probes. |
| Maximum Height Difference | Maximum allowed height range among support probes. |
| Sink Offset | Moves a fitted asset into the support surface by a small distance. |
| Wall Placement Height | Adds clearance between a wall asset's rotated lower bound and the wall baseline. Zero places it flush. |
| Wall Random Roll | Tries deterministic rotations around the wall normal without tilting the asset away from the wall. |
| Face Target | Rotates the asset toward the nearest active relative-placement anchor. |
| Asset-Relative Placement | Constrains this asset to a semantic anchor, local side, 3D distance interval, and optional facing policy. |
| Near Path | Constrains this asset by horizontal distance and side relative to a semantic path, with optional along/across-path facing. |
| Wall Relationship | Near Wall enforces a maximum horizontal bounds gap; Away From Wall reserves a minimum gap. Scene walls and steep terrain classified with the current Floor/Ceiling Angle thresholds participate. |

Rotation Offset is an asset-specific correction, not random variation. Genix rotates the prefab
bounds, center offset, and reserved clearance into the corrected placement frame once and caches
the result. Preview, validation, relative-facing directions, and final instantiation therefore use
the same geometry. Prefab root scale is applied to the generated center offset exactly once, so
scaled source prefabs still rest on their detected support face. A zero offset preserves the
prefab orientation and all existing placement behavior.

## Asset pools and semantics

- **Static** pools contain an explicit curated asset list. Use them when membership must remain stable.
- **Dynamic** pools resolve current catalog assets through placement, orientation, and semantic-tag filters. Use them for reusable content rules.
- **Shared Tag Counts** constrain the combined count of every pool asset carrying an asset tag. `Minimum 1 / Maximum 1` requires exactly one valid variant, such as either a standing or wall-mounted coat rack. Existing generated output participates across runs; an unmet minimum is reported when no matching variant can be placed.
- **Per-Anchor Groups** combine several dependent assets under one tag-based count for every matching anchor. For example, a `Display` group can require one to two total monitor or laptop variants per `Desk`, while each member keeps its own distance, side, and facing rule. Minimums are planned locally, maximums are enforced during normal candidate validation, and existing generated output participates across runs.
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

### Asset-relative placement

Asset definitions can additionally require a relationship to one exact asset or an
asset-compatible semantic tag. The rule can use objects accepted in the current run, existing
Genix output carrying generated metadata, explicit scene anchors, or both. **Required Sides** is a
multi-selection evaluated in the anchor's local horizontal frame: Front is local +Z, Back -Z,
Left -X, and Right +X. Any disables the side restriction. Minimum and maximum distances are
three-dimensional distances from the nearest point on the anchor bounds.

**Require Same Support Surface** additionally requires both objects to reference the same
`PlacementSurfaceDescriptor` instance. This keeps related objects such as a monitor, keyboard,
and mouse on one desk even when another matching workstation is nearby. Candidates without a
descriptor cannot satisfy the option. Fixed `Asset Relation Anchor` components therefore expose
an explicit **Support Surface** reference; a descriptor on the anchor GameObject is detected
automatically.

**Require Inside Anchor Bounds** treats an anchor as a semantic placement region and requires the
complete oriented asset bounds to fit inside it. Use broad bounds to constrain a vehicle to a
parking area or furniture to a rest area while retaining normal terrain projection. Bounds may be
tall when only horizontal containment matters; the physical support still determines height.

**Facing** may remain unchanged, point toward or away from the matched anchor, or match its local
+Z direction. Asset-relative facing takes precedence over the global `Face Target` orientation.
Wall assets remain flush with their wall; their side and distance rule still applies, but their
asset-relative facing choice is ignored.
**Max Facing Deviation** adds a deterministic yaw variation in either direction from that resolved
facing. Zero is exact alignment; 45 permits angles from -45 to +45 degrees without losing the
semantic target direction.

**Per Anchor Count** defines the cardinality of this dependent asset for every matching anchor.
**Unlimited** adds no count constraint. **At Most** keeps the relation optional while limiting its
instances. **At Least** and **Exactly** make the configured count mandatory. **Between** requires
the configured minimum while permitting optional instances up to its separate maximum. Required dependents are
planned immediately after their anchor, including transitive chains, and each generated part counts
toward **Object Count**. Genix reserves the complete mandatory closure before starting a new group;
if a required placement cannot be completed geometrically, the new group is rolled back rather than
leaving a partial composition. Existing generated output participates in the count across runs.
**Exactly** and **Between** also enforce their configured maximum, while **At Least** permits
additional optional instances. The policy is independent of the asset's global **Max Placements** limit.

Pool-level **Per-Anchor Groups** complement this asset-specific count. Select the anchor source and
one concrete anchor asset or anchor tag, then select a member asset tag and cardinality. Every
member must still define a compatible Asset-Relative Placement rule so Genix can assign it to a
concrete anchor. Use the asset-level count when one exact asset needs a quota; use a pool group when
several interchangeable or related assets must share one quota.

For a fixed scene object, select one or more scene roots and use **Scene Setup > Add Anchor**. The
menu can assign their represented Asset Definition during creation, and a single descriptor below
each selected root is adopted automatically as its Support Surface. Existing anchors appear under
the **Relation Anchors** filter, where represented assets and tags can be edited directly. The
equivalent hierarchy command is **GameObject > Genix > Add Asset Relation Anchor**. Renderer and
collider bounds are derived automatically; custom bounds are available for logical anchors without
geometry. The cyan arrow visualizes local Front.
**Front Yaw Offset** rotates this semantic frame without rotating the visible scene object. Use it
when a model's local +Z axis does not point toward the side that should be treated as Front.
`Match Support Forward` uses this corrected semantic frame when the sampled support belongs to the
anchor, keeping supported assets and their dependent relationship chains in one coordinate frame.

Asset-relative dependencies are resolved during the same generation run. Genix first places an
asset that has no unresolved relation, then makes it immediately available as an anchor for later
placements. Newly available dependent assets receive one initial priority attempt, so chains such
as monitor, keyboard, and mouse do not require separate manual runs. Missing scene anchors and
circular chains such as A requiring B while B requires A stop with an explicit diagnostic.

### Path placement

`PathPlacementSource` exposes an ordered centerline with asset-compatible semantic tags. A path is
not a narrow placement region: normal surface projection still chooses the physical terrain or
floor, while **Near Path** only constrains horizontal distance, optional authored side, and facing.
This lets a broad Rest Area region contain benches while a separate path rule keeps them close to
and facing the trail. Left and Right follow the authored point order. Facing can remain unchanged,
follow or oppose the path direction, or point toward or away from its nearest centerline point.
**Endpoint Margin** excludes a configurable length at both path ends, which keeps assets away from
entrances, exits, junctions, and geometry transitions. A value of zero leaves the complete path
available. Facing directions are interpolated across adjacent polyline segments so curved paths do
not introduce abrupt orientation changes at individual authored points.

**Regular Path Stations** is available on tag-based Asset-Relative Placement rules that accept
scene anchors. Genix derives virtual anchors at the configured spacing and lateral offset; it does
not create one scene object or semantic region per placement. **Both Sides** creates an atomic pair
for each usable station, so an exact per-anchor count produces symmetric roadside furniture.
Endpoint Margin omits stations near path ends, while Maximum Stations bounds both object count and
work. Station projection, support compatibility, and exclusion checks are resolved once per asset
and cached for the generation run. Cardinality still controls whether every station is mandatory,
optional, or bounded.

### Exclusion regions

Exclusion regions reserve space independently from Unity gameplay physics. **Box** and **Sphere**
define collider-free primitive volumes. **Child Colliders** reuses the enabled colliders below the
region object as exact authored exclusion geometry, which is useful for curved paths and bridges.
**Affected Targets** limits which placement types are rejected. **Exempt Asset Tags** permits
explicit path furniture or markers to overlap that geometry while ordinary rocks and vegetation
remain excluded.

## Workflow controls

- **Best Effort** keeps a valid partial plan when the requested count cannot be reached.
- **Use Seed** makes random decisions reproducible and enables candidate-cache reuse.
- **Detailed Diagnostics** stores per-attempt geometry and should be enabled only while investigating a run.

Profiling is intentionally not a Generator setting. Install the optional Genix DevTools package and use **Tools > Genix Developer > Profiler** when an instrumented run is required.
