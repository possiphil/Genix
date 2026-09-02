# Genix documentation

Genix plans and applies procedural object placements inside an SFS target area. Designers choose eligible assets, placement targets, a sampling style, and optional constraints. Genix then builds a spatial representation, generates candidate positions, validates asset attempts, and records a plan that can be previewed or applied.

## Start here

- [Getting started](getting-started.md): configure a scene and create a first preview.
- [Interface guide](interface-guide.md): understand every designer window and its common interaction patterns.
- [Generation pipeline](concepts/generation-pipeline.md): understand the data flow and ownership boundaries.
- [Codebase map](concepts/codebase-map.md): locate runtime, editor, integration, and test responsibilities.
- [Settings reference](reference/settings.md): learn what every generation and asset setting changes.
- [Public API overview](reference/public-api.md): find the supported extension and orchestration entry points.
- [Generated API reference](api/toc.yml): browse public Runtime and Editor types generated from code comments.
- [Diagnostics](reference/diagnostics.md): investigate incomplete runs and inspect rejected attempts.
- [Troubleshooting](troubleshooting.md): resolve common setup and placement failures.

## Interface modes

The shared **Advanced** toggle appears in Generator, Space Setup, and Content tabs that contain additional authoring controls. Tabs without an advanced layer omit it. Leave it disabled for the common generation and content-authoring workflow; enable it for distribution policies, semantic relationships, geometry overrides, and search budgets. The toggle changes only visibility: hidden values remain active and are never reset. Diagnostics uses a local **Technical Details** toggle because it changes only the depth of the selected report, not the shared authoring mode.

Automated tests, profiling, benchmarks, and evaluation campaigns belong to the optional **Genix DevTools** package. Installing it adds a separate **Tools > Genix Developer** menu without changing the designer workflow.

## Design principles

Genix separates candidate generation from asset validation. Sampling answers *where placements may be attempted*. Asset definitions and placement validation answer *what fits there*. This separation allows the same style to work with different assets and target areas while keeping constraints explicit.

Preview planning and scene application are also separate. A preview creates a retained plan and diagnostic visualization without instantiating prefabs. Applying a preview uses that exact plan, so review does not trigger a second random generation pass.
