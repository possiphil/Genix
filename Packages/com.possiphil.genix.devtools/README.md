# Genix DevTools

Optional development tooling for the Genix designer package.

The package contains the test dashboard and test assemblies, generation profiler,
performance benchmarks, automated evaluation runner, and small generic test fixtures.
Evaluation scenes and domain assets are supplied by the consuming host project. It
depends on `com.possiphil.genix`; the designer package has no
dependency on this package.

See the [DevTools documentation](Documentation~/index.md) for prerequisites, workflows, the complete interface guide, and the generated public API reference.

Install the package from this repository's `Packages/com.possiphil.genix.devtools`
subfolder. For a local checkout,
add the following dependency next to the main Genix dependency:

```json
"com.possiphil.genix.devtools": "file:../../com.possiphil.genix/Packages/com.possiphil.genix.devtools"
```

Developer windows are grouped under **Tools > Genix Developer**.
