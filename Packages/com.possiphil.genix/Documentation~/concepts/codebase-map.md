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

- `Editor/Generation` orchestrates asset filtering, target distribution, preview ownership, and scene application. `GenerationEngine` keeps random and quota-based planning as separate strategies, while relation-local candidate creation is split into wall sampling, position sampling, deterministic ordering, and geometry helpers.
- `Editor/Windows`, `Editor/Inspectors`, and `Editor/Drawers` provide designer workflows and contextual explanations. Stateful IMGUI types use responsibility-named partial files when several views share one serialized selection and Undo context; for example, the asset inspector separates support rules, fit and bounds, relational constraints, and semantic tags.
- `Editor/Integrations` adapts external spatial systems. The SFS integration is the current production area source.
- `Editor/Diagnostics` visualizes and persists designer-facing run explanations.
- `Editor/Layouts` captures, restores, and manages reusable layouts. The Content browser reads searchable
  list metadata from a project-local index and loads a `SavedLayout` asset only when the designer selects
  it or starts an explicit layout action.
- `Editor/Infrastructure` owns idempotent project-content construction. Starter Content separates taxonomy, persistent assets, and prefab geometry while retaining one transactional entry point.

## Optional DevTools package

- `Packages/com.possiphil.genix.devtools/Editor/Profiling` owns opt-in run instrumentation and profile reports.
- `Packages/com.possiphil.genix.devtools/Editor/Benchmarking` and `Editor/Evaluation` own unattended research campaigns.
- `Editor/Common/EditorCampaignSession` owns temporary editor-global state for both campaign types. `EditorCampaignAreaContext` prepares and resolves target areas once per loaded scene. Runners therefore share scene restoration, profiling isolation, assembly-reload locking, interruption detection, and target lifecycle semantics.
- `Packages/com.possiphil.genix.devtools/Tests` contains the Unity test assemblies and dashboard.
- `Packages/com.possiphil.genix.devtools/Evaluation` contains thesis scenes and assets rather than production designer content.

## Internal module boundaries

Large stateful or performance-sensitive types are divided with partial classes only when the parts share one lifecycle or a hot-path call graph. This keeps Unity serialization and direct calls intact while making ownership explicit. Independent providers and value types use separate classes and files instead: target routing, floor sampling, and ceiling sampling are distinct candidate providers, and `GenerationOutcome` is independent from the planning engine.

Partial-file suffixes name responsibilities rather than implementation chronology, such as `.Geometry`, `.Ordering`, `.Relations`, or `.Rendering`. A new behavior belongs in the narrowest matching part. If it requires no shared state, prefer a separate collaborator instead of extending a partial type.

## Dependency direction

Runtime code does not depend on UnityEditor or SFS types. Editor orchestration depends on the runtime contracts, while integrations implement those contracts for external systems. Tests target runtime behavior through the same public types used by the editor.

The designer package exposes a neutral instrumentation hook but never references the DevTools assembly. DevTools may depend on the designer package and register a profiler provider when installed. This direction keeps sampling and placement logic independently testable, prevents SFS-specific geometry from leaking into the solver, and allows the designer package to compile on its own.
