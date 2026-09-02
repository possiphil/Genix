# Changelog

All notable changes to this package are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow semantic versioning.

## [0.1.0] - 2026-09-02

### Added

- Unity package documentation under `Documentation~`.
- Contextual tooltips for generation, sampling, surface, diagnostics, and asset settings.
- XML documentation for central runtime contracts and architecture-oriented package guides.
- Asset-specific relative placement by exact asset or semantic tag, including local-side, 3D-distance, same-support-surface, and facing constraints.
- Explicit Asset Relation Anchor components for semantically identifying fixed scene objects.
- Dependency-aware same-run scheduling for chained asset-relative placement.
- Per-anchor cardinality (`Unlimited`, `At Most`, `At Least`, `Exactly`, and ranged `Between`) with transitive budget reservation and atomic required-composition planning.
- Mandatory floor and ceiling compositions use bounded local support candidates and branch backtracking, preventing tight dependency chains from failing because the global sampler missed a small valid neighborhood.
- Optional per-anchor limits for relationships such as one chair or waste bin per desk.
- Semantic forward-yaw offsets for fixed relation anchors whose model axes do not define the desired Front side.
- Deterministic maximum facing deviation for controlled variation around asset-relative directions.
- Optional count and weight budgets for individually selected semantic support tags, with one implicit default group for every unlisted surface.
- Shared asset-tag count ranges for requiring one or more variants from a larger pool while retaining a combined maximum across runs.
- Near-path endpoint margins for excluding entrances, exits, junctions, and geometry transitions.

### Changed

- The package is distributed under the MIT License.
- Runtime planning, projection, validation, relation lookup, and editor presentation are split by responsibility while preserving serialized fields and public behavior.
- Benchmark and evaluation campaigns share one editor-state and target-area lifecycle, including scene restoration, profiler isolation, reload locking, and interruption recovery.
- Surface discovery is represented by one three-value setting instead of a mode plus a legacy boolean.
- Boundary decomposition is shown only when it can affect floor or ceiling boundary regions.
- Asset pruning is conservative and only skips all remaining assets for asset-independent spacing failures.
- Fixed-seed intent is named consistently as `UseFixedSeed` in the runtime API.
- Asset-relative attempts wait for their anchors and prioritize newly unlocked dependencies once.
- Path-relative facing interpolates adjacent polyline tangents instead of changing abruptly at vertices.

### Fixed

- Candidate seed cache keys now include target distribution mode and weights.
- Detailed diagnostics no longer consume generated object names for rejected attempts.
- SFS source-collider filtering no longer treats colliders from unrelated spaces as part of the selected target.
- Asset-relative wall facing no longer disables otherwise valid random-roll attempts.
- Scene Setup can create, filter, and configure fixed Asset Relation Anchors, including multi-object creation and automatic support-surface assignment.
- Asset-relative placement accepts multiple local sides, such as Left or Right of the same anchor.
- Support-forward orientation now honors a parent relation anchor's corrected semantic Front direction.
- Case-only asset renames no longer receive an unnecessary numeric suffix.
- Inside-space Poisson spacing now uses three-dimensional distance instead of incorrectly blocking vertically separated objects by their horizontal projection.
- Lazy candidate generation now enforces Candidate Multiplier as a hard provider-independent budget and reports when an impossible request exhausts it.
- Designer windows now use a consistent responsive layout, progressive disclosure, terminology, tooltips, list interaction, and Undo-aware command structure.
- Layout browsing now indexes lightweight metadata and loads complete layout assets only on selection or an explicit action.
- Support-distribution count and percentage inputs now stay synchronized with deterministic nearest-integer rounding.

### Removed

- The unused single-value generation mode.
- The experimental global fast-generation mode, which did not provide a reliable speed-quality trade-off.
