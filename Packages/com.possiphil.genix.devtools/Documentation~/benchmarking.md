# Performance Benchmarks

Open **Tools > Genix Developer > Benchmarks** to configure and run unattended, scene-based benchmark campaigns. Benchmark suites are project assets, so the exact scenes, target areas, generation settings, object counts, cache conditions, and deterministic seeds can be reviewed and versioned.

## Measurement modes

- **Runtime** measures the production generation core with one external high-resolution timer. Internal timing probes, preview rendering, hierarchy application, logging, result hashing, scene loading, warm-up, cache clearing, memory snapshots, and file output are outside this boundary.
- **Phase breakdown** repeats the same case with Genix phase instrumentation enabled. Use its component values to explain Runtime results, not as the authoritative total runtime.

Runtime and phase-breakdown plans are hashed outside the timed section. The export reports mismatches, making instrumentation-induced semantic changes visible. Serialized suite and export values retain the internal names `Primary` and `Diagnostic` for compatibility.

## Cache conditions

- **Cold** clears Genix area, candidate, and scene-object caches before every measured sample. Unmeasured code warm-ups run first, and the caches are cleared again afterward.
- **Warm** primes reusable data once per scenario, measurement variant, and object count. Independent seeds model repeated designer previews; the candidate cache is cleared between equal-seed repetitions so repetitions estimate timing noise instead of cache lookup speed.

Scene loading and a configurable number of settle frames happen before measurements. Unity must use **Release** code optimization and the Unity Profiler must be disabled for a campaign.

The runner owns these temporary editor changes through one campaign session. Completion, cancellation, exceptions, and failed cleanup all follow the same restoration path for the original scene setup, profiling state, assembly-reload lock, and interruption marker. Target-area preparation is shared with evaluation campaigns and occurs at most once per loaded scene.

## Running a campaign

1. Create or select a Benchmark Suite.
2. Use **Add Evaluation Scenes** to import scenes under `Packages/com.possiphil.genix.devtools/Evaluation/Scenes/Performance` and `Packages/com.possiphil.genix.devtools/Evaluation/Scenes/RealWorld`, or add scenarios manually.
3. Assign a target area, asset pool, style preset, placement settings, and object-count series to every enabled scenario.
4. Select the cache states and whether the campaign should include a phase breakdown, then validate the suite.
5. Run the complete suite. Stop requests take effect after the current synchronous generation.

Results are written after measurement to `BenchmarkResults/<timestamp>_<suite>` outside the Unity `Assets` directory:

- `manifest.json` contains environment metadata and all raw records.
- `suite.asset.yaml` is the exact serialized suite configuration used by the campaign.
- `runs.csv` contains one row per measured seed and repetition.
- `summary.csv` groups samples by scenario, cache condition, measurement kind, and object count and reports total and valid sample counts, median, quartiles, IQR, P95, mean, sample standard deviation, completion rate, and semantic consistency. Failed and incomplete samples remain in the raw export but are excluded from runtime aggregates.

For publication-quality figures, use Runtime medians with IQR or confidence intervals as the main result. Phase-breakdown component timings should be shown separately and clearly labeled as instrumented measurements.
