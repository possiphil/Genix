# Profiling

Open **Tools > Genix Developer > Profiler** and enable **Capture Runs** before starting a measured generation or preview. When capture is disabled, the designer workflow receives a null profiler and does not collect phase timings or managed-memory snapshots.

The profiler reports total time and the major phases asset filtering, area build, candidate generation, planning, and scene application. Planning is further split into candidate iteration, asset ordering, candidate construction, validation, recording, and naming. Persisted reports are stored under `Assets/Genix/Profiles`.

Use profiling to explain one run, not as the authoritative total for performance comparisons. Instrumentation adds work. For controlled comparisons, use the Benchmark window's Runtime measurements and keep scene, object count, style, targets, seed policy, Unity version, and cache condition fixed.

Garbage-collection counts and managed-memory deltas can explain isolated slow runs. A fixed seed controls generation randomness, but editor scheduling, JIT compilation, and garbage collection can still change wall-clock time.
