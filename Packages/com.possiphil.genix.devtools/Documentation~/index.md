# Genix DevTools

Genix DevTools is the optional developer and research companion to the designer-facing Genix package. It adds instrumentation, automated verification, reproducible performance campaigns, and quality-evaluation workflows without adding controls to the designer Generator.

## Workflows

- [Profiling](profiling.md): instrument one interactive generation run and inspect phase-level costs.
- [Developer interface guide](interface-guide.md): understand the structure and controls of every DevTools window.
- [Testing and verification](testing.md): run Quick, Full, and Stress suites, coverage, and mutation checks.
- [Performance benchmarks](benchmarking.md): measure production totals separately from diagnostic phase timings.
- [Evaluation campaigns](evaluation.md): execute and export repeatable isolated and real-world scenario campaigns.

All windows are grouped under **Tools > Genix Developer**. Removing this package removes those windows, tests, profiling, benchmarking, and evaluation tooling; project-specific scenes and content remain in the consuming host project, and the main Genix package continues to compile and generate normally.

The [generated API reference](api/toc.yml) documents the public DevTools data and extension surface from source XML comments.
