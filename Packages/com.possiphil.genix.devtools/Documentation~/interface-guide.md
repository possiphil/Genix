# Developer interface guide

Genix DevTools separates verification and research workflows from the designer package. All windows open under **Tools > Genix Developer**, use the same right-aligned actions and full-row selection behavior, and retain enough raw data to inspect a result after a run finishes.

## Tests

Open **Tools > Genix Developer > Tests** for package and host-project EditMode tests.

- **Quick** is the normal edit-feedback preset, **Full** is the pre-commit suite, and **Stress** increases property cases and data sizes.
- Summary fields reflow individually onto a second line as the window narrows; the test list keeps a useful minimum width and scrolls horizontally only below that width.
- Groups show their aggregate result and expose a **Run** action. Expanded tests show type, property-case progress where applicable, duration, **Run**, and **Open** in one row.
- Long display names use an ellipsis; the tooltip retains the exact NUnit identifier. **Open** navigates to source without moving details to a distant panel.
- Search and filters change both visible rows and the displayed totals. **Export** writes the complete result model rather than only the filtered view.

The dashboard delegates discovery and execution to Unity Test Framework. Unity Test Runner and Code Coverage remain separate Unity windows because they own those platform-level workflows.

## Profiler

Open **Tools > Genix Developer > Profiler**, choose a Target Area and Generation Preset, then choose whether the run should preview or generate. **Profile Run** enables instrumentation only for that operation.

The current result is grouped into full-width foldouts for Run, Phase Breakdown, Managed Memory, Planning Steps, Area Build Steps, and Placement Targets. Nested target types remain collapsible without extra card borders. **Save** stores a Unity report asset; **Export CSV** writes a file without changing the Project selection or opening its folder.

Saved profiles are selectable across the whole row. Their summary keeps only total time, candidates, and planning time; complete values appear under **Profile Details**. The adjacent delete menu provides bulk cleanup without giving infrequent actions permanent space.

Use profiler timings to explain a run. Use Benchmarks for comparative totals because instrumentation adds overhead.

## Benchmarks

Open **Tools > Genix Developer > Benchmarks**. The left pane owns scenarios; the right pane owns the selected scenario's configuration. Both panes use the same responsive split as Evaluation.

Each scenario stores a scene, stable target-area identity, Generation Preset, and object-count series. Open the scene to change the target when it contains several areas; the stored identifier remains resolvable during automated scene switches. The preset supplies the same generation fields and names used by the Generator.

Campaign settings control cold and warm seeds, unmeasured code warm-ups, repetitions, settle frames, cache conditions, and optional Phase Breakdown. **Validate** reports setup problems without running. **Run Full Suite** executes enabled scenarios; **Run Selected** executes only the current row. **Create** creates a generic suite asset.

The result list can filter cache condition and measurement using single-choice **All ...** menus. **Runtime** is the authoritative production measurement; **Phase Breakdown** is the instrumented explanatory measurement.

## Evaluation

Open **Tools > Genix Developer > Evaluation**. Suite configuration mirrors Benchmarks where the underlying generation input is the same. Evaluation adds scenario type, automatic criteria, layout retention, and report review.

The top toolbar separates suite actions from report actions:

- **Suite Actions** creates a suite or removes superseded retained layouts.
- **Export** writes the selected report with its current automatic and visual evidence.
- **Capture Missing** validates hashes and renders only missing or outdated review sets.
- The capture menu creates or opens the aggregate review PDF and offers deliberate full recapture.

The report browser filters Scenario, Automatic Result, and Visual Review independently. A selected run shows automatic checks, scenario-wide asset and support coverage, visual rating, notes, layout application, and capture actions. **Pass** means no evaluation-relevant visible defect; **Acceptable** and **Fail** require an observable note. A missing layout or an unbacked rating is invalid evidence, while an unreviewed retained layout is incomplete evidence.

Review capture creates an overview, top view, and two perpendicular side views plus a contact sheet and hashed manifest. A batch commits each run atomically, can resume through **Capture Missing**, and assembles validated sheets into one PDF.

## Persistent output

- Test exports are chosen explicitly from the Tests window.
- Saved profiles live under `Assets/Genix/Profiles`; CSV exports use a chosen filesystem path.
- Benchmarks write raw and aggregate evidence under `BenchmarkResults` outside `Assets`.
- Evaluations write exports under `EvaluationResults`, review media under `EvaluationReview`, and retained layout assets under `Assets/Genix/Layouts`.

Campaigns temporarily control scene loading, assembly reload locking, and profiler state through one shared session. Completion, cancellation, failure, and interruption all use the same restoration path.
