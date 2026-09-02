# Genix packages

This repository contains two Unity Package Manager packages:

- `Packages/com.possiphil.genix` is the designer-facing Genix package. It includes
  generation, asset authoring, scene setup, Space Foundation authoring, diagnostics,
  layouts, and presets.
- `Packages/com.possiphil.genix.devtools` is optional. It adds tests, profiling,
  benchmarks, evaluation tooling, and small generic test fixtures. Project-specific
  benchmark and evaluation content belongs to the consuming host project.

Install the designer package by selecting its `package.json` in Unity Package Manager.
The project must already contain the Space Foundation System package
(`dev.dyrda.space-foundation-system`), which is Genix's spatial backend. Contributors can
install the DevTools package in the same way after installing Genix. SFS also requires an
initialized Addressables configuration; open **Window > Asset Management > Addressables > Groups**
once in a new host project before creating or testing SFS objects.

The corresponding Git URLs are:

```text
https://github.com/possiphil/Genix.git?path=/Packages/com.possiphil.genix
https://github.com/possiphil/Genix.git?path=/Packages/com.possiphil.genix.devtools
```

Both packages live on the same source branch. Their UPM manifests and one-way assembly dependency
provide the product boundary, so fixes do not need to be duplicated or merged between a designer
branch and a development branch.

Start with the designer [manual](Packages/com.possiphil.genix/Documentation~/index.md) or the optional
[DevTools manual](Packages/com.possiphil.genix.devtools/Documentation~/index.md). The repository is
currently all rights reserved; choose and apply an explicit distribution license before making it public.
