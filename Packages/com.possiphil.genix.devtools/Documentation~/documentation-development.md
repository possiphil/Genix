# Documentation development

DevTools follows the same documentation standard as the Genix designer package: public package APIs use XML comments, task and reference material lives in `Documentation~`, and internal comments explain only non-obvious invariants or ownership.

Open a Unity host project after assembly-definition changes, install DocFX, and run:

```sh
UNITY_PROJECT=/absolute/path/to/unity-project \
  Packages/com.possiphil.genix.devtools/Documentation~/build.sh
```

The build treats missing or malformed public XML documentation as an error, stages assemblies under the ignored `.artifacts` directory, generates API metadata under `api`, and writes the site to `_site`. Warnings about unresolved optional Unity dependencies are acceptable only when the Genix DevTools assemblies compile and their expected API pages are present.

Keep DevTools terminology consistent with the designer Generator whenever both controls represent the same setting. Research-specific terms should name the measured concept precisely and explain their methodological role in the surrounding manual or tooltip.
