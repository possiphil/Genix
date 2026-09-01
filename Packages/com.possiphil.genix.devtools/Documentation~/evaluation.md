# Evaluation campaigns

Open **Tools > Genix Developer > Evaluation** to run the configured isolated and real-world scenarios individually or as one campaign. Evaluation suites store scenes, target areas, presets, seeds, repetitions, automatic criteria, and visual-review state as versioned Unity assets.

Automatic checks establish measurable facts such as completion, requested relation cardinalities, placement counts, semantic mismatches, and missing evidence. They do not establish perceptual quality. Apply the retained layouts and review orientation, support contact, clearance, composition, and scene plausibility before marking visual evidence valid.

Checks are grouped internally by the claim they support: geometric validity and containment, semantic support and exclusions, and asset-relative or path-relative relationships. Unavailable evidence is reported separately from a failed check, so a missing spatial source cannot be mistaken for a successful evaluation.

Evaluation and benchmark runners share the same campaign-state owner and target-area context. The original scene setup, profiling state, assembly-reload lock, and interrupted-run marker are restored through one cleanup path after completion, cancellation, or failure.

Evaluation scenes supplied by the read-only developer package are opened through generated project copies under `Assets/Genix/Evaluations/Workspace`. Reports and captured layouts retain the canonical package-scene path, while the disposable workspace copy allows Unity to load and apply layouts without modifying package content.

Campaign exports retain raw runs and aggregate results. Keep the generated layouts required for visual evidence until the review is complete; old exploratory layouts can be removed separately.

Use **Clean Up** in the Evaluation window to remove superseded locked evaluation layouts without affecting designer-authored layouts. The preview retains the latest completed full campaign and the latest newer completed rerun for each scenario, reports exact keep and delete counts, and requires confirmation before deleting layout assets and their owned prefabs. Report assets are retained.
