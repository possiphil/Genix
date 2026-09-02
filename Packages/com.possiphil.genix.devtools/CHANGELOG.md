# Changelog

All notable changes to Genix DevTools are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow semantic versioning.

## [0.1.0] - 2026-09-02

### Added

- Responsive dashboards for Quick, Full, and Stress test presets with property-case evidence and targeted reruns.
- Opt-in generation profiling with phase, planning, area-build, memory, and placement-target breakdowns.
- Deterministic cold and warm benchmark campaigns with authoritative runtime measurements and separate instrumented phase breakdowns.
- Reproducible evaluation campaigns with automatic checks, retained layouts, visual ratings, standardized review captures, hashed manifests, and aggregate review PDFs.
- JSON and CSV evidence exports for tests, benchmarks, profiles, and evaluations.
- Generic package-scene workspace support and a minimal read-only scene fixture.

### Changed

- The package is distributed under the MIT License.
- Developer windows use the same responsive columns, terminology, full-row selection, foldouts, tooltips, and action placement as the designer package.
- Benchmark and evaluation campaigns share scene restoration, target-area preparation, profiler isolation, assembly-reload locking, and interruption recovery.
- Project-specific scenes, assets, suites, and generated evidence live in the consuming host project rather than this package.

### Fixed

- Batch benchmark startup no longer opens an interactive save-scene dialog.
- Evaluation report lists virtualize expensive asset access and keep long labels inside their panes.
- Review capture preserves relevant environment rendering, frames the selected target adaptively, resumes incomplete batches, and replaces each capture atomically.
- Automated benchmark preparation now resumes only after the clean Release compilation has reloaded the editor domain, preventing a preceding coverage run from leaking Debug assemblies into timing results.
