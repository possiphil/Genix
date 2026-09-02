# Genix

Genix is a Unity Editor tool for procedural object placement in spatially described scenes. It
combines reusable asset definitions, designer-authored generation styles, and Space Foundation
System (SFS) area data to plan and apply placements on floors, walls, ceilings, support surfaces,
and inside open space.

The tool is intended for level designers and game developers who want to populate an environment
with controlled variation. Instead of placing every object by hand, a designer describes which
assets are eligible, where they may appear, and which spatial or semantic relationships must hold.
Genix samples possible positions, validates each attempt, and produces an inspectable placement
plan that can be previewed before it changes the scene.

Genix places prefabs supplied by the project. It does not generate meshes, materials, terrain, or
SFS data, and it does not replace the spatial backend. SFS describes the available space; Genix
decides which configured objects can be placed within it.

## What Genix provides

- Placement on floors, walls, ceilings, semantic support surfaces, and within valid volumes
- Reusable asset definitions, semantic tags, asset pools, generation styles, and generation presets
- Spacing, overlap, clearance, containment, support, orientation, and placement-limit validation
- Optional object, anchor, path, proximity, and exclusion relationships
- Distribution controls across placement targets and semantic support surfaces
- Deterministic generation with fixed seeds when reproducible variants are required
- Preview and apply workflows that preserve the reviewed plan and integrate with Unity Undo
- Saved layouts for reviewing and reusing generated arrangements
- Designer-facing diagnostics for understanding incomplete runs and rejected placements
- Space Setup and scene-authoring tools for preparing SFS locations and Genix scene metadata

Typical uses include dressing rooms or outdoor spaces, creating repeatable environment variants,
placing related object groups such as a monitor and keyboard on a desk, and testing whether an asset
set and its constraints work across differently shaped spaces.

## Repository packages

This repository contains two Unity Package Manager packages with separate responsibilities:

| Package | Intended audience | Contents |
| --- | --- | --- |
| `com.possiphil.genix` | Designers and game developers | Generation, content authoring, Space Setup, scene setup, layouts, diagnostics, presets, and the SFS integration |
| `com.possiphil.genix.devtools` | Genix developers and researchers | Automated tests, coverage integration, profiling, performance benchmarks, evaluation campaigns, and small generic test fixtures |

The designer package is sufficient for normal authoring and generation. DevTools is optional and
does not add controls to the designer Generator. Removing it leaves the Genix content and normal
generation workflow intact.

Project-specific benchmark scenes, evaluation assets, and research results are deliberately not
part of either package. They live in the separate
[Genix Evaluation Project](https://github.com/possiphil/Genix-Evaluation-Project), which provides a
frozen host project for reproducing the thesis experiments.

## Requirements

- Unity `6000.0` or newer. The frozen evaluation project uses Unity `6000.5.4f1`.
- Access to the currently private
  [Space Foundation System](https://github.com/dyrdadev/space-foundation-system) repository, package
  ID `dev.dyrda.space-foundation-system`.
- An initialized Unity Addressables configuration, as required by SFS.
- Project prefabs and physical scene surfaces for custom content. The optional Starter Content can
  be used to explore Genix before creating these assets.

The DevTools package additionally declares Unity Test Framework `1.7.0` and the designer package as
package dependencies.

## Installation

Install SFS before Genix. The following URL works only for a GitHub account that has access to the
private SFS repository. In Unity Package Manager, choose **Install package from git URL** and use:

```text
https://github.com/dyrdadev/space-foundation-system.git?path=/Packages/dev.dyrda.space-foundation-system
```

In a new host project, open **Window > Asset Management > Addressables > Groups** once so SFS can
resolve its Addressables settings.

Install the designer package with:

```text
https://github.com/possiphil/Genix.git?path=/Packages/com.possiphil.genix
```

Install the optional developer package only when tests, profiling, benchmarks, or evaluation
campaigns are needed:

```text
https://github.com/possiphil/Genix.git?path=/Packages/com.possiphil.genix.devtools
```

For a reproducible project, append a release tag or commit hash to each Git URL. Both Genix packages
should resolve to the same revision.

## Quick start

The Starter Content is the shortest path to a complete, editable example:

1. Open **Tools > Genix > Generator** or **Tools > Genix > Content**.
2. Choose **Set Up Starter Content** when the project does not yet contain Genix content.
3. Genix creates an editable SFS room, semantic tags, five general-purpose styles, example prefabs,
   helper pools, and a Starter Room generation preset under `Assets/Genix`.
4. In the Generator, select the target area, asset pool, generation style, placement targets, and
   object count.
5. Choose **Preview** to inspect a plan without instantiating prefabs.
6. Inspect accepted placements and any rejection summary, then choose **Apply Preview** to
   instantiate that exact plan. **Generate** applies a new batch immediately.

Starter Content is regular project content, not a read-only package sample. Its assets can be
inspected, renamed, replaced, or deleted.

## Using Genix with your own scene

A custom generation workflow consists of four parts:

1. **Prepare the space.** Compute an SFS space and expose the intended location through a Genix
   target area. **Tools > Genix > Space Setup** can create common SFS anchors, box delimiters, and
   voxel-aligned locations.
2. **Describe the content.** Create Genix asset definitions for project prefabs, assign placement
   types and constraints, and collect eligible assets in a manual or rule-based pool. These assets
   are managed in **Tools > Genix > Content**.
3. **Configure the run.** In **Tools > Genix > Generator**, select the target area, pool, generation
   style, placement targets, and object count. Enable **Advanced** only for specialist controls such
   as semantic distributions, relative placement, fixed seeds, or search-budget tuning.
4. **Review the result.** Preview before applying when the exact arrangement matters. Use
   **Tools > Genix > Diagnostics** when a run is incomplete or an expected asset is not placed.

Floor, wall, and ceiling placement normally requires colliders on the configured placement layers.
Inside Space placement uses valid SFS volume cells and does not require a support collider, but it
still observes bounds, spacing, overlap, clearance, and relationship constraints.

## Main designer windows

| Window | Purpose |
| --- | --- |
| **Generator** | Configure, preview, generate, regenerate, save layouts, and clear generated objects |
| **Content** | Author tags, pools, target areas, asset definitions, layouts, and scene metadata |
| **Space Setup** | Create and validate common SFS inputs and voxel-aligned locations |
| **Diagnostics** | Inspect generation reports and understand incomplete or rejected placements |

The shared **Advanced** toggle reveals less common authoring controls without resetting hidden
values. Diagnostics instead uses a local **Technical Details** toggle because it changes only the
depth of the selected report.

## Documentation

- [Designer manual](Packages/com.possiphil.genix/Documentation~/index.md)
- [Getting started](Packages/com.possiphil.genix/Documentation~/getting-started.md)
- [Designer interface guide](Packages/com.possiphil.genix/Documentation~/interface-guide.md)
- [Generation pipeline](Packages/com.possiphil.genix/Documentation~/concepts/generation-pipeline.md)
- [Settings reference](Packages/com.possiphil.genix/Documentation~/reference/settings.md)
- [Public API overview](Packages/com.possiphil.genix/Documentation~/reference/public-api.md)
- [Diagnostics reference](Packages/com.possiphil.genix/Documentation~/reference/diagnostics.md)
- [Troubleshooting](Packages/com.possiphil.genix/Documentation~/troubleshooting.md)
- [DevTools manual](Packages/com.possiphil.genix.devtools/Documentation~/index.md)

## Development and stability

Both packages live on the same source branch. Their UPM manifests and one-way assembly dependency
form the product boundary, so designer and developer code do not require separate branches or
duplicated fixes.

Genix `0.1.0` is a pre-1.0 research prototype developed in the context of a bachelor's thesis. The
documented workflows and current public API are supported by the repository, but source
compatibility is not guaranteed until a stable release.

Genix and Genix DevTools are available under the [MIT License](LICENSE.md). Separately installed
dependencies retain their own terms; see the notices included with the
[designer package](Packages/com.possiphil.genix/Third%20Party%20Notices.md) and
[DevTools package](Packages/com.possiphil.genix.devtools/Third%20Party%20Notices.md).
