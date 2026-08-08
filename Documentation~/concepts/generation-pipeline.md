# Generation pipeline

## Data flow

1. `GenerationRequest` captures the designer's area, assets, target distribution, style, seed, and workflow settings.
2. `GenerationPreflight` rejects missing or contradictory input before expensive work begins.
3. `GenerationAssetFilter` resolves the asset pool and applies prefab, placement-type, and semantic-tag filters.
4. `GenerationContextFactory` asks the selected `IAreaSource` to build a `PlacementArea`, indexes existing scene objects, and creates the deterministic run context.
5. `CandidateSeedFactory` selects candidate providers for the active placement targets and style.
6. `PlacementSolver` evaluates candidate seeds against ordered assets.
7. `CandidateFactory` creates oriented attempts, including optional adaptive surface fitting.
8. `PlacementValidator` checks height, spacing, target containment, relative range, overlaps, support, and fixed-object clearance.
9. Accepted attempts enter `GenerationPlan`; rejected attempts update diagnostics and profiling.
10. `SceneGenerationService` applies an accepted plan through Unity's Undo-aware editor workflow.

## Areas and surfaces

`IAreaSource` isolates Genix from a concrete spatial system. The SFS implementation resolves subspace data, creates voxel masks and regions when needed, and exposes source-collider ownership. `PlacementArea` combines world bounds, valid volume cells, optional floor/ceiling regions, wall regions, and surface-projection settings.

The area caches are independent of the random seed. Candidate seeds may also be cached for fixed-seed requests, but their key includes all settings that change candidate generation, including target distribution and sampling parameters.

## Candidate generation and validation

A `CandidateSeed` is an inexpensive potential position with a placement type and optional surface information. It is not yet tied to an asset. This keeps sampling reusable and prevents costly surface-fit or overlap checks from running for every raw sample.

For each seed, Genix creates an asset order and tries compatible assets. Early spacing checks reject asset-independent failures before rotations and surface fitting. Geometry-dependent rejections do not prune differently shaped assets, because orientation, center offsets, and adaptive fitting can make a nominally larger asset valid where another failed.

## Preview ownership

Preview plans are retained by `GenerationWorkflow` and replace the previous preview. Releasing the old plan before a new run limits managed-memory growth. Detailed diagnostics are separate from object naming and random state, so enabling explainability does not alter accepted object identities.
