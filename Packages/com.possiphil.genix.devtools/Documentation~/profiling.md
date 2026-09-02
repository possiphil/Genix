# Profiling

Open **Tools > Genix Developer > Profiler**, select a target area from the current scene and a Generation Preset, then choose **Preview** or **Generate** and run **Profile Run**. Preview measures planning and preview preparation without placing objects. Generate additionally applies the plan to the scene and therefore includes scene-application cost. Instrumentation is enabled only for this one run; ordinary Generator operations remain uninstrumented.

The selected Generation Preset supplies the complete generation configuration, including content, object count, placement targets, style, surface search, relative placement, seed policy, and partial-result policy. Use a fixed-seed preset when comparing detailed profiles. The result appears after the run and can be exported as CSV or saved under `Assets/Genix/Profiles` for later inspection.

The profiler reports total time and the major phases asset filtering, area build, candidate generation, planning, and scene application. Planning is further split into candidate iteration, asset ordering, candidate construction, validation, recording, and naming. Persisted reports are stored under `Assets/Genix/Profiles`.

Use profiling to explain one run, not as the authoritative total for performance comparisons. Instrumentation adds work. For controlled comparisons, use the Benchmark window's Runtime measurements and keep scene, object count, style, targets, seed policy, Unity version, and cache condition fixed.

Garbage-collection counts and managed-memory deltas can explain isolated slow runs. A fixed seed controls generation randomness, but editor scheduling, JIT compilation, and garbage collection can still change wall-clock time.
