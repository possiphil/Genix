# Public API overview

Genix is primarily a designer tool, but its runtime assembly exposes a small set of useful orchestration and extension contracts.

## Generation orchestration

- `GenerationRequest` captures immutable designer intent.
- `GenerationPreflight.IsValid` performs inexpensive validation and returns an actionable error.
- `GenerationContextFactory.Create` resolves area and scene state for one run.
- `PlacementSolver` plans accepted objects into `GenerationContext.Plan`.
- `GenerationPlan` exposes accepted `PlannedObject` values and spatial queries.

Editor code should normally use the higher-level `GenerationWorkflow`, which owns preview replacement, diagnostics, profiling, and scene application.

## Spatial integration

Implement `IAreaSource` to connect another spatial representation. An implementation must:

1. expose stable source metadata and an owning transform;
2. identify colliders that belong to the source itself;
3. provide area semantic tags;
4. build a `PlacementArea` from `AreaBuildSettings`;
5. return an actionable error instead of a partial area when construction fails.

Implement `IAreaCacheControl` as an optional capability when the source owns persistent or in-memory spatial caches.

## Authoring data

`AssetDefinition`, `AssetPool`, `StylePreset`, semantic tags, and categories are Unity `ScriptableObject` assets. Prefer their custom inspectors and catalog services for authoring so bounds, references, and catalog membership remain synchronized.

## Stability

The package is a pre-1.0 research prototype. Public types support the current editor and integration architecture, but source compatibility is not guaranteed until a stable release.
