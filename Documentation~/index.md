# Genix documentation

Genix plans and applies procedural object placements inside an SFS target area. Designers choose eligible assets, placement targets, a sampling style, and optional constraints. Genix then builds a spatial representation, generates candidate positions, validates asset attempts, and records a plan that can be previewed or applied.

## Start here

- [Getting started](getting-started.md): configure a scene and create a first preview.
- [Generation pipeline](concepts/generation-pipeline.md): understand the data flow and ownership boundaries.
- [Codebase map](concepts/codebase-map.md): locate runtime, editor, integration, and test responsibilities.
- [Settings reference](reference/settings.md): learn what every generation and asset setting changes.
- [Public API overview](reference/public-api.md): find the supported extension and orchestration entry points.
- [Generated API reference](api/toc.yml): browse public Runtime and Editor types generated from code comments.
- [Diagnostics and profiling](reference/diagnostics-and-profiling.md): investigate incomplete or slow runs.
- [Testing and verification](testing.md): run regression, property, robustness, performance, coverage, and mutation checks.
- [Troubleshooting](troubleshooting.md): resolve common setup and placement failures.

## Design principles

Genix separates candidate generation from asset validation. Sampling answers *where placements may be attempted*. Asset definitions and placement validation answer *what fits there*. This separation allows the same style to work with different assets and target areas while keeping constraints explicit.

Preview planning and scene application are also separate. A preview creates a retained plan and diagnostic visualization without instantiating prefabs. Applying a preview uses that exact plan, so review does not trigger a second random generation pass.
