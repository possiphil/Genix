# Changelog

All notable changes to this package are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow semantic versioning.

## [Unreleased]

### Added

- Unity package documentation under `Documentation~`.
- Contextual tooltips for generation, sampling, surface, diagnostics, and asset settings.
- XML documentation for central runtime contracts and architecture-oriented package guides.

### Changed

- Surface discovery is represented by one three-value setting instead of a mode plus a legacy boolean.
- Boundary decomposition is shown only when it can affect floor or ceiling boundary regions.
- Asset pruning is conservative and only skips all remaining assets for asset-independent spacing failures.
- Fixed-seed intent is named consistently as `UseFixedSeed` in the runtime API.

### Fixed

- Candidate seed cache keys now include target distribution mode and weights.
- Detailed diagnostics no longer consume generated object names for rejected attempts.
- SFS source-collider filtering no longer treats colliders from unrelated spaces as part of the selected target.

### Removed

- The unused single-value generation mode.
- The experimental global fast-generation mode, which did not provide a reliable speed-quality trade-off.
