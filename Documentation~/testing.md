# Testing and verification

Genix combines several test techniques because no single technique establishes correctness on its own. Deterministic examples protect known contracts, property tests explore broad input spaces, integration tests cover editor workflows and serialization boundaries, and robustness tests exercise hostile inputs.

## Prerequisites

The host Unity project needs these packages:

- Unity Test Framework (`com.unity.test-framework`)
- Code Coverage (`com.unity.testtools.codecoverage`)
- FsCheck and FSharp.Core, installed through NuGetForUnity

Mutation testing is optional for package users. Contributors who run it need the .NET 9 SDK and the repository-local `dotnet-stryker` tool declared in `.config/dotnet-tools.json`.

## Test Dashboard

Open **Tools > Genix > Test Dashboard**. The preset starts at **Quick** for fast feedback. Opening the window never launches tests automatically.

| Preset | Intended use | Included checks |
|---|---|---|
| Quick | Frequent feedback while editing | Fast unit and regression smoke tests |
| Full | Normal pre-commit verification | Quick tests, property tests, EditMode integration tests, approved snapshots, and standard robustness checks |
| Stress | Deliberate high-volume verification | Full plus long property runs and large data structures |

Results are grouped by subsystem. Green means every completed test in the group passed, yellow means the group contains skipped or inconclusive results, red means at least one test failed, and gray means no result exists. Expand a group for individual duration, output, failure message, stack trace, source navigation, and targeted reruns. The summary reports conventional **NUnit** tests, **Properties**, and **Property Cases** separately. Every property row shows its own executed and configured cases, for example `2,000/2,000 cases`, instead of hiding them in one aggregate check count. Search and type, status, and subsystem filters change both the visible rows and the accompanying filtered totals. **Export** writes these per-property counts and the versioned detailed result set as JSON for evaluation evidence.

The dashboard delegates execution to Unity's `TestRunnerApi`; the built-in Test Runner remains the source of truth. Runs started in either interface are collected by the dashboard.

### Open-editor command runner

Contributors and coding tools can launch the same presets in an already-open Unity editor from the package root:

```sh
./Tools~/run-open-editor-tests.sh Quick
./Tools~/run-open-editor-tests.sh Full
./Tools~/run-open-editor-tests.sh Stress
```

The command uses a file bridge under the host project's `Library/Genix` directory, refreshes local package sources before execution, waits for Unity's `TestRunnerApi`, prints every failed assertion and stack trace, and exits with a nonzero status when a test fails. It therefore avoids opening a second Unity process against the same project and prevents tests from silently using a stale script assembly. The editor must have imported and compiled the bridge itself at least once; focus Unity or trigger a refresh if the first request after installing this feature is not consumed. Set `GENIX_UNITY_PROJECT` when the package is not next to its host project and `GENIX_TEST_TIMEOUT` to override the 900-second timeout.

Projects that install Space Foundation System also compile the optional
`Genix.Tests.SpaceFoundation.Editor` assembly. Its focused tests cover voxel flood fill and
surface extraction at the production adapter boundary without making SFS a dependency of the
core test assembly.

## Property and robustness tests

FsCheck generates deterministic streams of inputs and shrinks a failure toward a smaller counterexample. Genix uses properties for invariants such as deterministic random streams, symmetric oriented-bounds intersection, voxel-mask equivalence, bounded sampling, minimum Poisson spacing, and spatial-index completeness.

Property tests normally run in Full with 250 generated cases per property and in Stress with 2,000. A property rerun under Quick uses 32 cases for focused debugging. Each property expresses one invariant and generates many input combinations for that invariant; it is still one test definition, while its case counter reports the explored inputs. The dashboard reports the cases actually attempted, so a property that fails early can show fewer cases than its configured maximum. Set `GENIX_PROPERTY_TESTS` before launching Unity to override the count for a dedicated experiment. Preserve FsCheck's reported replay seed when filing a failure so it can be reproduced.

Targeted fuzzing is represented by robustness properties rather than a second random framework. This keeps shrinking, replay, categories, and reporting consistent. Stress tests are intentionally excluded from Full because they optimize confidence per overnight or evaluation run rather than feedback speed.

## Golden tests

Golden tests are deliberately limited to stable compatibility boundaries. The SavedLayout schema test records only top-level serialized field names, not complete object data or generated placements. A failure means the persistence contract changed; review migration compatibility before updating the approved value. Do not approve snapshots merely to make a run green.

## Performance benchmarks

Scene-scale timing is intentionally separate from correctness testing. Use the [Performance Benchmarks](benchmarking.md) window for reproducible cold/warm campaigns and use the generation profiler to explain one interactive run.

## Coverage

Use **Coverage** in the dashboard to open **Window > Analysis > Code Coverage**. Enable coverage, include `Genix.Runtime`, `Genix.Editor.Common`, `Genix.Editor`, and `Genix.SpaceFoundation.Editor`, enable automatic HTML report generation, then run Full from the dashboard. Coverage identifies unexecuted code; it does not show whether assertions are strong, so report it together with property results and the mutation score.

For thesis evaluation, retain the generated HTML report and record statement and branch coverage separately. Exclude generated code, third-party packages, and Unity framework assemblies from the denominator.

## Mutation testing

Mutation testing checks assertion strength by making temporary changes and measuring whether tests detect them. Genix runs Stryker in the Unity-ignored `Mutation~` folder against a small, explicitly linked pure-C# core. It never rewrites the Runtime files: the runner hashes every linked source before execution and verifies the hashes again through an exit trap, including failed runs.

From the package root, run:

```sh
Mutation~/run-mutation.sh
```

The command first runs the adapter's NUnit tests, then creates HTML and JSON reports under `Mutation~/StrykerOutput`. Mutants marked `CompileError` are excluded. Inspect surviving mutants before interpreting the score; equivalent mutations, such as two identical shift operators for an unsigned operand, cannot be killed by a meaningful test.

The adapter is intentionally narrow. Expanding mutation scope to Unity-dependent assemblies requires a separate compiled adapter and should be done only when the added maintenance cost is justified.

## What the suite does not claim

EditMode tests cover deterministic logic and editor orchestration without loading full gameplay scenes. Broad PlayMode tests are omitted because Genix is an editor planning tool and its runtime behavior is already exercised through EditMode-compatible APIs. Evaluation scenes remain necessary for perceptual placement quality, Space Foundation interoperability, large real-world terrains, and designer workflow studies.

For a defensible evaluation, report all of the following rather than one aggregate pass count:

1. Quick, Full, and Stress pass/fail totals with exported dashboard JSON.
2. Property case counts and replay seeds for any failures.
3. Statement and branch coverage for Genix-owned assemblies.
4. Performance distributions on fixed evaluation hardware and scenes.
5. Mutation score, mutation scope, exclusions, and reviewed equivalent mutants.
6. Separate manual or scene-based evaluation results for visual and interaction quality.
