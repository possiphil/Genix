# Codebase map

## Runtime

- `Runtime/Core` owns requests, preflight validation, run contexts, deterministic randomness, and generation plans.
- `Runtime/Areas` defines the spatial abstraction, placement areas, surface projection, classification, and containment.
- `Runtime/Sampling` implements candidate patterns without depending on a concrete asset.
- `Runtime/Placement` turns candidate seeds into asset attempts, validates them, and updates spatial indices.
- `Runtime/Assets`, `Runtime/Semantics`, and `Runtime/Styles` contain designer-authored configuration assets.
- `Runtime/Diagnostics` and `Runtime/Profiling` collect optional explanations and measurements without controlling generation outcomes.
- `Runtime/Layouts` stores reusable metadata for generated arrangements.

## Editor

- `Editor/Generation` orchestrates asset filtering, target distribution, preview ownership, and scene application.
- `Editor/Windows`, `Editor/Inspectors`, and `Editor/Drawers` provide designer workflows and contextual explanations.
- `Editor/Integrations` adapts external spatial systems. The SFS integration is the current production area source.
- `Editor/Diagnostics` visualizes and persists designer-facing run explanations.
- `Editor/Layouts` captures, restores, and manages reusable layouts.

## Optional DevTools package

- `Packages/com.possiphil.genix.devtools/Editor/Profiling` owns opt-in run instrumentation and profile reports.
- `Packages/com.possiphil.genix.devtools/Editor/Benchmarking` and `Editor/Evaluation` own unattended research campaigns.
- `Packages/com.possiphil.genix.devtools/Tests` contains the Unity test assemblies and dashboard.
- `Packages/com.possiphil.genix.devtools/Evaluation` contains thesis scenes and assets rather than production designer content.

## Dependency direction

Runtime code does not depend on UnityEditor or SFS types. Editor orchestration depends on the runtime contracts, while integrations implement those contracts for external systems. Tests target runtime behavior through the same public types used by the editor.

The designer package exposes a neutral instrumentation hook but never references the DevTools assembly. DevTools may depend on the designer package and register a profiler provider when installed. This direction keeps sampling and placement logic independently testable, prevents SFS-specific geometry from leaking into the solver, and allows the designer package to compile on its own.
