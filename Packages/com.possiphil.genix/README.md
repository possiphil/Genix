# Genix

Genix is a designer-facing procedural placement package for Unity. It combines Space Foundation System (SFS) volumes, semantic asset filtering, configurable sampling styles, placement validation, previews, layouts, and diagnostics.

## Documentation

Start with the [package documentation](Documentation~/index.md). It contains:

- a guided setup and first-generation workflow;
- a complete designer-interface guide;
- explanations of the generation pipeline, areas, sampling, and placement;
- a complete settings reference with recommended use cases;
- diagnostics and troubleshooting guides.

In Unity, select Genix in **Window > Package Manager** and choose **View documentation** to open the same documentation locally.

Profiling, automated tests, benchmarks, and evaluation campaigns are available separately in the optional `com.possiphil.genix.devtools` package. They are intentionally absent from the designer menus.

## Compatibility

- Unity 6000.0 or newer
- Space Foundation System package (`dev.dyrda.space-foundation-system`) installed in the project

The Space Foundation System repository is currently private. Installing and using the integration
therefore requires access granted by its owner. Genix references its APIs but does not redistribute
its source code or binaries. See [Third Party Notices](Third%20Party%20Notices.md) for attribution
and license terms.

## Status

Genix is currently a `0.1.0` research prototype. Public APIs and serialized data may change before `1.0.0`.
