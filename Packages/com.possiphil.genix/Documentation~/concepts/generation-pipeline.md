# Generation pipeline

## Data flow

1. `GenerationRequest` captures the designer's area, assets, target and support distribution, style, seed, and workflow settings.
2. `GenerationPreflight` rejects missing or contradictory input before expensive work begins.
3. `GenerationAssetFilter` resolves the asset pool and applies prefab, placement-type, and semantic-tag filters.
4. `GenerationContextFactory` asks the selected `IAreaSource` to build a `PlacementArea`, indexes existing scene objects, and creates the deterministic run context.
5. `CandidateSeedFactory` selects candidate providers for the active placement targets and style. `PlacementTargetCandidateProvider` divides the request budget before delegating to independent floor, wall, ceiling, and volume providers.
6. `PlacementSolver` evaluates candidate seeds against ordered assets.
7. `CandidateFactory` creates oriented attempts, including optional adaptive surface fitting.
8. `PlacementValidator` checks height, spacing, target containment, global and asset-specific relative placement, overlaps, support, and fixed-object clearance.
9. Accepted attempts enter `GenerationPlan`; rejected attempts update diagnostics and profiling.
10. `SceneGenerationService` applies an accepted plan through Unity's Undo-aware editor workflow.

## Areas and surfaces

`IAreaSource` isolates Genix from a concrete spatial system. The SFS implementation resolves subspace data, creates voxel masks and regions when needed, and exposes source-collider ownership. `PlacementArea` combines world bounds, valid volume cells, optional floor/ceiling regions, wall regions, and surface-projection settings.

The area caches are independent of the random seed. Candidate seeds may also be cached for fixed-seed requests, but their key includes all settings that change candidate generation, including target distribution and sampling parameters.

## Candidate generation and validation

A `CandidateSeed` is an inexpensive potential position with a placement type and optional surface information. It is not yet tied to an asset. This keeps sampling reusable and prevents costly surface-fit or overlap checks from running for every raw sample.

For each seed, Genix creates an asset order and tries compatible assets. Immutable support tags and allow/deny rules are evaluated before full candidate construction, so an asset that requires a Desktop support is never sent through geometry validation for a generic floor seed.

Optional support distribution adds accepted-placement budgets above this compatibility layer. The
planner first searches candidates belonging to underfilled explicit or default support groups.
Filtered reads preserve candidates from every other group, allowing those positions to be consumed
later rather than discarding them. Once requested budgets are satisfied or unavailable, remaining
groups can absorb partial-result overflow. With support distribution disabled, the normal sequential
candidate path is unchanged.

In all-matching surface mode, floor sampling reserves part of the existing candidate budget for each physical semantic support that can host at least one selected asset. The remaining budget stays global. Small desktops and shelves therefore receive candidates even when they occupy only a tiny fraction of a large target volume, without multiplying the normal candidate budget for typical scenes. Candidate diagnostics expose both support coverage and attempts eliminated by the compatibility prefilter.

Assets whose asset-relative anchor does not exist yet are deferred without running geometry validation. Assets without dependencies remain eligible, while a dependency becomes eligible as soon as its anchor exists in the scene, a previous run, or the current plan. Optional relations (`Unlimited` and `At Most`) receive one initial ordering preference but remain part of normal weighted generation. Mandatory relations (`At Least`, `Exactly`, and `Between`) form a dependency graph once per run. `Between` contributes its minimum to the reserved closure and retains its maximum as a normal capacity constraint. Before a root is attempted, the planner reserves enough object slots for its transitive mandatory closure. Floor and ceiling dependents first receive a small deterministic candidate set around their concrete anchor and support surface; other targets and exhausted local sets fall back to the normal global pool. Local candidates use bounded, placement-specific sampling and deterministic side/alignment ordering. If a valid parent leaves no valid placement for a mandatory descendant, the planner rolls back that branch and tries another bounded parent candidate. A composition that cannot fit the remaining object budget is not started. This permits complete monitor, keyboard, and mouse chains without excluding unrelated eligible assets from the remaining budget or multiplying the global candidate budget.

Semantic path sources remain independent from surface geometry. Near-path rules filter normal
surface candidates by centerline distance, side, endpoint margin, and optional facing. Tangents are
interpolated across polyline vertices to keep path-relative orientation continuous. Regular station
rules instead derive virtual scene anchors at deterministic distances along that centerline, project
each anchor onto a valid support, and reuse the mandatory relation planner for exact or paired
placements. The station set is cached per dependent asset and run, so candidate iteration performs
spatial queries against a stable list rather than repeating path sampling and projection.

Early spacing checks reject asset-independent failures before rotations and surface fitting. Geometry-dependent rejections do not prune differently shaped assets, because orientation, center offsets, and adaptive fitting can make a nominally larger asset valid where another failed.

Adaptive wall fitting samples the complete wall-facing footprint and rejects insufficient or overly
uneven support. The selected depth policy then anchors the asset at the mean, deepest, or outermost
supported probe. This separates contact behavior from Sink Offset: depth policy chooses the fitted
surface plane, while Sink Offset applies only the final deliberate embedding distance.

## Preview ownership

Preview plans are retained by `GenerationWorkflow` and replace the previous preview. Releasing the old plan before a new run limits managed-memory growth. Detailed diagnostics are separate from object naming and random state, so enabling explainability does not alter accepted object identities.
